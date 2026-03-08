using System.Collections.Generic;

/// <summary>
/// 庭审静态数据：NPC 属性、发言内容、阿卡那牌效果。
/// 所有数值均来自设计文档《庭审设计.md》。
/// </summary>
public static class CourtData
{
    // ══════════════════════════════════════════════════════════════
    //  NPC 身份枚举
    // ══════════════════════════════════════════════════════════════

    public enum NPCId { 皇帝, 恋人, 商人, 正义 }

    // ══════════════════════════════════════════════════════════════
    //  NPC 基础属性
    // ══════════════════════════════════════════════════════════════

    public struct NPCProfile
    {
        public NPCId id;
        public string name;
        public int initRational;
        public int initEmotional;
        public int rationalThreshold;
        /// <summary>感性阈值。-1 表示免疫（正义）。</summary>
        public int emotionalThreshold;
    }

    public static readonly NPCProfile[] NPCProfiles =
    {
        new NPCProfile
        {
            id = NPCId.皇帝, name = "皇帝",
            initRational = 80, initEmotional = 0,
            rationalThreshold = 40, emotionalThreshold = 40,
        },
        new NPCProfile
        {
            id = NPCId.恋人, name = "恋人",
            initRational = 60, initEmotional = 30,
            rationalThreshold = 50, emotionalThreshold = 50,
        },
        new NPCProfile
        {
            id = NPCId.商人, name = "商人",
            initRational = 70, initEmotional = 20,
            rationalThreshold = 50, emotionalThreshold = 40,
        },
        new NPCProfile
        {
            id = NPCId.正义, name = "正义",
            initRational = 80, initEmotional = 0,
            rationalThreshold = 50, emotionalThreshold = -1,
        },
    };

    // ══════════════════════════════════════════════════════════════
    //  回合发言（4 回合）
    // ══════════════════════════════════════════════════════════════

    public struct Speech
    {
        public NPCId speaker;
        public string text;
    }

    public const int TotalRounds = 4;

    public static readonly Speech[][] RoundSpeeches =
    {
        // ── 第 1 回合 ──
        new[]
        {
            new Speech { speaker = NPCId.皇帝, text = "秩序是共识的根基。任何动摇档案体系的行为，都必须被严厉清算。" },
            new Speech { speaker = NPCId.恋人, text = "可是……如果那些档案记录的，不是历史，而是某个人最深的记忆呢？" },
            new Speech { speaker = NPCId.商人, text = "感情归感情，但盗取核心档案会引发整个底层经济的震荡。这笔账不能不算。" },
        },
        // ── 第 2 回合 ──
        new[]
        {
            new Speech { speaker = NPCId.皇帝, text = "他竟敢利用最高权限篡改记录？这已经不止是违规——这是对权力结构的挑衅。" },
            new Speech { speaker = NPCId.恋人, text = "可他篡改的那段记忆……是关于一场被掩盖的屠杀。他只是想让真相不被抹去。" },
            new Speech { speaker = NPCId.商人, text = "真相归真相。但如果每个人都自行篡改档案，市场和信用体系将彻底崩塌。" },
        },
        // ── 第 3 回合 ──
        new[]
        {
            new Speech { speaker = NPCId.皇帝, text = "就算他所言为实，程序正义不可被个人英雄主义取代。" },
            new Speech { speaker = NPCId.商人, text = "我倒是想知道——谁来赔偿因档案混乱导致的合约纠纷？" },
            new Speech { speaker = NPCId.正义, text = "我只看事实。如果有证据能证明他的行为拯救了更多人，我会重新考量。" },
        },
        // ── 第 4 回合 ──
        new[]
        {
            new Speech { speaker = NPCId.皇帝, text = "最终裁决在即。各位陪审，请慎重。" },
            new Speech { speaker = NPCId.恋人, text = "我从一开始就相信他。记忆不应该被审判。" },
            new Speech { speaker = NPCId.商人, text = "好吧，给我看看最后的证据。如果值得，我可以改变立场。" },
        },
    };

    // ══════════════════════════════════════════════════════════════
    //  阿卡那牌效果
    // ══════════════════════════════════════════════════════════════

    public struct CardEffect
    {
        public AkanaCardId cardId;
        public string cardName;
        public string evidenceTitle;
        public string description;
        public int emotionalDelta;
        public int rationalDelta;
        public int chaosDelta;
        public bool affectsAll;
        public bool ignoreChaosPenalty;
    }

    public static readonly Dictionary<AkanaCardId, CardEffect> CardEffects =
        new Dictionary<AkanaCardId, CardEffect>
    {
        {
            AkanaCardId.圣杯, new CardEffect
            {
                cardId = AkanaCardId.圣杯,
                cardName = "圣杯牌\u00B7被掩埋的记忆",
                evidenceTitle = "\u300A罪徒的虐杀档案\u300B",
                description = "你找到了那段被系统标记为\u2018已销毁\u2019的底层虐杀影像。画面中，无数底层居民在毫无预警的清洗行动中被抹除记忆、甚至被物理消除。这段影像是罪徒冒死从核心档案库中复制出来的\u2014\u2014也是他被指控的核心证据。",
                emotionalDelta = 0,
                rationalDelta = -30,
                chaosDelta = -15,
                affectsAll = false,
                ignoreChaosPenalty = false,
            }
        },
        {
            AkanaCardId.宝剑, new CardEffect
            {
                cardId = AkanaCardId.宝剑,
                cardName = "宝剑牌\u00B7掩盖的行为",
                evidenceTitle = "\u300A被截获的\u201C物理级销毁\u201D指令单\u300B",
                description = "这不是普通的管道破裂。指令单显示，高层为了掩盖那段被你带走的虐杀记忆，甚至下达了\u2018切断冷却液并使主管道超载\u2019的命令。他们企图用水淹没底层服务器，把剩下的罪证连同这层楼一起进行物理级的抹杀！",
                emotionalDelta = 20,
                rationalDelta = -10,
                chaosDelta = 0,
                affectsAll = false,
                ignoreChaosPenalty = false,
            }
        },
        {
            AkanaCardId.星币, new CardEffect
            {
                cardId = AkanaCardId.星币,
                cardName = "星币牌\u00B7扭曲的账本",
                evidenceTitle = "\u300A底层经济抽取报告\u300B",
                description = "一本伪装成合规财务报告的文件中，隐藏着对底层资源的系统性抽取记录。底层居民创造的价值被以\u2018系统维护费\u2019\u2018网络负载均衡税\u2019等名目逐层抽走，而这些税目在上层账本中根本不存在。商人是这套规则的既得利益者。",
                emotionalDelta = 15,
                rationalDelta = -25,
                chaosDelta = 0,
                affectsAll = false,
                ignoreChaosPenalty = false,
            }
        },
        {
            AkanaCardId.权杖, new CardEffect
            {
                cardId = AkanaCardId.权杖,
                cardName = "权杖牌\u00B7走廊的真相",
                evidenceTitle = "\u300A罪徒的逃亡日志\u300B",
                description = "在逃亡途中，罪徒在服务器走廊的隐秘角落记下了一组日志。他详细描述了自己如何目睹底层数据被系统性篡改，以及他如何在追杀中保护那段虐杀记忆不被永久删除。",
                emotionalDelta = 30,
                rationalDelta = -30,
                chaosDelta = 0,
                affectsAll = true,
                ignoreChaosPenalty = true,
            }
        },
    };

    // ══════════════════════════════════════════════════════════════
    //  混乱值高压阈值与削弱系数
    // ══════════════════════════════════════════════════════════════

    public const int ChaosHighPressureThreshold = 80;
    public const float ChaosWeakenMultiplier = 0.5f;
}
