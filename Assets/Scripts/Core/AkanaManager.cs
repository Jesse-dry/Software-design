using UnityEngine;
using System;
using System.Collections.Generic;

/// <summary>
/// 阿卡那牌收集管理器（跨场景持久 — 挂在 [MANAGERS] 下）。
///
/// 四张阿卡那牌：
///   权杖 — Corridor（走廊迷宫）胜利后获得
///   星币 — PipePuzzle（接水管）胜利后获得
///   宝剑 — ServerRoom（服务器室 Q&amp;A）第 2 题答对后获得
///   圣杯 — DecodeGame（拷贝小游戏）胜利后获得
///
/// 使用方式：
///   AkanaManager.Instance.CollectCard(AkanaCardId.权杖);
///   bool has = AkanaManager.Instance.HasCard(AkanaCardId.星币);
///   int count = AkanaManager.Instance.CollectedCount;
/// </summary>
public class AkanaManager : MonoBehaviour
{
    public static AkanaManager Instance { get; private set; }

    // ── 收集数据 ─────────────────────────────────────────────────
    private readonly HashSet<AkanaCardId> _collectedCards = new HashSet<AkanaCardId>();

    /// <summary>卡牌收集事件（参数为刚收集到的卡牌 ID）</summary>
    public event Action<AkanaCardId> OnCardCollected;

    /// <summary>已收集卡牌数量</summary>
    public int CollectedCount => _collectedCards.Count;

    /// <summary>是否四张全部收齐</summary>
    public bool AllCollected => _collectedCards.Count >= 4;

    // ══════════════════════════════════════════════════════════════
    //  生命周期
    // ══════════════════════════════════════════════════════════════

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        if (transform.parent == null)
            DontDestroyOnLoad(gameObject);
    }

    // ══════════════════════════════════════════════════════════════
    //  公共接口
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// 收集一张阿卡那牌。重复收集会被忽略。
    /// 收集后触发 OnCardCollected 事件并弹出 Toast 提示。
    /// </summary>
    public void CollectCard(AkanaCardId cardId)
    {
        if (_collectedCards.Contains(cardId))
        {
            Debug.Log($"[AkanaManager] 卡牌 {cardId} 已收集过，跳过。");
            return;
        }

        _collectedCards.Add(cardId);
        Debug.Log($"[AkanaManager] 收集阿卡那牌: {cardId}（{CollectedCount}/4）");

        // Toast 飘字提示
        if (UIManager.Instance != null && UIManager.Instance.Toast != null)
        {
            UIManager.Instance.Toast.Show(
                $"获得阿卡那牌「{GetCardDisplayName(cardId)}」！",
                colorType: ToastColor.Positive);
        }

        OnCardCollected?.Invoke(cardId);
    }

    /// <summary>是否已收集指定卡牌</summary>
    public bool HasCard(AkanaCardId cardId)
    {
        return _collectedCards.Contains(cardId);
    }

    /// <summary>获取所有已收集的卡牌 ID 列表（只读副本）</summary>
    public List<AkanaCardId> GetCollectedCards()
    {
        return new List<AkanaCardId>(_collectedCards);
    }

    /// <summary>重置所有收集数据（用于新游戏）</summary>
    public void ResetAll()
    {
        _collectedCards.Clear();
        Debug.Log("[AkanaManager] 所有阿卡那牌数据已重置。");
    }

    // ══════════════════════════════════════════════════════════════
    //  辅助
    // ══════════════════════════════════════════════════════════════

    /// <summary>获取卡牌的中文展示名</summary>
    public static string GetCardDisplayName(AkanaCardId cardId)
    {
        return cardId switch
        {
            AkanaCardId.权杖 => "权杖",
            AkanaCardId.星币 => "星币",
            AkanaCardId.宝剑 => "宝剑",
            AkanaCardId.圣杯 => "圣杯",
            _ => cardId.ToString()
        };
    }

    /// <summary>
    /// 获取卡牌对应的 ModalLayer 中描述面板 GameObject 名称。
    /// 例如：权杖 → "权杖牌"，与 UIRoot_Abyss.prefab 中 ModalLayer 子物体名对应。
    /// </summary>
    public static string GetCardPanelName(AkanaCardId cardId)
    {
        return cardId switch
        {
            AkanaCardId.权杖 => "权杖牌",
            AkanaCardId.星币 => "星币牌",
            AkanaCardId.宝剑 => "宝剑牌",
            AkanaCardId.圣杯 => "圣杯牌",
            _ => cardId.ToString() + "牌"
        };
    }
}

/// <summary>
/// 阿卡那牌 ID 枚举。
/// 名称与 akanaMenu.prefab 中 4 张牌的 GameObject 名完全一致。
/// </summary>
public enum AkanaCardId
{
    权杖,   // Corridor 胜利奖励
    星币,   // PipePuzzle 胜利奖励
    宝剑,   // ServerRoom Q&A 奖励
    圣杯,   // DecodeGame 胜利奖励
}
