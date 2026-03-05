using UnityEngine;

/// <summary>
/// 道具/卡牌数据（跨场景持久，由 DataManager 管理）。
/// 与 EvidenceData（仅庭审使用）分离。
/// 
/// 用途：阿卡那牌、记忆碎片等收集类道具。
/// </summary>
[System.Serializable]
public class ItemData
{
    [Tooltip("唯一标识")]
    public string id;

    [Tooltip("道具名称")]
    public string title;

    [TextArea(2, 5)]
    [Tooltip("道具描述/牌面文本")]
    public string description;

    [Tooltip("道具类型")]
    public ItemType type;

    [Tooltip("道具图标（卡面图）")]
    public Sprite icon;

    [Tooltip("是否已收集")]
    public bool isCollected;

    [Tooltip("收集时间戳（用于排序）")]
    [HideInInspector]
    public float collectTime;
}
