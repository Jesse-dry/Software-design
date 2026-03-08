using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cysharp.Threading.Tasks;
using LLMModule;
using LLMModule.Data;
using UnityEngine;

/// <summary>
/// LLM 大模型桥接器（全局静态）。
///
/// 职责：
///   - 管理 LLM 模式的启用/禁用状态
///   - 持有运行时创建的 LLMTextGenerator
///   - 在后台生成阿卡那牌文本并缓存
///   - 供 AkanaHUDController / CourtUIController 查询注入
///   - 庭审阶段：生成 NPC 发言、评估玩家证词
///
/// 安全说明：
///   API Key 优先级（与 LLMConfig.GetApiKey() 保持一致）：
///     1. 环境变量 LLM_API_KEY（推荐，不会出现在任何日志或持久化数据中）
///     2. 玩家在主菜单手动输入（仅保留在 Session 内存中，不写入磁盘）
///   Key 从不以明文形式打印到 Console。
///
/// 设计原则：
///   - LLM 未启用时对游戏无任何影响（所有查询返回 null / -1）
///   - 调用方根据返回值决定是否替换 prefab 原始文本
///   - 庭审阶段：NPC 发言 / 玩家证词评分均有 fallback
/// </summary>
public static class LLMBridge
{
    /// <summary>LLM 模式是否已启用</summary>
    public static bool IsEnabled { get; private set; }

    /// <summary>卡牌文本是否已生成完毕</summary>
    public static bool HasGeneratedCardTexts { get; private set; }

    /// <summary>是否正在生成中</summary>
    public static bool IsGenerating { get; private set; }

    /// <summary>本次 Session 的 Key 来源说明（仅用于 UI 提示，不含 Key 内容）</summary>
    public static string KeySource { get; private set; } = "";

    /// <summary>
    /// 玩家自定义的叙事风格。
    /// 为空或 null 时，PromptBuilder 使用各场景的默认风格。
    /// 风格只影响 LLM 生成文本的语气和表达方式，不影响事实与立场。
    /// </summary>
    public static string Style { get; set; } = "";

    private static ILLMTextGenerator _generator;
    private static readonly Dictionary<AkanaCardId, string> _cardTexts = new();

    // ── 庭审 NPC 发言缓存：npcName → { round → speechText } ──
    private static readonly Dictionary<string, Dictionary<int, string>> _npcSpeeches = new();

    /// <summary>卡牌面板名前缀 → AkanaCardId 映射</summary>
    private static readonly Dictionary<string, AkanaCardId> _nameToCardId = new()
    {
        { "权杖牌", AkanaCardId.权杖 },
        { "星币牌", AkanaCardId.星币 },
        { "宝剑牌", AkanaCardId.宝剑 },
        { "圣杯牌", AkanaCardId.圣杯 },
    };

    // ══════════════════════════════════════════════════════════════
    //  初始化 / 禁用
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// 尝试从环境变量 LLM_API_KEY 自动初始化。
    /// 若环境变量存在且非空则返回 true，调用方可据此跳过 UI 输入弹窗。
    /// Key 不会以明文出现在日志中。
    /// </summary>
    public static bool TryInitializeFromEnvironment()
    {
        string envKey = System.Environment.GetEnvironmentVariable("LLM_API_KEY");
        if (string.IsNullOrWhiteSpace(envKey)) return false;

        return InitializeInternal(envKey, fromEnv: true);
    }

    /// <summary>
    /// 使用玩家手动输入的 API Key 初始化 LLM 模式。
    /// Key 仅保留在内存中，不写入磁盘或日志明文。
    /// </summary>
    public static void Initialize(string apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            Debug.LogWarning("[LLMBridge] API Key 为空，LLM 模式未启用。");
            IsEnabled = false;
            return;
        }

        InitializeInternal(apiKey.Trim(), fromEnv: false);
    }

    /// <summary>禁用 LLM 模式，清理缓存。</summary>
    public static void Disable()
    {
        IsEnabled = false;
        _generator?.ClearEvidenceCache();
        _generator = null;
        _cardTexts.Clear();
        _npcSpeeches.Clear();
        HasGeneratedCardTexts = false;
        IsGenerating = false;
        KeySource = "";
        Style = "";
        Debug.Log("[LLMBridge] LLM 模式已禁用。");
    }

    // ──────────────────────────────────────────────────────────────

    private static bool InitializeInternal(string apiKey, bool fromEnv)
    {
        var config = ScriptableObject.CreateInstance<LLMConfig>();
        // 直接赋值到 apiKey 字段；GetApiKey() 内部会优先检查环境变量，
        // 此处明确传入以支持运行时动态 Key，不依赖全局环境状态。
        config.apiKey = apiKey;

        try
        {
            _generator = new LLMTextGenerator(config);
            IsEnabled = true;
            KeySource = fromEnv ? "环境变量 LLM_API_KEY" : "手动输入";
            // 只打印脱敏前缀，不暴露 Key 全文
            int visible = System.Math.Min(4, apiKey.Length);
            string masked = apiKey.Substring(0, visible) + new string('*', System.Math.Max(0, apiKey.Length - visible));
            Debug.Log($"[LLMBridge] LLM 模式已启用，Key 来源: {KeySource}，前缀: {masked}");
            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[LLMBridge] 初始化失败: {e.Message}");
            IsEnabled = false;
            return false;
        }
    }

    // ══════════════════════════════════════════════════════════════
    //  卡牌文本查询
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// 获取指定阿卡那牌的 LLM 生成文本。
    /// 返回 null 表示 LLM 未启用或文本未就绪，调用方应使用 prefab 原始文本。
    /// </summary>
    public static string GetCardText(AkanaCardId cardId)
    {
        if (!IsEnabled || !HasGeneratedCardTexts) return null;
        return _cardTexts.TryGetValue(cardId, out var text) ? text : null;
    }

    // ══════════════════════════════════════════════════════════════
    //  后台生成
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// 在后台生成所有阿卡那牌文本（幂等，可安全多次调用）。
    /// 使用 fire-and-forget 模式：LLMBridge.GenerateCardTextsInBackground().Forget();
    /// </summary>
    public static async UniTaskVoid GenerateCardTextsInBackground()
    {
        if (!IsEnabled || HasGeneratedCardTexts || IsGenerating) return;

        IsGenerating = true;
        Debug.Log("[LLMBridge] 开始后台生成阿卡那牌文本...");

        try
        {
            var chapterConfig = LoadChapterConfig();
            if (chapterConfig == null)
            {
                Debug.LogError("[LLMBridge] 无法加载章节配置，生成中止。");
                return;
            }

            var request = new EvidenceRequest
            {
                chapter = chapterConfig.chapter,
                confirmedFacts = chapterConfig.confirmedFacts,
                cardDefinitions = chapterConfig.evidence.cardDefinitions
            };

            // 传入玩家自定义风格（为空则由 PromptBuilder 回退默认值）
            string style = string.IsNullOrWhiteSpace(Style) ? null : Style;
            var cards = await _generator.GenerateEvidenceCards(request, style);

            _cardTexts.Clear();
            foreach (var card in cards)
            {
                var cardId = ResolveCardId(card.name);
                if (cardId.HasValue)
                {
                    _cardTexts[cardId.Value] = card.text;
                    Debug.Log($"[LLMBridge] 卡牌文本已缓存: {card.name} → {cardId.Value}");
                }
                else
                {
                    Debug.LogWarning($"[LLMBridge] 无法匹配卡牌名到 AkanaCardId: {card.name}");
                }
            }

            HasGeneratedCardTexts = true;
            Debug.Log($"[LLMBridge] 阿卡那牌文本生成完成 ({_cardTexts.Count}/{cards.Length} 张匹配)");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[LLMBridge] 生成卡牌文本失败: {e.Message}");
        }
        finally
        {
            IsGenerating = false;
        }
    }

    // ══════════════════════════════════════════════════════════════
    //  庭审 ── NPC 发言
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// 传入庭审 NPC 的运行时状态快照，供 LLM 请求使用。
    /// 由 CourtController 在每轮开始时构建。
    /// </summary>
    public struct NPCStateSnapshot
    {
        public string name;
        public int rational;
        public int emotional;
        public bool isPersuaded;
    }

    /// <summary>
    /// 为指定回合生成 NPC 发言（异步）。
    /// 结果缓存于 _npcSpeeches，后续通过 GetNPCSpeech() 查询。
    /// </summary>
    /// <param name="round">回合索引 (0-based)</param>
    /// <param name="speakers">本轮需要发言的 NPC 状态快照</param>
    public static async UniTask GenerateNPCSpeechesForRound(int round, NPCStateSnapshot[] speakers)
    {
        if (!IsEnabled || _generator == null) return;

        var chapterConfig = LoadChapterConfig();
        if (chapterConfig?.trial == null)
        {
            Debug.LogWarning("[LLMBridge] 章节配置缺少 trial 节点，跳过 NPC 发言生成。");
            return;
        }

        try
        {
            // 构建 allNPCs
            var allNPCs = chapterConfig.trial.npcs
                .Select(n => n.ToTrialInfo())
                .ToArray();

            // 构建 speakers
            var speakerTargets = speakers
                .Select(s => new NPCSpeechTarget
                {
                    name = s.name,
                    reasonLevel = s.rational,
                    emotionLevel = s.emotional,
                    isPersuaded = s.isPersuaded
                })
                .ToArray();

            var request = new NPCSpeechRequest
            {
                chapter = chapterConfig.chapter,
                confirmedFacts = chapterConfig.confirmedFacts,
                topic = chapterConfig.trial.topic,
                allNPCs = allNPCs,
                speakers = speakerTargets
            };

            string style = string.IsNullOrWhiteSpace(Style) ? null : Style;
            var results = await _generator.GenerateNPCSpeeches(request, style);

            if (results == null) return;

            foreach (var r in results)
            {
                if (string.IsNullOrEmpty(r.name)) continue;
                if (!_npcSpeeches.ContainsKey(r.name))
                    _npcSpeeches[r.name] = new Dictionary<int, string>();
                _npcSpeeches[r.name][round] = r.speech;
                Debug.Log($"[LLMBridge] NPC 发言缓存: {r.name} 第{round}轮 → {r.speech.Substring(0, Mathf.Min(30, r.speech.Length))}...");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[LLMBridge] 生成 NPC 发言失败 (第{round}轮): {e.Message}");
        }
    }

    /// <summary>
    /// 查询已缓存的 NPC 发言。
    /// 返回 null 表示 LLM 未启用或该 NPC / 该轮无缓存，调用方应使用硬编码 fallback。
    /// </summary>
    public static string GetNPCSpeech(string npcName, int round)
    {
        if (!IsEnabled) return null;
        if (_npcSpeeches.TryGetValue(npcName, out var rounds) && rounds.TryGetValue(round, out var text))
            return text;
        return null;
    }

    /// <summary>清除庭审发言缓存（场景切换时调用）。</summary>
    public static void ClearTrialCache()
    {
        _npcSpeeches.Clear();
        Debug.Log("[LLMBridge] 庭审发言缓存已清除。");
    }

    // ══════════════════════════════════════════════════════════════
    //  庭审 ── 玩家证词评分
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// 评估玩家输入的证词，返回 0-10 分。
    /// 返回 -1 表示评估失败（网络错误、配置缺失等）。
    /// </summary>
    /// <param name="argument">玩家输入的证词文本</param>
    /// <param name="cardId">玩家使用的阿卡那牌</param>
    public static async UniTask<int> EvaluateArgument(string argument, AkanaCardId cardId)
    {
        if (!IsEnabled || _generator == null) return -1;

        var chapterConfig = LoadChapterConfig();
        if (chapterConfig?.trial == null)
        {
            Debug.LogWarning("[LLMBridge] 章节配置缺少 trial 节点，证词评估失败。");
            return -1;
        }

        try
        {
            // 卡牌名与文本
            string cardName = CourtData.CardEffects.TryGetValue(cardId, out var effect) ? effect.cardName : cardId.ToString();
            string cardText = GetCardText(cardId)
                ?? (CourtData.CardEffects.TryGetValue(cardId, out var eff) ? eff.description : "");

            var request = new ArgumentEvalRequest
            {
                chapter = chapterConfig.chapter,
                confirmedFacts = chapterConfig.confirmedFacts,
                topic = chapterConfig.trial.topic,
                argument = argument,
                cardName = cardName,
                cardText = cardText
            };

            string style = string.IsNullOrWhiteSpace(Style) ? null : Style;
            int score = await _generator.EvaluatePlayerArgument(request, style);
            Debug.Log($"[LLMBridge] 证词评分: {score}/10  (牌: {cardName})");
            return Mathf.Clamp(score, 0, 10);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[LLMBridge] 证词评估失败: {e.Message}");
            return -1;
        }
    }

    // ══════════════════════════════════════════════════════════════
    //  内部工具
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// 根据 LLM 返回的卡牌名（如 "星币牌·肮脏的交易"）解析出 AkanaCardId。
    /// 匹配规则：卡牌名以面板名前缀开头。
    /// </summary>
    private static AkanaCardId? ResolveCardId(string cardName)
    {
        if (string.IsNullOrEmpty(cardName)) return null;

        foreach (var kvp in _nameToCardId)
        {
            if (cardName.StartsWith(kvp.Key))
                return kvp.Value;
        }

        return null;
    }

    /// <summary>
    /// 从多个候选路径加载章节配置。
    /// 优先级：StreamingAssets/Data > Assets/LLMModule/Data（编辑器）。
    /// </summary>
    private static ChapterConfig LoadChapterConfig()
    {
        string[] candidates =
        {
            Path.Combine(Application.streamingAssetsPath, "Data", "chapter_01.json"),
            Path.Combine(Application.dataPath, "LLMModule", "Data", "chapter_01.json"),
        };

        foreach (var path in candidates)
        {
            if (File.Exists(path))
            {
                try
                {
                    Debug.Log($"[LLMBridge] 加载章节配置: {path}");
                    return ChapterConfigLoader.LoadFromPath(path);
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[LLMBridge] 加载配置失败 ({path}): {e.Message}");
                }
            }
        }

        Debug.LogError("[LLMBridge] 未找到 chapter_01.json！" +
            "请将文件放到 StreamingAssets/Data/ 或确认 Assets/LLMModule/Data/ 目录下存在该文件。");
        return null;
    }
}
