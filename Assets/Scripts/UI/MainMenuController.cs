using UnityEngine;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;

/// <summary>
/// 主菜单统一控制器。
/// 
/// 职责：
///   - 管理主菜单整体生命周期
///   - 连接开始按钮与 GameManager Phase 流程
///   - 管理退出游戏按钮
///   - 协调眼睛效果和按钮效果组件
///   - 引导玩家输入 API Key 和叙事风格
/// 
/// 使用方法：
///   1. 在 MainMenu 场景的根 Canvas / 空物体上挂载此脚本
///   2. 在 Inspector 中拖入按钮引用
///   3. 按钮的具体效果（眼球跟踪、悬停渐显）由各自独立组件控制
/// </summary>
public class MainMenuController : MonoBehaviour
{
    [Header("按钮引用")]
    [Tooltip("开始游戏按钮（可挂载 HoverRevealButton 组件实现悬停效果）")]
    [SerializeField] private Button startButton;

    [Tooltip("退出游戏按钮（可选）")]
    [SerializeField] private Button quitButton;

    [Header("流程配置")]
    [Tooltip("开始游戏后进入的阶段")]
    [SerializeField] private GamePhase targetPhase = GamePhase.Cutscene;

    [Tooltip("是否在进入阶段前播放转场动画")]
    [SerializeField] private bool useTransition = true;

    [Tooltip("转场动画持续时间（秒）")]
    [SerializeField, Range(0.1f, 3f)] private float transitionDuration = 1f;

    private bool _isTransitioning = false;

    private void Start()
    {
        EnsureUISceneRoot();
        SetupButtons();
    }

    // ══════════════════════════════════════════════════════════════
    //  运行时保证 UISceneRoot 存在（MainMenu 场景可能没有预制的 UISceneRoot）
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// 检查当前场景是否已有 UISceneRoot；如果没有，则动态创建一个最小版本，
    /// 确保 ModalSystem 等子系统可以正常工作（modalLayer 不为 null）。
    /// </summary>
    private void EnsureUISceneRoot()
    {
        if (UIManager.Instance != null && UIManager.Instance.HasSceneRoot)
            return;

        Debug.Log("[MainMenuController] 未检测到 UISceneRoot，动态创建...");

        // ── 根 Canvas ──
        var rootGO = new GameObject("UIRoot_MainMenu(Runtime)");
        var canvas = rootGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 0;
        var scaler = rootGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        rootGO.AddComponent<GraphicRaycaster>();

        // ── 标准三层 ──
        var hudLayer     = CreateUILayer(rootGO.transform, "HUDLayer",     10);
        var overlayLayer = CreateUILayer(rootGO.transform, "OverlayLayer", 50);
        var modalLayer   = CreateUILayer(rootGO.transform, "ModalLayer",   90);

        // ── UISceneRoot 组件 ──
        var sceneRoot = rootGO.AddComponent<UISceneRoot>();
        sceneRoot.hudLayer     = hudLayer;
        sceneRoot.overlayLayer = overlayLayer;
        sceneRoot.modalLayer   = modalLayer;
        // UISceneRoot.Awake() 会自动向 UIManager 注册

        Debug.Log("[MainMenuController] 动态 UISceneRoot 创建完成。");
    }

    /// <summary>创建一个带 Canvas + GraphicRaycaster 的 UI 层</summary>
    private static RectTransform CreateUILayer(Transform parent, string name, int sortOrder)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        var layerCanvas = go.AddComponent<Canvas>();
        layerCanvas.overrideSorting = true;
        layerCanvas.sortingOrder = sortOrder;
        go.AddComponent<GraphicRaycaster>();

        return rect;
    }

    private void SetupButtons()
    {
        // 绑定开始按钮
        if (startButton != null)
        {
            startButton.onClick.AddListener(OnStartClicked);
        }
        else
        {
            // 尝试从 HoverRevealButton 获取按钮引用
            var hoverBtn = GetComponentInChildren<HoverRevealButton>();
            if (hoverBtn != null)
            {
                startButton = hoverBtn.GetButton();
                startButton?.onClick.AddListener(OnStartClicked);
            }
            else
            {
                Debug.LogWarning("[MainMenuController] 未找到开始按钮引用！");
            }
        }

        // 绑定退出按钮
        if (quitButton != null)
        {
            quitButton.onClick.AddListener(OnQuitClicked);
        }
    }

    /// <summary>
    /// 开始游戏按钮点击 → 先检查环境变量，再决定是否弹出 LLM 输入提示。
    /// </summary>
    private void OnStartClicked()
    {
        if (_isTransitioning) return;

        Debug.Log("[MainMenuController] 开始游戏");

        // 优先从环境变量静默启用，无需打扰玩家
        if (LLMBridge.TryInitializeFromEnvironment())
        {
            // 环境变量自动启用后，仍询问玩家风格偏好
            ShowStyleInput(() =>
            {
                LLMBridge.GenerateCardTextsInBackground().Forget();
                UIManager.Instance?.Toast?.Show(
                    $"LLM 模式已启用（{LLMBridge.KeySource}）",
                    colorType: ToastColor.Positive);
                ProceedToGame();
            });
            return;
        }

        // 环境变量未配置 → 弹出 UI 询问玩家
        ShowLLMPrompt();
    }

    // ══════════════════════════════════════════════════════════════
    //  LLM 接入提示流程
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// 弹出确认框：是否输入 API Key 启用大模型增强叙事。
    /// </summary>
    private void ShowLLMPrompt()
    {
        var modal = UIManager.Instance?.Modal;
        if (modal == null)
        {
            Debug.LogWarning("[MainMenuController] ModalSystem 不可用，跳过 LLM 提示。");
            ProceedToGame();
            return;
        }

        modal.ShowConfirm(
            "本游戏支持大模型增强叙事体验。\n是否输入 API Key 启用？",
            onYes: ShowApiKeyInput,
            onNo: () =>
            {
                Debug.Log("[MainMenuController] 玩家选择不使用 LLM 模式。");
                ProceedToGame();
            }
        );
    }

    /// <summary>
    /// 弹出输入框让玩家输入 API Key。
    /// </summary>
    private void ShowApiKeyInput()
    {
        var modal = UIManager.Instance?.Modal;
        if (modal == null)
        {
            ProceedToGame();
            return;
        }

        modal.ShowInput(
            "请输入 API Key（DeepSeek 等兼容 OpenAI 格式的 API）：",
            "sk-xxxxxxxx...",
            onConfirm: (apiKey) =>
            {
                if (!string.IsNullOrWhiteSpace(apiKey))
                {
                    LLMBridge.Initialize(apiKey);
                    if (LLMBridge.IsEnabled)
                    {
                        // API Key 有效 → 继续询问风格
                        ShowStyleInput(() =>
                        {
                            LLMBridge.GenerateCardTextsInBackground().Forget();
                            UIManager.Instance?.Toast?.Show(
                                "LLM 模式已启用，阿卡那牌文本将由大模型生成",
                                colorType: ToastColor.Positive);
                            ProceedToGame();
                        });
                        return; // 等待风格输入完成后再 ProceedToGame
                    }
                    else
                    {
                        UIManager.Instance?.Toast?.Show(
                            "API Key 初始化失败，将使用默认文本",
                            colorType: ToastColor.Warning);
                    }
                }
                ProceedToGame();
            },
            onCancel: () =>
            {
                Debug.Log("[MainMenuController] 玩家取消输入 API Key。");
                ProceedToGame();
            }
        );
    }

    // ══════════════════════════════════════════════════════════════
    //  风格输入流程
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// 弹出输入框让玩家自定义叙事风格。
    /// 留空则使用默认风格"暗黑叙事，简洁有力"。
    /// 
    /// PromptBuilder 会根据 Style 参数调整 LLM 生成文本的语气和表达方式，
    /// 风格只影响表达方式，不影响事实与立场。
    /// </summary>
    /// <param name="onComplete">风格确认后的回调</param>
    private void ShowStyleInput(System.Action onComplete)
    {
        var modal = UIManager.Instance?.Modal;
        if (modal == null)
        {
            onComplete?.Invoke();
            return;
        }

        modal.ShowInput(
            "请输入你偏好的叙事风格（影响文本语气与表达，不影响游戏事实）：\n" +
            "留空将使用默认风格：\"暗黑叙事，简洁有力\"",
            "例如：哥特式悬疑、赛博朋克冷硬、诗意抒情...",
            onConfirm: (style) =>
            {
                if (!string.IsNullOrWhiteSpace(style))
                {
                    LLMBridge.Style = style.Trim();
                    UIManager.Instance?.Toast?.Show(
                        $"叙事风格已设为：{LLMBridge.Style}",
                        colorType: ToastColor.Positive);
                    Debug.Log($"[MainMenuController] 玩家设置叙事风格: {LLMBridge.Style}");
                }
                else
                {
                    Debug.Log("[MainMenuController] 玩家未输入风格，使用默认值。");
                }
                onComplete?.Invoke();
            },
            onCancel: () =>
            {
                Debug.Log("[MainMenuController] 玩家跳过风格设置。");
                onComplete?.Invoke();
            }
        );
    }

    /// <summary>
    /// LLM 提示结束后，继续原有的开始游戏流程。
    /// </summary>
    private void ProceedToGame()
    {
        if (useTransition)
        {
            StartGameWithTransition();
        }
        else
        {
            EnterTargetPhase();
        }
    }

    /// <summary>
    /// 带转场动画的开始流程
    /// </summary>
    private void StartGameWithTransition()
    {
        _isTransitioning = true;

        // 禁用按钮交互，防止重复点击
        if (startButton != null) startButton.interactable = false;
        if (quitButton != null) quitButton.interactable = false;

        // 尝试使用 TransitionSystem 播放转场
        var transitionSystem = FindObjectOfType<TransitionSystem>();
        if (transitionSystem != null)
        {
            transitionSystem.FadeOut(transitionDuration, () =>
            {
                EnterTargetPhase();
            });
        }
        else
        {
            // 没有转场系统，直接进入
            Debug.LogWarning("[MainMenuController] 未找到 TransitionSystem，直接进入目标阶段。");
            EnterTargetPhase();
        }
    }

    /// <summary>
    /// 通过 GameManager 进入目标阶段
    /// </summary>
    private void EnterTargetPhase()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.EnterPhase(targetPhase);
        }
        else
        {
            Debug.LogError("[MainMenuController] GameManager 实例不存在！无法切换阶段。");
            _isTransitioning = false;
        }
    }

    /// <summary>
    /// 退出游戏
    /// </summary>
    private void OnQuitClicked()
    {
        Debug.Log("[MainMenuController] 退出游戏");

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void OnDestroy()
    {
        // 清理监听，避免内存泄漏
        if (startButton != null)
            startButton.onClick.RemoveListener(OnStartClicked);

        if (quitButton != null)
            quitButton.onClick.RemoveListener(OnQuitClicked);
    }
}
