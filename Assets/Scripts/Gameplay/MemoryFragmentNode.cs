using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 记忆碎片节点（Memory 场景）。
///
/// 【交互流程】
///   玩家靠近 → ShowPrompt("按 E 交互")
///   按 E     → Interact() → 冻结玩家 → 打开模态弹窗
///   弹窗关闭 → OnClosed() → 收集碎片 → Toast → 销毁自身
///
/// 【设计原则】
///   - 无 Update()，不做键盘轮询
///   - 弹窗关闭由 ModalSystem 内部键盘处理 + 按钮点击双路径触发
///   - 所有状态恢复（Unfreeze、ReturnToFree）在 OnClosed 中同步完成
///   - 销毁前通知 PlayerInteraction 清理引用
/// </summary>
public class MemoryFragmentNode : MemoryNodeBase
{
    private static readonly HashSet<string> s_collectedFragmentIds = new();

    [Header("碎片内容")]
    [TextArea(3, 6)]
    public string fragmentTitle = "记忆碎片";

    [TextArea(3, 10)]
    public string fragmentBody = "一段模糊的记忆浮上心头……";

    [Header("== 交互文字 ==")]
    [Tooltip("玩家靠近时显示的交互提示文字（如 '按 E 交互'）")]
    public string interactPromptText = "按 E 交互";

    [Tooltip("弹窗关闭按钮文字")]
    public string modalCloseButtonText = "关闭";

    [Tooltip("提示文字偏移（相对自身 transform.position）")]
    public Vector3 promptOffset = new Vector3(0, 1.5f, 0);

    [Tooltip("碎片唯一 ID（用于记忆场景内的已收集隐藏）")]
    public string fragmentId = "fragment_1";

    // ── 状态 ─────────────────────────────────────────────────────
    private bool _collected;
    private PlayerMovement _playerMovement;
    private PlayerInteraction _playerInteraction;

    public static bool IsCollected(string id)
    {
        return !string.IsNullOrEmpty(id) && s_collectedFragmentIds.Contains(id);
    }

    public void SetFragmentId(string id)
    {
        if (!string.IsNullOrEmpty(id))
            fragmentId = id;
    }

    // ══════════════════════════════════════════════════════════════
    //  交互（由 PlayerInteraction.DoInteract 调用）
    // ══════════════════════════════════════════════════════════════

    public override void Interact()
    {
        if (_collected || IsCollected(fragmentId)) return;

        // 冻结玩家 + 锁定交互状态
        _playerMovement ??= FindAnyObjectByType<PlayerMovement>();
        _playerInteraction ??= FindAnyObjectByType<PlayerInteraction>();

        _playerMovement?.Freeze();
        _playerInteraction?.EnterInteracting();
        HidePrompt();

        // 打开弹窗
        var modal = UIManager.Instance?.Modal;
        if (modal != null)
        {
            modal.ShowText(fragmentTitle, fragmentBody, modalCloseButtonText, OnClosed);
        }
        else
        {
            Debug.LogWarning("[MemoryFragmentNode] ModalSystem 不可用，直接收集碎片。");
            OnClosed();
        }
    }

    // ══════════════════════════════════════════════════════════════
    //  弹窗关闭回调（幂等 — _collected 守卫）
    // ══════════════════════════════════════════════════════════════

    private void OnClosed()
    {
        if (_collected) return;
        _collected = true;

        if (!string.IsNullOrEmpty(fragmentId))
            s_collectedFragmentIds.Add(fragmentId);

        var ownCollider = GetComponent<Collider2D>();
        if (ownCollider != null)
            ownCollider.enabled = false;

        var visual = transform.Find("Visual");
        if (visual != null)
            visual.gameObject.SetActive(false);

        // ① 最优先：恢复玩家控制
        _playerMovement?.Unfreeze();
        _playerInteraction?.ReturnToFree();

        // ② 更新碎片计数（用于终点门判断）
        AbyssPortal.Instance?.CollectFragment();

        // ③ 清理 + 销毁
        _playerInteraction?.NotifyNodeDestroyed(this);
        DestroyPrompt();
        Destroy(gameObject);
    }

    // ══════════════════════════════════════════════════════════════
    //  范围检测
    // ══════════════════════════════════════════════════════════════

    public override void OnPlayerEnter(GameObject player)
    {
        if (_collected || IsCollected(fragmentId)) return;

        _playerMovement = player.GetComponent<PlayerMovement>();
        _playerInteraction = player.GetComponent<PlayerInteraction>();

        // 提示位置：必须在相机视野内，否则玩家看不到
        // 因为相机固定在原点，碎片逻辑位置（Y=3~15）大部分在视野外，
        // 所以提示文字需要显示在相机可视区域内。
        ShowPrompt(interactPromptText, GetCameraVisiblePromptPosition());
    }

    /// <summary>
    /// 获取相机可视区域内的提示位置。
    /// 优先使用视觉子物体位置（MemoryPerspectiveEffect 会将其移到屏幕内），
    /// 否则回退到相机视野上方居中。
    /// </summary>
    private Vector3 GetCameraVisiblePromptPosition()
    {
        // 尝试使用 Visual 子物体（已在相机视野内）
        var visual = transform.Find("Visual");
        if (visual != null)
        {
            var sr = visual.GetComponent<SpriteRenderer>();
            if (sr != null && sr.enabled)
                return visual.position + promptOffset;
        }

        // 回退：相机视野上方，比例由 promptCameraYRatio 控制
        var cam = Camera.main;
        if (cam != null)
        {
            float y = cam.transform.position.y + cam.orthographicSize * promptCameraYRatio;
            return new Vector3(0f, y, 0f);
        }

        return transform.position + promptOffset;
    }

    public override void OnPlayerExit(GameObject player)
    {
        HidePrompt();
    }
}
