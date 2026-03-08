using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using DG.Tweening;
using TMPro;
using System;
using System.Collections.Generic;

/// <summary>
/// 模态弹窗系统（挂在 UIManager 上）。
///
/// 【职责】
///   - 文本弹窗（标题 + 正文 + 关闭按钮）
///   - 确认弹窗（提示 + 是/否按钮）
///   - 自定义 Prefab 弹窗
///   - 入场/退场动画（FadeScale / SlideUp / GlitchIn）
///
/// 【键盘导航】
///   系统内部处理弹窗的键盘关闭，游戏逻辑层无需轮询键盘：
///   - 文本弹窗：E / Enter / Space / Esc → 关闭
///   - 确认弹窗：Enter / Space / E → 确认（Yes），Esc → 取消（No）
///   - 开弹窗后有 3 帧延迟，防止同帧按键误触
///
/// 【使用方式】
///   UIManager.Instance.Modal.ShowText("标题", "正文", "确认", () => Debug.Log("closed"));
///   UIManager.Instance.Modal.ShowConfirm("确认？", onYes, onNo);
/// </summary>
public class ModalSystem : MonoBehaviour
{
    // ── Inspector 配置 ───────────────────────────────────────────
    [Header("== 弹窗入场效果 ==")]
    [SerializeField] private ModalAnimationType animationType = ModalAnimationType.FadeScale;
    [SerializeField] private float animDuration = 0.35f;
    [SerializeField] private float scaleFrom = 0.8f;
    [SerializeField] private float slideDistance = 300f;
    [SerializeField] private Ease openEase = Ease.OutBack;
    [SerializeField] private Ease closeEase = Ease.InQuad;

    [Header("== 默认弹窗 Prefab ==")]
    [SerializeField] private GameObject textModalPrefab;
    [SerializeField] private GameObject confirmModalPrefab;

    [Header("== 按钮样式 ==")]
    [Tooltip("是否对弹窗按钮启用 StyledButton 悬停/按下效果")]
    [SerializeField] private bool useStyledButtons = true;

    [Tooltip("按钮大小覆盖（设为 (0,0) 则保留原始大小）")]
    [SerializeField] private Vector2 styledButtonSize = Vector2.zero;

    [Tooltip("悬停缩放")]
    [Range(1f, 1.3f)]
    [SerializeField] private float styledHoverScale = 1.08f;

    [Tooltip("按下缩放")]
    [Range(0.85f, 1f)]
    [SerializeField] private float styledPressScale = 0.95f;

    [Tooltip("常态背景颜色")]
    [SerializeField] private Color styledNormalColor = new Color(0.15f, 0.15f, 0.2f, 1f);

    [Tooltip("悬停背景颜色")]
    [SerializeField] private Color styledHoverColor = new Color(0.25f, 0.25f, 0.35f, 1f);

    [Tooltip("按下背景颜色")]
    [SerializeField] private Color styledPressColor = new Color(0.1f, 0.3f, 0.2f, 1f);

    [Tooltip("常态文字颜色")]
    [SerializeField] private Color styledNormalTextColor = new Color(0.7f, 0.9f, 0.7f, 1f);

    [Tooltip("悬停文字颜色")]
    [SerializeField] private Color styledHoverTextColor = new Color(0.9f, 1f, 0.9f, 1f);

    [Tooltip("是否启用发光边框")]
    [SerializeField] private bool styledGlowOutline = false;

    // ── 弹窗栈 ──────────────────────────────────────────────────
    private readonly Stack<ModalEntry> _modalStack = new();
    private Tween _currentAnim;

    /// <summary>当前弹窗栈深度</summary>
    public int StackDepth => _modalStack.Count;

    // ══════════════════════════════════════════════════════════════
    //  弹窗条目（存储面板 + 回调）
    // ══════════════════════════════════════════════════════════════

    private class ModalEntry
    {
        public GameObject panel;
        public Action onConfirm;   // Enter/Space/E → 触发
        public Action onCancel;    // Esc → 触发
        public int openFrame;      // 打开帧号（键盘延迟用）
    }

    // ══════════════════════════════════════════════════════════════
    //  初始化
    // ══════════════════════════════════════════════════════════════

    public void Initialize()
    {
        // 清理旧状态（场景切换时，panel 已随场景销毁）
        _currentAnim?.Kill();
        _modalStack.Clear();
    }

    /// <summary>
    /// 运行时批量配置按钮样式（供 MemorySceneSetup 等场景 Setup 脚本调用）。
    /// </summary>
    public void ConfigureButtonStyle(
        bool?    enabled      = null,
        Vector2? size         = null,
        float?   hoverScale   = null,
        float?   pressScale   = null,
        Color?   normalBg     = null,
        Color?   hoverBg      = null,
        Color?   pressBg      = null,
        Color?   normalText   = null,
        Color?   hoverText    = null,
        bool?    glow         = null)
    {
        if (enabled.HasValue)    useStyledButtons       = enabled.Value;
        if (size.HasValue)       styledButtonSize       = size.Value;
        if (hoverScale.HasValue) styledHoverScale       = hoverScale.Value;
        if (pressScale.HasValue) styledPressScale       = pressScale.Value;
        if (normalBg.HasValue)   styledNormalColor      = normalBg.Value;
        if (hoverBg.HasValue)    styledHoverColor       = hoverBg.Value;
        if (pressBg.HasValue)    styledPressColor       = pressBg.Value;
        if (normalText.HasValue) styledNormalTextColor   = normalText.Value;
        if (hoverText.HasValue)  styledHoverTextColor    = hoverText.Value;
        if (glow.HasValue)       styledGlowOutline       = glow.Value;
    }

    // ══════════════════════════════════════════════════════════════
    //  键盘导航（核心：弹窗关闭的键盘入口在此统一处理）
    // ══════════════════════════════════════════════════════════════

    private void Update()
    {
        if (_modalStack.Count == 0) return;

        var top = _modalStack.Peek();

        // 3 帧延迟：防止打开弹窗的 E 键同帧触发关闭
        if (Time.frameCount - top.openFrame < 3) return;

        var kb = Keyboard.current;
        if (kb == null) return;

        if (kb.enterKey.wasPressedThisFrame ||
            kb.spaceKey.wasPressedThisFrame ||
            kb.eKey.wasPressedThisFrame)
        {
            top.onConfirm?.Invoke();
        }
        else if (kb.escapeKey.wasPressedThisFrame)
        {
            top.onCancel?.Invoke();
        }
    }

    // ══════════════════════════════════════════════════════════════
    //  公开 API
    // ══════════════════════════════════════════════════════════════

    /// <summary>显示文本弹窗（标题 + 正文 + 关闭按钮）。</summary>
    public void ShowText(string title, string body, string buttonText = "确认", Action onClose = null)
    {
        var modalLayer = UIManager.Instance?.modalLayer;
        if (modalLayer == null)
        {
            Debug.LogWarning("[ModalSystem] modalLayer 为 null，无法打开弹窗。");
            onClose?.Invoke();
            return;
        }

        if (_modalStack.Count == 0)
            UIManager.Instance.ShowModalBackground();

        var panel = textModalPrefab != null
            ? Instantiate(textModalPrefab, modalLayer)
            : CreateRuntimeTextModal(modalLayer);

        panel.name = "TextModal";

        // 绑定内容
        var titleTMP = panel.transform.Find("Title")?.GetComponent<TMP_Text>();
        var bodyTMP  = panel.transform.Find("Body")?.GetComponent<TMP_Text>();
        var btn      = panel.transform.Find("CloseButton")?.GetComponent<Button>();
        var btnText  = btn?.GetComponentInChildren<TMP_Text>();

        if (titleTMP != null) titleTMP.text = title;
        if (bodyTMP != null)  bodyTMP.text = body;
        if (btnText != null)  btnText.text = buttonText;

        // 应用按钮样式
        if (btn != null) ApplyStyledButton(btn.gameObject);

        // 关闭动作——按钮和键盘共用同一个闭包，保证幂等
        bool closed = false;
        Action doClose = () =>
        {
            if (closed) return;
            closed = true;
            Close(onClose);
        };

        btn?.onClick.AddListener(() => doClose());

        // 入栈：Enter/Space/E 和 Esc 都执行关闭
        _modalStack.Push(new ModalEntry
        {
            panel = panel,
            onConfirm = doClose,
            onCancel = doClose,
            openFrame = Time.frameCount
        });

        PlayOpenAnimation(panel.transform, () => { });
    }

    /// <summary>显示确认弹窗（提示 + 是/否按钮）。</summary>
    public void ShowConfirm(string message, Action onYes, Action onNo = null)
    {
        var modalLayer = UIManager.Instance?.modalLayer;
        if (modalLayer == null)
        {
            Debug.LogWarning("[ModalSystem] modalLayer 为 null，无法打开确认弹窗。");
            onNo?.Invoke();
            return;
        }

        if (_modalStack.Count == 0)
            UIManager.Instance.ShowModalBackground();

        var panel = confirmModalPrefab != null
            ? Instantiate(confirmModalPrefab, modalLayer)
            : CreateRuntimeConfirmModal(modalLayer);

        panel.name = "ConfirmModal";

        var msgTMP = panel.transform.Find("Message")?.GetComponent<TMP_Text>();
        var yesBtn = panel.transform.Find("YesButton")?.GetComponent<Button>();
        var noBtn  = panel.transform.Find("NoButton")?.GetComponent<Button>();

        if (msgTMP != null) msgTMP.text = message;

        // 应用按钮样式
        if (yesBtn != null) ApplyStyledButton(yesBtn.gameObject);
        if (noBtn != null)  ApplyStyledButton(noBtn.gameObject);

        // 幂等回调
        bool decided = false;
        Action doYes = () =>
        {
            if (decided) return;
            decided = true;
            Close(() => onYes?.Invoke());
        };
        Action doNo = () =>
        {
            if (decided) return;
            decided = true;
            Close(() => onNo?.Invoke());
        };

        yesBtn?.onClick.AddListener(() => doYes());
        noBtn?.onClick.AddListener(() => doNo());

        // 入栈：Enter/Space/E → Yes，Esc → No
        _modalStack.Push(new ModalEntry
        {
            panel = panel,
            onConfirm = doYes,
            onCancel = doNo,
            openFrame = Time.frameCount
        });

        PlayOpenAnimation(panel.transform, () => { });
    }

    /// <summary>打开自定义 Prefab 弹窗。</summary>
    public GameObject ShowCustom(GameObject prefab, Action onClose = null)
    {
        if (prefab == null)
        {
            Debug.LogWarning("[ModalSystem] Prefab 为空！");
            onClose?.Invoke();
            return null;
        }

        var modalLayer = UIManager.Instance?.modalLayer;
        if (modalLayer == null)
        {
            Debug.LogWarning("[ModalSystem] modalLayer 为 null。");
            onClose?.Invoke();
            return null;
        }

        if (_modalStack.Count == 0)
            UIManager.Instance.ShowModalBackground();

        var instance = Instantiate(prefab, modalLayer);
        instance.name = prefab.name + "_Modal";

        bool closed = false;
        Action doClose = () =>
        {
            if (closed) return;
            closed = true;
            Close(onClose);
        };

        _modalStack.Push(new ModalEntry
        {
            panel = instance,
            onConfirm = doClose,
            onCancel = doClose,
            openFrame = Time.frameCount
        });

        PlayOpenAnimation(instance.transform, () => { });
        return instance;
    }

    /// <summary>显示输入弹窗（提示 + 输入框 + 确认/取消按钮）。</summary>
    public void ShowInput(string message, string placeholder, Action<string> onConfirm, Action onCancel = null)
    {
        var modalLayer = UIManager.Instance?.modalLayer;
        if (modalLayer == null)
        {
            Debug.LogWarning("[ModalSystem] modalLayer 为 null，无法打开输入弹窗。");
            onCancel?.Invoke();
            return;
        }

        if (_modalStack.Count == 0)
            UIManager.Instance.ShowModalBackground();

        var panel = CreateRuntimeInputModal(modalLayer, message, placeholder);
        panel.name = "InputModal";

        var inputField = panel.GetComponentInChildren<TMP_InputField>();
        var confirmBtn = panel.transform.Find("ConfirmButton")?.GetComponent<Button>();
        var cancelBtn  = panel.transform.Find("CancelButton")?.GetComponent<Button>();

        if (confirmBtn != null) ApplyStyledButton(confirmBtn.gameObject);
        if (cancelBtn != null)  ApplyStyledButton(cancelBtn.gameObject);

        bool decided = false;
        Action doConfirm = () =>
        {
            if (decided) return;
            decided = true;
            string value = inputField != null ? inputField.text : "";
            Close(() => onConfirm?.Invoke(value));
        };
        Action doCancel = () =>
        {
            if (decided) return;
            decided = true;
            Close(() => onCancel?.Invoke());
        };

        confirmBtn?.onClick.AddListener(() => doConfirm());
        cancelBtn?.onClick.AddListener(() => doCancel());

        _modalStack.Push(new ModalEntry
        {
            panel = panel,
            onConfirm = doConfirm,
            onCancel = doCancel,
            openFrame = Time.frameCount
        });

        PlayOpenAnimation(panel.transform, () =>
        {
            inputField?.ActivateInputField();
        });
    }

    // ══════════════════════════════════════════════════════════════
    //  关闭
    // ══════════════════════════════════════════════════════════════

    /// <summary>关闭最上层弹窗（带动画）。</summary>
    public void Close(Action onComplete = null)
    {
        if (_modalStack.Count == 0) { onComplete?.Invoke(); return; }

        var entry = _modalStack.Pop();

        PlayCloseAnimation(entry.panel.transform, () =>
        {
            if (entry.panel != null) Destroy(entry.panel);

            if (_modalStack.Count == 0)
                UIManager.Instance.HideModalBackground(() => onComplete?.Invoke());
            else
                onComplete?.Invoke();
        });
    }

    /// <summary>立即关闭所有弹窗（无动画）。</summary>
    public void CloseAll(Action onComplete = null)
    {
        while (_modalStack.Count > 0)
        {
            var entry = _modalStack.Pop();
            if (entry.panel != null) Destroy(entry.panel);
        }
        _currentAnim?.Kill();
        UIManager.Instance.HideModalBackground(onComplete);
    }

    // ══════════════════════════════════════════════════════════════
    //  动画
    // ══════════════════════════════════════════════════════════════

    private void PlayOpenAnimation(Transform target, Action onComplete)
    {
        _currentAnim?.Kill();
        var cg = GetOrAddCanvasGroup(target);

        switch (animationType)
        {
            case ModalAnimationType.FadeScale:
                cg.alpha = 0f;
                target.localScale = Vector3.one * scaleFrom;
                _currentAnim = DOTween.Sequence()
                    .Join(cg.DOFade(1f, animDuration))
                    .Join(target.DOScale(1f, animDuration).SetEase(openEase))
                    .SetUpdate(true)
                    .OnComplete(() => onComplete?.Invoke());
                break;

            case ModalAnimationType.SlideUp:
                cg.alpha = 0f;
                var rect = target.GetComponent<RectTransform>();
                var origPos = rect.anchoredPosition;
                rect.anchoredPosition = origPos + Vector2.down * slideDistance;
                _currentAnim = DOTween.Sequence()
                    .Join(cg.DOFade(1f, animDuration))
                    .Join(rect.DOAnchorPos(origPos, animDuration).SetEase(openEase))
                    .SetUpdate(true)
                    .OnComplete(() => onComplete?.Invoke());
                break;

            case ModalAnimationType.GlitchIn:
                StartCoroutine(GlitchInCoroutine(cg, onComplete));
                break;
        }
    }

    private void PlayCloseAnimation(Transform target, Action onComplete)
    {
        _currentAnim?.Kill();
        var cg = GetOrAddCanvasGroup(target);

        switch (animationType)
        {
            case ModalAnimationType.FadeScale:
                _currentAnim = DOTween.Sequence()
                    .Join(cg.DOFade(0f, animDuration * 0.5f))
                    .Join(target.DOScale(scaleFrom, animDuration * 0.5f).SetEase(closeEase))
                    .SetUpdate(true)
                    .OnComplete(() => onComplete?.Invoke());
                break;

            case ModalAnimationType.SlideUp:
                var rect = target.GetComponent<RectTransform>();
                _currentAnim = DOTween.Sequence()
                    .Join(cg.DOFade(0f, animDuration * 0.5f))
                    .Join(rect.DOAnchorPos(rect.anchoredPosition + Vector2.down * slideDistance,
                        animDuration * 0.5f).SetEase(closeEase))
                    .SetUpdate(true)
                    .OnComplete(() => onComplete?.Invoke());
                break;

            case ModalAnimationType.GlitchIn:
                cg.DOFade(0f, 0.15f).SetUpdate(true)
                    .OnComplete(() => onComplete?.Invoke());
                break;
        }
    }

    private System.Collections.IEnumerator GlitchInCoroutine(CanvasGroup cg, Action onComplete)
    {
        float elapsed = 0f;
        float dur = animDuration;
        while (elapsed < dur)
        {
            cg.alpha = UnityEngine.Random.value > 0.3f ? 1f : 0f;
            elapsed += 0.04f;
            yield return new WaitForSecondsRealtime(0.04f);
        }
        cg.alpha = 1f;
        onComplete?.Invoke();
    }

    private CanvasGroup GetOrAddCanvasGroup(Transform t)
    {
        var cg = t.GetComponent<CanvasGroup>();
        if (cg == null) cg = t.gameObject.AddComponent<CanvasGroup>();
        return cg;
    }

    // ══════════════════════════════════════════════════════════════
    //  运行时最小弹窗创建
    // ══════════════════════════════════════════════════════════════

    private GameObject CreateRuntimeTextModal(Transform parent)
    {
        var panel = CreateModalPanel(parent, new Vector2(600, 400));
        CreateTMPChild(panel.transform, "Title", "标题", 28, new Vector2(0, 120), new Vector2(500, 50));
        CreateTMPChild(panel.transform, "Body", "内容", 20, new Vector2(0, 0), new Vector2(500, 200));
        CreateButtonChild(panel.transform, "CloseButton", "确认", new Vector2(0, -150), new Vector2(160, 45));
        return panel;
    }

    private GameObject CreateRuntimeConfirmModal(Transform parent)
    {
        var panel = CreateModalPanel(parent, new Vector2(500, 250));
        CreateTMPChild(panel.transform, "Message", "确认？", 22, new Vector2(0, 30), new Vector2(400, 100));
        CreateButtonChild(panel.transform, "YesButton", "是", new Vector2(-80, -70), new Vector2(120, 40));
        CreateButtonChild(panel.transform, "NoButton", "否", new Vector2(80, -70), new Vector2(120, 40));
        return panel;
    }

    private GameObject CreateRuntimeInputModal(Transform parent, string message, string placeholder)
    {
        var panel = CreateModalPanel(parent, new Vector2(650, 300));

        // 提示文字
        CreateTMPChild(panel.transform, "Message", message, 20,
            new Vector2(0, 85), new Vector2(580, 70));

        // ── 输入框容器 ──
        var inputGO = new GameObject("InputField");
        inputGO.transform.SetParent(panel.transform, false);
        var inputRect = inputGO.AddComponent<RectTransform>();
        inputRect.anchoredPosition = new Vector2(0, 0);
        inputRect.sizeDelta = new Vector2(540, 50);
        var inputBg = inputGO.AddComponent<Image>();
        inputBg.color = new Color(0.1f, 0.1f, 0.15f, 1f);

        // Text Area（输入框的文本视口）
        var textArea = new GameObject("Text Area");
        textArea.transform.SetParent(inputGO.transform, false);
        var textAreaRect = textArea.AddComponent<RectTransform>();
        textAreaRect.anchorMin = Vector2.zero;
        textAreaRect.anchorMax = Vector2.one;
        textAreaRect.offsetMin = new Vector2(10, 5);
        textAreaRect.offsetMax = new Vector2(-10, -5);
        textArea.AddComponent<RectMask2D>();

        // Placeholder
        var placeholderGO = new GameObject("Placeholder");
        placeholderGO.transform.SetParent(textArea.transform, false);
        var phRect = placeholderGO.AddComponent<RectTransform>();
        phRect.anchorMin = Vector2.zero;
        phRect.anchorMax = Vector2.one;
        phRect.offsetMin = Vector2.zero;
        phRect.offsetMax = Vector2.zero;
        var phText = placeholderGO.AddComponent<TextMeshProUGUI>();
        phText.text = placeholder;
        phText.fontSize = 16;
        phText.color = new Color(0.5f, 0.5f, 0.5f, 0.6f);
        phText.fontStyle = FontStyles.Italic;
        ApplyChineseFont(phText);

        // 输入文本
        var inputTextGO = new GameObject("Text");
        inputTextGO.transform.SetParent(textArea.transform, false);
        var itRect = inputTextGO.AddComponent<RectTransform>();
        itRect.anchorMin = Vector2.zero;
        itRect.anchorMax = Vector2.one;
        itRect.offsetMin = Vector2.zero;
        itRect.offsetMax = Vector2.zero;
        var inputText = inputTextGO.AddComponent<TextMeshProUGUI>();
        inputText.fontSize = 16;
        inputText.color = new Color(0.9f, 0.9f, 0.9f, 1f);
        ApplyChineseFont(inputText);

        // 组装 TMP_InputField
        var inputField = inputGO.AddComponent<TMP_InputField>();
        inputField.textViewport = textAreaRect;
        inputField.textComponent = inputText;
        inputField.placeholder = phText;
        inputField.fontAsset = inputText.font;
        inputField.pointSize = 16;

        // 按钮
        CreateButtonChild(panel.transform, "ConfirmButton", "确认",
            new Vector2(-90, -100), new Vector2(140, 40));
        CreateButtonChild(panel.transform, "CancelButton", "取消",
            new Vector2(90, -100), new Vector2(140, 40));

        return panel;
    }

    private GameObject CreateModalPanel(Transform parent, Vector2 size)
    {
        var go = new GameObject("ModalPanel");
        go.transform.SetParent(parent, false);
        var rect = go.AddComponent<RectTransform>();
        rect.sizeDelta = size;
        var img = go.AddComponent<Image>();
        img.color = new Color(0.05f, 0.05f, 0.08f, 0.95f);
        return go;
    }

    private void CreateTMPChild(Transform parent, string name, string text, float fontSize,
        Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rect = go.AddComponent<RectTransform>();
        rect.anchoredPosition = pos;
        rect.sizeDelta = size;
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = new Color(0.85f, 0.85f, 0.9f);
        tmp.alignment = TextAlignmentOptions.Center;
        ApplyChineseFont(tmp);
    }

    private void CreateButtonChild(Transform parent, string name, string label,
        Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rect = go.AddComponent<RectTransform>();
        rect.anchoredPosition = pos;
        rect.sizeDelta = size;
        var img = go.AddComponent<Image>();
        img.color = new Color(0.15f, 0.15f, 0.2f, 1f);
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;

        var textGO = new GameObject("Text");
        textGO.transform.SetParent(go.transform, false);
        var textRect = textGO.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        var tmp = textGO.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 18;
        tmp.color = new Color(0.7f, 0.9f, 0.7f);
        tmp.alignment = TextAlignmentOptions.Center;
        ApplyChineseFont(tmp);

        // 应用样式
        ApplyStyledButton(go);
    }

    // ── 按钮样式应用 ─────────────────────────────────────────────

    /// <summary>
    /// 为指定按钮 GameObject 添加/配置 StyledButton 组件。
    /// 使用当前 ModalSystem 中的样式参数。
    /// </summary>
    private void ApplyStyledButton(GameObject btnGO)
    {
        if (!useStyledButtons || btnGO == null) return;

        var styled = btnGO.GetComponent<StyledButton>();
        if (styled == null)
            styled = btnGO.AddComponent<StyledButton>();

        styled.ApplyStyle(
            size:           styledButtonSize != Vector2.zero ? styledButtonSize : null,
            normalBg:       styledNormalColor,
            hoverBg:        styledHoverColor,
            pressBg:        styledPressColor,
            normalText:     styledNormalTextColor,
            hoverText:      styledHoverTextColor,
            hoverScaleVal:  styledHoverScale,
            pressScaleVal:  styledPressScale,
            glow:           styledGlowOutline);
    }

    // ── 中文字体 ─────────────────────────────────────────────────

    private static void ApplyChineseFont(TMP_Text tmp)
    {
        ChineseFontProvider.ApplyFont(tmp);
    }

    private void OnDestroy()
    {
        _currentAnim?.Kill();
    }
}

/// <summary>弹窗入场动画类型</summary>
public enum ModalAnimationType
{
    FadeScale,
    SlideUp,
    GlitchIn,
}
