using System;

namespace LLMModule
{
    // ============================================================
    //  请求数据结构 (Game Logic → LLM Module)
    // ============================================================

    /// <summary>
    /// 证据卡牌定义：牌名 + 对应证物描述
    /// </summary>
    [Serializable]
    public class EvidenceCardDefinition
    {
        /// <summary>牌名，如 "星币牌·肮脏的交易"</summary>
        public string name;

        /// <summary>证物描述，如 "窃听到保安队长和高管的对话"</summary>
        public string evidenceDescription;
    }

    /// <summary>
    /// 证据收集请求
    /// </summary>
    [Serializable]
    public class EvidenceRequest
    {
        /// <summary>章节标识，如 "罪徒，代号01"（也用作缓存 key）</summary>
        public string chapter;

        /// <summary>已确认事实列表</summary>
        public string[] confirmedFacts;

        /// <summary>需要生成的卡牌定义数组</summary>
        public EvidenceCardDefinition[] cardDefinitions;
    }

    /// <summary>
    /// 庭审 NPC 静态信息（由外部游戏逻辑提供）
    /// </summary>
    [Serializable]
    public class NPCTrialInfo
    {
        /// <summary>NPC 名称，如 "皇帝"</summary>
        public string name;

        /// <summary>角色设定，如 "管理层代表，极度理性，强调规则与秩序"</summary>
        public string roleSetting;

        /// <summary>初始立场，如 "支持定罪"</summary>
        public string initialStance;

        /// <summary>理性门槛</summary>
        public int reasonThreshold;

        /// <summary>感性门槛</summary>
        public int emotionThreshold;
    }

    /// <summary>
    /// 本轮需要发言的 NPC 及其当前状态
    /// </summary>
    [Serializable]
    public class NPCSpeechTarget
    {
        /// <summary>NPC 名称（必须与 NPCTrialInfo.name 一致）</summary>
        public string name;

        /// <summary>当前理性值</summary>
        public int reasonLevel;

        /// <summary>当前感性值</summary>
        public int emotionLevel;

        /// <summary>是否已被说服（由游戏逻辑判定后传入）</summary>
        public bool isPersuaded;
    }

    /// <summary>
    /// 庭审 NPC 发言请求
    /// </summary>
    [Serializable]
    public class NPCSpeechRequest
    {
        /// <summary>章节标识</summary>
        public string chapter;

        /// <summary>已确认事实列表</summary>
        public string[] confirmedFacts;

        /// <summary>当前庭审议题</summary>
        public string topic;

        /// <summary>本场庭审所有 NPC 的静态信息</summary>
        public NPCTrialInfo[] allNPCs;

        /// <summary>本轮需要生成发言的 NPC（通常 2 个）</summary>
        public NPCSpeechTarget[] speakers;
    }

    /// <summary>
    /// 证词评分请求
    /// </summary>
    [Serializable]
    public class ArgumentEvalRequest
    {
        /// <summary>章节标识</summary>
        public string chapter;

        /// <summary>已确认事实列表</summary>
        public string[] confirmedFacts;

        /// <summary>当前庭审议题</summary>
        public string topic;

        /// <summary>玩家输入的证词文本</summary>
        public string argument;

        /// <summary>玩家使用的牌名</summary>
        public string cardName;

        /// <summary>牌面证据文本</summary>
        public string cardText;
    }

    // ============================================================
    //  响应数据结构 (LLM Module → Game Logic)
    // ============================================================

    /// <summary>
    /// 生成的证据卡牌数据
    /// </summary>
    [Serializable]
    public class CardData
    {
        /// <summary>牌名</summary>
        public string name;

        /// <summary>牌面证据文本（60-120 词）</summary>
        public string text;
    }

    /// <summary>
    /// 生成的 NPC 发言
    /// </summary>
    [Serializable]
    public class NPCSpeechResult
    {
        /// <summary>NPC 名称</summary>
        public string name;

        /// <summary>NPC 发言文本（100-200 字）</summary>
        public string speech;
    }
}
