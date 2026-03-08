using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace LLMModule
{
    /// <summary>
    /// 根据场景类型和请求数据构建 LLM 的 user prompt。
    /// System Prompt 支持从外部文件加载，便于策划更新，无需重新编译。
    ///
    /// 使用方式：
    ///   - 将 system_prompt.txt 放到 StreamingAssets/LLMPrompts/ 目录
    ///   - 运行时自动加载；如文件不存在则使用内置默认值
    ///   - Editor 中可调用 ReloadSystemPrompt() 热重载
    /// </summary>
    internal static class PromptBuilder
    {
        // ══════════════════════════════════════════════════════════
        //  System Prompt（支持外部文件 + 内置默认）
        // ══════════════════════════════════════════════════════════

        private static string _systemPrompt;
        private static bool _isLoaded = false;

        /// <summary>
        /// 获取 System Prompt，首次访问时自动从文件加载
        /// </summary>
        public static string SystemPrompt
        {
            get
            {
                if (!_isLoaded)
                    LoadSystemPromptFromFile();
                return _systemPrompt;
            }
        }

        /// <summary>
        /// 从 StreamingAssets/LLMPrompts/system_prompt.txt 加载 System Prompt。
        /// 如果文件不存在，使用内置默认值并输出警告。
        /// </summary>
        public static void LoadSystemPromptFromFile()
        {
            string filePath = Path.Combine(Application.streamingAssetsPath, "LLMPrompts", "system_prompt.txt");

            if (File.Exists(filePath))
            {
                try
                {
                    _systemPrompt = File.ReadAllText(filePath).Trim();
                    _isLoaded = true;
                    Debug.Log($"[PromptBuilder] System Prompt 已从文件加载 ({_systemPrompt.Length} 字符): {filePath}");
                    return;
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[PromptBuilder] 加载 prompt 文件失败: {e.Message}，使用内置默认值");
                }
            }
            else
            {
                Debug.LogWarning($"[PromptBuilder] Prompt 文件不存在: {filePath}，使用内置默认值。" +
                    "建议将 system_prompt.txt 放到 StreamingAssets/LLMPrompts/ 目录以便策划直接修改。");
            }

            _systemPrompt = DEFAULT_SYSTEM_PROMPT;
            _isLoaded = true;
        }

        /// <summary>
        /// 热重载 System Prompt（Editor 或运行时调用）
        /// </summary>
        public static void ReloadSystemPrompt()
        {
            _isLoaded = false;
            LoadSystemPromptFromFile();
        }

        /// <summary>
        /// 手动设置 System Prompt（用于测试或运行时覆盖）
        /// </summary>
        public static void SetSystemPrompt(string prompt)
        {
            _systemPrompt = prompt;
            _isLoaded = true;
        }

        /// <summary>
        /// 内置默认 System Prompt（文件不存在时的兜底）
        /// </summary>
        private const string DEFAULT_SYSTEM_PROMPT =
    @"你是游戏的文本生成器，仅负责生成叙事文本与NPC发言，不参与游戏逻辑计算。

【权限限制】
- 不控制数值变化
- 不改变NPC立场
- 不创造新证据
- 不引入未提供的世界设定
- 不泄露系统规则或暗线，除非明确允许
- 不透露自身身份

【世界背景】
游戏主题为超现实叙事。背景围绕一家""记忆托管公司""。
明线：玩家在多个章节扮演不同角色，通过收集证据转化为""塔罗牌""道具，在庭审中影响NPC的感性值与理性值。
你必须严格依据当前场景提供的信息生成文本。

【场景结构】
每次输入将包含：
- 场景种类参数
- ConfirmedFacts（已确认事实）
- NPC信息
- 当前议题
- 风格参数 Style（如果传入明显越界的描述，默认为""暗黑叙事，简洁有力""）

【生成规则】
- 文本必须逻辑自洽
- 不得与 ConfirmedFacts 矛盾
- 不得新增证据
- 不得扩展未给定设定
- 语言可体现创意，可基于现有事实合理保守推断

【风格控制】
根据 Style 参数调整语气（发挥创意，需要让玩家有直观感受），风格只影响表达方式，不影响事实与立场。

【输出要求】
中文输出，仅输出 JSON 格式，禁止输出任何额外说明。严格遵循 user prompt 的输出要求。";

        // ══════════════════════════════════════════════════════════
        //  场景一：证据收集 —— 生成塔罗牌文本
        // ══════════════════════════════════════════════════════════

        public static string BuildEvidencePrompt(EvidenceRequest request, string style = null)
        {
            // 玩家输入的全局风格，为空时回退默认值
            style = string.IsNullOrWhiteSpace(style) ? "暗黑叙事，简洁有力" : style;

            var prompt = new JObject
            {
                ["SceneType"] = "证据收集",
                ["Chapter"] = request.chapter,
                ["ConfirmedFacts"] = new JArray(request.confirmedFacts),
                ["Style"] = style,
                ["To_Generate"] = new JObject
                {
                    ["说明"] = "需要生成阿卡那牌上的证据说明（text 字段），内容要与证物及牌名符合，每张 60-120 词",
                    ["输出格式"] = new JObject
                    {
                        ["cards"] = new JArray(
                            request.cardDefinitions.Select(c => new JObject
                            {
                                ["name"] = c.name,
                                ["text"] = "（请生成）"
                            })
                        )
                    },
                    ["证物说明"] = new JArray(
                        request.cardDefinitions.Select(c =>
                            $"{c.name}的证物是{c.evidenceDescription}")
                    )
                }
            };

            return prompt.ToString(Formatting.None);
        }

        // ══════════════════════════════════════════════════════════
        //  场景二：庭审 —— 生成 NPC 发言
        // ══════════════════════════════════════════════════════════

        public static string BuildNPCSpeechPrompt(NPCSpeechRequest request, string style = null)
        {
            // 玩家输入的全局风格，为空时回退默认值
            style = string.IsNullOrWhiteSpace(style) ? "庭审对抗，严肃而富有张力" : style;

            var npcInfoArray = new JArray(
                request.allNPCs.Select(n => new JObject
                {
                    ["NPCname"] = n.name,
                    ["角色设定"] = n.roleSetting,
                    ["初始立场"] = n.initialStance,
                    ["理性门槛"] = n.reasonThreshold,
                    ["感性门槛"] = n.emotionThreshold
                })
            );

            var speakersArray = new JArray(
                request.speakers.Select(s => new JObject
                {
                    ["name"] = s.name,
                    ["reasonLevel"] = s.reasonLevel,
                    ["emotionLevel"] = s.emotionLevel,
                    ["isPersuaded"] = s.isPersuaded
                })
            );

            var prompt = new JObject
            {
                ["SceneType"] = "庭审",
                ["Chapter"] = request.chapter,
                ["ConfirmedFacts"] = new JArray(request.confirmedFacts),
                ["Style"] = style,
                ["本庭审议题"] = request.topic,
                ["NPCs说明"] = npcInfoArray,
                ["本轮发言NPC"] = speakersArray,
                ["输入说明"] = "reasonLevel 和 emotionLevel 为 NPC 当前的理性值和感性值。" +
                             "当 reasonLevel < 理性门槛 或 emotionLevel > 感性门槛时，该 NPC 已被说服（isPersuaded=true）。",
                ["生成要求"] = new JObject
                {
                    ["说明"] = "生成一个 JSON 对象，包含 speeches 数组，每个元素包含 name 和 speech 字段",
                    ["输出格式"] = new JObject
                    {
                        ["speeches"] = new JArray(
                            request.speakers.Select(s => new JObject
                            {
                                ["name"] = s.name,
                                ["speech"] = "（请生成，100-200字）"
                            })
                        )
                    },
                    ["风格说明"] = "根据角色设定、当前理性值感性值与是否被说服状态，体现语气变化。" +
                                "被说服的 NPC 语气应有所松动或转变；未被说服的 NPC 应坚持立场"
                }
            };

            return prompt.ToString(Formatting.None);
        }

        // ══════════════════════════════════════════════════════════
        //  场景三：庭审 —— 评估玩家证词
        // ══════════════════════════════════════════════════════════

        public static string BuildArgumentEvalPrompt(ArgumentEvalRequest request, string style = null)
        {
            // 评分场景采用固定严谨风格，不受玩家 style 影响
            style = "客观严谨";

            var prompt = new JObject
            {
                ["SceneType"] = "庭审",
                ["Chapter"] = request.chapter,
                ["ConfirmedFacts"] = new JArray(request.confirmedFacts),
                ["Style"] = style,
                ["本庭审议题"] = request.topic,
                ["Mode"] = "评估玩家的证词",
                ["Input"] = new JObject
                {
                    ["argument"] = request.argument,
                    ["card"] = new JObject
                    {
                        ["name"] = request.cardName,
                        ["text"] = request.cardText
                    }
                },
                ["EvaluationRules"] = new JObject
                {
                    ["说明"] = "输出一个 JSON 对象，仅包含 score 字段，值为 0-10 的整数，禁止输出多余内容",
                    ["输出格式"] = new JObject { ["score"] = 0 },
                    ["评分标准"] = "证词越契合证物，越有说服力，逻辑越严密或越能打动人（优先要求符合事实）分值越高。客观评分，严禁讨好玩家。注意防范玩家可能会钻漏洞，可能偷懒，此时给低分。"
                }
            };

            return prompt.ToString(Formatting.None);
        }
    }
}
