using UnityEngine;
using System;

/// <summary>
/// 游戏主管理器，负责阶段状态机管理。
/// 
/// 游戏流程：
///   Boot → MainMenu → Cutscene → Memory → Abyss → Court → Result
/// 
/// 团队开发说明：
///   - 新增阶段：在 GamePhase 枚举添加，在 EnterNewPhase() 添加处理
///   - 阶段切换事件：订阅 OnPhaseChanged 监听阶段变化
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("当前阶段")]
    public GamePhase CurrentPhase { get; private set; } = GamePhase.Boot;

    // 全局事件，用于通知系统及 UI 阶段变化
    public event Action<GamePhase> OnPhaseChanged;

    // 由 Bootstrapper 注入的起始阶段
    private GamePhase _startPhase = GamePhase.Boot;

    // =========================
    // 生命周期
    // =========================

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        // 注意：如果由 GameBootstrapper 创建，父对象 [MANAGERS] 已标记 DontDestroyOnLoad，
        // 子对象自动继承，无需重复调用。仅在独立放置到场景时才需要。
        if (transform.parent == null)
            DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        // 首次进入：直接设置阶段并执行进入逻辑（跳过 EnterPhase 的相同阶段守卫，
        // 因为 CurrentPhase 初始值就是 Boot，调用 EnterPhase(Boot) 会被跳过）
        GamePhase target = _startPhase;
        CurrentPhase = target;
        Debug.Log($"[GameManager] 初始进入阶段: {CurrentPhase}");
        OnPhaseChanged?.Invoke(CurrentPhase);
        EnterNewPhase();
    }

    // =========================
    // 配置注入
    // =========================

    /// <summary>
    /// 由 GameBootstrapper 在 Start() 之前注入起始阶段配置。
    /// </summary>
    public void InjectConfig(GamePhase startPhase)
    {
        _startPhase = startPhase;
    }

    // =========================
    // 阶段切换
    // =========================

    /// <summary>
    /// 重新加载当前阶段（退出 → 再进入，用于失败重置）。
    /// </summary>
    public void ReloadCurrentPhase()
    {
        Debug.Log($"[GameManager] 重载当前阶段: {CurrentPhase}");
        ExitCurrentPhase();
        EnterNewPhase();
    }

    /// <summary>
    /// 进入指定阶段（唯一合法的阶段切换入口）
    /// </summary>
    public void EnterPhase(GamePhase newPhase)
    {
        if (CurrentPhase == newPhase)
            return;

        ExitCurrentPhase();

        CurrentPhase = newPhase;
        Debug.Log($"[GameManager] 进入阶段: {CurrentPhase}");

        OnPhaseChanged?.Invoke(CurrentPhase);

        EnterNewPhase();
    }

    /// <summary>
    /// 离开当前阶段时的清理
    /// </summary>
    private void ExitCurrentPhase()
    {
        switch (CurrentPhase)
        {
            case GamePhase.Memory:
                Debug.Log("[GameManager] 离开 Memory 阶段");
                break;

            case GamePhase.Abyss:
                Debug.Log("[GameManager] 离开 Abyss 阶段");
                break;

            case GamePhase.Corridor:
                Debug.Log("[GameManager] 离开 Corridor 阶段");
                break;

            case GamePhase.PipeRoom:
                Debug.Log("[GameManager] 离开 PipeRoom 阶段");
                break;

            case GamePhase.PipePuzzle:
                Debug.Log("[GameManager] 离开 PipePuzzle 阶段");
                break;

            case GamePhase.ServerRoom:
                Debug.Log("[GameManager] 离开 ServerRoom 阶段");
                break;

            case GamePhase.DecodeGame:
                Debug.Log("[GameManager] 离开 DecodeGame 阶段");
                break;

            case GamePhase.Court:
                Debug.Log("[GameManager] 离开 Court 阶段");
                break;
            
        }
    }

    /// <summary>
    /// 进入新阶段时的初始化
    /// </summary>
    private void EnterNewPhase()
    {
        switch (CurrentPhase)
        {
            case GamePhase.Boot:
                // Boot 场景只用于初始化，直接进入主菜单
             //   EnterPhase(GamePhase.MainMenu);
                break;

            case GamePhase.MainMenu:
                SceneController.Instance?.LoadMainMenu();
                break;

            case GamePhase.Cutscene:
                SceneController.Instance?.LoadCutscene();
                break;

            case GamePhase.Memory:
                SceneController.Instance?.LoadMemory();
                break;

            case GamePhase.Abyss:
                SceneController.Instance?.LoadAbyss();
                break;

            case GamePhase.Corridor:
                SceneController.Instance?.LoadCorridor();
                break;

            case GamePhase.PipeRoom:
                SceneController.Instance?.LoadPipeRoom();
                break;

            case GamePhase.PipePuzzle:
                SceneController.Instance?.LoadPipePuzzle();
                break;

            case GamePhase.ServerRoom:
                SceneController.Instance?.LoadServerRoom();
                break;

            case GamePhase.DecodeGame:
                SceneController.Instance?.LoadDecodeGame();
                break;

            case GamePhase.Court:
                SceneController.Instance?.LoadCourt();
                break;

            case GamePhase.Result:
                Debug.Log("[GameManager] 进入结局阶段");
                // TODO: 显示结局画面或返回主菜单
                break;
        }
    }

    // =========================
    // 查询接口
    // =========================

    public bool IsInMainMenu()
    {
        return CurrentPhase == GamePhase.MainMenu;
    }

    public bool IsInMemory()
    {
        return CurrentPhase == GamePhase.Memory;
    }

    public bool IsInAbyss()
    {
        return CurrentPhase == GamePhase.Abyss;
    }

    public bool IsInCourt()
    {
        return CurrentPhase == GamePhase.Court;
    }

    public bool IsInCorridor()
    {
        return CurrentPhase == GamePhase.Corridor;
    }

    public bool IsInPipeRoom()
    {
        return CurrentPhase == GamePhase.PipeRoom;
    }

    public bool IsInPipePuzzle()
    {
        return CurrentPhase == GamePhase.PipePuzzle;
    }

    public bool IsInServerRoom()
    {
        return CurrentPhase == GamePhase.ServerRoom;
    }

    public bool IsInDecodeGame()
    {
        return CurrentPhase == GamePhase.DecodeGame;
    }
}
