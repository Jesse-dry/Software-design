using UnityEngine;
using UnityEngine.SceneManagement;

public class DoorTransition : MonoBehaviour
{
    [Header("这扇门通向哪个场景？")]
    public string targetSceneName;

    [Header("这扇门对应的游戏阶段（优先使用）")]
    [Tooltip("如果设为非 Boot，将通过 GameManager 状态机切换；否则按 targetSceneName 降级处理")]
    public GamePhase targetPhase = GamePhase.Boot;

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
        // Corridor 场景已改用 CorridorEndTrigger + SelectRole，旧门逻辑不再生效
        if (GameManager.Instance != null && GameManager.Instance.CurrentPhase == GamePhase.Corridor)
        {
            if (ePromptUI != null) ePromptUI.SetActive(false);
            gameObject.SetActive(false);
            return;
        }

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

                // 2. 通过 GameManager 状态机切换场景
                Debug.Log($"[DoorTransition] 确认路线，前往: {targetSceneName}");
                TransitionToTarget();
            });
        }
        else
        {
            // 防报错：如果当前场景没有 UIManager，就按老规矩直接跳
            TransitionToTarget();
        }
    }

    /// <summary>
    /// 统一跳转逻辑：优先使用 targetPhase 通过 GameManager 切换。
    /// 如果 targetPhase 为 Boot（默认/未配置），则尝试根据 targetSceneName 推断阶段；
    /// 推断失败则降级为直接 SceneManager.LoadScene。
    /// </summary>
    private void TransitionToTarget()
    {
        GamePhase phase = targetPhase;

        // 如果未在 Inspector 中配置 targetPhase，尝试从场景名推断
        if (phase == GamePhase.Boot && !string.IsNullOrEmpty(targetSceneName))
        {
            phase = InferPhaseFromSceneName(targetSceneName);
        }

        if (phase != GamePhase.Boot && GameManager.Instance != null)
        {
            GameManager.Instance.EnterPhase(phase);
        }
        else
        {
            // 降级：直接加载场景
            Debug.LogWarning($"[DoorTransition] 无法通过状态机切换，直接加载场景: {targetSceneName}");
            SceneManager.LoadScene(targetSceneName);
        }
    }

    /// <summary>
    /// 根据场景名称推断 GamePhase（向后兼容旧的 Inspector 配置）
    /// </summary>
    private static GamePhase InferPhaseFromSceneName(string sceneName)
    {
        string lower = sceneName.ToLower();
        if (lower.Contains("corridor"))   return GamePhase.Corridor;
        if (lower.Contains("piperoom"))   return GamePhase.PipeRoom;
        if (lower.Contains("pipepuzzle")) return GamePhase.PipePuzzle;
        if (lower.Contains("server"))     return GamePhase.ServerRoom;
        if (lower.Contains("decode"))     return GamePhase.DecodeGame;
        if (lower.Contains("abyss"))      return GamePhase.Abyss;
        if (lower.Contains("court"))      return GamePhase.Court;
        if (lower.Contains("memory"))     return GamePhase.Memory;
        if (lower.Contains("menu"))       return GamePhase.MainMenu;
        return GamePhase.Boot; // 无法推断
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