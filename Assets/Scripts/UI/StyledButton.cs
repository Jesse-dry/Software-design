using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using DG.Tweening;
using TMPro;

/// <summary>
/// 通用样式按钮组件 — 提供悬停/按下视觉反馈。
///
/// 【功能】
///   - 悬停时：缩放动画 + 颜色过渡 + 可选发光边框
///   - 按下时：缩放回弹 + 颜色变化
///   - 大小可在 Inspector 中自由调节
///   - 可挂载在任意 Button 上，无需修改原有逻辑
///
/// 【使用方式】
///   1. 在 Button 对象上添加此组件
///   2. 在 Inspector 中调节 hoverScale / pressScale / 颜色等参数
///   3. 运行即生效
///
/// 【Memory 场景按钮替换】
///   此组件可替代默认的朴素按钮样式，为碎片弹窗和传送门弹窗
///   提供与 Cutscene Prefab 中按钮相似的悬停/按下效果。
///   MemorySceneSetup 可通过 buttonSize 字段统一控制按钮大小。
/// </summary>
[RequireComponent(typeof(Button))]
public class StyledButton : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler,
    IPointerDownHandler, IPointerUpHandler
{
    // ═══════════════════════════════════════════════════════════════
    //  Inspector 配置
    // ═══════════════════════════════════════════════════════════════

    [Header("== 大小 ==")]
    [Tooltip("按钮尺寸（覆盖 RectTransform.sizeDelta）。设为 (0,0) 则保留原始大小")]
    public Vector2 buttonSize = Vector2.zero;

    [Header("== 悬停效果 ==")]
    [Tooltip("悬停时的缩放倍率")]
    [Range(1f, 1.5f)]
    public float hoverScale = 1.08f;

    [Tooltip("悬停时背景颜色")]
    public Color hoverColor = new Color(0.25f, 0.25f, 0.35f, 1f);

    [Tooltip("悬停时文字颜色")]
    public Color hoverTextColor = new Color(0.9f, 1f, 0.9f, 1f);

    [Header("== 按下效果 ==")]
    [Tooltip("按下时的缩放倍率")]
    [Range(0.8f, 1f)]
    public float pressScale = 0.95f;

    [Tooltip("按下时背景颜色")]
    public Color pressColor = new Color(0.1f, 0.3f, 0.2f, 1f);

    [Header("== 常态颜色 ==")]
    [Tooltip("常态背景颜色（留空将自动读取 Image 当前颜色）")]
    public Color normalColor = new Color(0.15f, 0.15f, 0.2f, 1f);

    [Tooltip("常态文字颜色")]
    public Color normalTextColor = new Color(0.7f, 0.9f, 0.7f, 1f);

    [Header("== 动画 ==")]
    [Tooltip("过渡动画时长（秒）")]
    [Range(0.05f, 0.5f)]
    public float transitionDuration = 0.15f;

    [Tooltip("缩放缓动")]
    public Ease scaleEase = Ease.OutBack;

    [Header("== 发光边框（可选）==")]
    [Tooltip("是否在悬停时显示发光边框效果")]
    public bool useGlowOutline = false;

    [Tooltip("发光颜色")]
    public Color glowColor = new Color(0.3f, 0.8f, 0.5f, 0.6f);

    [Tooltip("发光边框宽度（像素）")]
    [Range(1f, 10f)]
    public float glowWidth = 2f;

    // ── 内部引用 ─────────────────────────────────────────────────
    private Button _button;
    private Image _bgImage;
    private TMP_Text _text;
    private RectTransform _rect;
    private Outline _outline;
    private Tween _scaleTween;
    private Tween _colorTween;
    private Tween _textColorTween;

    private Color _originalBgColor;
    private Color _originalTextColor;
    private bool _isHovered;

    // ══════════════════════════════════════════════════════════════
    //  生命周期
    // ══════════════════════════════════════════════════════════════

    private void Awake()
    {
        _button = GetComponent<Button>();
        _bgImage = GetComponent<Image>();
        _text = GetComponentInChildren<TMPro.TextMeshProUGUI>();
        if (_text == null) _text = GetComponentInChildren<TMPro.TMP_Text>();
        _rect = GetComponent<RectTransform>();

        // 记录原始颜色
        if (_bgImage != null)
            _originalBgColor = normalColor != default ? normalColor : _bgImage.color;
        if (_text != null)
            _originalTextColor = normalTextColor != default ? normalTextColor : _text.color;

        // 应用自定义大小
        if (buttonSize != Vector2.zero && _rect != null)
            _rect.sizeDelta = buttonSize;

        // 发光边框
        if (useGlowOutline)
        {
            _outline = GetComponent<Outline>();
            if (_outline == null) _outline = gameObject.AddComponent<Outline>();
            _outline.effectColor = new Color(glowColor.r, glowColor.g, glowColor.b, 0f);
            _outline.effectDistance = new Vector2(glowWidth, glowWidth);
            _outline.enabled = true;
        }
    }

    private void OnEnable()
    {
        // 确保进入时为常态
        ResetToNormal(true);
    }

    private void OnDestroy()
    {
        _scaleTween?.Kill();
        _colorTween?.Kill();
        _textColorTween?.Kill();
    }

    // ══════════════════════════════════════════════════════════════
    //  指针事件
    // ══════════════════════════════════════════════════════════════

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!_button.interactable) return;
        _isHovered = true;
        TransitionTo(hoverScale, hoverColor, hoverTextColor);
        if (useGlowOutline && _outline != null)
            _outline.effectColor = glowColor;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _isHovered = false;
        ResetToNormal(false);
        if (useGlowOutline && _outline != null)
            _outline.effectColor = new Color(glowColor.r, glowColor.g, glowColor.b, 0f);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!_button.interactable) return;
        TransitionTo(pressScale, pressColor, hoverTextColor);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (_isHovered)
            TransitionTo(hoverScale, hoverColor, hoverTextColor);
        else
            ResetToNormal(false);
    }

    // ══════════════════════════════════════════════════════════════
    //  过渡动画
    // ══════════════════════════════════════════════════════════════

    private void TransitionTo(float targetScale, Color targetBg, Color targetText)
    {
        _scaleTween?.Kill();
        _colorTween?.Kill();
        _textColorTween?.Kill();

        if (_rect != null)
        {
            _scaleTween = _rect.DOScale(targetScale, transitionDuration)
                .SetEase(scaleEase)
                .SetUpdate(true);
        }

        if (_bgImage != null)
        {
            _colorTween = _bgImage.DOColor(targetBg, transitionDuration)
                .SetUpdate(true);
        }

        if (_text != null)
        {
            _textColorTween = DOTween.To(
                () => _text.color, c => _text.color = c,
                targetText, transitionDuration)
                .SetUpdate(true);
        }
    }

    private void ResetToNormal(bool instant)
    {
        _scaleTween?.Kill();
        _colorTween?.Kill();
        _textColorTween?.Kill();

        if (instant)
        {
            if (_rect != null) _rect.localScale = Vector3.one;
            if (_bgImage != null) _bgImage.color = _originalBgColor;
            if (_text != null) _text.color = _originalTextColor;
        }
        else
        {
            TransitionTo(1f, _originalBgColor, _originalTextColor);
        }
    }

    // ══════════════════════════════════════════════════════════════
    //  运行时 API
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// 运行时设置按钮大小
    /// </summary>
    public void SetSize(Vector2 size)
    {
        buttonSize = size;
        if (_rect != null) _rect.sizeDelta = size;
    }

    /// <summary>
    /// 运行时批量配置样式
    /// </summary>
    public void ApplyStyle(
        Vector2? size = null,
        Color? normalBg = null,
        Color? hoverBg = null,
        Color? pressBg = null,
        Color? normalText = null,
        Color? hoverText = null,
        float? hoverScaleVal = null,
        float? pressScaleVal = null,
        bool? glow = null)
    {
        if (size.HasValue)        { buttonSize = size.Value; if (_rect != null) _rect.sizeDelta = size.Value; }
        if (normalBg.HasValue)    { normalColor = normalBg.Value; _originalBgColor = normalBg.Value; }
        if (hoverBg.HasValue)     hoverColor = hoverBg.Value;
        if (pressBg.HasValue)     pressColor = pressBg.Value;
        if (normalText.HasValue)  { normalTextColor = normalText.Value; _originalTextColor = normalText.Value; }
        if (hoverText.HasValue)   hoverTextColor = hoverText.Value;
        if (hoverScaleVal.HasValue) hoverScale = hoverScaleVal.Value;
        if (pressScaleVal.HasValue) pressScale = pressScaleVal.Value;
        if (glow.HasValue)
        {
            useGlowOutline = glow.Value;
            if (glow.Value && _outline == null)
            {
                _outline = gameObject.AddComponent<Outline>();
                _outline.effectColor = new Color(glowColor.r, glowColor.g, glowColor.b, 0f);
                _outline.effectDistance = new Vector2(glowWidth, glowWidth);
            }
        }

        // 刷新常态显示
        ResetToNormal(true);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // Inspector 修改时实时更新大小
        if (buttonSize != Vector2.zero)
        {
            var rt = GetComponent<RectTransform>();
            if (rt != null) rt.sizeDelta = buttonSize;
        }
    }
#endif
}
