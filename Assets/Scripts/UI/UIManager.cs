using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using System;

/// <summary>
/// UI 统一管理器（跨场景持久 — 挂在 [MANAGERS] 下）。
/// 
/// 架构：
///   UIManager 负责管理一个 DontDestroyOnLoad 的 UIRoot Canvas，
///   下辖多个功能层，按渲染顺序从低到高：
///     HUD (10) → Overlay (50) → Modal (90) → Transition (100)
/// 
///   各子系统以组件形式挂在 UIManager 上，通过 Inspector 引用各层 Transform。
///   场景内独有的 UI 仍放在各场景 Canvas 中，不受此管理器控制。
/// 
/// 使用方式：
///   UIManager.Instance.Toast.Show("拾取了记忆碎片");
///   UIManager.Instance.Modal.Open(somePrefab);
///   UIManager.Instance.Transition.FadeIn(1f);
///   UIManager.Instance.HUD.SetChaosValue(0.7f);
/// 
/// 初始化：
///   由 GameBootstrapper 创建并加载 UIRoot Prefab（Resources/Prefabs/UIRoot）。
/// </summary>
public class UIManager : MonoBehaviour
{
    // ── 单例 ─────────────────────────────────────────────────────
    public static UIManager Instance { get; private set; }

    // ── UIRoot Prefab 路径 ───────────────────────────────────────
    private const string UI_ROOT_PREFAB_PATH = "Prefabs/UIRoot";

    // ── 层引用（由 UIRoot Prefab 结构决定，Bootstrapper 创建后赋值） ──
    [Header("== UI 层引用 ==")]
    [Tooltip("最底层：数值条、状态灯、Toast 容器")]
    public RectTransform hudLayer;

    [Tooltip("中间层：道具面板、暂停菜单等可叠加面板")]
    public RectTransform overlayLayer;

    [Tooltip("模态层：黑色背景 + 对话/弹窗（打断游戏交互）")]
    public RectTransform modalLayer;

    [Tooltip("最上层：场景过渡（淡入淡出/Glitch）")]
    public RectTransform transitionLayer;

    // ── 模态背景 ─────────────────────────────────────────────────
    [Header("== 模态背景 ==")]
    [Tooltip("ModalLayer 下的全屏黑色 Image（带 RaycastTarget 阻断输入）")]
    public Image modalBackground;

    [Tooltip("模态背景目标透明度")]
    [Range(0f, 1f)]
    public float modalBgAlpha = 0.75f;

    [Tooltip("模态背景淡入时间")]
    public float modalBgFadeDuration = 0.3f;

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
    //  Bootstrapper 调用：加载 UIRoot Prefab
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// 从 Resources 加载 UIRoot Prefab 并实例化为 UIManager 的子对象。
    /// 如果 Prefab 不存在则创建最小化的运行时 UI 结构。
    /// </summary>
    public void InitializeUIRoot()
    {
        var prefab = Resources.Load<GameObject>(UI_ROOT_PREFAB_PATH);
        if (prefab != null)
        {
            var root = Instantiate(prefab, transform);
            root.name = "UIRoot";
            BindLayersFromRoot(root.transform);
            Debug.Log("[UIManager] UIRoot Prefab 加载成功。");
        }
        else
        {
            Debug.LogWarning(
                "[UIManager] 未找到 UIRoot Prefab，创建运行时最小 UI 结构。\n" +
                $"建议创建 Prefab：Assets/Resources/{UI_ROOT_PREFAB_PATH}.prefab");
            CreateRuntimeUIRoot();
        }

        // 初始隐藏模态背景
        if (modalBackground != null)
        {
            var c = modalBackground.color;
            c.a = 0f;
            modalBackground.color = c;
            modalBackground.gameObject.SetActive(false);
        }

        // 让子系统执行各自初始化
        Transition?.Initialize();
        Modal?.Initialize();
        Toast?.Initialize();
        HUD?.Initialize();
        Dialogue?.Initialize();
        ItemDisplay?.Initialize();
    }

    // ══════════════════════════════════════════════════════════════
    //  模态层控制
    // ══════════════════════════════════════════════════════════════

    /// <summary>显示模态背景</summary>
    public void ShowModalBackground(Action onComplete = null)
    {
        IsModalOpen = true;
        OnModalStateChanged?.Invoke(true);

        if (modalBackground != null)
        {
            modalBackground.gameObject.SetActive(true);
            modalBackground.DOFade(modalBgAlpha, modalBgFadeDuration)
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
        if (modalBackground != null)
        {
            modalBackground.DOFade(0f, modalBgFadeDuration)
                .SetUpdate(true)
                .OnComplete(() =>
                {
                    modalBackground.gameObject.SetActive(false);
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
    //  运行时最小 UI 创建（Prefab 缺失时回退）
    // ══════════════════════════════════════════════════════════════

    private void CreateRuntimeUIRoot()
    {
        // Canvas
        var canvasGO = new GameObject("UIRoot");
        canvasGO.transform.SetParent(transform, false);
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 0;
        canvasGO.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasGO.AddComponent<GraphicRaycaster>();

        // 创建层
        hudLayer       = CreateLayer(canvasGO.transform, "HUDLayer", 10);
        overlayLayer   = CreateLayer(canvasGO.transform, "OverlayLayer", 50);
        modalLayer     = CreateLayer(canvasGO.transform, "ModalLayer", 90);
        transitionLayer = CreateLayer(canvasGO.transform, "TransitionLayer", 100);

        // 模态背景
        var bgGO = new GameObject("ModalBackground");
        bgGO.transform.SetParent(modalLayer, false);
        bgGO.transform.SetAsFirstSibling();
        modalBackground = bgGO.AddComponent<Image>();
        modalBackground.color = new Color(0f, 0f, 0f, 0f);
        modalBackground.raycastTarget = true;
        var bgRect = bgGO.GetComponent<RectTransform>();
        StretchFull(bgRect);
    }

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

    /// <summary>
    /// 从已实例化的 UIRoot Prefab 中按名称绑定各层。
    /// Prefab 结构要求：UIRoot/HUDLayer, UIRoot/OverlayLayer, UIRoot/ModalLayer, UIRoot/TransitionLayer
    /// </summary>
    private void BindLayersFromRoot(Transform root)
    {
        hudLayer        = FindChildRect(root, "HUDLayer");
        overlayLayer    = FindChildRect(root, "OverlayLayer");
        modalLayer      = FindChildRect(root, "ModalLayer");
        transitionLayer = FindChildRect(root, "TransitionLayer");

        // 模态背景
        if (modalLayer != null)
        {
            var bgTrans = modalLayer.Find("ModalBackground");
            if (bgTrans != null)
                modalBackground = bgTrans.GetComponent<Image>();
        }
    }

    private static RectTransform FindChildRect(Transform root, string childName)
    {
        var child = root.Find(childName);
        if (child == null)
        {
            Debug.LogWarning($"[UIManager] UIRoot 中未找到 '{childName}'，请检查 Prefab 结构。");
            return null;
        }
        return child.GetComponent<RectTransform>();
    }
}
