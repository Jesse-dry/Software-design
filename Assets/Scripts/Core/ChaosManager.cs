using System;
using UnityEngine;

public class ChaosManager : MonoBehaviour
{
    // 单例模式，方便全局调用
    public static ChaosManager Instance { get; private set; }

    [Header("混乱值配置")]
    [SerializeField] private int maxChaos = 100;
    private int _currentChaos = 0;

    // 对外暴露的只读属性
    public int MaxChaos => maxChaos;
    public int CurrentChaos => _currentChaos;

    // UI 监听的事件：当混乱值改变时触发 (传递 当前值, 最大值)
    public event Action<int, int> OnChaosChanged;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// 修改混乱值
    /// </summary>
    /// <param name="amount">变化量（正数增加，负数减少）</param>
    /// <param name="reason">变化原因（用于 Debug 日志）</param>
    public void AddChaos(int amount, string reason = "")
    {
        int oldChaos = _currentChaos;
        // 限制数值在 0 到 maxChaos 之间
        _currentChaos = Mathf.Clamp(_currentChaos + amount, 0, maxChaos);

        if (_currentChaos != oldChaos)
        {
            Debug.Log($"[ChaosManager] 混乱值 {(amount > 0 ? "增加" : "减少")} {amount}. 当前: {_currentChaos}/{maxChaos}. 原因: {reason}");

            // 触发事件，通知 HUD 更新
            OnChaosChanged?.Invoke(_currentChaos, maxChaos);

            // 检查是否爆表
            if (_currentChaos >= maxChaos)
            {
                TriggerChaosBreakdown();
            }
        }
    }

    private void TriggerChaosBreakdown()
    {
        Debug.LogWarning("[ChaosManager] 混乱值达到上限！游戏可能进入坏结局。");
        // 这里可以调用 GameManager 切换到失败阶段
        // GameManager.Instance.EnterPhase(GamePhase.Result);
    }
}