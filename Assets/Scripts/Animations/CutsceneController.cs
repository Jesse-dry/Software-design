using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;
using DG.Tweening;
using TMPro;
using System.Collections;

/// <summary>
/// 过场动画控制器（重构版 — 协程驱动，不依赖 stopped 事件）。
///
/// 【流程】
///   1. 正常播放 Timeline 动画
///   2. 轮询检测动画播完（Hold 模式下 stopped 事件永远不触发，所以不能用它）
///   3. Pause director → 冻结末帧画面
///   4. 末帧停留 holdDuration 秒
///   5. modalImage1 全屏淡入，停留 imageDuration 秒
///   6. 淡出图片 + 淡入 myNewModal（标题/正文/按钮）
///   7. 文字特效播放（类型在 Inspector 可选）
///   8. 用户点击按钮 → 跳转 Memory 场景
///
/// 【为什么不用 director.stopped 事件？】
///   DirectorWrapMode.Hold 保持末帧画面不复位，但副作用是 director
///   永远停留在 Playing 状态，stopped 永远不触发。
///   因此改用协程轮询 director.time >= director.duration。
/// </summary>
public class CutsceneController : MonoBehaviour
{
    [Header("过场动画设置")]
    [Tooltip("拖入 VideoPlayer。留空会自动在场景中查找（视频永远优先于 Timeline）")]
    public VideoPlayer videoPlayer;

    [Tooltip("拖入带有 PlayableDirector 的物体。如果场景中只有 VideoPlayer 则本字段可空着")]
    public PlayableDirector director;

    [Tooltip("仅 GameManager 不可用时回退使用的场景名")]
    public string nextSceneName = "Memory";

    [Header("== 末帧停顿 ==")]
    [Tooltip("动画结束后停留在末帧的时长（秒）")]
    [Range(0.1f, 5f)]
    public float holdDuration = 2f;

    [Header("== 全屏图片覆盖 ==")]
    [Tooltip("全屏图片停留时长（秒）")]
    [Range(0.1f, 5f)]
    public float imageDuration = 1f;

    [Tooltip("全屏图片淡入时长（秒）")]
    [Range(0.05f, 1f)]
    public float imageFadeInDuration = 0.3f;

    [Header("== 文字特效 ==")]
    // 文字内容由 prefab 控制，此处仅配置特效类型
    public TextEffectType titleEffectType = TextEffectType.FadeInPerChar;
    public TextEffectType bodyEffectType  = TextEffectType.Typewriter;

    [Header("== Modal 过渡 ==")]
    [Range(0.1f, 1f)]
    public float modalFadeInDuration = 0.5f;

    [Header("== UI 引用（直接拖入，或自动按名称查找）==")]
    [Tooltip("全屏覆盖图片（prefab 中的 modalimage1）")]
    public GameObject modalImage1;

    [Tooltip("Modal 面板（prefab 中的 mynewmodal）")]
    public GameObject myNewModal;

    // ══════════════════════════════════════════════════════════════
    //  生命周期 — 唯一入口
    // ══════════════════════════════════════════════════════════════

    private void Start()
    {
        // ── 查找 VideoPlayer（优先） ─────────────────────────────
        if (videoPlayer == null) videoPlayer = GetComponent<VideoPlayer>();
        if (videoPlayer == null) videoPlayer = GetComponentInChildren<VideoPlayer>(true);
        if (videoPlayer == null) videoPlayer = FindAnyObjectByType<VideoPlayer>(FindObjectsInactive.Include);

        if (videoPlayer != null)
        {
            if (!videoPlayer.gameObject.activeInHierarchy)
                videoPlayer.gameObject.SetActive(true);

            // 确保视频对象位置居中对齐
            var rt = videoPlayer.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchoredPosition = Vector2.zero;
            }
            else
            {
                var t = videoPlayer.transform;
                t.localPosition = new Vector3(0f, 0f, t.localPosition.z);
            }

            Debug.Log($"[Cutscene] 找到 VideoPlayer: {videoPlayer.gameObject.name}, clip={videoPlayer.clip?.name}");
        }

        // ── 查找 PlayableDirector（备用，如果有 VideoPlayer 则不使用） ───────
        if (videoPlayer == null)
        {
            if (director == null) director = GetComponent<PlayableDirector>();
            if (director == null) director = GetComponentInChildren<PlayableDirector>(true);
            if (director == null) director = FindAnyObjectByType<PlayableDirector>(FindObjectsInactive.Include);

            if (director != null)
            {
                if (!director.gameObject.activeInHierarchy)
                    director.gameObject.SetActive(true);
                Debug.Log($"[Cutscene] 找到 PlayableDirector: {director.gameObject.name}, asset={director.playableAsset?.name}");
            }
            else
                Debug.LogError("[Cutscene] 场景中无 VideoPlayer 也无 PlayableDirector！请在 Inspector 中拖入。");
        }

        // ── 查找 UI（Inspector 拖入优先，名称搜索兜底）────────
        if (modalImage1 == null) modalImage1 = FindInScene("modalimage1", "ModalImage1");
        if (myNewModal  == null) myNewModal  = FindInScene("mynewmodal",  "myNewModal", "MyNewModal");

        // ── 隐藏 UI ─────────────────────────────────────────
        if (modalImage1 != null) { modalImage1.SetActive(false); Debug.Log("[Cutscene] modalImage1 已隐藏"); }
        else Debug.LogWarning("[Cutscene] modalImage1 未找到");

        if (myNewModal != null) { myNewModal.SetActive(false); Debug.Log("[Cutscene] myNewModal 已隐藏"); }
        else Debug.LogWarning("[Cutscene] myNewModal 未找到");

        // ── 启动唯一主流程协程 ──────────────────────────────
        StartCoroutine(MainSequence());
    }

    // ══════════════════════════════════════════════════════════════
    //  主流程协程（线性执行，无事件竞争）
    // ══════════════════════════════════════════════════════════════

    private IEnumerator MainSequence()
    {
        // ─────────────────────────────────────────────────────
        //  阶段 A：播放动画 + 等待播完
        //   优先 VideoPlayer，备用 PlayableDirector
        // ─────────────────────────────────────────────────────
        if (videoPlayer != null && videoPlayer.clip != null)
        {
            // ── VideoPlayer 路径 ─────────────────────────────
            double dur = videoPlayer.clip.length;

            // 停止并重置到第 0 帧（防止上一次播放残留状态）
            videoPlayer.Stop();
            videoPlayer.frame = 0;

            // 准备视频（确保解码器就绪）
            videoPlayer.Prepare();
            float prepareTimeout = 5f;
            while (!videoPlayer.isPrepared && prepareTimeout > 0f)
            {
                prepareTimeout -= Time.deltaTime;
                yield return null;
            }

            // 开始播放
            videoPlayer.Play();

            // 等待 isPlaying 变为 true（Play() 不会立刻生效，需要几帧）
            float startTimeout = 2f;
            while (!videoPlayer.isPlaying && startTimeout > 0f)
            {
                startTimeout -= Time.deltaTime;
                yield return null;
            }

            if (!videoPlayer.isPlaying)
            {
                Debug.LogError("[Cutscene] VideoPlayer 未能启动播放，跳过视频阶段");
            }
            else
            {
                Debug.Log($"[Cutscene] ▶ 视频开始播放，时长 {dur:F2}s");

                // 等视频自然播完（time 接近结尾 OR isPlaying 变 false）
                while (videoPlayer.isPlaying && videoPlayer.time < dur - 0.05)
                    yield return null;

                videoPlayer.Pause();
                Debug.Log("[Cutscene] ⏸ 视频播完，已 Pause");
            }
        }
        else if (director != null && director.playableAsset != null)
        {
            // ── PlayableDirector 路径 ────────────────────────
            director.extrapolationMode = DirectorWrapMode.Hold;
            director.Stop();
            director.time = 0;
            director.Play();

            yield return null;
            yield return null;

            double dur = director.duration;
            Debug.Log($"[Cutscene] ▶ Timeline 开始，时长 {dur:F2}s");

            while (director.state == PlayState.Playing && director.time < dur - 0.01)
                yield return null;

            director.Pause();
            Debug.Log("[Cutscene] ⏸ Timeline 播完，Pause 冻结在末帧");
        }
        else
        {
            Debug.LogError("[Cutscene] 无 VideoPlayer 也无 PlayableDirector，跳过动画阶段");
        }

        // ─────────────────────────────────────────────────────
        //  阶段 B：末帧停留
        // ─────────────────────────────────────────────────────
        Debug.Log($"[Cutscene] 末帧停留 {holdDuration}s …");
        yield return new WaitForSeconds(holdDuration);

        // ─────────────────────────────────────────────────────
        //  阶段 C：全屏图片覆盖
        // ─────────────────────────────────────────────────────
        if (modalImage1 == null)
        {
            Debug.LogWarning("[Cutscene] modalImage1 为 null，跳过图片阶段");
        }
        else
        {
            modalImage1.SetActive(true);
            var imgCG = GetOrAddCG(modalImage1);
            imgCG.alpha = 0f;

            yield return imgCG.DOFade(1f, imageFadeInDuration)
                              .SetUpdate(true)
                              .WaitForCompletion();

            Debug.Log($"[Cutscene] 🖼 图片显示，停留 {imageDuration}s …");
            yield return new WaitForSeconds(imageDuration);
        }

        // ─────────────────────────────────────────────────────
        //  阶段 D：过渡到 myNewModal
        // ─────────────────────────────────────────────────────
        if (myNewModal == null)
        {
            Debug.LogWarning("[Cutscene] myNewModal 为 null，直接跳转");
            LoadNextScene();
            yield break;
        }

        myNewModal.SetActive(true);
        SetupModal(myNewModal);

        var modalCG = GetOrAddCG(myNewModal);
        modalCG.alpha = 0f;

        if (modalImage1 != null)
        {
            var imgCG2 = GetOrAddCG(modalImage1);
            yield return DOTween.Sequence()
                .Join(imgCG2.DOFade(0f, modalFadeInDuration))
                .Join(modalCG.DOFade(1f, modalFadeInDuration))
                .SetUpdate(true)
                .WaitForCompletion();
            modalImage1.SetActive(false);
        }
        else
        {
            yield return modalCG.DOFade(1f, modalFadeInDuration)
                                .SetUpdate(true)
                                .WaitForCompletion();
        }

        Debug.Log("[Cutscene] ✅ myNewModal 已显示，等待按钮点击 …");
        // 流程暂停，等待用户按钮
    }

    // ══════════════════════════════════════════════════════════════
    //  Modal 内容配置
    // ══════════════════════════════════════════════════════════════

    private void SetupModal(GameObject modal)
    {
        // 文字内容完全由 prefab 控制，此处只播放特效 + 绑定按钮事件

        // 标题特效（文字内容不改，直接用 prefab 里的原始文字）
        var titleTMP = FindTMP(modal.transform, "Title", "title");
        if (titleTMP != null)
        {
            var fx = titleTMP.GetComponent<TextEffectPlayer>() ?? titleTMP.gameObject.AddComponent<TextEffectPlayer>();
            fx.Play(titleTMP.text, titleEffectType);
        }

        // 正文特效
        var bodyTMP = FindTMP(modal.transform, "Body", "body", "Text", "text");
        if (bodyTMP != null)
        {
            var fx = bodyTMP.GetComponent<TextEffectPlayer>() ?? bodyTMP.gameObject.AddComponent<TextEffectPlayer>();
            fx.Play(bodyTMP.text, bodyEffectType);
        }

        // 按钮：只绑定事件，不修改文字
        var btn = modal.GetComponentInChildren<Button>(true);
        if (btn != null)
        {
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(OnButtonClicked);

            if (btn.GetComponent<StyledButton>() == null)
                btn.gameObject.AddComponent<StyledButton>();
        }
    }

    private TMP_Text FindTMP(Transform parent, params string[] names)
    {
        foreach (var n in names)
        {
            var child = parent.Find(n);
            if (child != null)
            {
                var t = child.GetComponent<TMP_Text>();
                if (t != null) return t;
            }
        }
        return null;
    }

    // ══════════════════════════════════════════════════════════════
    //  按钮 → 场景跳转
    // ══════════════════════════════════════════════════════════════

    private void OnButtonClicked()
    {
        Debug.Log("[Cutscene] 按钮点击 → 跳转 Memory");
        LoadNextScene();
    }

    private void LoadNextScene()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.EnterPhase(GamePhase.Memory);
        else
            SceneManager.LoadScene(nextSceneName);
    }

    // ══════════════════════════════════════════════════════════════
    //  工具方法
    // ══════════════════════════════════════════════════════════════

    private static CanvasGroup GetOrAddCG(GameObject go)
    {
        var cg = go.GetComponent<CanvasGroup>();
        return cg != null ? cg : go.AddComponent<CanvasGroup>();
    }

    /// <summary>在场景所有 Canvas 中按候选名递归搜索（含未激活子物体）</summary>
    private static GameObject FindInScene(params string[] candidateNames)
    {
        foreach (var canvas in FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            foreach (var name in candidateNames)
            {
                var found = DeepFind(canvas.transform, name);
                if (found != null)
                {
                    Debug.Log($"[Cutscene] 找到 '{name}' → {FullPath(found)}");
                    return found.gameObject;
                }
            }
        }
        return null;
    }

    private static Transform DeepFind(Transform parent, string name)
    {
        if (parent.name == name) return parent;
        for (int i = 0; i < parent.childCount; i++)
        {
            var r = DeepFind(parent.GetChild(i), name);
            if (r != null) return r;
        }
        return null;
    }

    private static string FullPath(Transform t)
    {
        return t.parent == null ? t.name : FullPath(t.parent) + "/" + t.name;
    }
}