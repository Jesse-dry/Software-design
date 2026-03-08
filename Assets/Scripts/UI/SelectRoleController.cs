using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

/// <summary>
/// 角色选择面板控制器（全局持久，跨场景自动注入）。
///
/// ══ 架构说明 ══
///   复用 InGameMenuController / AkanaHUDController 的注入模式：
///   ┌──────── [MANAGERS] (DontDestroyOnLoad) ─────────────────────────┐
///   │  UIManager.InitializeGlobalUI()                                 │
///   │    └─ SelectRoleController.Initialize()  ← 创建一次，全局持久   │
///   └──────────────────────────────────────────────────────────────────┘
///
///   仅在 Corridor 场景中生效：
///   1. 在 ModalLayer 查找 "SelectRole" 面板 → 初始隐藏
///   2. 绑定 3 个角色按钮：
///      维修工   → PipeRoom    （水管维修路线）
///      技术人员 → ServerRoom  （服务器路线）
///      原皮     → DecodeGame  （解码路线）
///   3. 绑定 "continue" 按钮关闭面板
///
/// ══ UI 层级关系 ══
///   ModalLayer(90): SelectRole 面板 + ModalBackground 遮罩覆盖一切
///
/// ══ 场景衔接逻辑 ══
///   走廊尽头 → CorridorEndTrigger 触发 → 收集权杖卡 → 显示 SelectRole
///   PipeRoom / ServerRoom 完成 → SelectRoleController.ReturnToCorridorSelectRole()
///     → 自动设置标记并切换至 Corridor → 加载后立即弹出 SelectRole
///
/// ══ 不需要手动挂载。由 Bootstrapper 自动创建。══
/// </summary>
public class SelectRoleController : MonoBehaviour
{
    // ══════════════════════════════════════════════════════════════
    //  单例 + 初始化
    // ══════════════════════════════════════════════════════════════

    public static SelectRoleController Instance { get; private set; }

    /// <summary>
    /// 由 UIManager.InitializeGlobalUI() 调用，仅执行一次。
    /// </summary>
    public static SelectRoleController Initialize(Transform parent)
    {
        if (Instance != null) return Instance;

        var go = new GameObject("SelectRoleController_Logic");
        go.transform.SetParent(parent, false);
        Instance = go.AddComponent<SelectRoleController>();

        if (UIManager.Instance != null)
        {
            UIManager.Instance.OnSceneRootRegistered += Instance.OnSceneLoaded;

            // 防漏补丁
            if (UIManager.Instance.HasSceneRoot && UIManager.Instance.modalLayer != null)
            {
                var existingRoot = UIManager.Instance.modalLayer
                    .GetComponentInParent<UISceneRoot>();
                if (existingRoot != null)
                {
                    Debug.Log("[SelectRole] 检测到场景已存在，立即补发加载！");
                    Instance.OnSceneLoaded(existingRoot);
                }
            }
        }

        return Instance;
    }

    // ══════════════════════════════════════════════════════════════
    //  配置常量（与 UIRoot_Corridor.prefab 中 GameObject 名称对应）
    // ══════════════════════════════════════════════════════════════

    private const string PANEL_NAME  = "SelectRole";
    private const string BTN_REPAIR  = "维修工";    // → PipeRoom
    private const string BTN_TECH    = "技术人员";  // → ServerRoom
    private const string BTN_DECODE  = "原皮";      // → DecodeGame
    private const string BTN_CLOSE   = "continue";  // 隐藏，不再提供关闭

    // ══════════════════════════════════════════════════════════════
    //  动画参数
    // ══════════════════════════════════════════════════════════════

    private const float FADE_DURATION = 0.35f;
    private readonly Ease _showEase = Ease.OutQuad;
    private readonly Ease _hideEase = Ease.InQuad;

    // ══════════════════════════════════════════════════════════════
    //  运行时引用（每次 Corridor 加载重新赋值）
    // ══════════════════════════════════════════════════════════════

    private GameObject   _panel;
    private CanvasGroup  _panelCG;
    private Button       _btnRepair;
    private Button       _btnTech;
    private Button       _btnDecode;
    private bool         _isShowing = false;

    /// <summary>
    /// 下次加载 Corridor 场景时自动弹出 SelectRole 面板。
    /// 由 ReturnToCorridorSelectRole() 设置。
    /// </summary>
    public static bool AutoShowOnNextLoad { get; set; } = false;

    // ══════════════════════════════════════════════════════════════
    //  场景加载回调
    // ══════════════════════════════════════════════════════════════

    private void OnSceneLoaded(UISceneRoot root)
    {
        Debug.Log($"[SelectRole] 监听到新场景加载: {(root != null ? root.name : "null")}");

        // 清理旧绑定
        Cleanup();

        if (root == null) return;

        // 仅在 Corridor 场景中绑定 SelectRole 面板
        if (GameManager.Instance == null || !GameManager.Instance.IsInCorridor())
            return;

        if (root.modalLayer == null) return;

        var panelT = FindInChildren(root.modalLayer, PANEL_NAME);
        if (panelT == null)
        {
            Debug.LogWarning("[SelectRole] Corridor ModalLayer 中未找到 'SelectRole' 面板！");
            return;
        }

        _panel = panelT.gameObject;

        // 确保有 CanvasGroup（用于淡入淡出）
        _panelCG = _panel.GetComponent<CanvasGroup>();
        if (_panelCG == null) _panelCG = _panel.AddComponent<CanvasGroup>();

        // 绑定按钮
        _btnRepair = FindButton(_panel.transform, BTN_REPAIR);
        _btnTech   = FindButton(_panel.transform, BTN_TECH);
        _btnDecode = FindButton(_panel.transform, BTN_DECODE);

        if (_btnRepair != null) _btnRepair.onClick.AddListener(() => OnRoleChosen(GamePhase.PipeRoom));
        if (_btnTech   != null) _btnTech.onClick.AddListener(()   => OnRoleChosen(GamePhase.ServerRoom));
        if (_btnDecode != null) _btnDecode.onClick.AddListener(() => OnRoleChosen(GamePhase.DecodeGame));

        // 隐藏 "continue" 关闭按鈕（选角色面板不提供关闭功肃）
        var closeBtn = FindButton(_panel.transform, BTN_CLOSE);
        if (closeBtn != null) closeBtn.gameObject.SetActive(false);

        // 初始隐藏
        _panel.SetActive(false);
        _panelCG.alpha = 0f;
        _panelCG.interactable = false;
        _panelCG.blocksRaycasts = false;
        _isShowing = false;

        Debug.Log("[SelectRole] SelectRole 面板已绑定（维修工 / 技术人员 / 原皮）。");

        // 如果是从子任务返回（AutoShowOnNextLoad 标记），自动弹出
        if (AutoShowOnNextLoad)
        {
            AutoShowOnNextLoad = false;
            // 延迟 0.5s：等待所有 OnSceneRootRegistered 订阅者（AkanaHUD、InGameMenu 等）
            // 完成各自的初始化，再置顶弹出 SelectRole，避免被其他系统的节点操作压住。
            DOVirtual.DelayedCall(0.5f, ShowSelectRole).SetUpdate(true);
        }
    }

    // ══════════════════════════════════════════════════════════════
    //  公开 API
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// 显示角色选择面板。
    /// 由走廊尽头触发器 CorridorEndTrigger 或自动返回逻辑调用。
    /// </summary>
    public void ShowSelectRole()
    {
        if (_panel == null)
        {
            Debug.LogWarning("[SelectRole] ShowSelectRole() 调用失败：_panel 未绑定（SelectRole 面板未在 ModalLayer 中找到）。");
            return;
        }
        if (_isShowing) return;

        _isShowing = true;

        // ── 强制置顶：保证在 ModalLayer 内所有兄弟（ModalBg、fail、akana 详情等）之上 ──
        _panel.transform.SetAsLastSibling();

        _panel.SetActive(true);
        _panelCG.alpha = 0f;
        _panelCG.interactable = false;
        _panelCG.blocksRaycasts = true;

        // 淡入
        _panelCG.DOFade(1f, FADE_DURATION)
            .SetEase(_showEase)
            .SetUpdate(true)
            .OnComplete(() => _panelCG.interactable = true);

        // 显示模态背景（ModalBackground 在 SetupModalBackground 中已 SetAsFirstSibling，永远在底部）
        UIManager.Instance?.ShowModalBackground();

        Debug.Log("[SelectRole] 显示角色选择面板（已置顶）。");
    }

    /// <summary>
    /// 隐藏角色选择面板。
    /// </summary>
    public void HideSelectRole()
    {
        if (_panel == null || !_isShowing) return;

        _panelCG.interactable = false;

        // 淡出
        _panelCG.DOFade(0f, FADE_DURATION)
            .SetEase(_hideEase)
            .SetUpdate(true)
            .OnComplete(() =>
            {
                _panel.SetActive(false);
                _panelCG.blocksRaycasts = false;
                _isShowing = false;
            });

        // 隐藏模态背景
        UIManager.Instance?.HideModalBackground();
    }

    /// <summary>
    /// 子任务（PipeRoom / ServerRoom / PipePuzzle）完成后调用。
    /// 设置标记 → 切换到 Corridor → 加载后自动弹出 SelectRole。
    /// </summary>
    public static void ReturnToCorridorSelectRole()
    {
        AutoShowOnNextLoad = true;
        Debug.Log("[SelectRole] 设置自动弹出标记，返回 Corridor SelectRole。");
        GameManager.Instance?.EnterPhase(GamePhase.Corridor);
    }

    /// <summary>当前是否正在显示</summary>
    public bool IsShowing => _isShowing;

    // ══════════════════════════════════════════════════════════════
    //  内部逻辑
    // ══════════════════════════════════════════════════════════════

    private void OnRoleChosen(GamePhase target)
    {
        Debug.Log($"[SelectRole] 选择路线 → {target}");

        _panelCG.interactable = false;

        // 淡出面板 → 跳转
        _panelCG.DOFade(0f, FADE_DURATION)
            .SetEase(_hideEase)
            .SetUpdate(true)
            .OnComplete(() =>
            {
                _panel.SetActive(false);
                _panelCG.blocksRaycasts = false;
                _isShowing = false;

                UIManager.Instance?.HideModalBackground();

                // 通过状态机跳转目标场景
                GameManager.Instance?.EnterPhase(target);
            });
    }

    private void Cleanup()
    {
        if (_btnRepair != null) _btnRepair.onClick.RemoveAllListeners();
        if (_btnTech   != null) _btnTech.onClick.RemoveAllListeners();
        if (_btnDecode != null) _btnDecode.onClick.RemoveAllListeners();

        _panel   = null;
        _panelCG = null;
        _btnRepair = _btnTech = _btnDecode = null;
        _isShowing = false;
    }

    // ══════════════════════════════════════════════════════════════
    //  工具方法
    // ══════════════════════════════════════════════════════════════

    private static Transform FindInChildren(Transform parent, string name)
    {
        if (parent.name == name) return parent;
        for (int i = 0; i < parent.childCount; i++)
        {
            var result = FindInChildren(parent.GetChild(i), name);
            if (result != null) return result;
        }
        return null;
    }

    private static Button FindButton(Transform parent, string name)
    {
        var t = FindInChildren(parent, name);
        return t != null ? t.GetComponent<Button>() : null;
    }
}
