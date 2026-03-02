using UnityEngine;
using System;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Game State")]
    public GamePhase CurrentPhase { get; private set; } = GamePhase.Boot;

    // 全局事件，用于通知系统及 UI 阶段变化
    public event Action<GamePhase> OnPhaseChanged;

    // 由 Bootstrapper 注入的起始阶段（默认 Abyss）
    private GamePhase _startPhase = GamePhase.Abyss;

    // =========================
    // 配置注入（由 GameBootstrapper 调用）
    // =========================

    /// <summary>
    /// 由 GameBootstrapper 在 Start() 之前注入起始阶段配置。
    /// </summary>
    public void InjectConfig(GamePhase startPhase)
    {
        _startPhase = startPhase;
    }

    private void Awake()
    {
        // ����ģʽ������������Ŀ�ǳ��ʺϣ�
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
        // 使用 Bootstrapper 注入的起始阶段（或 Inspector 默认值）
        EnterPhase(_startPhase);
    }

    /// <summary>
    /// ����Ψһ�Ϸ��Ľ׶��л����
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
    /// �뿪��ǰ�׶�ʱ������
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
    /// �����½׶�ʱ��ʼ��
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

    // === �� UI / ����ϵͳ�õĿ�ݽӿ� ===

    public bool IsInAbyss()
    {
        return CurrentPhase == GamePhase.Abyss;
    }

    public bool IsInCourt()
    {
        return CurrentPhase == GamePhase.Court;
    }
}
