using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using System.Collections;

/// <summary>
/// 失败特效控制器（全局持久，跨场景自动注入）。
///
/// ══ 架构说明 ══
///   复用 InGameMenuController 的注入模式。
///   每次场景加载：在 ModalLayer 查找 "fail" 面板 → 直接抖动面板根节点。
///
/// ══ 行为流程 ══
///   ShowFailEffect() 被调用后：
///     1. 激活 fail 面板
///     2. 显示 ModalBackground 遮罩
///     3. fail 面板执行 DOTween 强烈抖动（ShakePosition + ShakeRotation）
///     4. 抖动结束 → 短暂停留 → 重新加载当前场景
///
/// ══ 适用场景 ══
///   Corridor  — 保安抓捕（LightDetection）
///   PipePuzzle — 时间耗尽 / 其他失败条件
///   DecodeGame — 撞墙（GameManager1.GameOver）/ 超时（CountdownTimer）
///
/// ══ 不需要手动挂载。由 Bootstrapper 自动创建。══
/// </summary>
public class FailEffectController : MonoBehaviour
{
    // ══════════════════════════════════════════════════════════════
    //  单例 + 初始化
    // ══════════════════════════════════════════════════════════════

    public static FailEffectController Instance { get; private set; }

    /// <summary>
    /// 由 UIManager.InitializeGlobalUI() 调用，仅执行一次。
    /// </summary>
    public static FailEffectController Initialize(Transform parent)
    {
        if (Instance != null) return Instance;

        var go = new GameObject("FailEffectController_Logic");
        go.transform.SetParent(parent, false);
        Instance = go.AddComponent<FailEffectController>();

        if (UIManager.Instance != null)
        {
            UIManager.Instance.OnSceneRootRegistered += Instance.OnSceneLoaded;

            // 防漏补丁
            if (UIManager.Instance.HasSceneRoot && UIManager.Instance.modalLayer != null)
            {
                var existingRoot = UIManager.Instance.modalLayer
                    .GetComponentInParent<UISceneRoot>();
                if (existingRoot != null)
                {
                    Debug.Log("[FailEffect] 检测到场景已存在，立即补发加载！");
                    Instance.OnSceneLoaded(existingRoot);
                }
            }
        }

        return Instance;
    }

    // ══════════════════════════════════════════════════════════════
    //  配置常量
    // ══════════════════════════════════════════════════════════════

    /// <summary>ModalLayer 中失败面板名称（与 fail.prefab 一致）</summary>
    private const string FAIL_PANEL_NAME = "fail";

    // ══════════════════════════════════════════════════════════════
    //  抖动参数（强烈震感）
    // ══════════════════════════════════════════════════════════════

    /// <summary>抖动持续时间</summary>
    private const float SHAKE_DURATION = 1.2f;

    /// <summary>位移抖动强度（像素）</summary>
    private const float SHAKE_STRENGTH = 60f;

    /// <summary>抖动频率（振动次数）</summary>
    private const int SHAKE_VIBRATO = 35;

    /// <summary>抖动随机性 (0-180)</summary>
    private const float SHAKE_RANDOMNESS = 90f;

    /// <summary>旋转抖动强度（度）</summary>
    private const float ROTATION_SHAKE_STRENGTH = 10f;

    /// <summary>抖动结束后到重载的等待时间</summary>
    private const float POST_SHAKE_DELAY = 0.8f;

    // ══════════════════════════════════════════════════════════════
    //  运行时引用（每次场景加载重新赋值）
    // ══════════════════════════════════════════════════════════════

    private GameObject    _failPanel;
    private RectTransform _failPanelRect;
    private bool          _isPlaying = false;

    // ══════════════════════════════════════════════════════════════
    //  场景加载回调
    // ══════════════════════════════════════════════════════════════

    private void OnSceneLoaded(UISceneRoot root)
    {
        _failPanel = null;
        _failPanelRect = null;
        _isPlaying = false;

        if (root == null || root.modalLayer == null) return;

        var panelT = FindInChildren(root.modalLayer, FAIL_PANEL_NAME);
        if (panelT == null)
        {
            Debug.Log($"[FailEffect] {root.name} ModalLayer 中无 'fail' 面板，跳过。");
            return;
        }

        _failPanel = panelT.gameObject;
        _failPanelRect = panelT.GetComponent<RectTransform>();

        // 强制拉满确保覆盖全屏
        if (_failPanelRect != null)
        {
            _failPanelRect.anchorMin = Vector2.zero;
            _failPanelRect.anchorMax = Vector2.one;
            _failPanelRect.anchoredPosition = Vector2.zero;
            _failPanelRect.sizeDelta = Vector2.zero;
        }

        // 初始隐藏
        _failPanel.SetActive(false);

        Debug.Log($"[FailEffect] fail 面板已绑定（{root.name}）。");
    }

    // ══════════════════════════════════════════════════════════════
    //  公开 API
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// 显示失败特效：激活 fail 面板 → Image 强烈抖动 → 重新加载当前场景。
    /// 如果当前场景没有 fail 面板，直接重载场景。
    /// </summary>
    public void ShowFailEffect()
    {
        if (_isPlaying) return;

        if (_failPanel == null)
        {
            Debug.LogWarning("[FailEffect] 当前场景无 fail 面板，直接重载。");
            ReloadCurrentScene();
            return;
        }

        _isPlaying = true;

        // ── 强制置顶：fail 面板在 ModalLayer 内覆盖一切（包括 ModalBackground） ──
        _failPanel.transform.SetAsLastSibling();

        _failPanel.SetActive(true);

        StartCoroutine(FailSequence());
    }

    /// <summary>当前是否正在播放失败特效</summary>
    public bool IsPlaying => _isPlaying;

    // ══════════════════════════════════════════════════════════════
    //  失败特效序列
    // ══════════════════════════════════════════════════════════════

    private IEnumerator FailSequence()
    {
        // 1. 显示模态背景遮罩
        UIManager.Instance?.ShowModalBackground();

        // 2. 直接抖动 fail 面板根节点（根上就是全屏失败图片）
        if (_failPanelRect != null)
        {
            // 位移抖动
            _failPanelRect.DOShakePosition(
                    SHAKE_DURATION,
                    new Vector3(SHAKE_STRENGTH, SHAKE_STRENGTH, 0f),
                    SHAKE_VIBRATO,
                    SHAKE_RANDOMNESS,
                    false,
                    true
                )
                .SetUpdate(true);

            // 旋转抖动
            _failPanelRect.DOShakeRotation(
                    SHAKE_DURATION,
                    new Vector3(0f, 0f, ROTATION_SHAKE_STRENGTH),
                    SHAKE_VIBRATO,
                    SHAKE_RANDOMNESS,
                    true
                )
                .SetUpdate(true);

            yield return new WaitForSecondsRealtime(SHAKE_DURATION);
        }
        else
        {
            yield return new WaitForSecondsRealtime(SHAKE_DURATION);
        }

        // 3. 抖动结束后短暂停留
        yield return new WaitForSecondsRealtime(POST_SHAKE_DELAY);

        // 4. 重载当前场景
        UIManager.Instance?.HideModalBackground();
        _isPlaying = false;
        ReloadCurrentScene();
    }

    // ══════════════════════════════════════════════════════════════
    //  场景重载
    // ══════════════════════════════════════════════════════════════

    private void ReloadCurrentScene()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.ReloadCurrentPhase();
        }
        else
        {
            // 降级：直接重载当前场景
            UnityEngine.SceneManagement.SceneManager.LoadScene(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
        }
    }

    // ══════════════════════════════════════════════════════════════
    //  工具方法
    // ══════════════════════════════════════════════════════════════

    private static Transform FindInChildren(Transform parent, string name)
    {
        if (parent.name == name) return parent;
        for (int i = 0; i < parent.childCount; i++)
        {
            var result = FindInChildren(parent.GetChild(i), name);
            if (result != null) return result;
        }
        return null;
    }
}
