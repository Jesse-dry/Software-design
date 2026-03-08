using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

/// <summary>
/// 庭审主控制器 ── 场景内单例，驱动 4 回合庭审流程。
///
/// 职责：
///   1. 维护庭审状态机（CourtState）
///   2. 管理 NPC 理性/感性值
///   3. 处理阿卡那牌打出效果 + 混乱值削弱
///   4. 判定说服 / 胜负
///   5. 通过事件驱动 CourtUIController 更新 UI
/// </summary>
public class CourtController : MonoBehaviour
{
    public static CourtController Instance { get; private set; }

    // ════════════════════════════════════════════════════════════════
    //  状态
    // ════════════════════════════════════════════════════════════════

    public CourtState CurrentState { get; private set; } = CourtState.Inactive;

    /// <summary>当前回合索引 (0-based)</summary>
    public int CurrentRound { get; private set; } = 0;

    /// <summary>当前回合内发言索引 (0-based)</summary>
    public int CurrentSpeechIndex { get; private set; } = 0;

    /// <summary>当前选中的阿卡那牌</summary>
    public AkanaCardId SelectedCard { get; private set; }

    // ════════════════════════════════════════════════════════════════
    //  NPC 运行时数据
    // ════════════════════════════════════════════════════════════════

    public class NPCRuntime
    {
        public CourtData.NPCId id;
        public string name;
        public int rational;
        public int emotional;
        public int rationalThreshold;
        public int emotionalThreshold; // -1 = 免疫
        public bool IsPersuaded =>
            (rational <= rationalThreshold) ||
            (emotionalThreshold >= 0 && emotional >= emotionalThreshold);
    }

    public readonly Dictionary<CourtData.NPCId, NPCRuntime> NPCs = new();

    public int PersuadedCount { get; private set; } = 0;

    private readonly HashSet<AkanaCardId> _usedCards = new();

    // ════════════════════════════════════════════════════════════════
    //  事件 ── UI 监听
    // ════════════════════════════════════════════════════════════════

    public event Action<CourtState> OnStateChanged;
    public event Action<CourtData.NPCId, string> OnNPCSpeech;
    public event Action<int> OnRoundResult;
    public event Action OnVictory;
    public event Action OnDefeat;
    public event Action OnRulePanelRequested;
    public event Action<AkanaCardId> OnCardSelected;
    public event Action<CourtData.NPCId, int, int> OnNPCStatChanged;

    /// <summary>LLM 模式下，出牌后请求玩家输入证词。参数：所选牌 ID。</summary>
    public event Action<AkanaCardId> OnArgumentInputRequested;

    /// <summary>LLM 模式下，证词评分结果。参数：评分 (0-10)，是否获得加成。</summary>
    public event Action<int, bool> OnArgumentScoreResult;

    // ════════════════════════════════════════════════════════════════
    //  生命周期
    // ════════════════════════════════════════════════════════════════

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        // \u4e0d\u4f9d\u8d56 GameManager.IsInCourt()\uff08Start \u65f6 GM \u53ef\u80fd\u8fd8\u672a\u521d\u59cb\u5316\u9636\u6bb5\uff09\uff0c
        // \u7528\u573a\u666f\u540d\u5224\u65ad\u662f\u5426\u5728\u5ead\u5ba1\u573a\u666f\u3002
        string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        bool isCourt = sceneName.Contains("Court") || sceneName.Contains("court");
        Debug.Log("[Court] Start() sceneName='" + sceneName + "' isCourt=" + isCourt);
        if (!isCourt)
        {
            enabled = false;
            return;
        }

        InitializeNPCs();
        Debug.Log("[Court] NPC\u521d\u59cb\u5316\u5b8c\u6210\uff0c\u542f\u52a8\u5ead\u5ba1\u6d41\u7a0b\u534f\u7a0b...");
        StartCoroutine(CourtFlowCoroutine());
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ════════════════════════════════════════════════════════════════
    //  初始化
    // ════════════════════════════════════════════════════════════════

    private void InitializeNPCs()
    {
        NPCs.Clear();
        _usedCards.Clear();
        PersuadedCount = 0;

        foreach (var p in CourtData.NPCProfiles)
        {
            NPCs[p.id] = new NPCRuntime
            {
                id = p.id,
                name = p.name,
                rational = p.initRational,
                emotional = p.initEmotional,
                rationalThreshold = p.rationalThreshold,
                emotionalThreshold = p.emotionalThreshold,
            };
        }
    }

    // ════════════════════════════════════════════════════════════════
    //  主流程协程
    // ════════════════════════════════════════════════════════════════

    private IEnumerator CourtFlowCoroutine()
    {
        yield return null; // 等一帧让 UI 绑定

        // 1. 显示规则面板
        SetState(CourtState.RulePanel);
        OnRulePanelRequested?.Invoke();
        yield return new WaitUntil(() => _rulePanelClosed);

        // 2. 四回合
        for (int round = 0; round < CourtData.TotalRounds; round++)
        {
            CurrentRound = round;
            var speeches = CourtData.RoundSpeeches[round];

            // ── LLM: 在本轮发言前，异步生成 NPC 发言 ──
            if (LLMBridge.IsEnabled)
            {
                _llmSpeechesReady = false;
                GenerateLLMSpeeches(round, speeches).Forget();
                yield return new WaitUntil(() => _llmSpeechesReady);
            }

            // 2a. NPC 依次发言
            for (int s = 0; s < speeches.Length; s++)
            {
                CurrentSpeechIndex = s;
                SetState(CourtState.NPCSpeech);

                // ── LLM: 替换发言文本 ──
                string speechText = speeches[s].text;
                if (LLMBridge.IsEnabled)
                {
                    string npcName = NPCs[speeches[s].speaker].name;
                    string llmText = LLMBridge.GetNPCSpeech(npcName, round);
                    if (!string.IsNullOrEmpty(llmText)) speechText = llmText;
                }

                _speechClosed = false;
                OnNPCSpeech?.Invoke(speeches[s].speaker, speechText);
                yield return new WaitUntil(() => _speechClosed);
            }

            // 2b. 弹出阿卡那菜单
            SetState(CourtState.AkanaMenu);
            _cardChosen = false;
            _cardSkipped = false;
            yield return new WaitUntil(() => _cardChosen || _cardSkipped);

            // Bug2: 如果玩家跳过出牌，直接进入回合结算
            if (!_cardSkipped)
            {
                // 2c. 卡牌详情（打出 / 回退循环）
                bool cardPlayed = false;
                while (!cardPlayed)
                {
                    SetState(CourtState.CardDetail);
                    _cardDetailAction = CardDetailAction.None;
                    OnCardSelected?.Invoke(SelectedCard);
                    yield return new WaitUntil(() => _cardDetailAction != CardDetailAction.None);

                    if (_cardDetailAction == CardDetailAction.Back)
                    {
                        SetState(CourtState.AkanaMenu);
                        _cardChosen = false;
                        _cardSkipped = false;
                        yield return new WaitUntil(() => _cardChosen || _cardSkipped);
                        if (_cardSkipped) break;
                        continue;
                    }
                    cardPlayed = true;
                }

                // 只有真正打出了牌才应用效果
                if (!_cardSkipped)
                {
                    // ── LLM: 出牌后请求证词输入 + 评分 ──
                    float bonusMultiplier = 1f;
                    if (LLMBridge.IsEnabled)
                    {
                        _argumentInputDone = false;
                        _argumentBonusMultiplier = 1f;
                        OnArgumentInputRequested?.Invoke(SelectedCard);
                        yield return new WaitUntil(() => _argumentInputDone);
                        bonusMultiplier = _argumentBonusMultiplier;
                    }

                    // 2d. 选择目标（权杖牌跳过）
                    var effect = CourtData.CardEffects[SelectedCard];
                    if (effect.affectsAll)
                    {
                        ApplyCardEffect(SelectedCard, CourtData.NPCId.皇帝, bonusMultiplier);
                    }
                    else
                    {
                        SetState(CourtState.SelectTarget);
                        _targetChosen = false;
                        yield return new WaitUntil(() => _targetChosen);
                        ApplyCardEffect(SelectedCard, _selectedTarget, bonusMultiplier);
                    }
                }
            }

            // 2e. 回合结算
            PersuadedCount = CountPersuaded();
            SetState(CourtState.RoundResult);
            OnRoundResult?.Invoke(PersuadedCount);
            yield return new WaitForSeconds(2.5f);

            if (PersuadedCount >= 4)
            {
                SetState(CourtState.Victory);
                OnVictory?.Invoke();
                yield break;
            }
        }

        // 3. 最终判定
        PersuadedCount = CountPersuaded();
        if (PersuadedCount >= 4)
        {
            SetState(CourtState.Victory);
            OnVictory?.Invoke();
        }
        else
        {
            SetState(CourtState.Defeat);
            OnDefeat?.Invoke();
        }
    }

    // ════════════════════════════════════════════════════════════════
    //  UI 回调信号（由 CourtUIController 调用）
    // ════════════════════════════════════════════════════════════════

    private bool _rulePanelClosed = false;
    private bool _speechClosed = false;
    private bool _cardChosen = false;
    private bool _cardSkipped = false;
    private bool _targetChosen = false;
    private CourtData.NPCId _selectedTarget;

    // ── LLM 信号 ──
    private bool _llmSpeechesReady = false;
    private bool _argumentInputDone = false;
    private float _argumentBonusMultiplier = 1f;

    private enum CardDetailAction { None, Play, Back }
    private CardDetailAction _cardDetailAction = CardDetailAction.None;

    public void NotifyRulePanelClosed()  { _rulePanelClosed = true; }
    public void NotifySpeechClosed()     { _speechClosed = true; }

    /// <summary>Bug2: 玩家选择不出牌，直接进入回合结算。</summary>
    public void NotifySkipCard()          { _cardSkipped = true; }

    public void NotifyCardChosen(AkanaCardId cardId)
    {
        SelectedCard = cardId;
        _cardChosen = true;
    }

    public void NotifyCardPlay()  { _cardDetailAction = CardDetailAction.Play; }
    public void NotifyCardBack()  { _cardDetailAction = CardDetailAction.Back; }

    public void NotifyTargetChosen(CourtData.NPCId targetId)
    {
        _selectedTarget = targetId;
        _targetChosen = true;
    }

    /// <summary>
    /// 由 CourtUIController 在证词输入 + 评分完成后调用。
    /// </summary>
    /// <param name="bonusMultiplier">1.0 = 无加成，1.2 = 20% 加成</param>
    public void NotifyArgumentResult(float bonusMultiplier)
    {
        _argumentBonusMultiplier = bonusMultiplier;
        _argumentInputDone = true;
    }

    public void RequestShowRulePanel() { OnRulePanelRequested?.Invoke(); }

    // ════════════════════════════════════════════════════════════════
    //  查询接口
    // ════════════════════════════════════════════════════════════════

    public bool IsCardAvailable(AkanaCardId cardId)
    {
        if (_usedCards.Contains(cardId)) return false;
        return AkanaManager.Instance != null && AkanaManager.Instance.HasCard(cardId);
    }

    public NPCRuntime GetNPC(CourtData.NPCId id)
    {
        return NPCs.TryGetValue(id, out var npc) ? npc : null;
    }

    // ════════════════════════════════════════════════════════════════
    //  内部逻辑
    // ════════════════════════════════════════════════════════════════

    private void SetState(CourtState newState)
    {
        CurrentState = newState;
        Debug.Log("[Court] State -> " + newState);
        OnStateChanged?.Invoke(newState);
    }

    private int CountPersuaded()
    {
        int count = 0;
        foreach (var kvp in NPCs)
            if (kvp.Value.IsPersuaded) count++;
        return count;
    }

    private void ApplyCardEffect(AkanaCardId cardId, CourtData.NPCId targetId, float bonusMultiplier = 1f)
    {
        if (!CourtData.CardEffects.TryGetValue(cardId, out var effect)) return;

        _usedCards.Add(cardId);
        AkanaManager.Instance?.ConsumeCard(cardId);

        // 混乱值高压削弱
        float multiplier = bonusMultiplier;
        if (!effect.ignoreChaosPenalty && ChaosManager.Instance != null
            && ChaosManager.Instance.CurrentChaos > CourtData.ChaosHighPressureThreshold)
        {
            multiplier *= CourtData.ChaosWeakenMultiplier;
            Debug.Log("[Court] 高压削弱生效！乘数 = " + multiplier);
        }

        if (bonusMultiplier > 1f)
            Debug.Log("[Court] LLM 证词加成生效！乘数 = " + bonusMultiplier);

        int emoDelta = Mathf.RoundToInt(effect.emotionalDelta * multiplier);
        int ratDelta = Mathf.RoundToInt(effect.rationalDelta * multiplier);

        if (effect.chaosDelta != 0 && ChaosManager.Instance != null)
            ChaosManager.Instance.AddChaos(effect.chaosDelta, "阿卡那牌 " + effect.cardName);

        if (effect.affectsAll)
        {
            foreach (var kvp in NPCs)
            {
                kvp.Value.rational += ratDelta;
                kvp.Value.emotional += emoDelta;
                OnNPCStatChanged?.Invoke(kvp.Value.id, kvp.Value.rational, kvp.Value.emotional);
            }
            Debug.Log("[Court] 权杖牌影响全体 NPC：理性 " + ratDelta + ", 感性 " + emoDelta);
        }
        else
        {
            if (NPCs.TryGetValue(targetId, out var npc))
            {
                npc.rational += ratDelta;
                npc.emotional += emoDelta;
                OnNPCStatChanged?.Invoke(npc.id, npc.rational, npc.emotional);
                Debug.Log("[Court] " + effect.cardName + " -> " + npc.name + "：理性 " + npc.rational + ", 感性 " + npc.emotional);
            }
        }
    }

    // ════════════════════════════════════════════════════════════════
    //  LLM 辅助
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// 异步生成本轮 NPC 发言并设置 _llmSpeechesReady 标志。
    /// 失败时静默 fallback 到硬编码发言。
    /// </summary>
    private async UniTaskVoid GenerateLLMSpeeches(int round, CourtData.Speech[] speeches)
    {
        try
        {
            // 收集本轮发言 NPC 的运行时状态
            var speakers = new List<LLMBridge.NPCStateSnapshot>();
            foreach (var speech in speeches)
            {
                if (NPCs.TryGetValue(speech.speaker, out var npc))
                {
                    speakers.Add(new LLMBridge.NPCStateSnapshot
                    {
                        name = npc.name,
                        rational = npc.rational,
                        emotional = npc.emotional,
                        isPersuaded = npc.IsPersuaded
                    });
                }
            }

            await LLMBridge.GenerateNPCSpeechesForRound(round, speakers.ToArray());
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[Court] LLM 发言生成异常，回退硬编码: " + e.Message);
        }
        finally
        {
            _llmSpeechesReady = true;
        }
    }
}
