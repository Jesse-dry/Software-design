using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 主菜单统一控制器。
/// 
/// 职责：
///   - 管理主菜单整体生命周期
///   - 连接开始按钮与 GameManager Phase 流程
///   - 管理退出游戏按钮
///   - 协调眼睛效果和按钮效果组件
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
        SetupButtons();
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
    /// 开始游戏按钮点击
    /// </summary>
    private void OnStartClicked()
    {
        if (_isTransitioning) return;

        Debug.Log("[MainMenuController] 开始游戏");

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
