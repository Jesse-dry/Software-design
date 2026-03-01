using UnityEngine;

namespace LLMModule
{
    /// <summary>
    /// MonoBehaviour 入口，挂在场景中的 GameObject 上。
    /// 其他模块通过引用此组件的 Generator 属性来调用 LLM 功能。
    ///
    /// 使用方式：
    ///   1. 在 Assets 中右键 → Create → LLM → Config，填写 API Key 等参数
    ///   2. 在场景中创建空 GameObject，挂上 LLMService，拖入 Config 资源
    ///   3. 其他脚本通过 GetComponent 或 Inspector 引用获取 service.Generator
    /// </summary>
    public class LLMService : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("拖入 LLMConfig 资源")]
        private LLMConfig config;

        /// <summary>
        /// 对外暴露的生成器接口，其他模块仅依赖此属性。
        /// </summary>
        public ILLMTextGenerator Generator { get; private set; }

        private void Awake()
        {
            if (config == null)
            {
                Debug.LogError("[LLMService] 未配置 LLMConfig，请在 Inspector 中拖入配置资源！");
                return;
            }

            Generator = new LLMTextGenerator(config);
            Debug.Log("[LLMService] LLM 文本生成器初始化完成");
        }

        private void OnDestroy()
        {
            Generator?.ClearEvidenceCache();
        }
    }
}
