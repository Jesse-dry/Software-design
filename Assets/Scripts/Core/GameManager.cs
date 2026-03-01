using UnityEngine;
using System;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Game State")]
    public GamePhase CurrentPhase { get; private set; } = GamePhase.Boot;

    // 全局事件：其他系统可以监听阶段变化
    public event Action<GamePhase> OnPhaseChanged;

    private void Awake()
    {
        // 单例模式（你们这种项目非常适合）
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        // 游戏启动后的初始阶段
        EnterPhase(GamePhase.Abyss);
    }

    /// <summary>
    /// 对外唯一合法的阶段切换入口
    /// </summary>
    public void EnterPhase(GamePhase newPhase)
    {
        if (CurrentPhase == newPhase)
            return;

        ExitCurrentPhase();

        CurrentPhase = newPhase;
        Debug.Log($"[GameManager] Enter Phase: {CurrentPhase}");

        OnPhaseChanged?.Invoke(CurrentPhase);

        EnterNewPhase();
    }

    /// <summary>
    /// 离开当前阶段时做清理
    /// </summary>
    private void ExitCurrentPhase()
    {
        switch (CurrentPhase)
        {
            case GamePhase.Abyss:
                Debug.Log("[GameManager] Exit Abyss");
                break;

            case GamePhase.Court:
                Debug.Log("[GameManager] Exit Court");
                break;
        }
    }

    /// <summary>
    /// 进入新阶段时初始化
    /// </summary>
    private void EnterNewPhase()
    {
        switch (CurrentPhase)
        {
            case GamePhase.Abyss:
                SceneController.Instance.LoadAbyss();
                break;

            case GamePhase.Court:
                SceneController.Instance.LoadCourt();
                break;

            case GamePhase.Result:
                Debug.Log("[GameManager] Enter Result Phase");
                break;
        }
    }

    // === 给 UI / 其他系统用的快捷接口 ===

    public bool IsInAbyss()
    {
        return CurrentPhase == GamePhase.Abyss;
    }

    public bool IsInCourt()
    {
        return CurrentPhase == GamePhase.Court;
    }
}
