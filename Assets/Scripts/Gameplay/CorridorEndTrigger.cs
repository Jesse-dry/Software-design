using UnityEngine;
using System.Collections;

/// <summary>
/// 走廊终点触发器 — 放在走廊场景末端（两扇门前方）。
///
/// 流程：
///   1. 玩家走到走廊末端触发
///   2. 收集【权杖】阿卡那牌
///   3. Toast 提示获得卡牌
///   4. Modal 询问是否查看阿卡那牌
///   5a. 是 → 弹出权杖牌说明面板 → 关闭后进入 SelectRole
///   5b. 否 → 直接进入 SelectRole
///
/// Awake 会自动确保 BoxCollider2D 存在且 isTrigger = true。
/// 如果位置留在原点 (0,0,0) 则自动修正到走廊末端 (-97, -2.7)。
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class CorridorEndTrigger : MonoBehaviour
{
    [Header("触发区")]
    [SerializeField] private Vector2 triggerSize = new Vector2(3f, 5f);

    [Header("走廊末端自动修正坐标（仅当物体还在原点时生效）")]
    [SerializeField] private Vector3 corridorEndPosition = new Vector3(-97f, -2.7f, 0f);

    [Header("提示文案")]
    [SerializeField] private string cardToastText = "获得了【权杖】阿卡那牌！";
    [SerializeField][TextArea(2, 4)]
    private string modalPromptText = "恭喜获得【权杖】阿卡那牌，是否查看牌面内容？";

    private bool _triggered = false;

    // ──────────────────────────────────────────
    //  初始化
    // ──────────────────────────────────────────

    private void Awake()
    {
        // ── 位置保底：如果物体还留在原点就自动挪到走廊末端 ──
        if (transform.position == Vector3.zero)
        {
            transform.position = corridorEndPosition;
            Debug.Log($"[CorridorEnd] 位置在原点，已自动修正到 {corridorEndPosition}");
        }

        // ── 确保 BoxCollider2D 存在且为 Trigger ──
        var col = GetComponent<BoxCollider2D>();
        if (col == null) col = gameObject.AddComponent<BoxCollider2D>();
        col.isTrigger = true;
        col.size = triggerSize;

        Debug.Log($"[CorridorEnd] Awake — pos={transform.position}, " +
                  $"size={col.size}, active={gameObject.activeInHierarchy}");
    }

    // ──────────────────────────────────────────
    //  物理触发
    // ──────────────────────────────────────────

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (_triggered) return;
        if (!other.CompareTag("Player")) return;

        _triggered = true;
        Debug.Log("[CorridorEnd] 玩家到达走廊终点，启动通关流程！");
        StartCoroutine(VictorySequence());
    }

    // ──────────────────────────────────────────
    //  通关结算流程
    // ──────────────────────────────────────────

    private IEnumerator VictorySequence()
    {
        // 1. 收集【权杖】
        if (AkanaManager.Instance != null)
            AkanaManager.Instance.CollectCard(AkanaCardId.权杖);

        // 2. Toast
        if (UIManager.Instance?.Toast != null)
            UIManager.Instance.Toast.Show(cardToastText, colorType: ToastColor.Positive);

        yield return new WaitForSeconds(1.0f);

        // 3. Modal 是否查看
        AkanaVictoryHelper.AskViewCard(
            AkanaCardId.权杖,
            modalPromptText,
            onFinished: OpenSelectRole
        );
    }

    private void OpenSelectRole()
    {
        var ctrl = SelectRoleController.Instance;
        if (ctrl != null)
        {
            ctrl.ShowSelectRole();
            Debug.Log("[CorridorEnd] SelectRole 已打开。");
        }
        else
        {
            Debug.LogWarning("[CorridorEnd] SelectRoleController 不存在！");
        }
    }
}
