using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using DG.Tweening;
using System;

/// <summary>
/// UI 统一管理器（跨场景持久 — 挂在 [MANAGERS] 下）。
/// 
/// 架构（路线 A — 场景专属 UISceneRoot + 全局覆盖层）：
/// 
///   ┌─────────── [MANAGERS] (DontDestroyOnLoad) ──────────────────┐
///   │  UIManager                                                  │
///   │    └─ GlobalCanvas (sortOrder ≥ 1000, ScreenSpace-Overlay) │
///   │         ├─ ToastLayer      (sortOrder = 1050)              │
///   │         └─ TransitionLayer (sortOrder = 1100)              │
///   └────────────────────────────────────────────────────────────┘
/// 
///   ┌─────────── Scene（随场景加载 / 卸载） ──────────────────────┐
///   │  UISceneRoot（美术定制的 Prefab）                           │
///   │    ├─ HUDLayer      (sortOrder = 10)                       │
///   │    ├─ OverlayLayer  (sortOrder = 50)                       │
///   │    └─ ModalLayer    (sortOrder = 90)                       │
///   └────────────────────────────────────────────────────────────┘
/// 
///   UISceneRoot 在 Awake 时自动向 UIManager 注册，OnDestroy 时注销。
///   美术可为每个场景制作独立的 UISceneRoot Prefab，自由定制层内容与布局。
/// 
/// 使用方式（对外不变）：
///   UIManager.Instance.Toast.Show("拾取了记忆碎片");
///   UIManager.Instance.Modal.ShowText("标题", "正文");
///   UIManager.Instance.Transition.FadeIn(1f);
///   UIManager.Instance.HUD.SetChaosValue(0.7f);
/// 
/// 初始化：
///   由 GameBootstrapper 创建 UIManager 并调用 InitializeGlobalUI()。
///   场景子系统在 UISceneRoot 注册时自动初始化。
/// </summary>
public class UIManager : MonoBehaviour
{
    // ── 单例 ─────────────────────────────────────────────────────
    public static UIManager Instance { get; private set; }

    // ── 全局 Canvas 配置 ─────────────────────────────────────────
    [Header("== 全局 Canvas ==")]
    [Tooltip("全局 Canvas sortingOrder 基线（场景 Canvas 建议低于此值）")]
    [SerializeField] private int globalCanvasSortOrder = 1000;

    // ── 全局层引用（DDOL，InitializeGlobalUI 创建） ─────────────
    private RectTransform _globalToastLayer;
    private RectTransform _globalTransitionLayer;

    // ── 当前场景根（由 UISceneRoot 注册 / 注销） ────────────────
    private UISceneRoot _currentSceneRoot;

    // ── 层访问器（子系统统一通过这些属性获取目标层） ─────────────
    /// <summary>场景 HUD 层（数值条、状态灯等，随场景切换）</summary>
    public RectTransform hudLayer =>
        _currentSceneRoot != null ? _currentSceneRoot.hudLayer : null;

    /// <summary>场景 Overlay 层（道具面板、暂停菜单等，随场景切换）</summary>
    public RectTransform overlayLayer =>
        _currentSceneRoot != null ? _currentSceneRoot.overlayLayer : null;

    /// <summary>场景 Modal 层（对话弹窗、确认框，随场景切换）</summary>
    public RectTransform modalLayer =>
        _currentSceneRoot != null ? _currentSceneRoot.modalLayer : null;

    /// <summary>全局 Transition 层（场景过渡，跨场景持久）</summary>
    public RectTransform transitionLayer => _globalTransitionLayer;

    /// <summary>全局 Toast 层（浮动提示，跨场景持久）</summary>
    public RectTransform toastLayer => _globalToastLayer;

    // ── 模态背景 ─────────────────────────────────────────────────
    [Header("== 模态背景 ==")]
    [Tooltip("模态背景目标透明度")]
    [Range(0f, 1f)]
    public float modalBgAlpha = 0.75f;

    [Tooltip("模态背景淡入时间")]
    public float modalBgFadeDuration = 0.3f;

    private Image _modalBackground;

    // ── 子系统引用（在 UIManager GameObject 上作为组件挂载） ─────
    [Header("== 子系统 ==")]
    public TransitionSystem Transition;
    public ModalSystem Modal;
    public ToastSystem Toast;
    public HUDSystem HUD;
    public DialoguePlayer Dialogue;
    public ItemDisplaySystem ItemDisplay;

    // ── 全局 UI 状态 ─────────────────────────────────────────────
    /// <summary>当前是否有模态 UI 打开（可供 Gameplay 查询以禁用输入）</summary>
    public bool IsModalOpen { get; private set; }

    /// <summary>模态打开/关闭事件（true=打开，false=关闭）</summary>
    public event Action<bool> OnModalStateChanged;

    /// <summary>场景根注册事件（场景子系统可监听以执行额外初始化）</summary>
    public event Action<UISceneRoot> OnSceneRootRegistered;

    /// <summary>场景根注销事件</summary>
    public event Action OnSceneRootUnregistered;

    /// <summary>当前是否有场景根注册</summary>
    public bool HasSceneRoot => _currentSceneRoot != null;

    // ══════════════════════════════════════════════════════════════
    //  生命周期
    // ══════════════════════════════════════════════════════════════

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        if (transform.parent == null)
            DontDestroyOnLoad(gameObject);

        // 确保 DOTween 已初始化
        DOTween.Init(false, true, LogBehaviour.ErrorsOnly).SetCapacity(200, 50);
    }

    // ══════════════════════════════════════════════════════════════
    //  全局 UI 初始化（Bootstrapper 调用，仅执行一次）
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// 创建跨场景持久的全局 Canvas（Transition + Toast 层）。
    /// 由 GameBootstrapper 在创建 UIManager 后调用。
    /// </summary>
    public void InitializeGlobalUI()
    {
        // ── 全局 Canvas ──
        var canvasGO = new GameObject("GlobalCanvas");
        canvasGO.transform.SetParent(transform, false);
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = globalCanvasSortOrder;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        canvasGO.AddComponent<GraphicRaycaster>();

        // ── Toast 层（全局，跨场景） ──
        _globalToastLayer = CreateLayer(canvasGO.transform, "ToastLayer",
            globalCanvasSortOrder + 50);

        // ── Transition 层（全局，跨场景） ──
        _globalTransitionLayer = CreateLayer(canvasGO.transform, "TransitionLayer",
            globalCanvasSortOrder + 100);

        // ── 初始化全局子系统（Transition + Toast） ──
        Transition?.Initialize();
        Toast?.Initialize();

        PauseSystem.Initialize(canvasGO.transform);
        InGameMenuController.Initialize(canvasGO.transform);
        AkanaHUDController.Initialize(canvasGO.transform);
        SelectRoleController.Initialize(canvasGO.transform);
        FailEffectController.Initialize(canvasGO.transform);
        InterrogationDialogueUI.Initialize(canvasGO.transform);
        CourtUIController.Initialize(canvasGO.transform);

        // 每次场景加载后，强制把该场景内所有 CanvasScaler 修正为 Expand，
        // 覆盖 Prefab/Scene 文件中可能残留的旧 matchWidthOrHeight 序列化值。
        SceneManager.sceneLoaded += OnSceneLoaded_PatchCanvasScalers;

        Debug.Log("[UIManager] 全局 UI 初始化完成（Transition + Toast + Akana + SelectRole + FailEffect + InterrogationDialogue + Court）。");
    }

    /// <summary>
    /// 场景加载完成时，遍历该场景内所有 CanvasScaler，
    /// 统一修正为 ScaleWithScreenSize + Expand，确保在任意分辨率下等比填满屏幕。
    /// </summary>
    private void OnSceneLoaded_PatchCanvasScalers(Scene scene, LoadSceneMode mode)
    {
        var scalers = UnityEngine.Object.FindObjectsOfType<CanvasScaler>(true);
        foreach (var s in scalers)
        {
            if (s.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize &&
                s.screenMatchMode != CanvasScaler.ScreenMatchMode.Expand)
            {
                s.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
                Debug.Log($"[UIManager] CanvasScaler 已修正为 Expand: {s.gameObject.name}");
            }
        }
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded_PatchCanvasScalers;
    }

    // ══════════════════════════════════════════════════════════════
    //  场景根注册 / 注销（由 UISceneRoot 自动调用）
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// 注册当前场景的 UISceneRoot。由 UISceneRoot.Awake() 自动调用。
    /// 触发场景相关子系统（HUD、Modal、Dialogue、ItemDisplay）的初始化。
    /// </summary>
    public void RegisterSceneRoot(UISceneRoot root)
    {
        if (root == null) return;

        if (_currentSceneRoot != null && _currentSceneRoot != root)
        {
            Debug.LogWarning(
                $"[UIManager] 替换场景根: {_currentSceneRoot.name} → {root.name}");
        }

        _currentSceneRoot = root;

        // 绑定/创建模态背景
        SetupModalBackground();

        // 初始化场景子系统
        HUD?.Initialize();
        Modal?.Initialize();
        Dialogue?.Initialize();
        ItemDisplay?.Initialize();

        OnSceneRootRegistered?.Invoke(root);
        Debug.Log($"[UIManager] 场景根已注册: {root.name}");
    }

    /// <summary>
    /// 注销场景根。由 UISceneRoot.OnDestroy() 自动调用。
    /// </summary>
    public void UnregisterSceneRoot(UISceneRoot root)
    {
        if (_currentSceneRoot != root) return;

        _currentSceneRoot = null;
        _modalBackground = null;

        OnSceneRootUnregistered?.Invoke();
        Debug.Log($"[UIManager] 场景根已注销: {root.name}");
    }

    // ══════════════════════════════════════════════════════════════
    //  模态层控制
    // ══════════════════════════════════════════════════════════════

    /// <summary>显示模态背景</summary>
    public void ShowModalBackground(Action onComplete = null)
    {
        IsModalOpen = true;
        OnModalStateChanged?.Invoke(true);

        if (_modalBackground != null)
        {
            _modalBackground.gameObject.SetActive(true);
            _modalBackground.DOFade(modalBgAlpha, modalBgFadeDuration)
                .SetUpdate(true)
                .OnComplete(() => onComplete?.Invoke());
        }
        else
        {
            onComplete?.Invoke();
        }
    }

    /// <summary>隐藏模态背景</summary>
    public void HideModalBackground(Action onComplete = null)
    {
        if (_modalBackground != null)
        {
            _modalBackground.DOFade(0f, modalBgFadeDuration)
                .SetUpdate(true)
                .OnComplete(() =>
                {
                    _modalBackground.gameObject.SetActive(false);
                    IsModalOpen = false;
                    OnModalStateChanged?.Invoke(false);
                    onComplete?.Invoke();
                });
        }
        else
        {
            IsModalOpen = false;
            OnModalStateChanged?.Invoke(false);
            onComplete?.Invoke();
        }
    }

    // ══════════════════════════════════════════════════════════════
    //  模态背景初始化（场景根注册时调用）
    // ══════════════════════════════════════════════════════════════

    private void SetupModalBackground()
    {
        if (modalLayer == null) return;

        // 优先查找 UISceneRoot Prefab 中已有的 ModalBackground
        var existing = modalLayer.Find("ModalBackground");
        if (existing != null)
        {
            _modalBackground = existing.GetComponent<Image>();
            // ── 关键：不管是 Prefab 预置还是运行时找到的，都强制置底 ──
            // 保证 ModalBackground 永远是 ModalLayer(sortOrder=90) 内的 index-0 节点，
            // fail / SelectRole / 卡牌详情面板等所有弹出层都会渲染在它之上。
            existing.SetAsFirstSibling();
        }
        else
        {
            // 运行时创建默认版本
            var bgGO = new GameObject("ModalBackground");
            bgGO.transform.SetParent(modalLayer, false);
            bgGO.transform.SetAsFirstSibling();
            _modalBackground = bgGO.AddComponent<Image>();
            _modalBackground.color = new Color(0f, 0f, 0f, 0f);
            _modalBackground.raycastTarget = true;
            StretchFull(bgGO.GetComponent<RectTransform>());
        }

        // 初始隐藏
        if (_modalBackground != null)
        {
            var c = _modalBackground.color;
            c.a = 0f;
            _modalBackground.color = c;
            _modalBackground.gameObject.SetActive(false);
        }
    }

    // ══════════════════════════════════════════════════════════════
    //  工具方法（供运行时创建 UI 结构）
    // ══════════════════════════════════════════════════════════════

    private RectTransform CreateLayer(Transform parent, string name, int sortOrder)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rect = go.AddComponent<RectTransform>();
        StretchFull(rect);

        // 用 Canvas 组件控制层排序
        var layerCanvas = go.AddComponent<Canvas>();
        layerCanvas.overrideSorting = true;
        layerCanvas.sortingOrder = sortOrder;
        go.AddComponent<GraphicRaycaster>();

        return rect;
    }

    private static void StretchFull(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
