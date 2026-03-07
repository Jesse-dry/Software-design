using UnityEngine;

public class AbyssClueNode : MonoBehaviour
{
    [Header("线索配置")]
    [Tooltip("弹窗标题")]
    public string clueTitle = "一张皱巴巴的纸条";

    [Tooltip("弹窗正文")]
    [TextArea(3, 5)]
    public string clueContent = "今天李工发烧请病假，王工顶班。";

    // 内部状态
    private bool _isPlayerInRange = false;

    private void Update()
    {
        if (!_isPlayerInRange) return;

        // 检测交互键
        if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.F))
        {
            Interact();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // 认准 Player 标签
        if (other.CompareTag("Player"))
        {
            _isPlayerInRange = true;
            if (UIManager.Instance != null && UIManager.Instance.Toast != null)
            {
                UIManager.Instance.Toast.Show("按 [E] 检查物品");
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
        // 弹窗系统，展示纯文本阅读界面
        if (UIManager.Instance != null && UIManager.Instance.Modal != null)
        {
            // 这里的 "记下了" 是关闭按钮的文字
            UIManager.Instance.Modal.ShowText(clueTitle, clueContent, "记下了", OnReadComplete);
        }
    }

    private void OnReadComplete()
    {
        // 玩家看完后给个提示
        if (UIManager.Instance != null && UIManager.Instance.Toast != null)
        {
            UIManager.Instance.Toast.Show($"情报已记录：{clueTitle}", colorType: ToastColor.Positive);
        }

        // 阅后即焚（销毁物体），防止玩家重复捡起
        Destroy(gameObject);
    }
}