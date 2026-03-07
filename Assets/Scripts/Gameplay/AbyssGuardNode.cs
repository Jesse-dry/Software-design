using UnityEngine;
using UnityEngine.InputSystem; // 兼容新版输入系统

public class AbyssGuardNPC : MonoBehaviour
{
    [Header("NPC 配置")]
    public string npcName = "安保主管";

    [Header("陷阱问题 (点'确认'会扣分)")]
    [TextArea] public string trapQuestion = "站住！机房重地。你是今天来替班的维修员李工吧？";

    [Header("过关问题 (点'确认'能过关)")]
    [TextArea] public string passQuestion = "行吧。那核对一下今天的安全口令。口令是 '协议-01'，确认吗？";

    [Header("惩罚配置")]
    public int chaosPenalty = 20;

    // 内部状态
    private bool _isPlayerInRange = false; // 玩家是否在身边
    private bool _hasPassed = false;
    private bool _askedTrap = false;

    private void Update()
    {
        // 1. 如果玩家不在跟前，直接忽略按键
        if (!_isPlayerInRange) return;

        // 2. 检测交互键 (同时兼容 E 键和 F 键)
        bool interactPressed = Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.F);

#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            interactPressed |= Keyboard.current.eKey.wasPressedThisFrame || Keyboard.current.fKey.wasPressedThisFrame;
        }
#endif

        // 3. 玩家在跟前且按下了交互键，开始盘问！
        if (interactPressed)
        {
            Interact();
        }
    }

    // 当玩家踏入警戒圈（Trigger）时触发
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            _isPlayerInRange = true;
            // 【新增】弹出靠近提示
            if (UIManager.Instance != null && UIManager.Instance.Toast != null)
            {
                UIManager.Instance.Toast.Show("按交互键接受盘问");
            }
        }
    }

    // 当玩家离开警戒圈时触发
    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            _isPlayerInRange = false;
            Debug.Log("[AbyssGuard] 玩家离开警戒范围。");
        }
    }

    // 核心拷问逻辑（与之前一致，完美对接队友的 UI）
    private void Interact()
    {
        if (_hasPassed)
        {
            UIManager.Instance.Toast.Show("“赶紧进去修终端，别磨蹭。”");
            return;
        }

        if (!_askedTrap)
        {
            string content = $"【{npcName}】\n{trapQuestion}\n\n(提示：根据线索，你是李工吗？\n点击【确认】承认，按 退出键 无视他)";
            UIManager.Instance.Modal.ShowConfirm(content, OnTrapConfirmed);
            _askedTrap = true;
        }
        else
        {
            string content = $"【{npcName}】\n{passQuestion}\n\n(点击【确认】报出口令)";
            UIManager.Instance.Modal.ShowConfirm(content, PassInterrogation);
        }
    }

    private void OnTrapConfirmed()
    {
        UIManager.Instance.Toast.Show("“不对吧？李工今天请病假了！你到底是谁？！”");
        if (ChaosManager.Instance != null) ChaosManager.Instance.AddChaos(chaosPenalty, "冒充身份被识破");
    }

    private void PassInterrogation()
    {
        _hasPassed = true;
        UIManager.Instance.Toast.Show("“口令正确，终端在里面，去吧。”");

        // 找到身上【不是Trigger】的那个实体碰撞体（墙），把它关掉，放行玩家！
        Collider2D[] colliders = GetComponents<Collider2D>();
        foreach (var col in colliders)
        {
            if (!col.isTrigger)
            {
                col.enabled = false;
            }
        }
    }
}