using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;

/// <summary>
/// 透明按钮悬停显示图形效果组件。
/// 
/// 使用方法：
///   1. 创建一个 Button 对象，将 Image 颜色设为全透明（作为隐形点击区域）
///   2. 创建一个子 Image 用于显示悬停时出现的图形（Reveal Image）
///   3. 挂载此脚本到 Button 对象上
///   4. 在 Inspector 中指定 Reveal Image 引用并调节参数
/// 
/// 鼠标悬停时，指定的图形会从透明渐显出现；离开后渐隐消失。
/// </summary>
[RequireComponent(typeof(Button))]
public class HoverRevealButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("悬停显示图形")]
    [Tooltip("鼠标悬停时渐显的 Image 组件（应预设为全透明）")]
    [SerializeField] private Image revealImage;

    [Tooltip("悬停时图形显示的目标透明度")]
    [SerializeField, Range(0f, 1f)] private float revealAlpha = 1f;

    [Header("位置设置")]
    [Tooltip("图形相对于按钮的本地偏移位置")]
    [SerializeField] private Vector2 revealOffset = Vector2.zero;

    [Tooltip("图形显示时的缩放")]
    [SerializeField] private Vector3 revealScale = Vector3.one;

    [Header("动画参数")]
    [Tooltip("渐显/渐隐的持续时间（秒）")]
    [SerializeField, Range(0.05f, 2f)] private float fadeDuration = 0.3f;

    [Tooltip("渐显时是否同时播放缩放动画")]
    [SerializeField] private bool useScaleAnimation = false;

    [Tooltip("缩放动画起始比例（从此值缩放到 revealScale）")]
    [SerializeField] private Vector3 scaleFrom = new Vector3(0.5f, 0.5f, 0.5f);

    [Header("按钮透明点击区域")]
    [Tooltip("按钮自身的 Image 是否设为完全透明（运行时自动处理）")]
    [SerializeField] private bool autoSetTransparent = true;

    [Header("检测区 (Hitbox)")]
    [Tooltip("是否在运行时扩大按钮的检测范围（通过修改 RectTransform.sizeDelta）")]
    [SerializeField] private bool expandHitbox = true;

    [Tooltip("在 X/Y 方向上扩大多少像素（将会加到 sizeDelta 上）")]
    [SerializeField] private Vector2 hitboxPadding = new Vector2(20f, 10f);

    private RectTransform _revealRect;
    private CanvasGroup _revealCanvasGroup;
    private Coroutine _fadeCoroutine;
    private Button _button;

    private void Awake()
    {
        _button = GetComponent<Button>();

        // 自动设置按钮背景为透明（保留点击区域）
        if (autoSetTransparent)
        {
            var btnImage = GetComponent<Image>();
            if (btnImage != null)
            {
                btnImage.color = new Color(1f, 1f, 1f, 0f);
                // 确保透明区域仍可接收射线
                btnImage.raycastTarget = true;
            }
        }

        SetupRevealImage();
    }

    private void Start()
    {
        // 确保图形初始为隐藏
        if (_revealCanvasGroup != null)
        {
            _revealCanvasGroup.alpha = 0f;
        }
    }

    /// <summary>
    /// 配置 Reveal Image 的初始状态
    /// </summary>
    private void SetupRevealImage()
    {
        if (revealImage == null)
        {
            Debug.LogWarning($"[HoverRevealButton] {gameObject.name}: 未指定 revealImage，悬停效果不可用。");
            return;
        }

        _revealRect = revealImage.GetComponent<RectTransform>();

        // 使用 CanvasGroup 控制淡入淡出（比直接改 Image.color.a 更灵活，支持子元素）
        _revealCanvasGroup = revealImage.GetComponent<CanvasGroup>();
        if (_revealCanvasGroup == null)
        {
            _revealCanvasGroup = revealImage.gameObject.AddComponent<CanvasGroup>();
        }

        // 设置初始状态
        _revealCanvasGroup.alpha = 0f;
        _revealCanvasGroup.blocksRaycasts = false; // 不阻挡按钮的射线检测

        // 应用位置偏移
        if (_revealRect != null)
        {
            _revealRect.anchoredPosition = revealOffset;
        }

        // 可选：扩大按钮的检测范围（调整自身 RectTransform.sizeDelta）
        if (expandHitbox)
        {
            var myRect = GetComponent<RectTransform>();
            if (myRect != null)
            {
                // 在编辑器/运行时均生效；对 Anchor 为中点的按钮安全
                myRect.sizeDelta = myRect.sizeDelta + hitboxPadding;
            }
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (revealImage == null) return;

        if (_fadeCoroutine != null)
            StopCoroutine(_fadeCoroutine);

        _fadeCoroutine = StartCoroutine(FadeReveal(true));
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (revealImage == null) return;

        if (_fadeCoroutine != null)
            StopCoroutine(_fadeCoroutine);

        _fadeCoroutine = StartCoroutine(FadeReveal(false));
    }

    /// <summary>
    /// 渐显/渐隐协程
    /// </summary>
    private IEnumerator FadeReveal(bool show)
    {
        if (_revealCanvasGroup == null) yield break;

        float startAlpha = _revealCanvasGroup.alpha;
        float targetAlpha = show ? revealAlpha : 0f;

        Vector3 startScale = _revealRect != null ? _revealRect.localScale : Vector3.one;
        Vector3 targetScale = show ? revealScale : scaleFrom;

        float elapsed = 0f;

        // 基于当前状态计算实际动画时间（从中间状态开始不需要完整时长）
        float alphaDistance = Mathf.Abs(targetAlpha - startAlpha);
        float actualDuration = fadeDuration * alphaDistance / Mathf.Max(revealAlpha, 0.01f);

        while (elapsed < actualDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / actualDuration);

            _revealCanvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, t);

            if (useScaleAnimation && _revealRect != null)
            {
                _revealRect.localScale = Vector3.Lerp(startScale, targetScale, t);
            }

            yield return null;
        }

        // 确保最终值精确
        _revealCanvasGroup.alpha = targetAlpha;
        if (useScaleAnimation && _revealRect != null)
        {
            _revealRect.localScale = targetScale;
        }

        _fadeCoroutine = null;
    }

    /// <summary>
    /// 运行时动态更新图形偏移位置
    /// </summary>
    public void SetRevealOffset(Vector2 newOffset)
    {
        revealOffset = newOffset;
        if (_revealRect != null)
        {
            _revealRect.anchoredPosition = revealOffset;
        }
    }

    /// <summary>
    /// 运行时获取按钮引用（供 MainMenuController 调用）
    /// </summary>
    public Button GetButton()
    {
        if (_button == null) _button = GetComponent<Button>();
        return _button;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // Inspector 中修改参数时实时更新位置（仅编辑器模式）
        if (revealImage != null)
        {
            var rt = revealImage.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchoredPosition = revealOffset;
            }
        }
    }
#endif
}
