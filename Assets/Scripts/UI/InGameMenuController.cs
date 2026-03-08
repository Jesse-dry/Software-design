using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

/// <summary>
/// 游戏内主菜单系统（全局持久，跨场景自动注入）。
///
/// ══ 架构说明 ══
///   与 PauseSystem 完全相同的模式：
///   ┌─────────── [MANAGERS] (DontDestroyOnLoad) ──────────────────────┐
///   │  UIManager.InitializeGlobalUI()                                 │
///   │    └─ InGameMenuController.Initialize()  ← 创建一次，全局持久   │
///   └──────────────────────────────────────────────────────────────────┘
///
///   每次场景加载（UISceneRoot.Awake 触发 UIManager.OnSceneRootRegistered）：
///   1. 把 Resources/UI/InGameMenu 面板实例化到新场景的 OverlayLayer
///   2. 在新场景的 HUDLayer 子级中按名称查找 ButtonOnNormal
///   3. 自动绑定所有按钮事件
///
/// ══ 不需要在任何场景 Prefab 上手动挂载此组件 ══
///
/// ══ 按钮行为 ══
///   ButtonOnNormal（HUD）→ 打开/关闭菜单，暂停 Time.timeScale
///   continue             → 关闭菜单，恢复 Time.timeScale = 1
///   exitbutton           → 恢复时间，GameManager.EnterPhase(MainMenu)
///
/// ══ 配置要求 ══
///   • 主菜单面板 Prefab 放到 Assets/Resources/UI/ 目录，命名 InGameMenu
///     （在面板内创建 continue 按钮和 exitbutton 按钮，名称可模糊匹配）
///   • 每个需要此功能的场景 UISceneRoot 的 HUDLayer 下放置 ButtonOnNormal 物体
///   • 没有 ButtonOnNormal 的场景（如主菜单、过场）不会显示该按钮，静默跳过
/// </summary>
public class InGameMenuController : MonoBehaviour
{
    // ══════════════════════════════════════════════════════════════
    //  全局单例 + 初始化
    // ══════════════════════════════════════════════════════════════

    public static InGameMenuController Instance { get; private set; }

    /// <summary>
    /// 由 UIManager.InitializeGlobalUI() 调用，仅执行一次。
    /// 创建全局持久的逻辑物体，监听场景加载事件。
    /// </summary>
    public static InGameMenuController Initialize(Transform parent)
    {
        if (Instance != null) return Instance;

        var go = new GameObject("InGameMenuController_Logic");
        go.transform.SetParent(parent, false);
        Instance = go.AddComponent<InGameMenuController>();

        if (UIManager.Instance != null)
        {
            // 监听后续场景切换
            UIManager.Instance.OnSceneRootRegistered += Instance.OnSceneLoaded;

            // 【防漏补丁】如果调用时场景根已经注册过，立刻补发一次
            if (UIManager.Instance.HasSceneRoot && UIManager.Instance.overlayLayer != null)
            {
                var existingRoot = UIManager.Instance.overlayLayer
                    .GetComponentInParent<UISceneRoot>();
                if (existingRoot != null)
                {
                    Debug.Log("[InGameMenuController] 检测到场景已存在，立即补发加载！");
                    Instance.OnSceneLoaded(existingRoot);
                }
            }
        }

        return Instance;
    }

    // ══════════════════════════════════════════════════════════════
    //  配置常量
    // ══════════════════════════════════════════════════════════════

    /// <summary>面板 Prefab 在 Resources 下的路径（Assets/Resources/UI/InGameMenu.prefab）</summary>
    private const string PanelPrefabPath = "UI/InGameMenu";

    /// <summary>HUD 中唤起按钮的 GameObject 名称</summary>
    private const string OpenButtonName = "ButtonOnNormal";

    /// <summary>面板内"继续"按钮名称（模糊匹配，不区分大小写）</summary>
    private const string ContinueButtonName = "continue";

    /// <summary>面板内"退出"按钮名称（模糊匹配，不区分大小写）</summary>
    private const string ExitButtonName = "exitbutton";

    // ══════════════════════════════════════════════════════════════
    //  动画参数（可在运行时创建的物体 Inspector 上调整）
    // ══════════════════════════════════════════════════════════════

    [Header("== 动画配置 ==")]
    [SerializeField] private float fadeDuration = 0.25f;
    [SerializeField] private bool  useScaleAnimation = true;
    [SerializeField, Range(0.5f, 1f)] private float scaleFrom = 0.85f;
    [SerializeField] private Ease showEase = Ease.OutBack;
    [SerializeField] private Ease hideEase = Ease.InQuad;

    // ══════════════════════════════════════════════════════════════
    //  运行时状态（每次场景加载后重新赋值）
    // ══════════════════════════════════════════════════════════════

    private GameObject    _menuPanel;
    private CanvasGroup   _menuCG;
    private RectTransform _menuRect;
    private Button        _openButton;
    private Button        _continueButton;
    private Button        _exitButton;

    private bool _isMenuOpen  = false;
    private bool _isAnimating = false;

    // ══════════════════════════════════════════════════════════════
    //  场景加载回调（每次场景切换自动触发）
    // ══════════════════════════════════════════════════════════════

    private void OnSceneLoaded(UISceneRoot root)
    {
        Debug.Log($"[InGameMenuController] 监听到新场景加载: {(root != null ? root.name : "null")}");

        // 销毁上一场景的面板实例
        if (_menuPanel != null)
        {
            _menuPanel.SetActive(false);
            Destroy(_menuPanel);
            _menuPanel = null;
        }

        UnbindAllButtons();

        if (root == null) return;

        if (root.overlayLayer != null)
            LoadMenuPanel(root.overlayLayer);

        if (root.hudLayer != null)
            BindOpenButton(root.hudLayer);
    }

    // ══════════════════════════════════════════════════════════════
    //  面板加载
    // ══════════════════════════════════════════════════════════════

    private void LoadMenuPanel(Transform overlayLayer)
    {
        // ── Step 1：优先复用场景内已静态放置的面板 ──────────────────────
        // 美术在 UIRoot Prefab 的 OverlayLayer 下直接放了 "主菜单" → 复用它，
        // 避免"静态放置 + Resources 注入"产生双实例的问题。
        var existingTransform = FindInChildren(overlayLayer, "主菜单");
        if (existingTransform != null)
        {
            _menuPanel = existingTransform.gameObject;
            Debug.Log("[InGameMenuController] 复用场景内已有的 '主菜单' 面板。");
        }
        else
        {
            // 回退：从 Resources 动态加载
            var prefab = Resources.Load<GameObject>(PanelPrefabPath);
            if (prefab == null)
            {
                Debug.LogError(
                    $"[InGameMenuController] 面板未找到。\n" +
                    $"方案 A（推荐）：在场景 UIRoot 的 OverlayLayer 下放置名为 '主菜单' 的面板。\n" +
                    $"方案 B：把 主菜单.prefab 保存到 Assets/Resources/UI/InGameMenu.prefab。");
                return;
            }
            _menuPanel = Instantiate(prefab, overlayLayer, false);
            // 全屏拉伸（动态创建时才需要）
            var rectDyn = _menuPanel.GetComponent<RectTransform>();
            if (rectDyn != null)
            {
                rectDyn.anchorMin        = Vector2.zero;
                rectDyn.anchorMax        = Vector2.one;
                rectDyn.offsetMin        = Vector2.zero;
                rectDyn.offsetMax        = Vector2.zero;
                rectDyn.localScale       = Vector3.one;
                rectDyn.anchoredPosition = Vector2.zero;
            }
            Debug.Log("[InGameMenuController] 从 Resources 实例化主菜单面板。");
        }

        _menuRect = _menuPanel.GetComponent<RectTransform>();

        // ── Step 2：修复根 Canvas 的排序与交互问题 ─────────────────────
        // Bug A（显示在最上层）：prefab 根节点的 Canvas sortingOrder 过高，
        //         超过全局层（1000）会遮挡所有 UI，超过 overlayLayer（50）会不受控。
        //         修正到 overlayLayer(50) + 5 = 55，保持在正确层级。
        // Bug B（无法交互）：根节点有独立 Canvas 但缺少 GraphicRaycaster，
        //         导致按钮永远收不到射线事件。
        var rootCanvas = _menuPanel.GetComponent<Canvas>();
        if (rootCanvas != null)
        {
            if (rootCanvas.sortingOrder > 100)
            {
                Debug.Log($"[InGameMenuController] 面板根Canvas.sortingOrder={rootCanvas.sortingOrder} 过高，已修正为 55。");
                rootCanvas.sortingOrder = 55;
            }
            if (_menuPanel.GetComponent<GraphicRaycaster>() == null)
            {
                _menuPanel.AddComponent<GraphicRaycaster>();
                Debug.Log("[InGameMenuController] 已自动添加 GraphicRaycaster（按钮交互修复）。");
            }
        }

        // ── Step 3：CanvasGroup（动画 + 交互控制）──────────────────────
        _menuCG = _menuPanel.GetComponent<CanvasGroup>();
        if (_menuCG == null) _menuCG = _menuPanel.AddComponent<CanvasGroup>();

        // ── Step 4：绑定面板内按钮 ──────────────────────────────────────
        _continueButton = FindButtonInChildren(_menuPanel.transform, ContinueButtonName);
        _exitButton     = FindButtonInChildren(_menuPanel.transform, ExitButtonName);

        if (_continueButton != null) _continueButton.onClick.AddListener(OnContinueClicked);
        else Debug.LogWarning($"[InGameMenuController] 面板内未找到 '{ContinueButtonName}' 按钮。");

        if (_exitButton != null) _exitButton.onClick.AddListener(OnExitClicked);
        else Debug.LogWarning($"[InGameMenuController] 面板内未找到 '{ExitButtonName}' 按钮。");

        // ── Step 5：强制初始隐藏（覆盖 prefab 中 alpha/active 默认值）──
        _menuCG.alpha          = 0f;
        _menuCG.interactable   = false;
        _menuCG.blocksRaycasts = false;
        _menuPanel.SetActive(false);

        _isMenuOpen  = false;
        _isAnimating = false;

        Debug.Log("[InGameMenuController] 游戏内主菜单面板初始化完成。");
    }

    // ══════════════════════════════════════════════════════════════
    //  HUD 唤起按钮绑定
    // ══════════════════════════════════════════════════════════════

    private void BindOpenButton(Transform hudLayer)
    {
        var btnTransform = FindInChildren(hudLayer, OpenButtonName);
        if (btnTransform != null)
        {
            _openButton = btnTransform.GetComponent<Button>();
            if (_openButton != null)
            {
                _openButton.onClick.AddListener(OnOpenMenuClicked);
                Debug.Log($"[InGameMenuController] 已绑定 HUD 按钮 '{OpenButtonName}'。");
            }
            else
            {
                Debug.LogWarning($"[InGameMenuController] 找到 '{OpenButtonName}' 但没有 Button 组件！");
            }
        }
        else
        {
            // 该场景 HUDLayer 没有此按钮，静默跳过（不是错误）
            Debug.Log($"[InGameMenuController] 当前场景 HUDLayer 中无 '{OpenButtonName}'，本场景不显示游戏内主菜单按钮。");
        }
    }

    private void UnbindAllButtons()
    {
        if (_openButton    != null) { _openButton.onClick.RemoveListener(OnOpenMenuClicked);   _openButton    = null; }
        if (_continueButton != null) { _continueButton.onClick.RemoveListener(OnContinueClicked); _continueButton = null; }
        if (_exitButton    != null) { _exitButton.onClick.RemoveListener(OnExitClicked);       _exitButton    = null; }
    }

    // ══════════════════════════════════════════════════════════════
    //  按钮回调
    // ══════════════════════════════════════════════════════════════

    private void OnOpenMenuClicked()
    {
        if (_isAnimating) return;
        if (_isMenuOpen) HideMenu(); else ShowMenu();
    }

    private void OnContinueClicked()
    {
        if (_isAnimating) return;
        Debug.Log("[InGameMenuController] 继续游戏");
        HideMenu();
    }

    private void OnExitClicked()
    {
        if (_isAnimating) return;
        Debug.Log("[InGameMenuController] 返回主菜单");

        Time.timeScale = 1f;
        _isMenuOpen    = false;

        if (GameManager.Instance != null)
        {
            GameManager.Instance.EnterPhase(GamePhase.MainMenu);
        }
        else
        {
            Debug.LogWarning("[InGameMenuController] GameManager 不可用，通过 SceneController 回退。");
            SceneController.Instance?.LoadMainMenu();
        }
    }

    // ══════════════════════════════════════════════════════════════
    //  菜单显示 / 隐藏（DOTween 动画）
    // ══════════════════════════════════════════════════════════════

    public void ShowMenu()
    {
        if (_menuPanel == null || _isMenuOpen) return;

        _isMenuOpen  = true;
        _isAnimating = true;

        Time.timeScale = 0f;
        _menuPanel.SetActive(true);

        _menuCG.alpha          = 0f;
        _menuCG.interactable   = false;
        _menuCG.blocksRaycasts = false;

        var seq = DOTween.Sequence().SetUpdate(true);

        if (useScaleAnimation && _menuRect != null)
        {
            _menuRect.localScale = Vector3.one * scaleFrom;
            seq.Join(_menuRect.DOScale(Vector3.one, fadeDuration).SetEase(showEase));
        }

        seq.Join(_menuCG.DOFade(1f, fadeDuration));
        seq.OnComplete(() =>
        {
            _menuCG.interactable   = true;
            _menuCG.blocksRaycasts = true;
            _isAnimating = false;
        });
    }

    public void HideMenu()
    {
        if (_menuPanel == null || !_isMenuOpen) return;

        _isMenuOpen  = false;
        _isAnimating = true;

        _menuCG.interactable   = false;
        _menuCG.blocksRaycasts = false;

        var seq = DOTween.Sequence().SetUpdate(true);

        if (useScaleAnimation && _menuRect != null)
            seq.Join(_menuRect.DOScale(Vector3.one * scaleFrom, fadeDuration).SetEase(hideEase));

        seq.Join(_menuCG.DOFade(0f, fadeDuration));
        seq.OnComplete(() =>
        {
            _menuPanel.SetActive(false);
            _isAnimating = false;
            Time.timeScale = 1f;
        });
    }

    /// <summary>菜单当前是否打开（供 PauseSystem 查询，避免 ESC 重叠）</summary>
    public bool IsMenuOpen => _isMenuOpen;

    /// <summary>切换菜单，可由外部调用</summary>
    public void ToggleMenu()
    {
        if (_isAnimating) return;
        if (_isMenuOpen) HideMenu(); else ShowMenu();
    }

    // ══════════════════════════════════════════════════════════════
    //  工具方法
    // ══════════════════════════════════════════════════════════════

    private static Transform FindInChildren(Transform parent, string name)
    {
        if (parent == null) return null;
        foreach (Transform child in parent)
        {
            if (child.name == name) return child;
            var r = FindInChildren(child, name);
            if (r != null) return r;
        }
        return null;
    }

    private static Button FindButtonInChildren(Transform parent, string namePart)
    {
        if (parent == null) return null;
        string lower = namePart.ToLower();
        foreach (var btn in parent.GetComponentsInChildren<Button>(true))
        {
            if (btn.gameObject.name.ToLower().Contains(lower))
                return btn;
        }
        return null;
    }

    // ══════════════════════════════════════════════════════════════
    //  销毁清理
    // ══════════════════════════════════════════════════════════════

    private void OnDestroy()
    {
        if (UIManager.Instance != null)
            UIManager.Instance.OnSceneRootRegistered -= OnSceneLoaded;

        UnbindAllButtons();

        if (_isMenuOpen)
            Time.timeScale = 1f;

        Instance = null;    }
}