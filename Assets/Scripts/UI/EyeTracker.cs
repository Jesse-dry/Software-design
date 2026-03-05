using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 眼球跟踪鼠标效果组件。
/// 
/// 使用方法：
///   1. 准备眼睛底图（眼眶）作为父 Image
///   2. 将眼球作为子 Image，挂载此脚本
///   3. 在 Inspector 中调节眼球活动范围和跟随速度
/// 
/// 眼球会在限定范围内平滑跟随鼠标方向，模拟注视效果。
/// 支持圆形和椭圆形活动范围（X/Y 分别可调）。
/// </summary>
public class EyeTracker : MonoBehaviour
{
    [Header("眼球活动范围")]
    [Tooltip("眼球在 X 方向的最大偏移量（像素）")]
    [SerializeField] private float maxOffsetX = 15f;

    [Tooltip("眼球在 Y 方向的最大偏移量（像素）")]
    [SerializeField] private float maxOffsetY = 15f;

    [Header("跟随参数")]
    [Tooltip("眼球跟随鼠标的平滑速度，越大越灵敏")]
    [SerializeField, Range(1f, 30f)] private float followSpeed = 8f;

    [Header("中心点偏移")]
    [Tooltip("眼球静止时相对于父物体的本地偏移位置")]
    [SerializeField] private Vector2 centerOffset = Vector2.zero;

    [Header("调试")]
    [Tooltip("在 Scene 视图中绘制活动范围")]
    [SerializeField] private bool drawGizmos = true;

    private RectTransform _rectTransform;
    private RectTransform _parentRectTransform;
    private Canvas _rootCanvas;
    private Camera _canvasCamera;

    private void Awake()
    {
        _rectTransform = GetComponent<RectTransform>();
        _parentRectTransform = transform.parent?.GetComponent<RectTransform>();
        _rootCanvas = GetComponentInParent<Canvas>()?.rootCanvas;

        if (_rootCanvas != null)
        {
            _canvasCamera = _rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : _rootCanvas.worldCamera;
        }
    }

    private void Start()
    {
        // 初始化眼球到中心位置
        _rectTransform.anchoredPosition = centerOffset;
    }

    private void Update()
    {
        if (_parentRectTransform == null) return;

        Vector2 targetOffset = CalculateTargetOffset();
        Vector2 clampedOffset = ClampToEllipse(targetOffset);

        // 平滑移动到目标位置
        Vector2 currentPos = _rectTransform.anchoredPosition;
        Vector2 targetPos = centerOffset + clampedOffset;
        _rectTransform.anchoredPosition = Vector2.Lerp(currentPos, targetPos, followSpeed * Time.deltaTime);
    }

    /// <summary>
    /// 计算鼠标相对于眼睛中心的归一化方向偏移
    /// </summary>
    private Vector2 CalculateTargetOffset()
    {
        Vector3 mouseScreenPos = Input.mousePosition;

        // 将鼠标屏幕坐标转换为父 RectTransform 的本地坐标
        Vector2 localMousePos;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _parentRectTransform,
            mouseScreenPos,
            _canvasCamera,
            out localMousePos
        );

        // 计算鼠标相对于眼睛中心的方向
        Vector2 direction = localMousePos - centerOffset;

        // 归一化方向，然后按最大偏移量缩放
        if (direction.magnitude < 0.01f) return Vector2.zero;

        // 计算归一化比例（基于与屏幕尺寸的关系，使得鼠标在屏幕边缘时眼球接近最大偏移）
        float screenHalfWidth = Screen.width * 0.5f;
        float screenHalfHeight = Screen.height * 0.5f;

        float normalizedX = Mathf.Clamp(direction.x / screenHalfWidth, -1f, 1f);
        float normalizedY = Mathf.Clamp(direction.y / screenHalfHeight, -1f, 1f);

        return new Vector2(normalizedX * maxOffsetX, normalizedY * maxOffsetY);
    }

    /// <summary>
    /// 将偏移量限制在椭圆范围内
    /// </summary>
    private Vector2 ClampToEllipse(Vector2 offset)
    {
        if (maxOffsetX <= 0 || maxOffsetY <= 0) return Vector2.zero;

        // 椭圆方程：(x/a)^2 + (y/b)^2 <= 1
        float nx = offset.x / maxOffsetX;
        float ny = offset.y / maxOffsetY;
        float dist = nx * nx + ny * ny;

        if (dist > 1f)
        {
            float scale = 1f / Mathf.Sqrt(dist);
            offset.x *= scale;
            offset.y *= scale;
        }

        return offset;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (!drawGizmos) return;

        var rt = GetComponent<RectTransform>();
        if (rt == null) return;

        // 绘制椭圆活动范围
        Gizmos.color = new Color(0f, 1f, 0.5f, 0.6f);
        Vector3 worldCenter = transform.parent != null ? transform.parent.position : transform.position;

        int segments = 36;
        Vector3 prevPoint = Vector3.zero;
        for (int i = 0; i <= segments; i++)
        {
            float angle = (float)i / segments * Mathf.PI * 2f;
            float x = Mathf.Cos(angle) * maxOffsetX;
            float y = Mathf.Sin(angle) * maxOffsetY;
            Vector3 point = worldCenter + new Vector3(x + centerOffset.x, y + centerOffset.y, 0f);

            if (i > 0)
                Gizmos.DrawLine(prevPoint, point);

            prevPoint = point;
        }
    }
#endif
}
