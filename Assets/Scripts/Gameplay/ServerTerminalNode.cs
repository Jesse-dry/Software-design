using UnityEngine;

public class ServerTerminalNode : MonoBehaviour
{
    [Header("终端配置")]
    public string terminalName = "核心数据库终端";
    [TextArea]
    public string promptText = "警告：检测到极高加密级别。是否接入个人终端开始物理破解？\n(即将进入接水管协议)";

    private bool _isPlayerInRange = false;

    private void Update()
    {
        if (!_isPlayerInRange) return;

        // 检测按键 E 或 F
        if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.F))
        {
            Interact();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            _isPlayerInRange = true;
            if (UIManager.Instance != null && UIManager.Instance.Toast != null)
            {
                UIManager.Instance.Toast.Show("按 [E] 接入核心终端");
            }
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            _isPlayerInRange = false;
        }
    }

    private void Interact()
    {
        // 弹出确认框（完美复用刚才和保安一样的 API）
        if (UIManager.Instance != null && UIManager.Instance.Modal != null)
        {
            string content = $"【{terminalName}】\n{promptText}\n\n(点击【确认】开始破解)";
            UIManager.Instance.Modal.ShowConfirm(content, StartHacking);
        }
    }

    private void StartHacking()
    {
        Debug.Log("[ServerTerminal] 玩家确认破解，准备跳转小游戏场景...");

        // 【核心】呼叫队友的 GameManager 进行华丽的跨场景跳转！
        if (GameManager.Instance != null)
        {
            GameManager.Instance.EnterPhase(GamePhase.DecodeGame);
        }
    }
}