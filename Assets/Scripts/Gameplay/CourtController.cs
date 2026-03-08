using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

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
        if (GameManager.Instance == null || !GameManager.Instance.IsInCourt())
        {
            enabled = false;
            return;
        }

        InitializeNPCs();
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

            // 2a. NPC 依次发言
            for (int s = 0; s < speeches.Length; s++)
            {
                CurrentSpeechIndex = s;
                SetState(CourtState.NPCSpeech);

                _speechClosed = false;
                OnNPCSpeech?.Invoke(speeches[s].speaker, speeches[s].text);
                yield return new WaitUntil(() => _speechClosed);
            }

            // 2b. 弹出阿卡那菜单
            SetState(CourtState.AkanaMenu);
            _cardChosen = false;
            yield return new WaitUntil(() => _cardChosen);

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
                    yield return new WaitUntil(() => _cardChosen);
                    continue;
                }
                cardPlayed = true;
            }

            // 2d. 选择目标（权杖牌跳过）
            var effect = CourtData.CardEffects[SelectedCard];
            if (effect.affectsAll)
            {
                ApplyCardEffect(SelectedCard, CourtData.NPCId.皇帝);
            }
            else
            {
                SetState(CourtState.SelectTarget);
                _targetChosen = false;
                yield return new WaitUntil(() => _targetChosen);
                ApplyCardEffect(SelectedCard, _selectedTarget);
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
    private bool _targetChosen = false;
    private CourtData.NPCId _selectedTarget;

    private enum CardDetailAction { None, Play, Back }
    private CardDetailAction _cardDetailAction = CardDetailAction.None;

    public void NotifyRulePanelClosed()  { _rulePanelClosed = true; }
    public void NotifySpeechClosed()     { _speechClosed = true; }

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

    private void ApplyCardEffect(AkanaCardId cardId, CourtData.NPCId targetId)
    {
        if (!CourtData.CardEffects.TryGetValue(cardId, out var effect)) return;

        _usedCards.Add(cardId);
        AkanaManager.Instance?.ConsumeCard(cardId);

        // 混乱值高压削弱
        float multiplier = 1f;
        if (!effect.ignoreChaosPenalty && ChaosManager.Instance != null
            && ChaosManager.Instance.CurrentChaos > CourtData.ChaosHighPressureThreshold)
        {
            multiplier = CourtData.ChaosWeakenMultiplier;
            Debug.Log("[Court] 高压削弱生效！乘数 = " + multiplier);
        }

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
}
