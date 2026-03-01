using System.Threading;
using Cysharp.Threading.Tasks;

namespace LLMModule
{
    /// <summary>
    /// LLM 文本生成器对外唯一接口。
    /// 其他游戏模块仅依赖此接口 + DTOs，无需了解 prompt 结构或 API 细节。
    /// </summary>
    public interface ILLMTextGenerator
    {
        /// <summary>
        /// 【证据收集】生成塔罗牌证据文本。
        /// 相同 chapter 的结果会被缓存，重复调用直接返回缓存。
        /// </summary>
        /// <param name="request">证据请求（章节、事实、卡牌定义）</param>
        /// <param name="style">玩家输入的全局风格参数（为空时用默认风格）</param>
        /// <param name="ct">取消令牌</param>
        /// <returns>生成的卡牌数据数组</returns>
        UniTask<CardData[]> GenerateEvidenceCards(EvidenceRequest request, string style = null, CancellationToken ct = default);

        /// <summary>
        /// 【庭审】生成本轮 NPC 发言。
        /// </summary>
        /// <param name="request">NPC 发言请求（NPC 状态、事实、议题）</param>
        /// <param name="style">玩家输入的全局风格参数（为空时用默认风格）</param>
        /// <param name="ct">取消令牌</param>
        /// <returns>NPC 发言数组</returns>
        UniTask<NPCSpeechResult[]> GenerateNPCSpeeches(NPCSpeechRequest request, string style = null, CancellationToken ct = default);

        /// <summary>
        /// 【庭审】评估玩家证词，返回 0-10 分。
        /// </summary>
        /// <param name="request">评分请求（玩家证词、使用的牌）</param>
        /// <param name="style">玩家输入的全局风格参数（为空时用默认风格）</param>
        /// <param name="ct">取消令牌</param>
        /// <returns>0-10 的整数评分</returns>
        UniTask<int> EvaluatePlayerArgument(ArgumentEvalRequest request, string style = null, CancellationToken ct = default);

        /// <summary>
        /// 清除证据卡牌缓存（章节切换时调用）。
        /// </summary>
        void ClearEvidenceCache();
    }
}
