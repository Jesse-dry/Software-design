using UnityEngine;

/// <summary>
/// 潜渊传送门（Memory 场景终点）。
///
/// 【职责】
///   - 碎片收集计数
///   - 确认弹窗 → 场景切换
///
/// 【设计原则】
///   - 无 Update()，不做键盘轮询
///   - 确认弹窗的键盘操作由 ModalSystem 内部处理（Enter/Space 确认，Esc 取消）
///   - 场景跳转走 GameManager → SceneController → SceneManager 三级 fallback
/// </summary>
public class AbyssPortal : MonoBehaviour
{
    public static AbyssPortal Instance;

    [Header("过关条件")]
    public int requiredFragments = 4;
    private int currentFragments = 0;

    private bool _transitioning;
    private PlayerMovement _frozenPlayer;
    private PlayerInteraction _playerInteraction;

    public int CurrentFragments => currentFragments;
    public int RequiredFragments => requiredFragments;

    private void Awake() { Instance = this; }

    // ══════════════════════════════════════════════════════════════
    //  碎片收集
    // ══════════════════════════════════════════════════════════════

    public void CollectFragment()
    {
        currentFragments++;
        Debug.Log($"[AbyssPortal] 碎片进度: {currentFragments}/{requiredFragments}");
        if (currentFragments >= requiredFragments)
            Debug.Log("[AbyssPortal] 全部碎片已收集，传送门已激活！");
    }

    // ══════════════════════════════════════════════════════════════
    //  尝试进入深渊（由 AbyssPortalNode.Interact 调用）
    // ══════════════════════════════════════════════════════════════

    public void TryEnterAbyss()
    {
        if (_transitioning) return;

        if (currentFragments < requiredFragments)
        {
            UIManager.Instance?.Toast?.Show(
                $"记忆碎片不足（{currentFragments}/{requiredFragments}），继续探索吧。");
            return;
        }

        _transitioning = true;

        // 冻结玩家 + 锁定交互
        _frozenPlayer = FindAnyObjectByType<PlayerMovement>();
        _playerInteraction = FindAnyObjectByType<PlayerInteraction>();
        _frozenPlayer?.Freeze();
        _playerInteraction?.EnterInteracting();

        // 打开确认弹窗
        var modal = UIManager.Instance?.Modal;
        if (modal != null)
        {
            modal.ShowConfirm("潜入深渊，探寻真相？",
                onYes: OnConfirm,
                onNo:  OnCancel);
        }
        else
        {
            Debug.LogWarning("[AbyssPortal] ModalSystem 不可用，直接进入深渊。");
            OnConfirm();
        }
    }

    // ══════════════════════════════════════════════════════════════
    //  确认 / 取消回调
    // ══════════════════════════════════════════════════════════════

    private void OnConfirm()
    {
        // 有转场系统 → CrossFade（淡入黑 → 加载场景 → 淡出显示新场景）
        // 旧代码只调用 FadeIn 不调用 FadeOut，导致加载后画面永远停留在黑屏。
        var transition = UIManager.Instance?.Transition;
        if (transition != null)
        {
            transition.CrossFade(
                fadeInDur: 1.5f,
                midAction: () => LoadAbyss(),
                fadeOutDur: 1.5f,
                onComplete: null);
        }
        else
        {
            LoadAbyss();
        }
    }

    private void OnCancel()
    {
        _transitioning = false;
        _frozenPlayer?.Unfreeze();
        _playerInteraction?.ReturnToFree();
    }

    // ══════════════════════════════════════════════════════════════
    //  场景加载（三级 fallback）
    // ══════════════════════════════════════════════════════════════

    private void LoadAbyss()
    {
        // 解冻（新场景会创建新 Player，这里防止切换失败时卡死）
        _frozenPlayer?.Unfreeze();
        _playerInteraction?.ReturnToFree();

        if (GameManager.Instance != null)
        {
            GameManager.Instance.EnterPhase(GamePhase.Abyss);
        }
        else if (SceneController.Instance != null)
        {
            Debug.LogWarning("[AbyssPortal] GameManager 为 null，由 SceneController 直接加载。");
            SceneController.Instance.LoadAbyss();
        }
        else
        {
            Debug.LogError("[AbyssPortal] GameManager、SceneController 均为 null！直接 LoadScene。");
            UnityEngine.SceneManagement.SceneManager.LoadScene("Abyss");
        }
    }
}
