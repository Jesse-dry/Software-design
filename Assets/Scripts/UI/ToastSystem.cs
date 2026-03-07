using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// 浮动提示系统（非打断式 Toast / Floating Text）。
/// 
/// 在 HUDLayer 上显示短暂的文字通知，不暂停游戏。
/// 支持多种表现风格，可在 Inspector 中配置。
/// 
/// 表现类型：
///   - FadeSlideUp: 渐入 → 上浮 → 渐出（默认，适合拾取/状态提示）
///   - TypewriterFade: 打字机式出现 → 停留 → 渐出（适合叙事提示）
///   - GlitchFlash: 闪烁出现 → 停留 → 闪烁消失（适合异常/警告）
/// 
/// 使用示例：
///   UIManager.Instance.Toast.Show("拾取了记忆碎片 1/3");
///   UIManager.Instance.Toast.Show("+3 理性", ToastStyle.GlitchFlash);
///   UIManager.Instance.Toast.ShowAtWorld(worldPos, "碎片!");
/// </summary>
public class ToastSystem : MonoBehaviour
{
    // ── Inspector 配置 ───────────────────────────────────────────
    [Header("== Toast 配置 ==")]
    [Tooltip("默认表现类型")]
    [SerializeField] private ToastStyle defaultStyle = ToastStyle.FadeSlideUp;

    [Tooltip("默认显示时长")]
    [SerializeField] private float defaultDuration = 2f;

    [Tooltip("最大同时显示数量")]
    [SerializeField] private int maxToasts = 5;

    [Tooltip("相邻 Toast 间距")]
    [SerializeField] private float toastSpacing = 50f;

    [Header("== 文字样式 ==")]
    [Tooltip("字体大小")]
    [SerializeField] private float fontSize = 22f;

    [Tooltip("文字颜色")]
    [SerializeField] private Color textColor = new Color(0.85f, 0.85f, 0.9f, 1f);

    [Tooltip("警告/异常文字颜色")]
    [SerializeField] private Color warningColor = new Color(0.9f, 0.3f, 0.3f, 1f);

    [Tooltip("正面反馈文字颜色")]
    [SerializeField] private Color positiveColor = new Color(0.4f, 0.9f, 0.5f, 1f);

    [Header("== 动画参数 ==")]
    [Tooltip("淡入时长")]
    [SerializeField] private float fadeInDuration = 0.3f;
    [Tooltip("淡出时长")]
    [SerializeField] private float fadeOutDuration = 0.5f;
    [Tooltip("上浮距离 (FadeSlideUp)")]
    [SerializeField] private float slideUpDistance = 60f;
    [Tooltip("起始偏移位置（屏幕下方居中）")]
    [SerializeField] private Vector2 startOffset = new Vector2(0, 150f);

    [Header("== Toast Prefab（可选）==")]
    [Tooltip("自定义 Toast Prefab。需包含 TMP_Text 子组件 'Text'。留空则运行时创建。")]
    [SerializeField] private GameObject toastPrefab;

    // ── 内部 ─────────────────────────────────────────────────────
    private readonly LinkedList<ActiveToast> activeToasts = new LinkedList<ActiveToast>();
    private RectTransform container;

    private class ActiveToast
    {
        public GameObject go;
        public float expireTime;
        public Sequence sequence;
    }

    // ══════════════════════════════════════════════════════════════
    //  初始化
    // ══════════════════════════════════════════════════════════════

    public void Initialize()
    {
        var layer = UIManager.Instance?.toastLayer;
        if (layer == null) return;

        // 创建 Toast 容器（在全局 ToastLayer 下，跨场景持久）
        var containerGO = new GameObject("ToastContainer");
        containerGO.transform.SetParent(layer, false);
        container = containerGO.AddComponent<RectTransform>();
        container.anchorMin = new Vector2(0.5f, 0f);
        container.anchorMax = new Vector2(0.5f, 0f);
        container.pivot = new Vector2(0.5f, 0f);
        container.anchoredPosition = startOffset;
        container.sizeDelta = new Vector2(800, 400);

        // 添加 VerticalLayoutGroup 便于自动排列
        var layout = containerGO.AddComponent<VerticalLayoutGroup>();
        layout.childAlignment = TextAnchor.LowerCenter;
        layout.spacing = toastSpacing;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        // 添加 ContentSizeFitter
        var fitter = containerGO.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }

    // ══════════════════════════════════════════════════════════════
    //  公开 API
    // ══════════════════════════════════════════════════════════════

    /// <summary>显示一条 Toast 提示</summary>
    public void Show(string message, ToastStyle? styleOverride = null, float duration = -1f,
        ToastColor colorType = ToastColor.Default)
    {
        if (container == null) return;

        var style = styleOverride ?? defaultStyle;
        duration = duration < 0 ? defaultDuration : duration;

        // 限制数量
        while (activeToasts.Count >= maxToasts)
        {
            ForceRemoveOldest();
        }

        // 创建 Toast 元素
        var toast = CreateToastElement(message, colorType);
        toast.transform.SetParent(container, false);
        toast.transform.SetAsLastSibling();

        // 动画
        var cg = toast.GetComponent<CanvasGroup>();
        if (cg == null)
        {
            cg = toast.AddComponent<CanvasGroup>();
        }
        var seq = AnimateToast(toast, cg, style, duration);

        var entry = new ActiveToast
        {
            go = toast,
            expireTime = Time.unscaledTime + duration + fadeInDuration + fadeOutDuration,
            sequence = seq,
        };
        activeToasts.AddLast(entry);
    }

    /// <summary>在世界坐标位置显示浮动文字</summary>
    public void ShowAtWorld(Vector3 worldPos, string message, Camera cam = null,
        ToastColor colorType = ToastColor.Default)
    {
        if (cam == null) cam = Camera.main;
        if (cam == null) return;

        Vector2 screenPos = cam.WorldToScreenPoint(worldPos);

        // 创建临时 Toast 固定在屏幕位置
        var layer = UIManager.Instance?.toastLayer;
        if (layer == null) return;

        var toast = CreateToastElement(message, colorType);
        toast.transform.SetParent(layer, false);
        var rect = toast.GetComponent<RectTransform>();

        // 转换为 Canvas 坐标
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            layer, screenPos, null, out Vector2 localPos);
        rect.anchoredPosition = localPos;

        // 简单上浮 + 淡出
        var cg = toast.AddComponent<CanvasGroup>();
        DOTween.Sequence()
            .Join(cg.DOFade(0f, 1.5f).From(1f).SetEase(Ease.InQuad))
            .Join(rect.DOAnchorPosY(localPos.y + slideUpDistance, 1.5f).SetEase(Ease.OutQuad))
            .SetUpdate(true)
            .OnComplete(() => Destroy(toast));
    }

    // ══════════════════════════════════════════════════════════════
    //  动画编排
    // ══════════════════════════════════════════════════════════════

    private Sequence AnimateToast(GameObject toast, CanvasGroup cg, ToastStyle style, float duration)
    {
        var rect = toast.GetComponent<RectTransform>();
        Sequence seq = DOTween.Sequence().SetUpdate(true);

        switch (style)
        {
            case ToastStyle.FadeSlideUp:
                cg.alpha = 0f;
                var startY = rect.anchoredPosition.y;
                rect.anchoredPosition += Vector2.down * 20f;
                seq.Append(cg.DOFade(1f, fadeInDuration))
                   .Join(rect.DOAnchorPosY(startY, fadeInDuration).SetEase(Ease.OutQuad))
                   .AppendInterval(duration)
                   .Append(cg.DOFade(0f, fadeOutDuration))
                   .Join(rect.DOAnchorPosY(startY + slideUpDistance, fadeOutDuration).SetEase(Ease.InQuad));
                break;

            case ToastStyle.TypewriterFade:
                cg.alpha = 1f;
                var tmp = toast.GetComponentInChildren<TMP_Text>();
                if (tmp != null)
                {
                    string full = tmp.text;
                    tmp.maxVisibleCharacters = 0;
                    int len = full.Length;
                    float charDelay = 0.03f;
                    float typeDuration = len * charDelay;
                    seq.Append(DOTween.To(() => tmp.maxVisibleCharacters,
                            x => tmp.maxVisibleCharacters = x, len, typeDuration)
                        .SetEase(Ease.Linear))
                       .AppendInterval(duration)
                       .Append(cg.DOFade(0f, fadeOutDuration));
                }
                break;

            case ToastStyle.GlitchFlash:
                cg.alpha = 0f;
                float flashDur = 0.3f;
                int flashes = 4;
                float interval = flashDur / flashes;
                for (int i = 0; i < flashes; i++)
                {
                    seq.Append(cg.DOFade(1f, interval * 0.3f));
                    seq.Append(cg.DOFade(0f, interval * 0.7f));
                }
                seq.Append(cg.DOFade(1f, 0.05f))
                   .AppendInterval(duration)
                   .Append(cg.DOFade(0f, fadeOutDuration));
                break;
        }

        seq.OnComplete(() =>
        {
            // 从链表移除
            var node = activeToasts.First;
            while (node != null)
            {
                if (node.Value.go == toast)
                {
                    activeToasts.Remove(node);
                    break;
                }
                node = node.Next;
            }
            Destroy(toast);
        });

        return seq;
    }

    // ══════════════════════════════════════════════════════════════
    //  内部
    // ══════════════════════════════════════════════════════════════

    private GameObject CreateToastElement(string message, ToastColor colorType)
    {
        if (toastPrefab != null)
        {
            var inst = Instantiate(toastPrefab);
            var t = inst.GetComponentInChildren<TMP_Text>();
            if (t != null)
            {
                t.text = message;
                t.color = GetColor(colorType);
            }
            return inst;
        }

        // 运行时创建
        var go = new GameObject("Toast");
        var rect = go.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(600, 40);

        // 可选背景
        var bg = go.AddComponent<Image>();
        bg.color = new Color(0.02f, 0.02f, 0.05f, 0.6f);
        bg.raycastTarget = false;

        // 文字
        var textGO = new GameObject("Text");
        textGO.transform.SetParent(go.transform, false);
        var textRect = textGO.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(10, 0);
        textRect.offsetMax = new Vector2(-10, 0);
        var tmp = textGO.AddComponent<TextMeshProUGUI>();
        tmp.text = message;
        tmp.fontSize = fontSize;
        tmp.color = GetColor(colorType);
        tmp.alignment = TextAlignmentOptions.Center;
        ApplyChineseFont(tmp);

        return go;
    }

    private Color GetColor(ToastColor colorType)
    {
        return colorType switch
        {
            ToastColor.Warning  => warningColor,
            ToastColor.Positive => positiveColor,
            _                   => textColor,
        };
    }

    private void ForceRemoveOldest()
    {
        if (activeToasts.Count == 0) return;
        var oldest = activeToasts.First.Value;
        oldest.sequence?.Kill();
        if (oldest.go != null) Destroy(oldest.go);
        activeToasts.RemoveFirst();
    }

    // ── 中文字体支持 ─────────────────────────────────────────────

    private static void ApplyChineseFont(TMP_Text tmp)
    {
        ChineseFontProvider.ApplyFont(tmp);
    }
}

/// <summary>Toast 表现类型</summary>
public enum ToastStyle
{
    [Tooltip("渐入 → 上浮 → 渐出")]
    FadeSlideUp,
    [Tooltip("打字机出现 → 停留 → 渐出")]
    TypewriterFade,
    [Tooltip("闪烁出现 → 停留 → 闪烁消失")]
    GlitchFlash,
}

/// <summary>Toast 颜色类型</summary>
public enum ToastColor
{
    Default,
    Warning,
    Positive,
}
