using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 庭审前的游戏状态快照。
/// 
/// 在进入 Court 场景前捕获，用于庭审失败后"重新庭审"时
/// 将所有数值恢复到庭审开始前的状态。
/// 
/// 捕获内容：
///   - 阿卡那牌收集状态
///   - 混乱值
///   - DataManager 中的道具 / 证据状态
/// </summary>
[Serializable]
public class CourtDataSnapshot
{
    // ── 阿卡那牌 ──
    public List<AkanaCardId> collectedCards = new();

    // ── 混乱值 ──
    public int chaosValue;

    // ── 道具收集状态（id → isCollected） ──
    public List<ItemSnapshot> items = new();

    // ── 证据解锁状态（id → isUnlocked） ──
    public List<EvidenceSnapshot> evidences = new();

    [Serializable]
    public struct ItemSnapshot
    {
        public string id;
        public bool isCollected;
        public float collectTime;
    }

    [Serializable]
    public struct EvidenceSnapshot
    {
        public string id;
        public bool isUnlocked;
    }

    // ══════════════════════════════════════════════════════════════
    //  静态工厂：捕获当前状态
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// 捕获当前所有管理器的状态，生成快照。
    /// 在进入庭审场景前调用。
    /// </summary>
    public static CourtDataSnapshot CaptureNow()
    {
        var snap = new CourtDataSnapshot();

        // 1. 阿卡那牌
        if (AkanaManager.Instance != null)
        {
            snap.collectedCards = AkanaManager.Instance.GetCollectedCards();
        }

        // 2. 混乱值
        if (ChaosManager.Instance != null)
        {
            snap.chaosValue = ChaosManager.Instance.CurrentChaos;
        }

        // 3. 道具 & 证据（通过 DataManager 反射访问或公共接口）
        if (DataManager.Instance != null)
        {
            snap.items = DataManager.Instance.CaptureItemSnapshots();
            snap.evidences = DataManager.Instance.CaptureEvidenceSnapshots();
        }

        Debug.Log($"[CourtDataSnapshot] 快照已捕获: " +
                  $"阿卡那牌={snap.collectedCards.Count}, " +
                  $"混乱值={snap.chaosValue}, " +
                  $"道具={snap.items.Count}, " +
                  $"证据={snap.evidences.Count}");
        return snap;
    }

    // ══════════════════════════════════════════════════════════════
    //  恢复快照
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// 将所有管理器的状态恢复到快照时刻。
    /// 庭审失败后"重新庭审"时调用。
    /// </summary>
    public void Restore()
    {
        // 1. 阿卡那牌
        if (AkanaManager.Instance != null)
        {
            AkanaManager.Instance.ResetAll();
            foreach (var cardId in collectedCards)
            {
                AkanaManager.Instance.RestoreCard(cardId);
            }
        }

        // 2. 混乱值
        if (ChaosManager.Instance != null)
        {
            ChaosManager.Instance.SetChaos(chaosValue);
        }

        // 3. 道具 & 证据
        if (DataManager.Instance != null)
        {
            DataManager.Instance.RestoreFromSnapshot(items, evidences);
        }

        Debug.Log("[CourtDataSnapshot] 快照已恢复。");
    }
}
