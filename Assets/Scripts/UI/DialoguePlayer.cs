using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using TMPro;
using System;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 对话播放器 — 在模态层显示带打字机效果的对话文本。
/// 
/// 适用于：
///   - 庭审 NPC 发言
///   - 记忆碎片阅读
///   - 剧情对话
///   - 小游戏胜负结果
/// 
/// 对话以"序列"方式播放：调用者提交一组对话条目（speaker + text），
/// 系统逐条以打字机效果展示，玩家可点击/按键推进或跳过。
/// 
/// 对话表现可在 Inspector 中配置：
///   - 打字机速度、跳过键
///   - 说话人名颜色、文字颜色
///   - 是否显示立绘（头像）
///   - 文字效果类型（复用 TextEffectPlayer）
/// 
/// 使用示例：
///   var entries = new List&lt;DialogueEntry&gt; {
///     new("爱丽丝", "你确定这就是全部的真相吗？"),
///     new("系统", "证据已提交。"),
///   };
///   UIManager.Instance.Dialogue.PlaySequence(entries, onAllComplete);
/// </summary>
public class DialoguePlayer : MonoBehaviour
{
    // ── Inspector 配置 ───────────────────────────────────────────
    [Header("== 对话面板 ==")]
    [Tooltip("对话面板 Prefab。需包含：SpeakerName (TMP), DialogueText (TMP), Portrait (Image, 可选), AdvanceButton (Button, 可选)。留空则运行时创建。")]
    [SerializeField] private GameObject dialoguePanelPrefab;

    [Header("== 文字表现 ==")]
    [Tooltip("对话文字效果类型")]
    [SerializeField] private TextEffectType textEffect = TextEffectType.Typewriter;

    [Tooltip("打字机每字符间隔")]
    [SerializeField] private float charDelay = 0.04f;

    [Tooltip("推进/跳过键")]
    [SerializeField] private KeyCode advanceKey = KeyCode.Space;
    [SerializeField] private KeyCode skipKey = KeyCode.Return;

    [Header("== 颜色 ==")]
    [Tooltip("说话人名颜色")]
    [SerializeField] private Color speakerColor = new Color(0.4f, 0.9f, 0.65f, 1f);
    [Tooltip("对话文字颜色")]
    [SerializeField] private Color dialogueColor = new Color(0.85f, 0.85f, 0.9f, 1f);

    [Header("== 动画 ==")]
    [Tooltip("面板入场动画时长")]
    [SerializeField] private float panelAnimDuration = 0.3f;

    // ── 引用 ─────────────────────────────────────────────────────
    private GameObject panelInstance;
    private CanvasGroup panelCG;
    private TMP_Text speakerNameText;
    private TMP_Text dialogueText;
    private Image portraitImage;
    private Button advanceButton;
    private TextEffectPlayer textEffectPlayer;

    // ── 状态 ─────────────────────────────────────────────────────
    private List<DialogueEntry> currentSequence;
    private int currentIndex;
    private bool isShowingText;
    private bool isActive;
    private Action onSequenceComplete;

    /// <summary>是否正在播放对话</summary>
    public bool IsActive => isActive;

    // ══════════════════════════════════════════════════════════════
    //  初始化
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// 运行时批量配置对话参数（供 MemorySceneSetup 等场景 Setup 脚本调用）。
    /// 负值 / null 表示保留当前值不变。
    /// 必须在 Initialize() 之后调用（Initialize 由 UIManager.RegisterSceneRoot 触发）。
    /// </summary>
    /// <param name="effect">文字特效类型</param>
    /// <param name="newCharDelay">打字机字符间隔（&lt;0 保留原值）</param>
    /// <param name="speakerCol">说话人名颜色（null 保留原值）</param>
    /// <param name="bodyCol">正文颜色（null 保留原值）</param>
    /// <param name="newPanelAnim">面板淡入淡出时长（&lt;0 保留原值）</param>
    public void Configure(
        TextEffectType effect,
        float          newCharDelay  = -1f,
        Color?         speakerCol    = null,
        Color?         bodyCol       = null,
        float          newPanelAnim  = -1f)
    {
        textEffect = effect;
        if (newCharDelay >= 0f)     charDelay         = newCharDelay;
        if (speakerCol.HasValue)    speakerColor      = speakerCol.Value;
        if (bodyCol.HasValue)       dialogueColor     = bodyCol.Value;
        if (newPanelAnim >= 0f)     panelAnimDuration = newPanelAnim;

        // 如果面板已创建（Initialize 已运行），同步更新面板内文字组件颜色
        if (speakerNameText != null) speakerNameText.color = speakerColor;
        if (dialogueText    != null) dialogueText.color    = dialogueColor;
    }

    public void Initialize()
    {
        // 清理旧状态（场景切换时，旧面板已随场景销毁）
        isActive = false;
        currentSequence = null;
        currentIndex = 0;
        panelInstance = null;
        panelCG = null;
        speakerNameText = null;
        dialogueText = null;
        portraitImage = null;
        advanceButton = null;
        textEffectPlayer = null;

        var modalLayer = UIManager.Instance?.modalLayer;
        if (modalLayer == null) return;

        if (dialoguePanelPrefab != null)
        {
            panelInstance = Instantiate(dialoguePanelPrefab, modalLayer);
        }
        else
        {
            panelInstance = CreateRuntimeDialoguePanel(modalLayer);
        }

        panelInstance.name = "DialoguePanel";
        panelCG = panelInstance.GetComponent<CanvasGroup>();
        if (panelCG == null) panelCG = panelInstance.AddComponent<CanvasGroup>();

        // 绑定
        speakerNameText = panelInstance.transform.Find("SpeakerName")?.GetComponent<TMP_Text>();
        dialogueText    = panelInstance.transform.Find("DialogueText")?.GetComponent<TMP_Text>();
        portraitImage   = panelInstance.transform.Find("Portrait")?.GetComponent<Image>();
        advanceButton   = panelInstance.transform.Find("AdvanceButton")?.GetComponent<Button>();

        // 添加 TextEffectPlayer
        if (dialogueText != null)
        {
            textEffectPlayer = dialogueText.gameObject.GetComponent<TextEffectPlayer>();
            if (textEffectPlayer == null)
                textEffectPlayer = dialogueText.gameObject.AddComponent<TextEffectPlayer>();
        }

        advanceButton?.onClick.AddListener(OnAdvance);

        // 初始隐藏
        panelCG.alpha = 0f;
        panelCG.interactable = false;
        panelCG.blocksRaycasts = false;
        panelInstance.SetActive(false);
    }

    private void Update()
    {
        if (!isActive) return;

        if (Input.GetKeyDown(advanceKey))
        {
            OnAdvance();
        }
        else if (Input.GetKeyDown(skipKey))
        {
            SkipCurrent();
        }
    }

    // ══════════════════════════════════════════════════════════════
    //  公开 API
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// 播放一组对话序列。
    /// 对话结束后触发 onComplete 回调。
    /// </summary>
    public void PlaySequence(List<DialogueEntry> entries, Action onComplete = null)
    {
        if (entries == null || entries.Count == 0)
        {
            onComplete?.Invoke();
            return;
        }

        currentSequence = entries;
        currentIndex = 0;
        onSequenceComplete = onComplete;
        isActive = true;

        // 显示模态背景 + 对话面板
        UIManager.Instance?.ShowModalBackground();
        panelInstance.SetActive(true);
        panelCG.DOFade(1f, panelAnimDuration).SetUpdate(true)
            .OnComplete(() =>
            {
                panelCG.interactable = true;
                panelCG.blocksRaycasts = true;
                ShowEntry(currentIndex);
            });
    }

    /// <summary>播放单条对话（快捷方法）</summary>
    public void PlaySingle(string speaker, string text, Action onComplete = null, Sprite portrait = null)
    {
        PlaySequence(new List<DialogueEntry>
        {
            new DialogueEntry(speaker, text, portrait)
        }, onComplete);
    }

    /// <summary>强制关闭对话</summary>
    public void ForceClose()
    {
        if (!isActive) return;
        textEffectPlayer?.Stop();
        ClosePanel(null);
    }

    // ══════════════════════════════════════════════════════════════
    //  内部流程
    // ══════════════════════════════════════════════════════════════

    private void ShowEntry(int index)
    {
        if (index >= currentSequence.Count)
        {
            ClosePanel(onSequenceComplete);
            return;
        }

        var entry = currentSequence[index];

        // 说话人
        if (speakerNameText != null)
        {
            speakerNameText.text = entry.speaker ?? "";
            speakerNameText.color = speakerColor;
        }

        // 立绘
        if (portraitImage != null)
        {
            if (entry.portrait != null)
            {
                portraitImage.sprite = entry.portrait;
                portraitImage.gameObject.SetActive(true);
            }
            else
            {
                portraitImage.gameObject.SetActive(false);
            }
        }

        // 文字效果
        if (dialogueText != null)
        {
            dialogueText.color = dialogueColor;
        }

        isShowingText = true;

        if (textEffectPlayer != null)
        {
            textEffectPlayer.OnEffectComplete -= OnTextEffectDone;
            textEffectPlayer.OnEffectComplete += OnTextEffectDone;
            textEffectPlayer.Play(entry.text, textEffect);
        }
        else if (dialogueText != null)
        {
            dialogueText.text = entry.text;
            isShowingText = false;
        }
    }

    private void OnTextEffectDone()
    {
        isShowingText = false;
    }

    private void OnAdvance()
    {
        if (isShowingText)
        {
            // 文字还在播放 → 跳到完整显示
            textEffectPlayer?.Skip();
            isShowingText = false;
        }
        else
        {
            // 文字已完整 → 下一条
            currentIndex++;
            ShowEntry(currentIndex);
        }
    }

    private void SkipCurrent()
    {
        // 跳过整个打字效果
        textEffectPlayer?.Skip();
        isShowingText = false;
    }

    private void ClosePanel(Action onComplete)
    {
        isActive = false;
        panelCG.interactable = false;
        panelCG.blocksRaycasts = false;

        panelCG.DOFade(0f, panelAnimDuration * 0.5f).SetUpdate(true)
            .OnComplete(() =>
            {
                panelInstance.SetActive(false);
                UIManager.Instance?.HideModalBackground(() => onComplete?.Invoke());
            });
    }

    // ══════════════════════════════════════════════════════════════
    //  运行时创建
    // ══════════════════════════════════════════════════════════════

    private GameObject CreateRuntimeDialoguePanel(Transform parent)
    {
        // 底部对话栏风格
        var panel = new GameObject("DialoguePanel");
        panel.transform.SetParent(parent, false);
        var rect = panel.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0, 0);
        rect.anchorMax = new Vector2(1, 0);
        rect.pivot = new Vector2(0.5f, 0);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(0, 220);
        var bg = panel.AddComponent<Image>();
        bg.color = new Color(0.02f, 0.02f, 0.05f, 0.92f);

        // Portrait (左侧)
        var portraitGO = new GameObject("Portrait");
        portraitGO.transform.SetParent(panel.transform, false);
        var pRect = portraitGO.AddComponent<RectTransform>();
        pRect.anchorMin = new Vector2(0, 0);
        pRect.anchorMax = new Vector2(0, 1);
        pRect.pivot = new Vector2(0, 0.5f);
        pRect.anchoredPosition = new Vector2(15, 0);
        pRect.sizeDelta = new Vector2(140, -20);
        portraitGO.AddComponent<Image>().color = new Color(0.1f, 0.1f, 0.15f, 0.5f);

        // SpeakerName
        var nameGO = new GameObject("SpeakerName");
        nameGO.transform.SetParent(panel.transform, false);
        var nRect = nameGO.AddComponent<RectTransform>();
        nRect.anchorMin = new Vector2(0, 1);
        nRect.anchorMax = new Vector2(0, 1);
        nRect.pivot = new Vector2(0, 1);
        nRect.anchoredPosition = new Vector2(170, -12);
        nRect.sizeDelta = new Vector2(300, 35);
        var nameTMP = nameGO.AddComponent<TextMeshProUGUI>();
        nameTMP.fontSize = 24;
        nameTMP.color = speakerColor;
        nameTMP.fontStyle = FontStyles.Bold;
        nameTMP.alignment = TextAlignmentOptions.BottomLeft;
        ChineseFontProvider.ApplyFont(nameTMP);

        // DialogueText
        var textGO = new GameObject("DialogueText");
        textGO.transform.SetParent(panel.transform, false);
        var tRect = textGO.AddComponent<RectTransform>();
        tRect.anchorMin = new Vector2(0, 0);
        tRect.anchorMax = new Vector2(1, 1);
        tRect.offsetMin = new Vector2(170, 15);
        tRect.offsetMax = new Vector2(-20, -55);
        var textTMP = textGO.AddComponent<TextMeshProUGUI>();
        textTMP.fontSize = 20;
        textTMP.color = dialogueColor;
        textTMP.alignment = TextAlignmentOptions.TopLeft;
        ChineseFontProvider.ApplyFont(textTMP);

        // AdvanceButton（右下角三角提示）
        var advGO = new GameObject("AdvanceButton");
        advGO.transform.SetParent(panel.transform, false);
        var aRect = advGO.AddComponent<RectTransform>();
        aRect.anchorMin = new Vector2(1, 0);
        aRect.anchorMax = new Vector2(1, 0);
        aRect.pivot = new Vector2(1, 0);
        aRect.anchoredPosition = new Vector2(-15, 10);
        aRect.sizeDelta = new Vector2(30, 30);
        var advImg = advGO.AddComponent<Image>();
        advImg.color = new Color(0.6f, 0.6f, 0.7f, 0.5f);
        advGO.AddComponent<Button>().targetGraphic = advImg;

        return panel;
    }
}

/// <summary>
/// 单条对话数据。
/// </summary>
[System.Serializable]
public class DialogueEntry
{
    [Tooltip("说话人名称")]
    public string speaker;

    [TextArea(2, 6)]
    [Tooltip("对话内容")]
    public string text;

    [Tooltip("说话人立绘（可选）")]
    public Sprite portrait;

    public DialogueEntry() { }

    public DialogueEntry(string speaker, string text, Sprite portrait = null)
    {
        this.speaker = speaker;
        this.text = text;
        this.portrait = portrait;
    }
}
