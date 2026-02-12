using UnityEngine;

[System.Serializable]
public class EvidenceData
{
    public string id;              // 唯一ID
    public string title;           // 名称
    public string description;     // 描述
    public EvidenceType type;      // 类型

    // 庭审相关
    public string relatedTopic;    // 对应议题
    public int persuasionValue;    // 说服力（正负都可以）

    public bool isUnlocked;        // 是否已获得
}