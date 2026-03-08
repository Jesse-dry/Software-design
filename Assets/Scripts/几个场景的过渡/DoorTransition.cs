using UnityEngine;
using UnityEngine.SceneManagement;

public class DoorTransition : MonoBehaviour
{
    [Header("这扇门通向哪个场景？")]
    public string targetSceneName;

    [Header("把提示按E键的文字/图片拖到这里")]
    public GameObject ePromptUI;

    // ==========================================
    // 【新增】路线选择与弹窗配置
    // ==========================================
    [Header("路线选择配置")]
    [Tooltip("弹窗里显示的提示文字")]
    [TextArea(3, 6)]
    public string modalContent = "是否确认进入？";

    [Tooltip("扣除或增加的混乱值（不填就是 0）")]
    public int chaosChange = 0;

    [Tooltip("混乱值变化的理由（用于飘字提示）")]
    public string chaosReason = "路线选择";

    private bool canEnter = false;

    void Start()
    {
        if (ePromptUI != null) ePromptUI.SetActive(false);
    }

    void Update()
    {
        if (canEnter && Input.GetKeyDown(KeyCode.E))
        {
            // 【修改】按 E 键后，不再直接跳转，而是呼出弹窗！
            Interact();
        }
    }

    // 【新增】弹窗交互逻辑
    private void Interact()
    {
        // 呼叫全局 UI 弹窗
        if (UIManager.Instance != null && UIManager.Instance.Modal != null)
        {
            string title = $"【身份确认】";
            // 弹出确认框，并把“跳转代码”打包成回调函数传给它
            UIManager.Instance.Modal.ShowConfirm(title + "\n" + modalContent, () =>
            {
                // ==========================================
                // 以下代码只有在玩家点击【确认】后才会执行！
                // ==========================================

                // 1. 结算混乱值
                if (chaosChange != 0 && ChaosManager.Instance != null)
                {
                    ChaosManager.Instance.AddChaos(chaosChange, chaosReason);
                }

                // 2. 原封不动地调用队友的跳转代码！
                Debug.Log($"[DoorTransition] 确认路线，前往场景：{targetSceneName}");
                SceneManager.LoadScene(targetSceneName);
            });
        }
        else
        {
            // 防报错：如果当前场景没有 UIManager，就按老规矩直接跳
            SceneManager.LoadScene(targetSceneName);
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            canEnter = true;
            if (ePromptUI != null) ePromptUI.SetActive(true);
        }
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            canEnter = false;
            if (ePromptUI != null) ePromptUI.SetActive(false);
        }
    }
}