using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

/// <summary>
/// 玩家交互检测（Memory 场景）。
///
/// 【职责】
///   1. Trigger2D 检测附近的 MemoryNodeBase 节点
///   2. 按 E/F 键 → 调用 currentNode.Interact()
///   3. 维护交互状态（Free / Interacting），阻止弹窗期间重复交互
///
/// 【设计原则】
///   - 交互输入只在此脚本检测，节点脚本不做键盘轮询
///   - 弹窗关闭的键盘操作由 ModalSystem 内部处理
///   - 节点销毁时必须调用 NotifyNodeDestroyed() 清理引用
/// </summary>
public class PlayerInteraction : MonoBehaviour
{
    // ── 交互状态 ─────────────────────────────────────────────────
    public enum State { Free, Interacting }

    /// <summary>当前状态。Interacting 期间不响应新的交互请求。</summary>
    public State CurrentState { get; private set; } = State.Free;

    /// <summary>当前范围内可交互的节点</summary>
    public MemoryNodeBase CurrentNode => _currentNode;

    private MemoryNodeBase _currentNode;

    // 重叠碰撞对计数（同一 Rigidbody 多 Collider 场景）
    private readonly Dictionary<MemoryNodeBase, int> _overlapCounts = new();

    // 帧级防重复
    private int _lastInteractFrame = -1;

    // ══════════════════════════════════════════════════════════════
    //  Update — 仅在 Free 状态处理交互输入
    // ══════════════════════════════════════════════════════════════

    private void Update()
    {
        if (CurrentState != State.Free) return;
        if (_currentNode == null) return;

        if (IsInteractPressed())
        {
            if (Time.frameCount == _lastInteractFrame) return;
            _lastInteractFrame = Time.frameCount;
            _currentNode.Interact();
        }
    }

    /// <summary>检测交互键（E / F），纯键盘轮询，不依赖 InputAction</summary>
    private static bool IsInteractPressed()
    {
        var kb = Keyboard.current;
        if (kb != null)
            return kb.eKey.wasPressedThisFrame || kb.fKey.wasPressedThisFrame;
        return Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.F);
    }

    // ══════════════════════════════════════════════════════════════
    //  状态管理（供节点脚本调用）
    // ══════════════════════════════════════════════════════════════

    /// <summary>进入交互状态（弹窗打开期间调用）</summary>
    public void EnterInteracting() => CurrentState = State.Interacting;

    /// <summary>返回自由状态（弹窗关闭后调用）</summary>
    public void ReturnToFree() => CurrentState = State.Free;

    /// <summary>
    /// 节点即将销毁时调用，清理所有引用。
    /// </summary>
    public void NotifyNodeDestroyed(MemoryNodeBase node)
    {
        _overlapCounts.Remove(node);
        if (_currentNode == node)
            _currentNode = null;
    }

    // ══════════════════════════════════════════════════════════════
    //  Trigger2D 检测
    // ══════════════════════════════════════════════════════════════

    private void OnTriggerEnter2D(Collider2D other)
    {
        var node = FindNode(other);
        if (node == null) return;

        _overlapCounts.TryGetValue(node, out int count);
        _overlapCounts[node] = count + 1;

        if (count == 0)
        {
            if (_currentNode != null && _currentNode != node)
            {
                _currentNode.OnPlayerExit(gameObject);
                _overlapCounts.Remove(_currentNode);
            }
            _currentNode = node;
            _currentNode.OnPlayerEnter(gameObject);
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        var node = FindNode(other);
        if (node == null) return;
        if (!_overlapCounts.ContainsKey(node)) return;

        _overlapCounts[node]--;

        if (_overlapCounts[node] <= 0)
        {
            _overlapCounts.Remove(node);
            if (_currentNode == node)
            {
                _currentNode.OnPlayerExit(gameObject);
                _currentNode = null;
            }
        }
    }

    private static MemoryNodeBase FindNode(Collider2D col)
    {
        var node = col.GetComponent<MemoryNodeBase>();
        return node != null ? node : col.GetComponentInParent<MemoryNodeBase>();
    }
}
