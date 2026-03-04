using UnityEngine;

/// <summary>
/// 潜渊传送门（Memory 场景终点）。
/// 
/// 收集全部碎片后，玩家靠近门 → 世界坐标飘字 → 确认弹窗 → 过渡动画 → 加载 Abyss 场景。
/// 碎片不足时显示 Toast 提示。
/// </summary>
public class AbyssPortal : MonoBehaviour
{
    public static AbyssPortal Instance;

    [Header("过关条件")]
    [Tooltip("需要收集的碎片总数")]
    public int requiredFragments = 4;
    private int currentFragments = 0;

    [Header("提示")]
    [Tooltip("世界坐标提示偏移")]
    public Vector3 promptOffset = new Vector3(0, 1.5f, 0);

    private bool isTransitioning = false;
    private bool playerInRange = false;

    /// <summary>当前已收集碎片数（供 MemoryFragmentNode 读取）</summary>
    public int CurrentFragments => currentFragments;

    /// <summary>需要的碎片总数（供 MemoryFragmentNode 读取）</summary>
    public int RequiredFragments => requiredFragments;

    private void Awake()
    {
        Instance = this;
    }

    /// <summary>碎片被收集时调用</summary>
    public void CollectFragment()
    {
        currentFragments++;
        Debug.Log($"[AbyssPortal] 碎片进度: {currentFragments}/{requiredFragments}");

        if (currentFragments >= requiredFragments)
        {
            Debug.Log("[AbyssPortal] 全部碎片已收集，传送门已激活！");
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player") || isTransitioning) return;

        playerInRange = true;

        if (currentFragments >= requiredFragments)
        {
            // 碎片已集齐 → 飘字 + 确认弹窗
            UIManager.Instance?.Toast.ShowAtWorld(
                transform.position + promptOffset,
                "按 E 进入");
        }
        else
        {
            // 碎片不足 → Toast 提示
            UIManager.Instance?.Toast.Show(
                $"记忆碎片不足（{currentFragments}/{requiredFragments}），继续探索吧。");
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        playerInRange = false;
    }

    /// <summary>
    /// 供 PlayerInteraction 调用（当本对象也挂 MemoryNodeBase 或单独处理时）。
    /// 也可以由专门的 AbyssPortalNode 触发。
    /// </summary>
    public void TryEnterAbyss()
    {
        if (isTransitioning) return;

        if (currentFragments < requiredFragments)
        {
            UIManager.Instance?.Toast.Show(
                $"记忆碎片不足（{currentFragments}/{requiredFragments}），继续探索吧。");
            return;
        }

        isTransitioning = true;

        // 冻结玩家
        var player = FindAnyObjectByType<PlayerMovement>();
        player?.Freeze();

        // 弹出确认弹窗
        UIManager.Instance?.Modal.ShowConfirm(
            "潜入深渊，探寻真相？",
            onYes: () =>
            {
                // 确认 → 过渡动画 → 切换场景
                UIManager.Instance?.Transition.FadeIn(1.5f, () =>
                {
                    GameManager.Instance?.EnterPhase(GamePhase.Abyss);
                });
            },
            onNo: () =>
            {
                // 取消 → 解冻玩家
                isTransitioning = false;
                player?.Unfreeze();
            }
        );
    }
}