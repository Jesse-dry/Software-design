using UnityEngine;

/// <summary>
/// 记忆碎片节点（Memory 场景专用）。
/// 
/// 靠近时：世界坐标飘字 "按 E 交互"
/// 按 E 后：模态弹窗展示碎片内容 → 关闭后碎片消失 + 碎片计数 +1
/// </summary>
public class MemoryFragmentNode : MemoryNodeBase
{
    [Header("碎片设置")]
    [TextArea(3, 6)]
    [Tooltip("碎片标题")]
    public string fragmentTitle = "记忆碎片";

    [TextArea(3, 10)]
    [Tooltip("碎片弹窗正文内容")]
    public string fragmentBody = "一段模糊的记忆浮上心头……";

    [Tooltip("世界坐标提示文字偏移（相对自身位置）")]
    public Vector3 promptOffset = new Vector3(0, 1.5f, 0);

    private bool isPlayerNearby = false;
    private bool isCollected = false;

    public override void Interact()
    {
        if (isCollected) return;

        // 冻结玩家
        var player = FindAnyObjectByType<PlayerMovement>();
        player?.Freeze();

        // 弹出模态弹窗展示碎片内容
        UIManager.Instance?.Modal.ShowText(
            fragmentTitle,
            fragmentBody,
            "关闭",
            () =>
            {
                // 关闭弹窗后：标记已收集 → 通知 AbyssPortal → 解冻 → 销毁碎片
                isCollected = true;
                AbyssPortal.Instance?.CollectFragment();
                player?.Unfreeze();

                UIManager.Instance?.Toast.Show(
                    $"拾取了记忆碎片（{AbyssPortal.Instance?.CurrentFragments ?? 0}/{AbyssPortal.Instance?.RequiredFragments ?? 4}）");

                Destroy(gameObject);
            }
        );
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (isCollected) return;
        if (!other.CompareTag("Player")) return;

        isPlayerNearby = true;

        // 世界坐标飘字提示
        UIManager.Instance?.Toast.ShowAtWorld(
            transform.position + promptOffset,
            "按 E 交互");
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        isPlayerNearby = false;
    }
}