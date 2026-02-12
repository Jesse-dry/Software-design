using UnityEngine;
using System;

public class CourtController : MonoBehaviour
{
    public static CourtController Instance { get; private set; }

    [Header("Court State")]
    public CourtState CurrentState { get; private set; }

    [Header("Current Topic")]
    [SerializeField] private string currentTopicId;

    // 事件（给 UI / Ink / 音效 用）
    public event Action<CourtState> OnCourtStateChanged;
    public event Action<string> OnTopicResolved;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        if (GameManager.Instance == null)
        {
            Debug.LogError("GameManager not found! CourtController requires GameManager to function.");
            enabled = false;
            return;
        }
        // 只有在庭审阶段才启动
        if (!GameManager.Instance.IsInCourt())
        {
            enabled = false;
            return;
        }

        EnterState(CourtState.Intro);
    }

    // =========================
    // 状态控制
    // =========================

    public void EnterState(CourtState newState)
    {
        if (CurrentState == newState) return;

        ExitState(CurrentState);
        CurrentState = newState;

        Debug.Log($"[Court] Enter State: {CurrentState}");
        OnCourtStateChanged?.Invoke(CurrentState);

        EnterNewState(CurrentState);
    }

    private void ExitState(CourtState state)
    {
        // 预留：以后清理 UI、停止输入等
    }

    private void EnterNewState(CourtState state)
    {
        switch (state)
        {
            case CourtState.Intro:
                // Ink：开庭陈述
                break;

            case CourtState.Debate:
                // UI：允许提交证据
                break;

            case CourtState.Verdict:
                EvaluateVerdict();
                break;

            case CourtState.End:
                EndCourt();
                break;
        }
    }

    // =========================
    // 对外接口（UI / Ink 调用）
    // =========================

    public void StartDebate()
    {
        if (CurrentState != CourtState.Intro) return;
        EnterState(CourtState.Debate);
    }

    public void SubmitEvidence(string evidenceId)
    {
        if (CurrentState != CourtState.Debate)
        {
            Debug.LogWarning("[Court] Cannot submit evidence now.");
            return;
        }

        DataManager.Instance.SubmitEvidenceToCourt(evidenceId);

        // 每次提交后都检查议题是否解决
        if (DataManager.Instance.IsTopicResolved(currentTopicId))
        {
            OnTopicResolved?.Invoke(currentTopicId);
            EnterState(CourtState.Verdict);
        }
    }

    // =========================
    // 裁决逻辑
    // =========================

    private void EvaluateVerdict()
    {
        bool success = DataManager.Instance.IsTopicResolved(currentTopicId);

        Debug.Log(success
            ? "[Court] Consensus Reached."
            : "[Court] Consensus Failed.");

        // 这里以后可以：
        // - 触发 Ink 不同分支
        // - 播放裁决演出

        EnterState(CourtState.End);
    }

    private void EndCourt()
    {
        Debug.Log("[Court] Court Ended.");

        // 通知 GameManager 进入结果阶段或返回潜渊
        GameManager.Instance.EnterPhase(GamePhase.Result);
    }
}
