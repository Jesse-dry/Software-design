using System;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace LLMModule
{
    /// <summary>
    /// 负责与 OpenAI 兼容 API（DeepSeek 等）通信的底层 HTTP 客户端。
    /// 内部使用，不对外暴露。
    /// </summary>
    internal class LLMApiClient
    {
        private readonly LLMConfig _config;
        private readonly string _resolvedApiKey;
        private readonly SemaphoreSlim _concurrencyLimiter;

        public LLMApiClient(LLMConfig config)
        {
            _config = config;
            _resolvedApiKey = config.GetApiKey(); // 启动时校验，立即发现问题
            _concurrencyLimiter = new SemaphoreSlim(config.maxConcurrentRequests);
        }

        /// <summary>
        /// 发送 Chat Completion 请求，返回 assistant message 的 content 字符串。
        /// 改进：并发限制 + 指数退避重试 + 进度回调 + 可配置超时
        /// </summary>
        /// <param name="onProgress">可选进度回调，用于 UI 显示加载状态</param>
        public async UniTask<string> SendChatRequest(
            string systemPrompt,
            string userPrompt,
            System.Action<string> onProgress = null,
            CancellationToken ct = default)
        {
            // 并发限制：防止庭审多 NPC 同时请求导致拥塞
            await _concurrencyLimiter.WaitAsync(ct);
            try
            {
                return await SendChatRequestInternal(systemPrompt, userPrompt, onProgress, ct);
            }
            finally
            {
                _concurrencyLimiter.Release();
            }
        }

        private async UniTask<string> SendChatRequestInternal(
            string systemPrompt,
            string userPrompt,
            System.Action<string> onProgress,
            CancellationToken ct)
        {
            var requestBody = new
            {
                model = _config.model,
                messages = new object[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = userPrompt }
                },
                temperature = _config.temperature,
                max_tokens = _config.maxTokens,
                response_format = new { type = "json_object" }
            };

            string jsonBody = JsonConvert.SerializeObject(requestBody);
            // 使用配置中的 endpointPath（支持自定义不同提供商的路径）
            string endpoint = string.IsNullOrWhiteSpace(_config.endpointPath)
                ? string.Empty
                : _config.endpointPath.TrimStart('/');
            string url = string.IsNullOrEmpty(endpoint)
                ? _config.baseUrl.TrimEnd('/')
                : $"{_config.baseUrl.TrimEnd('/')}/{endpoint}";
            int totalAttempts = _config.maxRetries + 1;

            for (int attempt = 0; attempt < totalAttempts; attempt++)
            {
                ct.ThrowIfCancellationRequested();

                try
                {
                    onProgress?.Invoke($"正在请求 LLM（第 {attempt + 1}/{totalAttempts} 次）...");

                    string rawResponse = await PostJson(url, jsonBody, ct);

                    // 从 OpenAI 格式响应中提取 content
                    var response = JObject.Parse(rawResponse);
                    string content = response["choices"]?[0]?["message"]?["content"]?.ToString();

                    if (string.IsNullOrEmpty(content))
                        throw new LLMException("LLM 响应 content 为空");

                    // 清理可能的 markdown 代码块包装
                    content = StripMarkdownCodeBlock(content);

                    onProgress?.Invoke("LLM 响应完成");
                    return content;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception e) when (attempt < totalAttempts - 1)
                {
                    // 指数退避：delay × 2^attempt
                    double delay = _config.enableExponentialBackoff
                        ? _config.retryDelaySeconds * Math.Pow(2, attempt)
                        : _config.retryDelaySeconds;

                    Debug.LogWarning($"[LLM] 第 {attempt + 1} 次请求失败: {e.Message}，{delay:F1}s 后重试（指数退避）");
                    onProgress?.Invoke($"请求失败，{delay:F1}s 后重试...");

                    await UniTask.Delay(
                        TimeSpan.FromSeconds(delay),
                        cancellationToken: ct);
                }
                catch (Exception e)
                {
                    onProgress?.Invoke("LLM 请求失败");
                    throw new LLMException(
                        $"LLM API 请求在 {totalAttempts} 次尝试后仍然失败: {e.Message}", e);
                }
            }

            throw new LLMException("超出重试上限");
        }

        // ── HTTP 发送 ───────────────────────────────────────────

        private async UniTask<string> PostJson(string url, string jsonBody, CancellationToken ct)
        {
            byte[] bodyBytes = Encoding.UTF8.GetBytes(jsonBody);

            using var request = new UnityWebRequest(url, "POST");
            request.uploadHandler = new UploadHandlerRaw(bodyBytes);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Authorization", $"Bearer {_resolvedApiKey}");
            request.timeout = _config.requestTimeoutSeconds;

            await request.SendWebRequest().WithCancellation(ct);

            if (request.result != UnityWebRequest.Result.Success)
            {
                string errorBody = request.downloadHandler?.text ?? "";
                throw new LLMException(
                    $"HTTP {request.responseCode}: {request.error}\n{errorBody}");
            }

            return request.downloadHandler.text;
        }

        // ── 工具方法 ────────────────────────────────────────────

        /// <summary>
        /// 移除 LLM 偶尔返回的 ```json ... ``` 包装
        /// </summary>
        private static string StripMarkdownCodeBlock(string text)
        {
            if (text == null) return null;
            text = text.Trim();

            if (text.StartsWith("```"))
            {
                int firstNewline = text.IndexOf('\n');
                if (firstNewline > 0)
                    text = text.Substring(firstNewline + 1);

                if (text.EndsWith("```"))
                    text = text.Substring(0, text.Length - 3);

                text = text.Trim();
            }

            return text;
        }
    }

    // ── 自定义异常 ──────────────────────────────────────────────

    /// <summary>
    /// LLM 模块专用异常，便于调用方区分和捕获。
    /// </summary>
    public class LLMException : Exception
    {
        public LLMException(string message) : base(message) { }
        public LLMException(string message, Exception inner) : base(message, inner) { }
    }
}
