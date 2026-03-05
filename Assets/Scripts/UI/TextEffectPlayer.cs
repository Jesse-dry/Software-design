using UnityEngine;
using TMPro;
using DG.Tweening;
using System.Collections;

/// <summary>
/// 通用文字特效组件 — 挂在任何有 TMP_Text 的 GameObject 上。
/// 
/// 在 Inspector 中选择效果类型和参数，调用 Play() 即可播放。
/// 也可被其他系统（如 DialoguePlayer / Toast）调用。
/// 
/// 效果类型：
///   - Typewriter: 逐字显示（打字机）
///   - Decode: 随机字符逐字还原（黑客风格）
///   - GlitchLoop: 持续 glitch 顶点抖动
///   - Wave: 文字波浪起伏
///   - FadeInPerChar: 每个字符依次淡入
///   - Shake: 整体震动
///   - ChromaticSplit: RGB 分离效果
/// 
/// 使用示例：
///   textEffect.Play("这是一段文字");
///   textEffect.Play("这是一段文字", TextEffectType.Decode);
///   textEffect.Stop();
/// </summary>
[RequireComponent(typeof(TMP_Text))]
public class TextEffectPlayer : MonoBehaviour
{
    // ── Inspector 配置 ───────────────────────────────────────────
    [Header("== 效果类型 ==")]
    [Tooltip("默认特效类型")]
    [SerializeField] private TextEffectType effectType = TextEffectType.Typewriter;

    [Header("== Typewriter ==")]
    [Tooltip("每个字符的间隔（秒）")]
    [SerializeField] private float typewriterCharDelay = 0.04f;
    [Tooltip("是否播放打字音效（需要 AudioSource）")]
    [SerializeField] private bool typewriterSfx = false;

    [Header("== Decode ==")]
    [Tooltip("解码用随机字符集")]
    [SerializeField] private string decodeChars = "!@#$%^&*()_+-=[]{}|;':,.<>/?0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    [Tooltip("每个字符解码尝试时长")]
    [SerializeField] private float decodeCharDuration = 0.4f;
    [Tooltip("解码刷新频率")]
    [SerializeField] private float decodeRefreshRate = 0.04f;

    [Header("== Glitch ==")]
    [Tooltip("Glitch 触发概率（每帧）")]
    [SerializeField] [Range(0f, 1f)] private float glitchChance = 0.08f;
    [Tooltip("Glitch 位移强度")]
    [SerializeField] private float glitchIntensity = 3f;

    [Header("== Wave ==")]
    [Tooltip("波浪幅度（像素）")]
    [SerializeField] private float waveAmplitude = 5f;
    [Tooltip("波浪速度")]
    [SerializeField] private float waveSpeed = 3f;
    [Tooltip("波浪相位偏移（字符间）")]
    [SerializeField] private float wavePhaseOffset = 0.5f;

    [Header("== FadeInPerChar ==")]
    [Tooltip("每字符淡入时长")]
    [SerializeField] private float fadePerCharDuration = 0.15f;
    [Tooltip("字符间淡入间隔")]
    [SerializeField] private float fadePerCharDelay = 0.03f;

    [Header("== Shake ==")]
    [Tooltip("震动强度")]
    [SerializeField] private float shakeStrength = 3f;
    [Tooltip("震动频率（次/秒）")]
    [SerializeField] private int shakeVibrato = 20;
    [Tooltip("震动时长")]
    [SerializeField] private float shakeDuration = 0.5f;

    [Header("== ChromaticSplit ==")]
    [Tooltip("RGB 分离偏移量")]
    [SerializeField] private float chromaOffset = 2f;

    // ── 引用 ─────────────────────────────────────────────────────
    private TMP_Text tmpText;
    private Coroutine activeCoroutine;
    private bool isPlaying;

    /// <summary>当前是否正在播放效果</summary>
    public bool IsPlaying => isPlaying;

    /// <summary>效果播放完成回调</summary>
    public event System.Action OnEffectComplete;

    private void Awake()
    {
        tmpText = GetComponent<TMP_Text>();
    }

    // ══════════════════════════════════════════════════════════════
    //  公开 API
    // ══════════════════════════════════════════════════════════════

    /// <summary>播放指定文字的效果（使用 Inspector 中设置的类型）</summary>
    public void Play(string content)
    {
        Play(content, effectType);
    }

    /// <summary>播放指定文字的效果，覆盖类型</summary>
    public void Play(string content, TextEffectType overrideType)
    {
        Stop();
        tmpText.text = content;
        activeCoroutine = StartCoroutine(RunEffect(overrideType));
    }

    /// <summary>停止当前效果，立即显示完整文字</summary>
    public void Stop()
    {
        if (activeCoroutine != null)
        {
            StopCoroutine(activeCoroutine);
            activeCoroutine = null;
        }
        isPlaying = false;
        // 恢复完整文字
        if (tmpText != null)
        {
            tmpText.maxVisibleCharacters = tmpText.text.Length;
            tmpText.ForceMeshUpdate();
            RestoreVertices();
        }
    }

    /// <summary>跳过当前效果（立即完成）</summary>
    public void Skip()
    {
        Stop();
        OnEffectComplete?.Invoke();
    }

    // ══════════════════════════════════════════════════════════════
    //  效果协程
    // ══════════════════════════════════════════════════════════════

    private IEnumerator RunEffect(TextEffectType type)
    {
        isPlaying = true;

        switch (type)
        {
            case TextEffectType.Typewriter:
                yield return TypewriterEffect();
                break;
            case TextEffectType.Decode:
                yield return DecodeEffect();
                break;
            case TextEffectType.GlitchLoop:
                yield return GlitchLoopEffect(); // 无限循环，需手动 Stop
                break;
            case TextEffectType.Wave:
                yield return WaveEffect(); // 无限循环
                break;
            case TextEffectType.FadeInPerChar:
                yield return FadeInPerCharEffect();
                break;
            case TextEffectType.Shake:
                yield return ShakeEffect();
                break;
            case TextEffectType.ChromaticSplit:
                yield return ChromaticSplitEffect(); // 无限循环
                break;
        }

        isPlaying = false;
        OnEffectComplete?.Invoke();
    }

    // ── Typewriter ───────────────────────────────────────────────
    private IEnumerator TypewriterEffect()
    {
        tmpText.ForceMeshUpdate();
        int total = tmpText.textInfo.characterCount;
        tmpText.maxVisibleCharacters = 0;

        for (int i = 0; i <= total; i++)
        {
            tmpText.maxVisibleCharacters = i;
            yield return new WaitForSecondsRealtime(typewriterCharDelay);
        }
    }

    // ── Decode ───────────────────────────────────────────────────
    private IEnumerator DecodeEffect()
    {
        string original = tmpText.text;
        int len = original.Length;
        char[] display = new char[len];
        for (int i = 0; i < len; i++) display[i] = ' ';

        tmpText.maxVisibleCharacters = len;

        for (int i = 0; i < len; i++)
        {
            if (original[i] == ' ')
            {
                display[i] = ' ';
                tmpText.text = new string(display);
                continue;
            }

            float timer = 0f;
            while (timer < decodeCharDuration)
            {
                display[i] = decodeChars[Random.Range(0, decodeChars.Length)];
                tmpText.text = new string(display);
                timer += decodeRefreshRate;
                yield return new WaitForSecondsRealtime(decodeRefreshRate);
            }
            display[i] = original[i];
            tmpText.text = new string(display);
        }

        tmpText.text = original;
    }

    // ── GlitchLoop ───────────────────────────────────────────────
    private IEnumerator GlitchLoopEffect()
    {
        tmpText.maxVisibleCharacters = tmpText.text.Length;

        while (true)
        {
            tmpText.ForceMeshUpdate();
            var textInfo = tmpText.textInfo;

            if (Random.value < glitchChance && textInfo.characterCount > 0)
            {
                int idx = Random.Range(0, textInfo.characterCount);
                if (textInfo.characterInfo[idx].isVisible)
                {
                    int matIdx = textInfo.characterInfo[idx].materialReferenceIndex;
                    int vertIdx = textInfo.characterInfo[idx].vertexIndex;
                    var verts = textInfo.meshInfo[matIdx].vertices;

                    Vector3 offset = new Vector3(
                        Random.Range(-glitchIntensity, glitchIntensity),
                        Random.Range(-glitchIntensity * 0.5f, glitchIntensity * 0.5f), 0);

                    for (int j = 0; j < 4; j++)
                        verts[vertIdx + j] += offset;

                    tmpText.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices);
                }
            }

            yield return new WaitForSecondsRealtime(0.05f);

            // 每隔一段时间恢复
            if (Random.value < 0.3f)
            {
                tmpText.ForceMeshUpdate();
            }
        }
    }

    // ── Wave ─────────────────────────────────────────────────────
    private IEnumerator WaveEffect()
    {
        tmpText.maxVisibleCharacters = tmpText.text.Length;

        while (true)
        {
            tmpText.ForceMeshUpdate();
            var textInfo = tmpText.textInfo;

            for (int i = 0; i < textInfo.characterCount; i++)
            {
                if (!textInfo.characterInfo[i].isVisible) continue;

                int matIdx = textInfo.characterInfo[i].materialReferenceIndex;
                int vertIdx = textInfo.characterInfo[i].vertexIndex;
                var verts = textInfo.meshInfo[matIdx].vertices;

                float yOffset = Mathf.Sin(Time.unscaledTime * waveSpeed + i * wavePhaseOffset) * waveAmplitude;
                Vector3 offset = new Vector3(0, yOffset, 0);

                for (int j = 0; j < 4; j++)
                    verts[vertIdx + j] += offset;
            }

            for (int i = 0; i < textInfo.meshInfo.Length; i++)
            {
                textInfo.meshInfo[i].mesh.vertices = textInfo.meshInfo[i].vertices;
                tmpText.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices);
            }

            yield return null;
        }
    }

    // ── FadeInPerChar ────────────────────────────────────────────
    private IEnumerator FadeInPerCharEffect()
    {
        tmpText.maxVisibleCharacters = tmpText.text.Length;
        tmpText.ForceMeshUpdate();
        var textInfo = tmpText.textInfo;

        // 先设所有字符透明
        for (int i = 0; i < textInfo.characterCount; i++)
        {
            if (!textInfo.characterInfo[i].isVisible) continue;
            SetCharAlpha(textInfo, i, 0);
        }
        tmpText.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);

        // 逐字淡入
        for (int i = 0; i < textInfo.characterCount; i++)
        {
            if (!textInfo.characterInfo[i].isVisible) continue;

            float elapsed = 0f;
            while (elapsed < fadePerCharDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                byte alpha = (byte)(Mathf.Clamp01(elapsed / fadePerCharDuration) * 255);
                SetCharAlpha(textInfo, i, alpha);
                tmpText.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);
                yield return null;
            }
            SetCharAlpha(textInfo, i, 255);
            tmpText.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);

            if (fadePerCharDelay > 0)
                yield return new WaitForSecondsRealtime(fadePerCharDelay);
        }
    }

    // ── Shake ────────────────────────────────────────────────────
    private IEnumerator ShakeEffect()
    {
        tmpText.maxVisibleCharacters = tmpText.text.Length;
        tmpText.rectTransform.DOShakePosition(shakeDuration, shakeStrength, shakeVibrato)
            .SetUpdate(true);
        yield return new WaitForSecondsRealtime(shakeDuration);
    }

    // ── ChromaticSplit ───────────────────────────────────────────
    private IEnumerator ChromaticSplitEffect()
    {
        tmpText.maxVisibleCharacters = tmpText.text.Length;

        while (true)
        {
            tmpText.ForceMeshUpdate();
            var textInfo = tmpText.textInfo;

            for (int i = 0; i < textInfo.characterCount; i++)
            {
                if (!textInfo.characterInfo[i].isVisible) continue;

                int matIdx = textInfo.characterInfo[i].materialReferenceIndex;
                int vertIdx = textInfo.characterInfo[i].vertexIndex;
                var colors = textInfo.meshInfo[matIdx].colors32;

                // 奇偶字符做不同色偏
                float timeOsc = Mathf.Sin(Time.unscaledTime * 5f + i) * 0.5f + 0.5f;
                Color32 tint = i % 2 == 0
                    ? new Color32((byte)(255 * timeOsc), 100, (byte)(255 * (1 - timeOsc)), 255)
                    : new Color32(100, (byte)(255 * (1 - timeOsc)), (byte)(255 * timeOsc), 255);

                for (int j = 0; j < 4; j++)
                    colors[vertIdx + j] = tint;
            }

            tmpText.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);
            yield return null;
        }
    }

    // ══════════════════════════════════════════════════════════════
    //  Helpers
    // ══════════════════════════════════════════════════════════════

    private void SetCharAlpha(TMP_TextInfo textInfo, int charIndex, byte alpha)
    {
        var info = textInfo.characterInfo[charIndex];
        int matIdx = info.materialReferenceIndex;
        int vertIdx = info.vertexIndex;
        var colors = textInfo.meshInfo[matIdx].colors32;
        for (int j = 0; j < 4; j++)
        {
            var c = colors[vertIdx + j];
            c.a = alpha;
            colors[vertIdx + j] = c;
        }
    }

    private void RestoreVertices()
    {
        if (tmpText == null) return;
        tmpText.ForceMeshUpdate();
    }
}

/// <summary>文字特效类型枚举</summary>
public enum TextEffectType
{
    [Tooltip("逐字显示（打字机）")]
    Typewriter,
    [Tooltip("随机字符逐字还原（黑客风格）")]
    Decode,
    [Tooltip("持续 glitch 顶点抖动")]
    GlitchLoop,
    [Tooltip("文字波浪起伏")]
    Wave,
    [Tooltip("每个字符依次淡入")]
    FadeInPerChar,
    [Tooltip("整体震动")]
    Shake,
    [Tooltip("RGB 分离/色偏效果（循环）")]
    ChromaticSplit,
}
