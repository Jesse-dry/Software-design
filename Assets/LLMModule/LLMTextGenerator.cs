using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace LLMModule
{
    /// <summary>
    /// ILLMTextGenerator 的核心实现。
    /// 串联 PromptBuilder → LLMApiClient → ResponseParser，
    /// 并管理证据卡牌缓存。
    /// </summary>
    public class LLMTextGenerator : ILLMTextGenerator
    {
        private readonly LLMApiClient _apiClient;
        private readonly Dictionary<string, CardData[]> _evidenceCache = new();

        public LLMTextGenerator(LLMConfig config)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config), "LLMConfig 不能为 null");

            _apiClient = new LLMApiClient(config);
        }

        // ══════════════════════════════════════════════════════════
        //  证据收集
        // ══════════════════════════════════════════════════════════

        public async UniTask<CardData[]> GenerateEvidenceCards(
            EvidenceRequest request, string style = null, CancellationToken ct = default)
        {
            ValidateRequest(request);

            // 缓存命中则直接返回
            string cacheKey = request.chapter;
            if (_evidenceCache.TryGetValue(cacheKey, out var cached))
            {
                Debug.Log($"[LLM] 证据卡牌缓存命中: {cacheKey}");
                return cached;
            }

            string userPrompt = PromptBuilder.BuildEvidencePrompt(request, style);
            string response = await _apiClient.SendChatRequest(
                PromptBuilder.SystemPrompt, userPrompt, ct: ct);

            var cards = ResponseParser.ParseEvidenceCards(response);
            _evidenceCache[cacheKey] = cards;

            Debug.Log($"[LLM] 成功生成 {cards.Length} 张证据卡牌 ({cacheKey})");
            return cards;
        }

        // ══════════════════════════════════════════════════════════
        //  庭审 —— NPC 发言
        // ══════════════════════════════════════════════════════════

        public async UniTask<NPCSpeechResult[]> GenerateNPCSpeeches(
            NPCSpeechRequest request, string style = null, CancellationToken ct = default)
        {
            ValidateRequest(request);

            string userPrompt = PromptBuilder.BuildNPCSpeechPrompt(request, style);
            string response = await _apiClient.SendChatRequest(
                PromptBuilder.SystemPrompt, userPrompt, ct: ct);

            var speeches = ResponseParser.ParseNPCSpeeches(response);

            Debug.Log($"[LLM] 成功生成 {speeches.Length} 段 NPC 发言");
            return speeches;
        }

        // ══════════════════════════════════════════════════════════
        //  庭审 —— 证词评分
        // ══════════════════════════════════════════════════════════

        public async UniTask<int> EvaluatePlayerArgument(
            ArgumentEvalRequest request, string style = null, CancellationToken ct = default)
        {
            ValidateRequest(request);

            string userPrompt = PromptBuilder.BuildArgumentEvalPrompt(request, style);
            string response = await _apiClient.SendChatRequest(
                PromptBuilder.SystemPrompt, userPrompt, ct: ct);

            int score = ResponseParser.ParseScore(response);

            Debug.Log($"[LLM] 证词评分结果: {score}/10");
            return score;
        }

        // ══════════════════════════════════════════════════════════
        //  缓存管理
        // ══════════════════════════════════════════════════════════

        public void ClearEvidenceCache()
        {
            _evidenceCache.Clear();
            Debug.Log("[LLM] 证据卡牌缓存已清空");
        }

        // ══════════════════════════════════════════════════════════
        //  参数校验
        // ══════════════════════════════════════════════════════════

        private static void ValidateRequest(EvidenceRequest r)
        {
            if (r == null) throw new ArgumentNullException(nameof(r));
            if (string.IsNullOrEmpty(r.chapter))
                throw new ArgumentException("chapter 不能为空");
            if (r.confirmedFacts == null || r.confirmedFacts.Length == 0)
                throw new ArgumentException("confirmedFacts 不能为空");
            if (r.cardDefinitions == null || r.cardDefinitions.Length == 0)
                throw new ArgumentException("cardDefinitions 不能为空");
        }

        private static void ValidateRequest(NPCSpeechRequest r)
        {
            if (r == null) throw new ArgumentNullException(nameof(r));
            if (string.IsNullOrEmpty(r.chapter))
                throw new ArgumentException("chapter 不能为空");
            if (r.confirmedFacts == null || r.confirmedFacts.Length == 0)
                throw new ArgumentException("confirmedFacts 不能为空");
            if (r.allNPCs == null || r.allNPCs.Length == 0)
                throw new ArgumentException("allNPCs 不能为空");
            if (r.speakers == null || r.speakers.Length == 0)
                throw new ArgumentException("speakers 不能为空");
        }

        private static void ValidateRequest(ArgumentEvalRequest r)
        {
            if (r == null) throw new ArgumentNullException(nameof(r));
            if (string.IsNullOrEmpty(r.argument))
                throw new ArgumentException("argument 不能为空");
            if (string.IsNullOrEmpty(r.cardName))
                throw new ArgumentException("cardName 不能为空");
        }
    }
}
