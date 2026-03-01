using System;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace LLMModule
{
    /// <summary>
    /// 将 LLM 返回的 JSON 字符串解析为游戏数据结构。
    /// 内置容错：自动搜索数组/字段，兼容 LLM 输出的不同 key 命名。
    /// </summary>
    internal static class ResponseParser
    {
        /// <summary>
        /// 解析证据卡牌生成结果
        /// 期望格式: { "cards": [ { "name": "...", "text": "..." }, ... ] }
        /// </summary>
        public static CardData[] ParseEvidenceCards(string json)
        {
            try
            {
                var root = JObject.Parse(json);

                // 尝试多种可能的 key
                JToken cardsToken = root["cards"] ?? root["cards_01"];

                // 兜底：找第一个 JArray 类型的属性
                if (cardsToken == null)
                {
                    foreach (var prop in root.Properties())
                    {
                        if (prop.Value is JArray arr && arr.Count > 0 &&
                            arr[0]["name"] != null && arr[0]["text"] != null)
                        {
                            cardsToken = arr;
                            break;
                        }
                    }
                }

                if (cardsToken == null)
                    throw new LLMException("无法在 LLM 响应中找到 cards 数组");

                var cards = cardsToken.ToObject<CardData[]>();

                // 基础校验
                foreach (var card in cards)
                {
                    if (string.IsNullOrEmpty(card.name) || string.IsNullOrEmpty(card.text))
                        Debug.LogWarning($"[LLM] 证据卡牌数据不完整: name={card.name}");
                }

                return cards;
            }
            catch (LLMException) { throw; }
            catch (Exception e)
            {
                Debug.LogError($"[LLM] 解析证据卡牌失败: {e.Message}\n原始响应: {json}");
                throw new LLMException($"解析证据卡牌失败: {e.Message}", e);
            }
        }

        /// <summary>
        /// 解析 NPC 发言结果
        /// 期望格式: { "speeches": [ { "name": "...", "speech": "..." }, ... ] }
        /// </summary>
        public static NPCSpeechResult[] ParseNPCSpeeches(string json)
        {
            try
            {
                var root = JObject.Parse(json);

                JToken speechesToken = root["speeches"];

                // 兜底：找第一个含 name/speech 字段的 JArray
                if (speechesToken == null)
                {
                    foreach (var prop in root.Properties())
                    {
                        if (prop.Value is JArray arr && arr.Count > 0 &&
                            arr[0]["name"] != null && arr[0]["speech"] != null)
                        {
                            speechesToken = arr;
                            break;
                        }
                    }
                }

                if (speechesToken == null)
                    throw new LLMException("无法在 LLM 响应中找到 speeches 数组");

                var speeches = speechesToken.ToObject<NPCSpeechResult[]>();

                foreach (var s in speeches)
                {
                    if (string.IsNullOrEmpty(s.speech))
                        Debug.LogWarning($"[LLM] NPC 发言为空: name={s.name}");
                }

                return speeches;
            }
            catch (LLMException) { throw; }
            catch (Exception e)
            {
                Debug.LogError($"[LLM] 解析 NPC 发言失败: {e.Message}\n原始响应: {json}");
                throw new LLMException($"解析 NPC 发言失败: {e.Message}", e);
            }
        }

        /// <summary>
        /// 解析证词评分结果
        /// 期望格式: { "score": 7 }
        /// </summary>
        public static int ParseScore(string json)
        {
            try
            {
                var root = JObject.Parse(json);

                var scoreToken = root["score"];
                if (scoreToken == null)
                    throw new LLMException("无法在 LLM 响应中找到 score 字段");

                int score = scoreToken.Value<int>();

                // 强制 clamp 到 [0, 10]
                score = Mathf.Clamp(score, 0, 10);

                return score;
            }
            catch (LLMException) { throw; }
            catch (Exception e)
            {
                Debug.LogError($"[LLM] 解析评分失败: {e.Message}\n原始响应: {json}");
                throw new LLMException($"解析评分失败: {e.Message}", e);
            }
        }
    }
}
