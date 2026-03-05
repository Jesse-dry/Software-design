using UnityEngine;

/// <summary>
/// 记忆场景透视增强：控制碎片从远处飘来的视觉效果。
/// 根据玩家 Y 轴进度与目标碎片的距离，动态调整碎片的【Scale】（大小）和【LocalPosition】（发散）。
/// </summary>
public class MemoryPerspectiveEffect : MonoBehaviour
{
    [Header("绑定目标")]
    [Tooltip("玩家逻辑对象（用于获取当前 Y 坐标）")]
    public Transform playerLogicTransform;

    [Tooltip("碎片逻辑根节点（决定什么时候碎片到达最理想状态）")]
    public Transform fragmentLogicRoot;

    [Tooltip("视觉子物体（实际被缩放/移动的对象）")]
    public Transform visualTransform;

    [Header("透视参数")]
    [Tooltip("开始显现的距离（玩家距离碎片多远时开始显示）")]
    public float appearDistance = 8f;

    [Tooltip("完全显现的距离（玩家距离碎片多近时达到最大状态）")]
    public float fullDistance = 0.5f;

    [Header("效果配置")]
    [Tooltip("最小缩放（远处时的大小）")]
    public float minScale = 0.2f;

    [Tooltip("最大缩放（近处时的大小）")]
    public float maxScale = 1.0f;

    [Tooltip("使用世界坐标插值由消失点飞向目标点（推荐 true）")]
    public bool useWorldSpaceLerp = true;

    [Tooltip("世界坐标消失点（通常是屏幕中心，如 (0,0)）")]
    public Vector3 vanishingPoint = Vector3.zero;

    [Tooltip("世界坐标目标点（完全显现时的位置）")]
    public Vector3 targetWorldPos;

    // 保留旧参数兼容（如果不使用 worldSpaceLerp）
    [Tooltip("远处的中心点收缩系数（0=完全在屏幕中心，1=完全在设定位置）")]
    [Range(0f, 1f)]
    public float centerBias = 0.3f; 

    private Vector3 originalVisualPos;
    private SpriteRenderer sr;

    void Start()
    {
        if (visualTransform != null)
        {
            originalVisualPos = visualTransform.localPosition;
            sr = visualTransform.GetComponent<SpriteRenderer>();

            // 如果 targetWorldPos 未被 Setup 设置，尝试自动推断（仅作为 fallback）
            if (targetWorldPos == Vector3.zero && !useWorldSpaceLerp) 
            {
               // Legacy mode fallback
            }
        }
    }

    void LateUpdate()
    {
        if (playerLogicTransform == null || fragmentLogicRoot == null || visualTransform == null)
            return;

        // 计算玩家与碎片的逻辑距离（Y轴）
        // postive distance means fragment is ahead of player
        float dist = fragmentLogicRoot.position.y - playerLogicTransform.position.y;

        // 如果碎片在玩家身后太远，或者是还由于太远没出现
        if (dist < -2f || dist > appearDistance)
        {
            if (sr != null) sr.enabled = false;
            return;
        }

        if (sr != null) sr.enabled = true;

        // 计算进度 t (0 = 刚出现/最远, 1 = 到达/最近)
        float t = Mathf.InverseLerp(appearDistance, fullDistance, dist);
        // 使用 Ease In Out 让变化更平滑
        t = Mathf.SmoothStep(0f, 1f, t);

        // 1. 缩放处理
        float newScale = Mathf.Lerp(minScale, maxScale, t);
        visualTransform.localScale = Vector3.one * newScale;

        // 2. 位移处理
        if (useWorldSpaceLerp)
        {
            // 从 vanishingPoint 飞向 targetWorldPos
            visualTransform.position = Vector3.Lerp(vanishingPoint, targetWorldPos, t);
        }
        else
        {
            // 旧逻辑：基于 localPosition 处理
            // 假设 visualTransform.localPosition 就是最终期望的「近处」相对位置。
            // 那么远处就是这个位置 * centerBias。
            visualTransform.localPosition = Vector3.Lerp(originalVisualPos * centerBias, originalVisualPos, t);
        }
        
        // 透明度淡入
        if (sr != null)
        {
            Color c = sr.color;
            c.a = Mathf.Clamp01(t * 1.5f); // 稍微快点显示
            sr.color = c;
        }
    }

}
