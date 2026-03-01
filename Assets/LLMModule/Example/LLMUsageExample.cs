using Cysharp.Threading.Tasks;
using LLMModule;
using UnityEngine;

/// <summary>
/// 调用示例 —— 展示其他游戏模块如何使用 LLM 接口。
/// 此文件仅作参考，不需要放入正式项目。
/// </summary>
public class LLMUsageExample : MonoBehaviour
{
    [SerializeField] private LLMService llmService;

    // ─── 示例一：证据收集场景 ─────────────────────────────────

    public async UniTaskVoid Example_GenerateEvidenceCards()
    {
        var request = new EvidenceRequest
        {
            chapter = "罪徒，代号01",
            confirmedFacts = new[]
            {
                "记忆托管机构主要服务对象为权贵、富豪、政客等，客户可以将自己带有罪证的记忆抽取出来托管在中心的服务器中。客户本体将忘记这段记忆，从而逃避法律的测谎",
                "该场景某富豪的记忆显示他虐杀少女并通过记忆托管逃避法律责任",
                "行业有一条共识秩序《绝对静默》：档案员仅作为数据的容器与搬运工。严禁查看、严禁拷贝、严禁对客户记忆产生任何主观解读。数据即是数据，无关善恶。",
                "01私自窥探富豪记忆并拷贝，决心带记忆逃离公司伸张正义"
            },
            cardDefinitions = new[]
            {
                new EvidenceCardDefinition
                {
                    name = "星币牌·肮脏的交易",
                    evidenceDescription = "窃听到保安队长和高管的对话"
                },
                new EvidenceCardDefinition
                {
                    name = "宝剑牌·掩盖的行为",
                    evidenceDescription = "一段违规删除记录"
                },
                new EvidenceCardDefinition
                {
                    name = "圣杯牌·破碎的家庭",
                    evidenceDescription = "受害者女孩的全家福"
                },
                new EvidenceCardDefinition
                {
                    name = "权杖牌·受害者的遗物",
                    evidenceDescription = "小女孩的头绳"
                }
            }
        };

        CardData[] cards = await llmService.Generator.GenerateEvidenceCards(request);

        foreach (var card in cards)
        {
            Debug.Log($"[{card.name}] {card.text}");
            // → 将 card.text 显示到 UI 上，card 对象可直接传给庭审模块使用
        }
    }

    // ─── 示例二：庭审 —— NPC 发言 ─────────────────────────────

    public async UniTaskVoid Example_GenerateNPCSpeeches()
    {
        var request = new NPCSpeechRequest
        {
            chapter = "罪徒，代号01",
            confirmedFacts = new[]
            {
                "记忆托管机构主要服务对象为权贵、富豪、政客等",
                "该场景某富豪的记忆显示他虐杀少女并通过记忆托管逃避法律责任",
                "行业有一条共识秩序《绝对静默》",
                "01私自窥探富豪记忆并拷贝，决心带记忆逃离公司伸张正义",
                "本庭审议题：是否判决01有罪"
            },
            topic = "是否维持对"罪徒"的指控（罪徒是否应该被放逐）",
            allNPCs = new[]
            {
                new NPCTrialInfo
                {
                    name = "皇帝",
                    roleSetting = "管理层代表，极度理性，强调规则与秩序",
                    initialStance = "支持定罪",
                    reasonThreshold = 80,
                    emotionThreshold = 20
                },
                new NPCTrialInfo
                {
                    name = "正义",
                    roleSetting = "法务代表，理性与情感平衡，关注程序正义",
                    initialStance = "支持定罪",
                    reasonThreshold = 60,
                    emotionThreshold = 40
                },
                new NPCTrialInfo
                {
                    name = "恋人",
                    roleSetting = "底层员工，极度感性，容易被真相打动",
                    initialStance = "动摇中",
                    reasonThreshold = 30,
                    emotionThreshold = 70
                }
            },
            speakers = new[]
            {
                new NPCSpeechTarget
                {
                    name = "皇帝",
                    reasonLevel = 85,     // 当前理性值
                    emotionLevel = 15,    // 当前感性值
                    isPersuaded = false   // 游戏逻辑判定：85 >= 80 且 15 <= 20 → 未说服
                },
                new NPCSpeechTarget
                {
                    name = "恋人",
                    reasonLevel = 40,
                    emotionLevel = 75,
                    isPersuaded = true    // 75 > 70 → 已被说服
                }
            }
        };

        NPCSpeechResult[] speeches = await llmService.Generator.GenerateNPCSpeeches(request);

        foreach (var s in speeches)
        {
            Debug.Log($"[{s.name}]: {s.speech}");
            // → 将 s.speech 显示到庭审 UI 对话框中
        }
        //显示如何输出到UI
    }

    // ─── 示例三：庭审 —— 评估玩家证词 ─────────────────────────

    public async UniTaskVoid Example_EvaluateArgument()
    {
        // 假设玩家打出了 "星币牌·肮脏的交易" 并输入了一段证词
        var request = new ArgumentEvalRequest
        {
            chapter = "罪徒，代号01",
            confirmedFacts = new[]
            {
                "记忆托管机构主要服务对象为权贵、富豪、政客等",
                "该场景某富豪的记忆显示他虐杀少女并通过记忆托管逃避法律责任",
                "行业有一条共识秩序《绝对静默》",
                "01私自窥探富豪记忆并拷贝，决心带记忆逃离公司伸张正义"
            },
            topic = "是否维持对"罪徒"的指控",
            cardName = "星币牌·肮脏的交易",
            cardText = "（此处为之前生成的牌面文本）",
            argument = "这份契约的标的物本身就是违法的。组织收受了封口费，这不再是商业契约，而是共谋犯罪。"
        };

        int score = await llmService.Generator.EvaluatePlayerArgument(request);

        Debug.Log($"证词评分: {score}/10");
        // → 将 score 传回游戏逻辑，用于计算 NPC 理性值/感性值变化
    }
}
