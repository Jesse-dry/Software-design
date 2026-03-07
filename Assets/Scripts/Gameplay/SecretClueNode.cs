using UnityEngine;

public class SecretClueNode : MemoryNodeBase
{
    [Header("环境线索（仅供阅读，不入庭审）")]
    [Tooltip("线索弹窗的标题")]
    public string clueTitle = "皱巴巴的纸条";

    [Tooltip("线索的具体内容文字")]
    [TextArea(3, 5)] // 在 Inspector 里变成一个大文本框，方便你输入多行文字
    public string clueContent = "李工今天发高烧请病假了，王工顶班。";

    public override void Interact()
    {
        // 冻结玩家操作并弹出阅读窗口
        // 这里完美调用了队友的 ModalSystem.ShowText (标题, 正文, 按钮文字, 关闭后的回调)
        if (UIManager.Instance != null && UIManager.Instance.Modal != null)
        {
            UIManager.Instance.Modal.ShowText(
                clueTitle,
                clueContent,
                "记下了",         // 将关闭按钮的文字改成更有代入感的词
                OnReadComplete   // 玩家关掉弹窗后执行
            );
        }
    }

    private void OnReadComplete()
    {
        // 玩家阅读完毕后，给个微小的反馈
        if (UIManager.Instance != null && UIManager.Instance.Toast != null)
        {
            UIManager.Instance.Toast.Show($"阅读完毕：{clueTitle}");
        }

        // 阅后即焚（从场景中销毁），避免玩家重复点击
        Destroy(gameObject);
    }
}