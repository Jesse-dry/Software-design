using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 全局音频管理器（跨场景持久 — 挂在 [MANAGERS] 下）。
///
/// 负责：
///   - 根据场景自动切换 BGM（淡入淡出过渡）
///   - 播放一次性音效（失败 / 胜利等）
///   - 场景切换时自动静音旧场景 MainCamera 上的 AudioSource，防止重复播放
///
/// 使用示例：
///   AudioManager.Instance.PlayFailSFX();
///   AudioManager.Instance.PlayVictorySFX();
///   AudioManager.Instance.StopBGM();
///   AudioManager.Instance.PauseBGM();
///   AudioManager.Instance.ResumeBGM();
///
/// 不需要手动挂载。由 GameBootstrapper 自动创建。
/// </summary>
public class AudioManager : MonoBehaviour
{
    // ══════════════════════════════════════════════════════════════
    //  单例
    // ══════════════════════════════════════════════════════════════

    public static AudioManager Instance { get; private set; }

    // ══════════════════════════════════════════════════════════════
    //  配置
    // ══════════════════════════════════════════════════════════════

    private float bgmVolume = 0.5f;
    private float sfxVolume = 0.8f;
    private float crossFadeDuration = 1.5f;

    // ══════════════════════════════════════════════════════════════
    //  AudioSource
    // ══════════════════════════════════════════════════════════════

    private AudioSource bgmSource;
    private AudioSource sfxSource;

    private Coroutine fadeCoroutine;
    private bool isBGMPaused;

    // ══════════════════════════════════════════════════════════════
    //  场景 → BGM 资源路径映射（Resources 相对路径，不含扩展名）
    // ══════════════════════════════════════════════════════════════

    private static readonly Dictionary<string, string> SceneBGMMap = new()
    {
        { "MainMenu",       "Audio/神秘稍轻快" },
        { "CutsceneScene",  "Audio/宏大诡异（记忆空间）" },
        { "Memory",         "Audio/宏大诡异（记忆空间）" },
        { "Abyss",          "Audio/宏大诡异（记忆空间）" },
        { "Corridor",       "Audio/空灵紧张" },
        { "PipeRoom",       "Audio/诡异安静" },
        { "ServerRoom",     "Audio/诡异安静" },
        { "PipePuzzle",     "Audio/紧张激进" },
        { "DecodeGame",     "Audio/紧张激进" },
        { "Court",          "Audio/宏大紧张（庭审）" },
    };

    // SFX 缓存（避免重复 Resources.Load）
    private readonly Dictionary<string, AudioClip> sfxCache = new();

    // ══════════════════════════════════════════════════════════════
    //  生命周期
    // ══════════════════════════════════════════════════════════════

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        if (transform.parent == null)
            DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// 由 GameBootstrapper 在创建 AudioManager 后调用，仅执行一次。
    /// </summary>
    public void Initialize()
    {
        // BGM AudioSource
        bgmSource = gameObject.AddComponent<AudioSource>();
        bgmSource.loop = true;
        bgmSource.playOnAwake = false;
        bgmSource.volume = bgmVolume;

        // SFX AudioSource
        sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.loop = false;
        sfxSource.playOnAwake = false;
        sfxSource.volume = sfxVolume;

        // 监听场景加载
        SceneManager.sceneLoaded += OnSceneLoaded;

        Debug.Log("[AudioManager] 初始化完成。");
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (Instance == this) Instance = null;
    }

    // ══════════════════════════════════════════════════════════════
    //  场景加载回调 → 自动切换 BGM
    // ══════════════════════════════════════════════════════════════

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // 等一帧，确保场景所有 Start() 都执行完毕
        // （sceneLoaded 在 Awake/OnEnable 之后、Start 之前触发，
        //   若 AudioSource 在 Start() 里 Play()，不延迟会错过）
        StartCoroutine(HandleSceneLoaded(scene.name));
    }

    private IEnumerator HandleSceneLoaded(string sceneName)
    {
        // 等一帧，让场景所有 Start() 运行完毕
        yield return null;

        // 停止残留的一次性音效（如失败/胜利 SFX，防止跨场景叠播）
        sfxSource.Stop();

        // 停止场景内所有循环播放的 AudioSource（BGM 特征），防止与 AudioManager 冲突
        MuteSceneAudioSources();

        if (SceneBGMMap.TryGetValue(sceneName, out string bgmPath))
        {
            var clip = Resources.Load<AudioClip>(bgmPath);
            if (clip != null)
            {
                PlayBGM(clip);
            }
            else
            {
                Debug.LogWarning($"[AudioManager] 未找到 BGM 资源: {bgmPath}");
                StopBGM();
            }
        }
        else
        {
            // 未配置 BGM 的场景（Boot 等），淡出当前 BGM
            StopBGMWithFade();
        }
    }

    /// <summary>
    /// 静音场景中所有循环播放（loop=true）的 AudioSource，避免与 AudioManager 的 BGM 双重播放。
    /// 只针对循环源，避免误杀一次性 SFX（如 LightDetection 的抓捕音效）。
    /// </summary>
    private void MuteSceneAudioSources()
    {
        var allSources = FindObjectsByType<AudioSource>(FindObjectsSortMode.None);
        foreach (var src in allSources)
        {
            // 跳过 AudioManager 自身的 AudioSource
            if (src.gameObject == gameObject) continue;
            // 只处理循环播放的 AudioSource（BGM 特征）
            if (!src.loop) continue;

            src.Stop();
            src.enabled = false;
        }
    }

    // ══════════════════════════════════════════════════════════════
    //  BGM 控制
    // ══════════════════════════════════════════════════════════════

    /// <summary>播放 BGM，与当前 BGM 做交叉淡入淡出</summary>
    public void PlayBGM(AudioClip clip)
    {
        if (clip == null) return;

        // 相同曲目不重复播放
        if (bgmSource.clip == clip && bgmSource.isPlaying) return;

        if (fadeCoroutine != null)
            StopCoroutine(fadeCoroutine);

        fadeCoroutine = StartCoroutine(CrossFadeBGM(clip));
    }

    /// <summary>立即停止 BGM</summary>
    public void StopBGM()
    {
        if (fadeCoroutine != null)
            StopCoroutine(fadeCoroutine);
        bgmSource.Stop();
        bgmSource.clip = null;
        isBGMPaused = false;
    }

    /// <summary>淡出后停止 BGM</summary>
    public void StopBGMWithFade()
    {
        if (fadeCoroutine != null)
            StopCoroutine(fadeCoroutine);
        fadeCoroutine = StartCoroutine(FadeOutBGM());
    }

    /// <summary>
    /// 立即停止所有音频（BGM + SFX + 场景 AudioSource）。
    /// 适用于失败/胜利等需要全场静音的时刻。
    /// </summary>
    public void StopAllAudio()
    {
        StopBGM();
        sfxSource.Stop();
        MuteSceneAudioSources();
    }

    /// <summary>暂停 BGM（可恢复）</summary>
    public void PauseBGM()
    {
        if (bgmSource.isPlaying)
        {
            bgmSource.Pause();
            isBGMPaused = true;
        }
    }

    /// <summary>恢复暂停的 BGM</summary>
    public void ResumeBGM()
    {
        if (isBGMPaused)
        {
            bgmSource.UnPause();
            isBGMPaused = false;
        }
    }

    /// <summary>设置 BGM 音量 (0~1)</summary>
    public void SetBGMVolume(float vol)
    {
        bgmVolume = Mathf.Clamp01(vol);
        bgmSource.volume = bgmVolume;
    }

    /// <summary>设置 SFX 音量 (0~1)</summary>
    public void SetSFXVolume(float vol)
    {
        sfxVolume = Mathf.Clamp01(vol);
        sfxSource.volume = sfxVolume;
    }

    // ══════════════════════════════════════════════════════════════
    //  SFX 播放
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// 播放一次性音效。资源从 Resources/Audio/ 加载。
    /// </summary>
    /// <param name="sfxName">音效文件名（不含路径前缀和扩展名）</param>
    public void PlaySFX(string sfxName)
    {
        if (string.IsNullOrEmpty(sfxName)) return;

        if (!sfxCache.TryGetValue(sfxName, out var clip))
        {
            clip = Resources.Load<AudioClip>("Audio/" + sfxName);
            if (clip != null)
                sfxCache[sfxName] = clip;
        }

        if (clip != null)
        {
            sfxSource.PlayOneShot(clip, sfxVolume);
        }
        else
        {
            Debug.LogWarning($"[AudioManager] 未找到 SFX: Audio/{sfxName}");
        }
    }

    /// <summary>播放失败音效</summary>
    public void PlayFailSFX()
    {
        PlaySFX("失败");
    }

    /// <summary>播放胜利音效</summary>
    public void PlayVictorySFX()
    {
        PlaySFX("胜利");
    }

    // ══════════════════════════════════════════════════════════════
    //  BGM 淡入淡出协程
    // ══════════════════════════════════════════════════════════════

    private IEnumerator CrossFadeBGM(AudioClip newClip)
    {
        float halfDur = crossFadeDuration * 0.5f;

        // ── 淡出旧 BGM ──
        if (bgmSource.isPlaying)
        {
            float startVol = bgmSource.volume;
            float timer = 0f;

            while (timer < halfDur)
            {
                timer += Time.unscaledDeltaTime;
                bgmSource.volume = Mathf.Lerp(startVol, 0f, timer / halfDur);
                yield return null;
            }
            bgmSource.Stop();
        }

        // ── 淡入新 BGM ──
        bgmSource.clip = newClip;
        bgmSource.volume = 0f;
        bgmSource.Play();
        isBGMPaused = false;

        {
            float timer = 0f;
            while (timer < halfDur)
            {
                timer += Time.unscaledDeltaTime;
                bgmSource.volume = Mathf.Lerp(0f, bgmVolume, timer / halfDur);
                yield return null;
            }
            bgmSource.volume = bgmVolume;
        }

        fadeCoroutine = null;
    }

    private IEnumerator FadeOutBGM()
    {
        if (!bgmSource.isPlaying)
        {
            fadeCoroutine = null;
            yield break;
        }

        float startVol = bgmSource.volume;
        float timer = 0f;

        while (timer < crossFadeDuration)
        {
            timer += Time.unscaledDeltaTime;
            bgmSource.volume = Mathf.Lerp(startVol, 0f, timer / crossFadeDuration);
            yield return null;
        }

        bgmSource.Stop();
        bgmSource.clip = null;
        fadeCoroutine = null;
    }
}
