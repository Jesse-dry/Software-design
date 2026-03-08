using UnityEngine;
using UnityEngine.InputSystem; // 兼容新版输入系统
using System.Collections;

public class AbyssGuardNPC : MonoBehaviour
{
    [Header("NPC 配置")]
    public string npcName = "安保主管";

    [Header("陷阱问题 (点'确认'会扣分)")]
    [TextArea] public string trapQuestion = "站住！机房重地。你是今天来替班的维修员李工吧？";

    [Header("过关问题 (点'确认'能过关)")]
    [TextArea] public string passQuestion = "行吧。那核对一下今天的安全口令。口令是 'Consensus-01'，确认吗？";

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
                UIManager.Instance.Toast.Show("按E键接受盘问");
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

    // 核心拷问逻辑 — 使用新的盘问对话UI
    private void Interact()
    {
        if (_hasPassed)
        {
            UIManager.Instance.Toast.Show("赶紧进去修终端，别磨蹭。");
            return;
        }

        // 获取盘问对话UI
        var interrogationUI = InterrogationDialogueUI.Instance;
        if (interrogationUI == null)
        {
            interrogationUI = FindAnyObjectByType<InterrogationDialogueUI>();
        }

        if (!_askedTrap)
        {
            string title = npcName;
            string content = trapQuestion + "\n\n(提示：根据线索，你是李工吗？\n点击【是】承认，点击【否】保持沉默)";

            if (interrogationUI != null)
            {
                interrogationUI.Show(title, content, OnTrapConfirmed, OnTrapSilenced);
            }
            else
            {
                UIManager.Instance.Modal.ShowConfirm("【" + npcName + "】\n" + content, OnTrapConfirmed, OnTrapSilenced);
            }
            _askedTrap = true;
        }
        else
        {
            string title = npcName;
            string content = passQuestion + "\n\n(点击【是】报出口令，点击【否】保持沉默)";

            if (interrogationUI != null)
            {
                interrogationUI.Show(title, content, PassInterrogation, OnPasswordSilenced);
            }
            else
            {
                UIManager.Instance.Modal.ShowConfirm("【" + npcName + "】\n" + content, PassInterrogation, OnPasswordSilenced);
            }
        }
    }

    private void OnTrapConfirmed()
    {
        UIManager.Instance.Toast.Show("“不对吧？李工今天请病假了！你到底是谁？！”");
        if (ChaosManager.Instance != null) ChaosManager.Instance.AddChaos(chaosPenalty, "冒充身份被识破");
    }

    private void OnTrapSilenced()
    {
        // 躲过了陷阱
        UIManager.Instance.Toast.Show("“算你聪明... 那核对一下口令吧。”");
    }

    private void OnPasswordSilenced()
    {
        // 答不出口令，保安直接翻脸！
        if (UIManager.Instance != null && UIManager.Instance.Toast != null)
        {
            // GlitchFlash 闪烁效果最适合警报
            UIManager.Instance.Toast.Show("“来人！把这个可疑分子押下去！”", ToastStyle.GlitchFlash, colorType: ToastColor.Warning);
        }

        if (ChaosManager.Instance != null)
        {
            // 严厉惩罚：加 30 点混乱值
            ChaosManager.Instance.AddChaos(30, "入侵机房被捕");
        }

        // 没收玩家的行动权（防止被捕期间还能乱跑按E）
        _isPlayerInRange = false;

        // 开启逮捕转场协程
        StartCoroutine(ArrestAndTransition());
    }

    // 【新增】逮捕并送上法庭的延迟转场
    private IEnumerator ArrestAndTransition()
    {
        // 停顿 2 秒，让玩家看看红字警告和混乱值飙升的惨状
        yield return new WaitForSeconds(2.0f);

        // 强行拉入法庭阶段！
        if (GameManager.Instance != null)
        {
            Debug.Log("[AbyssGuard] 玩家被捕，押送 Court（法庭）场景！");
            GameManager.Instance.EnterPhase(GamePhase.Court);
        }
    }

    private void PassInterrogation()
    {
        _hasPassed = true;
        UIManager.Instance.Toast.Show("口令正确，终端在里面，去吧。");

        // 关闭实体碰撞体（放行玩家）
        Collider2D[] colliders = GetComponents<Collider2D>();
        foreach (var col in colliders)
        {
            if (!col.isTrigger)
            {
                col.enabled = false;
            }
        }

        // 收集阿卡那牌 — 宝剑
        if (AkanaManager.Instance != null)
            AkanaManager.Instance.CollectCard(AkanaCardId.宝剑);

        // 延迟后弹出查看流程
        StartCoroutine(PassAndTransition());
    }

    private System.Collections.IEnumerator PassAndTransition()
    {
        // Toast 提示获得卡牌
        if (UIManager.Instance?.Toast != null)
            UIManager.Instance.Toast.Show("获得了【宝剑】阿卡那牌！", colorType: ToastColor.Positive);

        yield return new WaitForSeconds(1.0f);

        // 询问是否查看宝剑牌说明 → 关闭后返回走廊选角色
        AkanaVictoryHelper.AskViewCard(
            AkanaCardId.宝剑,
            "恭喜获得【宝剑】阿卡那牌，是否查看牌面内容？",
            onFinished: () =>
            {
                Debug.Log("[AbyssGuard] 盘问通过，返回 Corridor SelectRole！");
                SelectRoleController.ReturnToCorridorSelectRole();
            }
        );
    }
}