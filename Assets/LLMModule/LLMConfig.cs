using System;
using UnityEngine;

namespace LLMModule
{
    /// <summary>
    /// LLM API 配置，通过 ScriptableObject 在 Inspector 中管理。
    /// 创建方式：Assets > Create > LLM > Config
    ///
    /// API Key 安全策略（按优先级）：
    ///   1. 环境变量 LLM_API_KEY
    ///   2. ScriptableObject 中的 apiKey 字段（仅用于本地开发，切勿提交到版本控制）
    /// </summary>
    [CreateAssetMenu(fileName = "LLMConfig", menuName = "LLM/Config")]
    public class LLMConfig : ScriptableObject
    {
        [Header("API 连接")]
        [Tooltip("LLM API 密钥（建议留空，改用环境变量 LLM_API_KEY）")]
        public string apiKey = "";

        [Tooltip("API 基础地址（不含 /v1/chat/completions）")]
        public string baseUrl = "https://api.deepseek.com";

        [Tooltip("模型名称")]
        public string model = "deepseek-chat";
        
        [Tooltip("API 的相对路径（例如 /v1/chat/completions），可留空表示直接使用 baseUrl")] 
        public string endpointPath = "/v1/chat/completions";

        [Header("生成参数")]
        [Range(0f, 2f)]
        [Tooltip("温度，越高越随机")]
        public float temperature = 0.7f;

        [Tooltip("最大生成 token 数")]
        public int maxTokens = 2048;

        [Header("超时与重试")]
        [Tooltip("单次请求超时（秒）")]
        public int requestTimeoutSeconds = 60;

        [Tooltip("最大重试次数（0 = 不重试）")]
        public int maxRetries = 2;

        [Tooltip("首次重试间隔（秒），启用指数退避后会逐次翻倍")]
        public float retryDelaySeconds = 1.5f;

        [Tooltip("启用指数退避（重试间隔 = retryDelay × 2^attempt）")]
        public bool enableExponentialBackoff = true;

        [Header("并发控制")]
        [Tooltip("最大同时请求数（防止庭审多NPC请求拥塞）")]
        [Range(1, 10)]
        public int maxConcurrentRequests = 3;

        [Header("降级策略")]
        [Tooltip("API 不可用时是否启用 fallback 文本")]
        public bool enableFallback = true;

        // ── 运行时安全获取 API Key ──────────────────────────────

        /// <summary>
        /// 安全获取 API Key：优先环境变量，其次 Inspector 配置。
        /// 如果都为空则抛出异常。
        /// </summary>
        public string GetApiKey()
        {
            // 优先从环境变量读取
            string envKey = Environment.GetEnvironmentVariable("LLM_API_KEY");
            if (!string.IsNullOrWhiteSpace(envKey))
                return envKey;

            // 其次使用 ScriptableObject 中的值
            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                Debug.LogWarning("[LLMConfig] API Key 从 ScriptableObject 加载，建议改用环境变量 LLM_API_KEY 以避免泄露");
                return apiKey;
            }

            throw new InvalidOperationException(
                "[LLMConfig] API Key 未配置！请设置环境变量 LLM_API_KEY 或在 Inspector 中填写 apiKey");
        }

        /// <summary>
        /// 日志安全的遮蔽 Key 显示
        /// </summary>
        public string GetMaskedKey()
        {
            try
            {
                string key = GetApiKey();
                if (key.Length < 8) return "***";
                return key[..4] + "****" + key[^4..];
            }
            catch
            {
                return "(未配置)";
            }
        }
    }
}
