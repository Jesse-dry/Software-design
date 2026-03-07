using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using System;
using System.Collections;

/// <summary>
/// 场景转场系统（挂在 UIManager 上）。
/// 
/// 提供多种转场效果，可在 Inspector 中选择/切换：
///   - FadeBlack：纯黑色淡入淡出（默认）
///   - FadeWhite：白色闪光淡入淡出
///   - GlitchFade：Glitch 干扰 + 淡入淡出
/// 
/// 使用示例：
///   UIManager.Instance.Transition.FadeIn(1f, onComplete);
///   UIManager.Instance.Transition.FadeOut(1f, onComplete);
///   UIManager.Instance.Transition.CrossFade(1f, midAction, 1f, onComplete);
/// </summary>
public class TransitionSystem : MonoBehaviour
{
    // ── Inspector 配置 ───────────────────────────────────────────
    [Header("== 转场配置 ==")]
    [Tooltip("转场所用的全屏 Image（放在 TransitionLayer 下）")]
    [SerializeField] private Image fadeImage;

    [Tooltip("默认转场时长")]
    [SerializeField] private float defaultDuration = 1f;

    [Tooltip("转场效果类型")]
    [SerializeField] private TransitionType transitionType = TransitionType.FadeBlack;

    [Header("== Glitch 转场 ==")]
    [Tooltip("Glitch 强度（仅 GlitchFade 模式）")]
    [SerializeField] private float glitchIntensity = 5f;
    [Tooltip("Glitch 频率")]
    [SerializeField] private float glitchFrequency = 0.05f;
    [Tooltip("Glitch 持续时间")]
    [SerializeField] private float glitchDuration = 0.5f;

    [Header("== 缓动曲线 ==")]
    [Tooltip("淡入缓动")]
    [SerializeField] private Ease fadeInEase = Ease.InQuad;
    [Tooltip("淡出缓动")]
    [SerializeField] private Ease fadeOutEase = Ease.OutQuad;

    // ── 状态 ─────────────────────────────────────────────────────
    private bool isTransitioning;
    public bool IsTransitioning => isTransitioning;

    private Tween currentTween;

    // ══════════════════════════════════════════════════════════════
    //  初始化
    // ══════════════════════════════════════════════════════════════

    public void Initialize()
    {
        if (fadeImage == null)
        {
            // 如果未指定，尝试在 transitionLayer 下查找或创建
            var transLayer = UIManager.Instance?.transitionLayer;
            if (transLayer != null)
            {
                var existing = transLayer.GetComponentInChildren<Image>(true);
                if (existing != null)
                {
                    fadeImage = existing;
                }
                else
                {
                    fadeImage = CreateFadeImage(transLayer);
                }
            }
        }

        if (fadeImage != null)
        {
            SetAlpha(0f);
            fadeImage.gameObject.SetActive(false);
        }
    }

    // ══════════════════════════════════════════════════════════════
    //  公开接口
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// 淡入（画面变暗/不可见）。alpha: 0→1
    /// </summary>
    public void FadeIn(float duration = -1f, Action onComplete = null)
    {
        duration = duration < 0 ? defaultDuration : duration;
        
        switch (transitionType)
        {
            case TransitionType.FadeBlack:
                DoFade(0f, 1f, Color.black, duration, fadeInEase, onComplete);
                break;
            case TransitionType.FadeWhite:
                DoFade(0f, 1f, Color.white, duration, fadeInEase, onComplete);
                break;
            case TransitionType.GlitchFade:
                StartCoroutine(GlitchFadeCoroutine(0f, 1f, duration, onComplete));
                break;
        }
    }

    /// <summary>
    /// 淡出（画面恢复可见）。alpha: 1→0
    /// </summary>
    public void FadeOut(float duration = -1f, Action onComplete = null)
    {
        duration = duration < 0 ? defaultDuration : duration;

        switch (transitionType)
        {
            case TransitionType.FadeBlack:
                DoFade(1f, 0f, Color.black, duration, fadeOutEase, onComplete);
                break;
            case TransitionType.FadeWhite:
                DoFade(1f, 0f, Color.white, duration, fadeOutEase, onComplete);
                break;
            case TransitionType.GlitchFade:
                StartCoroutine(GlitchFadeCoroutine(1f, 0f, duration, onComplete));
                break;
        }
    }

    /// <summary>
    /// 先淡入 → 执行 midAction → 再淡出（常用于场景切换）。
    /// </summary>
    public void CrossFade(
        float fadeInDur = -1f,
        Action midAction = null,
        float fadeOutDur = -1f,
        Action onComplete = null)
    {
        fadeInDur = fadeInDur < 0 ? defaultDuration : fadeInDur;
        fadeOutDur = fadeOutDur < 0 ? defaultDuration : fadeOutDur;

        FadeIn(fadeInDur, () =>
        {
            midAction?.Invoke();
            FadeOut(fadeOutDur, onComplete);
        });
    }

    /// <summary>强制立即设为全黑/全透明</summary>
    public void SetBlack()
    {
        if (fadeImage == null) return;
        fadeImage.gameObject.SetActive(true);
        fadeImage.color = Color.black;
    }

    /// <summary>强制立即清除</summary>
    public void SetClear()
    {
        if (fadeImage == null) return;
        fadeImage.color = new Color(0, 0, 0, 0);
        fadeImage.gameObject.SetActive(false);
        isTransitioning = false;
    }

    /// <summary>动态切换转场类型</summary>
    public void SetTransitionType(TransitionType type)
    {
        transitionType = type;
    }

    /// <summary>
    /// 运行时批量配置转场参数（供 MemorySceneSetup 等场景 Setup 脚本调用）。
    /// 负值 / Ease.Unset 表示保留当前值不变。
    /// </summary>
    /// <param name="type">转场效果类型</param>
    /// <param name="duration">默认淡入淡出时长（&gt;0 才生效）</param>
    /// <param name="inEase">淡入缓动（Ease.Unset 保留原值）</param>
    /// <param name="outEase">淡出缓动（Ease.Unset 保留原值）</param>
    /// <param name="newGlitchIntensity">Glitch 强度（&lt;0 保留原值）</param>
    /// <param name="newGlitchFrequency">Glitch 频率（&lt;0 保留原值）</param>
    /// <param name="newGlitchDuration">Glitch 持续时间（&lt;0 保留原值）</param>
    public void Configure(
        TransitionType type,
        float          duration           = -1f,
        Ease           inEase             = Ease.Unset,
        Ease           outEase            = Ease.Unset,
        float          newGlitchIntensity = -1f,
        float          newGlitchFrequency = -1f,
        float          newGlitchDuration  = -1f)
    {
        transitionType = type;
        if (duration           > 0f)          defaultDuration  = duration;
        if (inEase             != Ease.Unset) fadeInEase       = inEase;
        if (outEase            != Ease.Unset) fadeOutEase      = outEase;
        if (newGlitchIntensity >= 0f)         glitchIntensity  = newGlitchIntensity;
        if (newGlitchFrequency >= 0f)         glitchFrequency  = newGlitchFrequency;
        if (newGlitchDuration  >= 0f)         glitchDuration   = newGlitchDuration;
    }

    // ══════════════════════════════════════════════════════════════
    //  内部实现
    // ══════════════════════════════════════════════════════════════

    private void DoFade(float from, float to, Color baseColor, float duration, Ease ease, Action onComplete)
    {
        if (fadeImage == null) { onComplete?.Invoke(); return; }

        currentTween?.Kill();
        isTransitioning = true;
        fadeImage.gameObject.SetActive(true);

        baseColor.a = from;
        fadeImage.color = baseColor;

        currentTween = fadeImage.DOFade(to, duration)
            .SetEase(ease)
            .SetUpdate(true) // 不受 Time.timeScale 影响
            .OnComplete(() =>
            {
                isTransitioning = false;
                if (to <= 0f)
                    fadeImage.gameObject.SetActive(false);
                onComplete?.Invoke();
            });
    }

    private IEnumerator GlitchFadeCoroutine(float from, float to, float duration, Action onComplete)
    {
        if (fadeImage == null) { onComplete?.Invoke(); yield break; }

        isTransitioning = true;
        fadeImage.gameObject.SetActive(true);

        Color c = Color.black;
        c.a = from;
        fadeImage.color = c;

        // Glitch 抖动阶段
        float glitchTimer = 0f;
        while (glitchTimer < glitchDuration)
        {
            glitchTimer += glitchFrequency;
            float flicker = Mathf.Lerp(from, to, glitchTimer / glitchDuration);
            flicker += UnityEngine.Random.Range(-0.3f, 0.3f);
            flicker = Mathf.Clamp01(flicker);
            c.a = flicker;

            // RGB 偏移效果（通过轻微位移模拟）
            fadeImage.rectTransform.anchoredPosition = new Vector2(
                UnityEngine.Random.Range(-glitchIntensity, glitchIntensity),
                UnityEngine.Random.Range(-glitchIntensity, glitchIntensity));

            fadeImage.color = c;
            yield return new WaitForSecondsRealtime(glitchFrequency);
        }

        // 复位位移
        fadeImage.rectTransform.anchoredPosition = Vector2.zero;

        // 平滑过渡到目标
        float remaining = duration - glitchDuration;
        if (remaining > 0f)
        {
            c.a = fadeImage.color.a;
            fadeImage.color = c;
            currentTween = fadeImage.DOFade(to, remaining)
                .SetUpdate(true)
                .OnComplete(() =>
                {
                    isTransitioning = false;
                    if (to <= 0f) fadeImage.gameObject.SetActive(false);
                    onComplete?.Invoke();
                });
        }
        else
        {
            c.a = to;
            fadeImage.color = c;
            isTransitioning = false;
            if (to <= 0f) fadeImage.gameObject.SetActive(false);
            onComplete?.Invoke();
        }
    }

    private void SetAlpha(float a)
    {
        if (fadeImage == null) return;
        var c = fadeImage.color;
        c.a = a;
        fadeImage.color = c;
    }

    private Image CreateFadeImage(Transform parent)
    {
        var go = new GameObject("FadeImage");
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = new Color(0, 0, 0, 0);
        img.raycastTarget = true;
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return img;
    }

    private void OnDestroy()
    {
        currentTween?.Kill();
    }
}

/// <summary>转场效果枚举，可在 Inspector 中切换</summary>
public enum TransitionType
{
    [Tooltip("纯黑淡入淡出")]
    FadeBlack,
    [Tooltip("白色闪光淡入淡出")]
    FadeWhite,
    [Tooltip("Glitch 干扰 + 淡入淡出")]
    GlitchFade,
}
