using System;

namespace LLMModule.Data
{
    // ================================================================
    //  章节策划配置数据结构
    //  从 StreamingAssets/Data/chapter_XX.json 加载
    //  所有字段均为【静态策划数据】，运行时不修改
    // ================================================================

    /// <summary>
    /// 一个完整章节的策划配置，包含证据收集 + 庭审所需的全部静态数据。
    /// </summary>
    [Serializable]
    public class ChapterConfig
    {
        /// <summary>章节标识，如 "罪徒，代号01"</summary>
        public string chapter;

        /// <summary>已确认事实列表（本章固定，所有场景共用）</summary>
        public string[] confirmedFacts;

        /// <summary>证据收集配置</summary>
        public EvidenceConfig evidence;

        /// <summary>庭审配置</summary>
        public TrialConfig trial;
    }

    /// <summary>
    /// 证据收集场景的静态配置
    /// </summary>
    [Serializable]
    public class EvidenceConfig
    {
        /// <summary>需要生成的卡牌定义数组</summary>
        public EvidenceCardDefinition[] cardDefinitions;
    }

    /// <summary>
    /// 庭审场景的静态配置
    /// </summary>
    [Serializable]
    public class TrialConfig
    {
        /// <summary>庭审议题</summary>
        public string topic;

        /// <summary>本场庭审所有 NPC 的静态信息</summary>
        public NPCConfig[] npcs;
    }

    /// <summary>
    /// 单个 NPC 的策划配置（含静态设定 + 初始数值）。
    /// 庭审开始时，initialReasonLevel/initialEmotionLevel 会拷贝为运行时状态。
    /// </summary>
    [Serializable]
    public class NPCConfig
    {
        /// <summary>NPC 名称，如 "皇帝"</summary>
        public string name;

        /// <summary>角色设定描述</summary>
        public string roleSetting;

        /// <summary>初始立场，如 "支持定罪"</summary>
        public string initialStance;

        /// <summary>理性门槛（reasonLevel 低于此值时被说服）</summary>
        public int reasonThreshold;

        /// <summary>感性门槛（emotionLevel 高于此值时被说服）</summary>
        public int emotionThreshold;

        /// <summary>庭审开始时的理性值初始值</summary>
        public int initialReasonLevel;

        /// <summary>庭审开始时的感性值初始值</summary>
        public int initialEmotionLevel;

        // ── 工具方法 ──

        /// <summary>
        /// 转换为 LLM 请求所需的 NPCTrialInfo（不含运行时数值）
        /// </summary>
        public NPCTrialInfo ToTrialInfo()
        {
            return new NPCTrialInfo
            {
                name = this.name,
                roleSetting = this.roleSetting,
                initialStance = this.initialStance,
                reasonThreshold = this.reasonThreshold,
                emotionThreshold = this.emotionThreshold
            };
        }
    }
}
