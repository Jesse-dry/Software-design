using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

/// <summary>
/// Memory 场景专用 HUD — 碎片收集进度显示。
///
/// 挂在 UISceneRoot Prefab 的 HUDLayer 下的 "FragmentCounter" 子对象上，
/// 或由 MemorySceneSetup 在运行时关联。
///
/// 美术可在 Prefab 中自定义：
///   - 图标 Sprite / 颜色
///   - 文字字体 / 大小 / 颜色
///   - 动画参数（收集时脉冲强度等）
///
/// 使用示例：
///   MemoryHUD.Instance?.UpdateCount(currentFragments, totalFragments);
///   MemoryHUD.Instance?.PlayCollectPulse();
/// </summary>
public class MemoryHUD : MonoBehaviour
{
    public static MemoryHUD Instance { get; private set; }

    [Header("== 绑定 ==")]
    [Tooltip("计数文本组件（优先 TMP，回退到 Text）")]
    [SerializeField] private TMP_Text countTextTMP;
    [SerializeField] private Text countTextLegacy;

    [Tooltip("碎片图标")]
    [SerializeField] private Image fragmentIcon;

    [Tooltip("交互提示栏（底部提示 '按 E 交互' 的面板）")]
    [SerializeField] private GameObject interactionPrompt;
    [SerializeField] private TMP_Text promptTextTMP;
    [SerializeField] private Text promptTextLegacy;

    [Header("== 格式 ==")]
    [Tooltip("计数格式。{0}=当前 {1}=总数")]
    [SerializeField] private string countFormat = "碎片 {0}/{1}";

    [Header("== 收集动画 ==")]
    [Tooltip("收集时图标脉冲缩放")]
    [SerializeField] private float pulseScale = 1.3f;
    [Tooltip("脉冲动画时长")]
    [SerializeField] private float pulseDuration = 0.3f;

    // ── 状态 ─────────────────────────────────────────────────────
    private int _current;
    private int _total = 4;

    private void Awake()
    {
        Instance = this;

        // 自动绑定子对象（如果 Inspector 未手动赋值）
        if (countTextTMP == null)
            countTextTMP = transform.Find("CountText")?.GetComponent<TMP_Text>();
        if (countTextLegacy == null && countTextTMP == null)
            countTextLegacy = transform.Find("CountText")?.GetComponent<Text>();

        if (fragmentIcon == null)
            fragmentIcon = transform.Find("Icon")?.GetComponent<Image>();

        if (interactionPrompt == null)
        {
            // 尝试在父层（HUDLayer）找
            var hudLayer = transform.parent;
            if (hudLayer != null)
            {
                var prompt = hudLayer.Find("InteractionPrompt");
                if (prompt != null)
                {
                    interactionPrompt = prompt.gameObject;
                    promptTextTMP = prompt.Find("PromptText")?.GetComponent<TMP_Text>();
                    if (promptTextTMP == null)
                        promptTextLegacy = prompt.Find("PromptText")?.GetComponent<Text>();
                }
            }
        }

        // 应用中文字体
        if (countTextTMP != null) ChineseFontProvider.ApplyFont(countTextTMP);
        if (promptTextTMP != null) ChineseFontProvider.ApplyFont(promptTextTMP);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ══════════════════════════════════════════════════════════════
    //  公开 API
    // ══════════════════════════════════════════════════════════════

    /// <summary>更新碎片计数</summary>
    public void UpdateCount(int current, int total)
    {
        _current = current;
        _total = total;

        string text = string.Format(countFormat, current, total);
        if (countTextTMP != null) countTextTMP.text = text;
        else if (countTextLegacy != null) countTextLegacy.text = text;
    }

    /// <summary>播放收集脉冲动画</summary>
    public void PlayCollectPulse()
    {
        if (fragmentIcon != null)
        {
            fragmentIcon.transform.DOScale(pulseScale, pulseDuration * 0.5f)
                .SetEase(Ease.OutQuad)
                .SetUpdate(true)
                .OnComplete(() =>
                    fragmentIcon.transform.DOScale(1f, pulseDuration * 0.5f)
                        .SetEase(Ease.InQuad)
                        .SetUpdate(true));
        }
    }

    /// <summary>显示交互提示</summary>
    public void ShowInteractionPrompt(string text)
    {
        if (interactionPrompt == null) return;
        interactionPrompt.SetActive(true);
        if (promptTextTMP != null) promptTextTMP.text = text;
        else if (promptTextLegacy != null) promptTextLegacy.text = text;
    }

    /// <summary>隐藏交互提示</summary>
    public void HideInteractionPrompt()
    {
        if (interactionPrompt != null)
            interactionPrompt.SetActive(false);
    }

    /// <summary>设置整体可见性</summary>
    public void SetVisible(bool visible)
    {
        gameObject.SetActive(visible);
    }
}
