using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using LLMModule;
using LLMModule.Data;
using UnityEngine;
using UnityEngine.UI;

namespace LLMModule.Example
{
    /// <summary>
    /// 庭审 UI 输出示例 —— 展示如何：
    ///   1. 从 Data/ 加载策划配置
    ///   2. 用玩家输入的 style 调用 LLM
    ///   3. 将结果输出到 Unity UI
    ///
    /// 场景结构（Inspector 中绑定）：
    ///   - InputField: 玩家输入证词 / 风格
    ///   - Text/TMP:   显示 NPC 发言和评分
    ///   - Button:     触发操作
    /// </summary>
    public class TrialUIExample : MonoBehaviour
    {
        [Header("依赖")]
        [SerializeField] private LLMService llmService;

        [Header("UI 引用")]
        [SerializeField] private InputField styleInput;         // 玩家输入风格
        [SerializeField] private InputField argumentInput;      // 玩家输入证词
        [SerializeField] private Text npcSpeechDisplay;         // 显示 NPC 发言
        [SerializeField] private Text scoreDisplay;             // 显示评分
        [SerializeField] private Text evidenceCardDisplay;      // 显示证据卡牌
        [SerializeField] private Text statusDisplay;            // 显示状态/加载中
        [SerializeField] private Button generateEvidenceBtn;    // 生成证据按钮
        [SerializeField] private Button npcSpeechBtn;           // 生成 NPC 发言按钮
        [SerializeField] private Button evaluateBtn;            // 评估证词按钮

        // ── 运行时状态 ───────────────────────────────────────────
        private ChapterConfig _chapterConfig;
        private CardData[] _cachedCards;
        private NPCRuntimeState[] _npcStates;             // NPC 运行时数值
        private CancellationTokenSource _cts;

        // ── NPC 运行时状态（动态，每回合更新） ─────────────────────
        private class NPCRuntimeState
        {
            public string name;
            public int reasonLevel;
            public int emotionLevel;
            public int reasonThreshold;
            public int emotionThreshold;
            public bool IsPersuaded =>
                reasonLevel < reasonThreshold || emotionLevel > emotionThreshold;
        }

        // ══════════════════════════════════════════════════════════
        //  生命周期
        // ══════════════════════════════════════════════════════════

        private void Start()
        {
            _cts = new CancellationTokenSource();

            // 加载策划配置（静态数据）
            _chapterConfig = ChapterConfigLoader.Load("chapter_01");

            // 从配置初始化 NPC 运行时状态（动态数据）
            _npcStates = _chapterConfig.trial.npcs.Select(n => new NPCRuntimeState
            {
                name = n.name,
                reasonLevel = n.initialReasonLevel,
                emotionLevel = n.initialEmotionLevel,
                reasonThreshold = n.reasonThreshold,
                emotionThreshold = n.emotionThreshold
            }).ToArray();

            // 绑定按钮事件
            generateEvidenceBtn?.onClick.AddListener(() => OnGenerateEvidence().Forget());
            npcSpeechBtn?.onClick.AddListener(() => OnGenerateNPCSpeech().Forget());
            evaluateBtn?.onClick.AddListener(() => OnEvaluateArgument().Forget());

            SetStatus("配置已加载，准备就绪");
        }

        private void OnDestroy()
        {
            _cts?.Cancel();
            _cts?.Dispose();
        }

        // ══════════════════════════════════════════════════════════
        //  按钮事件：生成证据卡牌
        // ══════════════════════════════════════════════════════════

        private async UniTaskVoid OnGenerateEvidence()
        {
            SetStatus("正在生成证据卡牌...");

            var request = new EvidenceRequest
            {
                chapter = _chapterConfig.chapter,
                confirmedFacts = _chapterConfig.confirmedFacts,
                cardDefinitions = _chapterConfig.evidence.cardDefinitions
            };

            string style = styleInput?.text;

            try
            {
                _cachedCards = await llmService.Generator.GenerateEvidenceCards(
                    request, style, _cts.Token);

                // ── 输出到 UI ──
                string display = "";
                foreach (var card in _cachedCards)
                {
                    display += $"<b>【{card.name}】</b>\n{card.text}\n\n";
                }
                SetText(evidenceCardDisplay, display);
                SetStatus($"已生成 {_cachedCards.Length} 张证据卡牌");
            }
            catch (System.Exception e)
            {
                SetStatus($"生成失败: {e.Message}");
            }
        }

        // ══════════════════════════════════════════════════════════
        //  按钮事件：生成 NPC 发言（示例：选前两个 NPC）
        // ══════════════════════════════════════════════════════════

        private async UniTaskVoid OnGenerateNPCSpeech()
        {
            SetStatus("正在生成 NPC 发言...");

            // 本轮发言的 NPC（示例：取前 2 个，实际由回合管理器决定）
            var speakers = _npcStates.Take(2).Select(s => new NPCSpeechTarget
            {
                name = s.name,
                reasonLevel = s.reasonLevel,
                emotionLevel = s.emotionLevel,
                isPersuaded = s.IsPersuaded
            }).ToArray();

            var request = new NPCSpeechRequest
            {
                chapter = _chapterConfig.chapter,
                confirmedFacts = _chapterConfig.confirmedFacts,
                topic = _chapterConfig.trial.topic,
                allNPCs = _chapterConfig.trial.npcs.Select(n => n.ToTrialInfo()).ToArray(),
                speakers = speakers
            };

            string style = styleInput?.text;

            try
            {
                var speeches = await llmService.Generator.GenerateNPCSpeeches(
                    request, style, _cts.Token);

                // ── 输出到 UI ──
                string display = "";
                foreach (var s in speeches)
                {
                    // 查找该 NPC 的说服状态
                    var state = _npcStates.FirstOrDefault(n => n.name == s.name);
                    string tag = state?.IsPersuaded == true ? " [已动摇]" : "";
                    display += $"<b>{s.name}{tag}</b>\n{s.speech}\n\n";
                }
                SetText(npcSpeechDisplay, display);
                SetStatus($"已生成 {speeches.Length} 段 NPC 发言");
            }
            catch (System.Exception e)
            {
                SetStatus($"生成失败: {e.Message}");
            }
        }

        // ══════════════════════════════════════════════════════════
        //  按钮事件：评估玩家证词
        // ══════════════════════════════════════════════════════════

        private async UniTaskVoid OnEvaluateArgument()
        {
            string argument = argumentInput?.text;
            if (string.IsNullOrWhiteSpace(argument))
            {
                SetStatus("请先输入证词！");
                return;
            }

            // 默认使用第一张牌（实际应由玩家选择）
            if (_cachedCards == null || _cachedCards.Length == 0)
            {
                SetStatus("请先生成证据卡牌！");
                return;
            }

            var card = _cachedCards[0]; // 示例：用第一张牌

            SetStatus($"正在评估证词（使用 {card.name}）...");

            var request = new ArgumentEvalRequest
            {
                chapter = _chapterConfig.chapter,
                confirmedFacts = _chapterConfig.confirmedFacts,
                topic = _chapterConfig.trial.topic,
                argument = argument,
                cardName = card.name,
                cardText = card.text
            };

            string style = styleInput?.text;

            try
            {
                int score = await llmService.Generator.EvaluatePlayerArgument(
                    request, style, _cts.Token);

                // ── 输出到 UI ──
                SetText(scoreDisplay, $"证词评分: <b>{score}</b> / 10");

                // ── 示例：根据评分更新 NPC 状态（简单规则） ──
                // 实际逻辑应在回合管理器中实现
                UpdateNPCStatesFromScore(score);

                SetStatus($"评分完成: {score}/10，NPC 状态已更新");
            }
            catch (System.Exception e)
            {
                SetStatus($"评分失败: {e.Message}");
            }
        }

        // ══════════════════════════════════════════════════════════
        //  辅助方法
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// 简单示例：根据评分更新所有 NPC 的理性/感性值。
        /// 实际游戏中应由回合管理器根据规则针对特定 NPC 更新。
        /// </summary>
        private void UpdateNPCStatesFromScore(int score)
        {
            foreach (var npc in _npcStates)
            {
                // 评分越高 → 理性值下降越多、感性值上升越多
                npc.reasonLevel -= score;
                npc.emotionLevel += score;

                // Clamp
                npc.reasonLevel = Mathf.Clamp(npc.reasonLevel, 0, 100);
                npc.emotionLevel = Mathf.Clamp(npc.emotionLevel, 0, 100);

                Debug.Log($"[NPC] {npc.name}: reason={npc.reasonLevel}, emotion={npc.emotionLevel}, persuaded={npc.IsPersuaded}");
            }
        }

        private void SetStatus(string msg)
        {
            Debug.Log($"[TrialUI] {msg}");
            SetText(statusDisplay, msg);
        }

        private static void SetText(Text textComponent, string content)
        {
            if (textComponent != null)
                textComponent.text = content;
        }
    }
}
