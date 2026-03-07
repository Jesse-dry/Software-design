using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using TMPro;
using System;
using System.Collections.Generic;

/// <summary>
/// HUD 系统 — 数值条 + 状态指示器（挂在 UIManager 上）。
/// 
/// 管理两类 HUD 元素：
///   A. 数值条（如混乱值、理性值）— 水平进度条 + 数值变化飘字
///   B. 状态灯（如庭审 NPC 是否被说服）— 图标 + 开关状态
/// 
/// 数值条支持多种填充效果：
///   - SmoothLerp: 平滑插值过渡
///   - Pulse: 变化时脉冲闪烁
///   - Glitch: 不稳定抖动（适合"混乱值"）
/// 
/// 使用示例：
///   UIManager.Instance.HUD.SetValue("chaos", 0.7f);
///   UIManager.Instance.HUD.SetIndicator("npc_alice", true);
///   UIManager.Instance.HUD.FlashValue("reason", 0.3f, "+3 理性");
/// </summary>
public class HUDSystem : MonoBehaviour
{
    // ── Inspector 配置 ───────────────────────────────────────────
    [Header("== HUD 整体 ==")]
    [Tooltip("HUD 容器 Prefab（可选，留空则运行时创建骨架）")]
    [SerializeField] private GameObject hudPrefab;

    [Tooltip("HUD 的初始可见性")]
    [SerializeField] private bool visibleOnStart = false;

    [Header("== 数值条样式 ==")]
    [Tooltip("值条填充效果")]
    [SerializeField] private ValueBarStyle barStyle = ValueBarStyle.SmoothLerp;

    [Tooltip("填充过渡时长")]
    [SerializeField] private float barTweenDuration = 0.5f;

    [Tooltip("填充条颜色（低→高渐变）")]
    [SerializeField] private Gradient barColorGradient;

    [Tooltip("变化飘字颜色")]
    [SerializeField] private Color deltaTextColor = new Color(0.9f, 0.85f, 0.4f, 1f);

    [Header("== 状态灯样式 ==")]
    [Tooltip("状态灯 OFF 颜色")]
    [SerializeField] private Color indicatorOff = new Color(0.15f, 0.15f, 0.2f, 0.8f);
    [Tooltip("状态灯 ON 颜色")]
    [SerializeField] private Color indicatorOn = new Color(0.3f, 0.95f, 0.5f, 1f);
    [Tooltip("状态灯切换动画时长")]
    [SerializeField] private float indicatorAnimDuration = 0.4f;

    // ── 数据绑定 ─────────────────────────────────────────────────
    // 运行时注册的值条: key → ValueBarBinding
    private readonly Dictionary<string, ValueBarBinding> valueBars = new();
    // 运行时注册的状态灯: key → IndicatorBinding
    private readonly Dictionary<string, IndicatorBinding> indicators = new();

    private RectTransform hudRoot;
    private CanvasGroup hudCanvasGroup;

    /// <summary>数值变化事件 (key, oldValue, newValue)</summary>
    public event Action<string, float, float> OnValueChanged;
    /// <summary>状态灯变化事件 (key, isOn)</summary>
    public event Action<string, bool> OnIndicatorChanged;

    // ══════════════════════════════════════════════════════════════
    //  初始化
    // ══════════════════════════════════════════════════════════════

    public void Initialize()
    {
        // 清理旧状态（场景切换时会重新初始化）
        valueBars.Clear();
        indicators.Clear();
        hudRoot = null;
        hudCanvasGroup = null;

        var hudLayer = UIManager.Instance?.hudLayer;
        if (hudLayer == null) return;

        if (hudPrefab != null)
        {
            var inst = Instantiate(hudPrefab, hudLayer);
            inst.name = "HUD";
            hudRoot = inst.GetComponent<RectTransform>();
        }
        else
        {
            var go = new GameObject("HUD");
            go.transform.SetParent(hudLayer, false);
            hudRoot = go.AddComponent<RectTransform>();
            hudRoot.anchorMin = Vector2.zero;
            hudRoot.anchorMax = Vector2.one;
            hudRoot.offsetMin = Vector2.zero;
            hudRoot.offsetMax = Vector2.zero;
        }

        hudCanvasGroup = hudRoot.GetComponent<CanvasGroup>();
        if (hudCanvasGroup == null)
            hudCanvasGroup = hudRoot.gameObject.AddComponent<CanvasGroup>();

        SetVisible(visibleOnStart);

        // 初始化默认渐变（如果未在 Inspector 设置）
        if (barColorGradient == null || barColorGradient.colorKeys.Length == 0)
        {
            barColorGradient = new Gradient();
            barColorGradient.SetKeys(
                new[] {
                    new GradientColorKey(new Color(0.2f, 0.6f, 0.9f), 0f),   // 低值：冷蓝
                    new GradientColorKey(new Color(0.9f, 0.3f, 0.3f), 1f),   // 高值：暗红
                },
                new[] {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(1f, 1f),
                }
            );
        }
    }

    // ══════════════════════════════════════════════════════════════
    //  公开 API — 数值条
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// 注册一条数值条。需传入已存在的 UI 元素引用。
    /// barFill: Slider 或 Image(Filled); label: 可选 TMP 标签。
    /// </summary>
    public void RegisterValueBar(string key, Image barFill, TMP_Text label = null,
        TMP_Text valueText = null)
    {
        valueBars[key] = new ValueBarBinding
        {
            fillImage = barFill,
            label = label,
            valueText = valueText,
            currentValue = 0f,
        };
    }

    /// <summary>设置指定数值条的值 (0~1 归一化)</summary>
    public void SetValue(string key, float normalizedValue, string deltaText = null)
    {
        normalizedValue = Mathf.Clamp01(normalizedValue);

        if (!valueBars.TryGetValue(key, out var binding)) return;

        float oldValue = binding.currentValue;
        binding.currentValue = normalizedValue;
        OnValueChanged?.Invoke(key, oldValue, normalizedValue);

        // 更新颜色
        if (binding.fillImage != null)
            binding.fillImage.color = barColorGradient.Evaluate(normalizedValue);

        // 动画填充
        AnimateBar(binding, normalizedValue);

        // 数值飘字
        if (!string.IsNullOrEmpty(deltaText))
            SpawnDeltaText(binding, deltaText);

        // 更新数值文本
        if (binding.valueText != null)
            binding.valueText.text = $"{(normalizedValue * 100):0}%";
    }

    /// <summary>快捷方法：设置值并显示增减量</summary>
    public void FlashValue(string key, float newNormalized, string deltaLabel)
    {
        SetValue(key, newNormalized, deltaLabel);
    }

    // ══════════════════════════════════════════════════════════════
    //  公开 API — 状态灯
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// 注册一个状态灯。icon: Image 组件。
    /// </summary>
    public void RegisterIndicator(string key, Image icon, TMP_Text label = null)
    {
        indicators[key] = new IndicatorBinding
        {
            icon = icon,
            label = label,
            isOn = false,
        };

        if (icon != null)
            icon.color = indicatorOff;
    }

    /// <summary>设置状态灯开关</summary>
    public void SetIndicator(string key, bool isOn)
    {
        if (!indicators.TryGetValue(key, out var binding)) return;
        if (binding.isOn == isOn) return;

        binding.isOn = isOn;
        OnIndicatorChanged?.Invoke(key, isOn);

        Color target = isOn ? indicatorOn : indicatorOff;
        if (binding.icon != null)
        {
            binding.icon.DOColor(target, indicatorAnimDuration)
                .SetUpdate(true);

            // 亮灯时加一个脉冲缩放
            if (isOn)
            {
                binding.icon.transform.DOScale(1.3f, indicatorAnimDuration * 0.5f)
                    .SetUpdate(true)
                    .SetLoops(2, LoopType.Yoyo);
            }
        }
    }

    /// <summary>批量设置多个状态灯</summary>
    public void SetIndicators(Dictionary<string, bool> states)
    {
        foreach (var kv in states)
            SetIndicator(kv.Key, kv.Value);
    }

    // ══════════════════════════════════════════════════════════════
    //  公开 API — 显隐
    // ══════════════════════════════════════════════════════════════

    public void SetVisible(bool visible, float fadeDur = 0.3f)
    {
        if (hudCanvasGroup == null) return;
        float target = visible ? 1f : 0f;
        hudCanvasGroup.DOFade(target, fadeDur).SetUpdate(true);
        hudCanvasGroup.interactable = visible;
        hudCanvasGroup.blocksRaycasts = visible;
    }

    public bool IsVisible => hudCanvasGroup != null && hudCanvasGroup.alpha > 0.5f;

    // ══════════════════════════════════════════════════════════════
    //  动画内部
    // ══════════════════════════════════════════════════════════════

    private void AnimateBar(ValueBarBinding binding, float target)
    {
        if (binding.fillImage == null) return;
        binding.tween?.Kill();

        switch (barStyle)
        {
            case ValueBarStyle.SmoothLerp:
                binding.tween = binding.fillImage.DOFillAmount(target, barTweenDuration)
                    .SetEase(Ease.OutQuad).SetUpdate(true);
                break;

            case ValueBarStyle.Pulse:
                binding.tween = DOTween.Sequence()
                    .Append(binding.fillImage.DOFillAmount(target, barTweenDuration * 0.6f)
                        .SetEase(Ease.OutQuad))
                    .Append(binding.fillImage.transform.DOScale(1.05f, 0.1f))
                    .Append(binding.fillImage.transform.DOScale(1f, 0.1f))
                    .SetUpdate(true);
                break;

            case ValueBarStyle.Glitch:
                StartCoroutine(GlitchBarCoroutine(binding, target));
                break;
        }
    }

    private System.Collections.IEnumerator GlitchBarCoroutine(ValueBarBinding binding, float target)
    {
        float elapsed = 0f;
        float dur = barTweenDuration;
        float startVal = binding.fillImage.fillAmount;
        while (elapsed < dur)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / dur;
            float val = Mathf.Lerp(startVal, target, t);
            // 加入随机抖动
            val += UnityEngine.Random.Range(-0.05f, 0.05f);
            binding.fillImage.fillAmount = Mathf.Clamp01(val);
            yield return null;
        }
        binding.fillImage.fillAmount = target;
    }

    private void SpawnDeltaText(ValueBarBinding binding, string text)
    {
        if (binding.fillImage == null) return;

        var go = new GameObject("DeltaText");
        go.transform.SetParent(binding.fillImage.transform.parent, false);
        var rect = go.AddComponent<RectTransform>();
        rect.anchoredPosition = new Vector2(0, 30);
        rect.sizeDelta = new Vector2(200, 30);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = 18;
        tmp.color = deltaTextColor;
        tmp.alignment = TextAlignmentOptions.Center;
        ChineseFontProvider.ApplyFont(tmp);

        var cg = go.AddComponent<CanvasGroup>();
        DOTween.Sequence()
            .Append(rect.DOAnchorPosY(60f, 1f).SetEase(Ease.OutQuad))
            .Join(cg.DOFade(0f, 1f).SetEase(Ease.InQuad))
            .SetUpdate(true)
            .OnComplete(() => Destroy(go));
    }

    // ══════════════════════════════════════════════════════════════
    //  内部数据结构
    // ══════════════════════════════════════════════════════════════

    private class ValueBarBinding
    {
        public Image fillImage;
        public TMP_Text label;
        public TMP_Text valueText;
        public float currentValue;
        public Tween tween;
    }

    private class IndicatorBinding
    {
        public Image icon;
        public TMP_Text label;
        public bool isOn;
    }
}

/// <summary>数值条填充效果</summary>
public enum ValueBarStyle
{
    [Tooltip("平滑插值过渡")]
    SmoothLerp,
    [Tooltip("变化时脉冲闪烁")]
    Pulse,
    [Tooltip("不稳定抖动（适合混乱值）")]
    Glitch,
}
