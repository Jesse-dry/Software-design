using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using System;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 庭审 UI 控制器（全局持久，跨场景自动注入）。
///
/// 对 prefab 中已有的美术面板 → 查找并绑定；
/// 对 prefab 中不存在的面板 → 运行时创建。
/// </summary>
public class CourtUIController : MonoBehaviour
{
    public static CourtUIController Instance { get; private set; }

    // ════  初始化  ════════════════════════════════════════════════════════

    public static CourtUIController Initialize(Transform parent)
    {
        if (Instance != null) return Instance;
        var go = new GameObject("CourtUIController_Logic");
        go.transform.SetParent(parent, false);
        Instance = go.AddComponent<CourtUIController>();

        if (UIManager.Instance != null)
        {
            UIManager.Instance.OnSceneRootRegistered += Instance.OnSceneLoaded;
            if (UIManager.Instance.HasSceneRoot && UIManager.Instance.hudLayer != null)
            {
                var existing = UIManager.Instance.hudLayer.GetComponentInParent<UISceneRoot>();
                if (existing != null) Instance.OnSceneLoaded(existing);
            }
        }
        return Instance;
    }

    // ════  风格常量  ════════════════════════════════════════════════════════

    private static readonly Color BG_DARK = new Color(0.08f, 0.08f, 0.12f, 0.95f);
    private static readonly Color BG_PANEL = new Color(0.12f, 0.12f, 0.18f, 0.92f);
    private static readonly Color TEXT_MAIN = new Color(0.85f, 0.88f, 0.92f, 1f);
    private static readonly Color TEXT_TITLE = new Color(0.6f, 0.85f, 0.7f, 1f);
    private static readonly Color TEXT_DIM = new Color(0.55f, 0.55f, 0.6f, 1f);
    private static readonly Color BTN_NORMAL = new Color(0.18f, 0.22f, 0.28f, 1f);
    private static readonly Color BTN_HIGHLIGHT = new Color(0.25f, 0.55f, 0.4f, 1f);
    private static readonly Color CARD_UNAVAILABLE = Color.black;

    private const float FADE_DURATION = 0.3f;
    private const float PANEL_WIDTH = 900f;
    private const float PANEL_HEIGHT = 550f;

    // ════  UI 引用  ════════════════════════════════════════════════════════

    private RectTransform _hudLayer;
    private RectTransform _overlayLayer;
    private RectTransform _modalLayer;

    private Button _ruleButton;

    // 规则面板（绑定 prefab）
    private GameObject _rulePanel;
    private CanvasGroup _rulePanelCG;
    private readonly Dictionary<CourtData.NPCId, TextMeshProUGUI> _ruleNPCStatTexts = new();

    // NPC 发言面板（绑定 prefab）
    private readonly Dictionary<CourtData.NPCId, GameObject> _speechPanels = new();
    private readonly Dictionary<CourtData.NPCId, TextMeshProUGUI> _speechTexts = new();
    private readonly Dictionary<CourtData.NPCId, TextMeshProUGUI> _speechNames = new();

    // 选择目标面板（绑定 prefab）
    private GameObject _selectTargetPanel;
    private CanvasGroup _selectTargetCG;

    // 回合结算面板（绑定 prefab）
    private readonly GameObject[] _roundResultPanels = new GameObject[5];

    // 阿卡那菜单（绑定 prefab: OverlayLayer/akanaMenu）
    private GameObject _akanaMenuPanel;
    private CanvasGroup _akanaMenuCG;
    private readonly Dictionary<AkanaCardId, Button> _akanaCardButtons = new();
    private readonly Dictionary<AkanaCardId, Image> _akanaCardImages = new();

    // 卡牌详情面板（绑定 prefab: ModalLayer/圣杯牌 等）
    private readonly Dictionary<AkanaCardId, GameObject> _cardDetailPanels = new();
    private readonly Dictionary<AkanaCardId, CanvasGroup> _cardDetailCGs = new();

    // 胜利 / 失败面板（绑定 prefab: ModalLayer/victory, ModalLayer/fail）
    private GameObject _victoryPanel;
    private GameObject _defeatPanel;

    // 运行时创建的 GO，清理时 Destroy
    private readonly List<GameObject> _runtimeCreated = new();

    private bool _isCourt = false;

    // ════  场景加载  ════════════════════════════════════════════════════════

    private void OnSceneLoaded(UISceneRoot root)
    {
        Cleanup();

        if (root == null) return;
        if (GameManager.Instance == null || !GameManager.Instance.IsInCourt()) return;

        _isCourt = true;
        _hudLayer = root.hudLayer;
        _overlayLayer = root.overlayLayer;
        _modalLayer = root.modalLayer;

        if (_hudLayer == null)
        {
            var found = root.transform.Find("HUDLayer (1)") ?? root.transform.Find("HUDLayer");
            if (found != null) _hudLayer = found as RectTransform;
        }

        if (_hudLayer == null || _overlayLayer == null || _modalLayer == null)
        {
            Debug.LogWarning("[CourtUI] \u7f3a\u5c11\u5fc5\u8981\u7684 UI \u5c42\u3002");
            return;
        }

        // Phase 0: \u5148\u9690\u85cf\u6240\u6709\u5c42\u7ea7\u5b50\u7269\u4f53\uff0c\u7531\u72b6\u6001\u673a\u6309\u9700\u663e\u793a
        HideAllLayerChildren();

        // Phase 1: \u7ed1\u5b9a\u6240\u6709 prefab \u4e2d\u5df2\u6709\u7684\u7f8e\u672f\u9762\u677f
        BindRuleButton();
        BindRulePanel();
        BindSpeechPanels();
        BindSelectTargetPanel();
        BindRoundResultPanels();
        BindAkanaMenu();
        BindCardDetailPanels();
        BindVictoryPanel();
        BindDefeatPanel();

        // Phase 3: \u7ed1\u5b9a\u63a7\u5236\u5668\u4e8b\u4ef6
        BindCourtController();

        Debug.Log("[CourtUI] \u5ead\u5ba1 UI \u521d\u59cb\u5316\u5b8c\u6210\u3002");
    }

    // ════  绑定 Prefab 面板  ════════════════════════════════════════════════════

    private void BindRuleButton()
    {
        var found = _hudLayer.Find("RuleButton") ?? _hudLayer.Find("ButtonOnNormal");
        if (found == null)
        {
            Debug.LogWarning("[CourtUI] \u672a\u627e\u5230 RuleButton\uff0c\u521b\u5efa\u5907\u7528\u3002");
            var btnGO = CreatePanel(_hudLayer, "RuleButton", 120, 45, new Vector2(80, -35));
            SetImage(btnGO, BTN_NORMAL);
            var btn = btnGO.AddComponent<Button>();
            btn.targetGraphic = btnGO.GetComponent<Image>();
            CreateTMP(btnGO.transform, "\u89c4\u5219", 20, TEXT_MAIN, TextAlignmentOptions.Center);
            _ruleButton = btn;
            _runtimeCreated.Add(btnGO);
        }
        else
        {
            _ruleButton = found.GetComponent<Button>();
            if (_ruleButton == null) _ruleButton = found.gameObject.AddComponent<Button>();
            var img = found.GetComponent<Image>();
            if (img != null) _ruleButton.targetGraphic = img;
        }
        _ruleButton.onClick.AddListener(OnRuleButtonClicked);
    }

    private void BindRulePanel()
    {
        var found = _overlayLayer.Find("\u89c4\u5219\u5c5e\u6027\u7248\u9762");
        if (found == null)
        {
            Debug.LogWarning("[CourtUI] \u672a\u627e\u5230 \u89c4\u5219\u5c5e\u6027\u7248\u9762\u3002");
            return;
        }

        _rulePanel = found.gameObject;
        _rulePanelCG = EnsureCG(_rulePanel);

        // \u67e5\u627e NPC \u5c5e\u6027\u6587\u672c
        _ruleNPCStatTexts.Clear();
        string[] npcStatNames = { "\u7687\u5e1d\u5c5e\u6027", "\u604b\u4eba\u5c5e\u6027", "\u5546\u4eba\u5c5e\u6027", "\u6b63\u4e49\u5c5e\u6027" };
        CourtData.NPCId[] npcIds = { CourtData.NPCId.\u7687\u5e1d, CourtData.NPCId.\u604b\u4eba, CourtData.NPCId.\u5546\u4eba, CourtData.NPCId.\u6b63\u4e49 };
        for (int i = 0; i < npcStatNames.Length; i++)
        {
            var child = found.Find(npcStatNames[i]);
            if (child == null) continue;
            var tmp = child.GetComponent<TextMeshProUGUI>();
            if (tmp == null) tmp = child.GetComponentInChildren<TextMeshProUGUI>(true);
            if (tmp != null) _ruleNPCStatTexts[npcIds[i]] = tmp;
        }

        // \u67e5\u627e\u6216\u521b\u5efa\u5173\u95ed\u6309\u94ae
        var existingBtn = found.GetComponentInChildren<Button>(true);
        if (existingBtn != null)
        {
            existingBtn.onClick.AddListener(OnRulePanelClose);
        }
        else
        {
            var closeBtnGO = CreatePanel(found, "CloseBtn_RT", 180, 48, new Vector2(0, -280));
            SetImage(closeBtnGO, BTN_HIGHLIGHT);
            var closeBtn = closeBtnGO.AddComponent<Button>();
            closeBtn.targetGraphic = closeBtnGO.GetComponent<Image>();
            CreateTMP(closeBtnGO.transform, "\u6211\u5df2\u4e86\u89e3", 22, Color.white, TextAlignmentOptions.Center);
            closeBtn.onClick.AddListener(OnRulePanelClose);
            _runtimeCreated.Add(closeBtnGO);
        }

        HideImmediate(_rulePanel, _rulePanelCG);
    }

    private void BindSpeechPanels()
    {
        _speechPanels.Clear();
        _speechTexts.Clear();
        _speechNames.Clear();

        foreach (var p in CourtData.NPCProfiles)
        {
            var found = _modalLayer.Find(p.name);
            if (found == null)
            {
                Debug.LogWarning("[CourtUI] \u672a\u627e\u5230\u53d1\u8a00\u9762\u677f: " + p.name);
                continue;
            }

            var panel = found.gameObject;
            var cg = EnsureCG(panel);

            // \u67e5\u627e\u540d\u79f0\u548c\u6587\u672c\u7ec4\u4ef6
            var nameTmp = FindTMP(found, "Name");
            var speechTmp = FindTMP(found, "TextForInput");

            if (nameTmp != null) _speechNames[p.id] = nameTmp;
            if (speechTmp != null) _speechTexts[p.id] = speechTmp;

            // \u67e5\u627e\u6216\u521b\u5efa\u201c\u7ee7\u7eed\u201d\u6309\u94ae
            var existingBtn = panel.GetComponentInChildren<Button>(true);
            if (existingBtn != null)
            {
                var capturedId = p.id;
                existingBtn.onClick.AddListener(() => OnSpeechClose(capturedId));
            }
            else
            {
                var closeBtnGO = CreatePanel(panel.transform, "ContinueBtn_RT", 160, 45, new Vector2(0, -230));
                SetImage(closeBtnGO, BTN_NORMAL);
                var closeBtn = closeBtnGO.AddComponent<Button>();
                closeBtn.targetGraphic = closeBtnGO.GetComponent<Image>();
                CreateTMP(closeBtnGO.transform, "\u7ee7\u7eed", 20, TEXT_MAIN, TextAlignmentOptions.Center);
                var capturedId = p.id;
                closeBtn.onClick.AddListener(() => OnSpeechClose(capturedId));
                _runtimeCreated.Add(closeBtnGO);
            }

            _speechPanels[p.id] = panel;
            HideImmediate(panel, cg);
        }
    }

    private void BindSelectTargetPanel()
    {
        var found = _modalLayer.Find("\u9009\u62e9\u89d2\u8272\u9762\u677f");
        if (found == null)
        {
            Debug.LogWarning("[CourtUI] \u672a\u627e\u5230 \u9009\u62e9\u89d2\u8272\u9762\u677f\u3002");
            return;
        }

        _selectTargetPanel = found.gameObject;
        _selectTargetCG = EnsureCG(_selectTargetPanel);

        // \u7ed1\u5b9a\u6bcf\u4e2a NPC \u5b50\u7269\u4f53\u7684\u70b9\u51fb\u4e8b\u4ef6
        CourtData.NPCId[] npcIds = { CourtData.NPCId.\u7687\u5e1d, CourtData.NPCId.\u604b\u4eba, CourtData.NPCId.\u5546\u4eba, CourtData.NPCId.\u6b63\u4e49 };
        foreach (var npcId in npcIds)
        {
            string npcName = CourtData.NPCProfiles[(int)npcId].name;
            var npcChild = found.Find(npcName);
            if (npcChild == null) continue;

            var btn = npcChild.GetComponent<Button>();
            if (btn == null)
            {
                btn = npcChild.gameObject.AddComponent<Button>();
                var img = npcChild.GetComponent<Image>();
                if (img != null) btn.targetGraphic = img;
            }

            var captured = npcId;
            btn.onClick.AddListener(() => OnTargetChosen(captured));
        }

        HideImmediate(_selectTargetPanel, _selectTargetCG);
    }

    private void BindRoundResultPanels()
    {
        string[] names = { "0\u4eba", "1\u4eba", "2\u4eba", "3\u4eba", "4\u4eba" };
        for (int i = 0; i < 5; i++)
        {
            var found = _modalLayer.Find(names[i]);
            if (found == null) continue;
            _roundResultPanels[i] = found.gameObject;
            var cg = EnsureCG(found.gameObject);
            HideImmediate(found.gameObject, cg);
        }
    }

    // ════  初始隐藏所有面板  ═══════════════════════════════════════════════════

    /// <summary>
    /// 场景加载时先隐藏 OverlayLayer / ModalLayer 所有直接子物体。
    /// 由状态机按需 FadeIn。
    /// </summary>
    private void HideAllLayerChildren()
    {
        HideLayerChildren(_overlayLayer);
        HideLayerChildren(_modalLayer);
    }

    private void HideLayerChildren(Transform layer)
    {
        for (int i = 0; i < layer.childCount; i++)
        {
            var child = layer.GetChild(i).gameObject;
            var cg = EnsureCG(child);
            cg.alpha = 0f;
            cg.interactable = false;
            cg.blocksRaycasts = false;
            child.SetActive(false);
        }
    }

    // ════  绑定 Prefab 面板（二）：卡牌 / 胜负  ═══════════════════════════════════

    /// <summary>AkanaCardId → prefab 面板名映射</summary>
    private static readonly Dictionary<AkanaCardId, string> CardPanelNames = new()
    {
        { AkanaCardId.\u5723\u676f, "\u5723\u676f\u724c" },
        { AkanaCardId.\u5b9d\u5251, "\u5b9d\u5251\u724c" },
        { AkanaCardId.\u661f\u5e01, "\u661f\u5e01\u724c" },
        { AkanaCardId.\u6743\u6756, "\u6743\u6756\u724c" },
    };

    private void BindAkanaMenu()
    {
        _akanaCardButtons.Clear();
        _akanaCardImages.Clear();

        // akanaMenu 在 OverlayLayer 中
        var found = _overlayLayer.Find("akanaMenu");
        if (found == null)
        {
            Debug.LogWarning("[CourtUI] \u672a\u627e\u5230 akanaMenu\uff0c\u5c1d\u8bd5 ModalLayer...");
            found = _modalLayer.Find("akanaMenu");
        }
        if (found == null)
        {
            Debug.LogWarning("[CourtUI] \u672a\u627e\u5230 akanaMenu\u3002");
            return;
        }

        _akanaMenuPanel = found.gameObject;
        _akanaMenuCG = EnsureCG(_akanaMenuPanel);
        // \u5df2\u7531 HideAllLayerChildren \u9690\u85cf

        // \u67e5\u627e\u5185\u90e8\u5361\u724c\u6309\u94ae\uff08\u4ee5\u5361\u724c ID \u540d\u79f0\u67e5\u627e\u5b50\u7269\u4f53\uff09
        foreach (var kvp in CardPanelNames)
        {
            var cardId = kvp.Key;
            string displayName = AkanaManager.GetCardDisplayName(cardId);
            // \u5c1d\u8bd5\u591a\u79cd\u540d\u79f0\u5339\u914d
            var child = found.Find(kvp.Value) ?? found.Find(displayName) ?? found.Find(cardId.ToString());
            if (child == null)
            {
                // \u641c\u7d22\u6240\u6709\u5b50\u7269\u4f53
                for (int i = 0; i < found.childCount; i++)
                {
                    var c = found.GetChild(i);
                    if (c.name.Contains(cardId.ToString()) || c.name.Contains(kvp.Value))
                    { child = c; break; }
                }
            }
            if (child == null) continue;

            var btn = child.GetComponent<Button>();
            if (btn == null)
            {
                btn = child.gameObject.AddComponent<Button>();
                var img = child.GetComponent<Image>();
                if (img != null) btn.targetGraphic = img;
            }

            var capturedId = cardId;
            btn.onClick.AddListener(() => OnAkanaCardClicked(capturedId));
            _akanaCardButtons[capturedId] = btn;
            var cardImg = child.GetComponent<Image>();
            if (cardImg != null) _akanaCardImages[capturedId] = cardImg;
        }
    }

    private void BindCardDetailPanels()
    {
        _cardDetailPanels.Clear();
        _cardDetailCGs.Clear();

        foreach (var kvp in CardPanelNames)
        {
            var cardId = kvp.Key;
            string panelName = kvp.Value;

            var found = _modalLayer.Find(panelName);
            if (found == null) continue;

            var panel = found.gameObject;
            var cg = EnsureCG(panel);
            // \u5df2\u7531 HideAllLayerChildren \u9690\u85cf

            // \u67e5\u627e\u6253\u51fa / \u56de\u9000 \u6309\u94ae\uff08prefab \u4e2d\u5df2\u6709\uff09
            var playBtn = FindButton(found, "\u6253\u51fa");
            var backBtn = FindButton(found, "\u56de\u9000");

            var capturedId = cardId;
            if (playBtn != null)
                playBtn.onClick.AddListener(() => OnCardDetailPlay(capturedId));
            if (backBtn != null)
                backBtn.onClick.AddListener(() => OnCardDetailBack(capturedId));

            _cardDetailPanels[cardId] = panel;
            _cardDetailCGs[cardId] = cg;
        }
    }

    private void BindVictoryPanel()
    {
        var found = _modalLayer.Find("victory");
        if (found == null)
        {
            Debug.LogWarning("[CourtUI] \u672a\u627e\u5230 victory \u9762\u677f\u3002");
            return;
        }

        _victoryPanel = found.gameObject;
        EnsureCG(_victoryPanel);
        // \u5df2\u7531 HideAllLayerChildren \u9690\u85cf

        // \u7ed1\u5b9a\u5185\u90e8\u6309\u94ae
        var btn = found.GetComponentInChildren<Button>(true);
        if (btn != null)
            btn.onClick.AddListener(OnVictoryConfirm);
    }

    private void BindDefeatPanel()
    {
        var found = _modalLayer.Find("fail");
        if (found == null)
        {
            Debug.LogWarning("[CourtUI] \u672a\u627e\u5230 fail \u9762\u677f\u3002");
            return;
        }

        _defeatPanel = found.gameObject;
        EnsureCG(_defeatPanel);
        // \u5df2\u7531 HideAllLayerChildren \u9690\u85cf
    }

    /// <summary>\u5728 parent \u53ca\u5176\u5b50\u7269\u4f53\u4e2d\u67e5\u627e\u540d\u4e3a name \u7684 Button\u3002</summary>
    private static Button FindButton(Transform parent, string name)
    {
        var child = parent.Find(name);
        if (child != null)
        {
            var btn = child.GetComponent<Button>();
            return btn ?? child.gameObject.AddComponent<Button>();
        }
        // \u6df1\u5ea6\u641c\u7d22
        foreach (Transform t in parent.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == name)
            {
                var btn = t.GetComponent<Button>();
                return btn ?? t.gameObject.AddComponent<Button>();
            }
        }
        return null;
    }

    // ════  绑定 CourtController 事件  ══════════════════════════════════════════════

    private void BindCourtController()
    {
        StartCoroutine(WaitAndBind());
    }

    private IEnumerator WaitAndBind()
    {
        float timeout = 3f;
        while (CourtController.Instance == null && timeout > 0f)
        {
            timeout -= Time.unscaledDeltaTime;
            yield return null;
        }
        var cc = CourtController.Instance;
        if (cc == null)
        {
            Debug.LogWarning("[CourtUI] \u672a\u627e\u5230 CourtController\uff01");
            yield break;
        }

        cc.OnRulePanelRequested += ShowRulePanel;
        cc.OnNPCSpeech += ShowSpeech;
        cc.OnStateChanged += OnCourtStateChanged;
        cc.OnCardSelected += ShowCardDetail;
        cc.OnRoundResult += ShowRoundResult;
        cc.OnVictory += ShowVictory;
        cc.OnDefeat += ShowDefeat;
        cc.OnNPCStatChanged += OnNPCStatChanged;

        Debug.Log("[CourtUI] \u5df2\u7ed1\u5b9a CourtController \u4e8b\u4ef6\u3002");
    }

    // ════  状态变化响应  ════════════════════════════════════════════════════

    private void OnCourtStateChanged(CourtState state)
    {
        switch (state)
        {
            case CourtState.AkanaMenu:
                ShowAkanaMenu();
                break;
            case CourtState.SelectTarget:
                ShowSelectTarget();
                break;
        }
    }

    // ════  规则面板  ════════════════════════════════════════════════════════

    private void ShowRulePanel()
    {
        if (_rulePanel == null) return;
        RefreshRulePanelStats();
        _rulePanel.transform.SetAsLastSibling();
        FadeIn(_rulePanel, _rulePanelCG);
    }

    private void RefreshRulePanelStats()
    {
        var cc = CourtController.Instance;
        if (cc == null) return;

        foreach (var p in CourtData.NPCProfiles)
        {
            if (!_ruleNPCStatTexts.TryGetValue(p.id, out var tmp)) continue;
            var npc = cc.GetNPC(p.id);
            if (npc == null) continue;

            string threshold = p.emotionalThreshold < 0
                ? "\u7406\u2264" + p.rationalThreshold + " / \u611f\uff1a\u514d\u75ab"
                : "\u7406\u2264" + p.rationalThreshold + " / \u611f\u2265" + p.emotionalThreshold;
            tmp.text = FormatNPCStatLine(p.name, npc.rational, npc.emotional, threshold);
        }
    }

    private void OnRulePanelClose()
    {
        FadeOut(_rulePanel, _rulePanelCG);
        CourtController.Instance?.NotifyRulePanelClosed();
    }

    private void OnRuleButtonClicked()
    {
        if (_rulePanel != null && _rulePanel.activeSelf) return;
        var cc = CourtController.Instance;
        if (cc != null)
        {
            var state = cc.CurrentState;
            if (state == CourtState.NPCSpeech || state == CourtState.AkanaMenu ||
                state == CourtState.CardDetail || state == CourtState.SelectTarget ||
                state == CourtState.RoundResult || state == CourtState.Victory ||
                state == CourtState.Defeat)
                return;
        }
        cc?.RequestShowRulePanel();
    }

    // ════  NPC 发言  ══════════════════════════════════════════════════════════

    private void ShowSpeech(CourtData.NPCId speaker, string text)
    {
        if (!_speechPanels.TryGetValue(speaker, out var panel)) return;
        if (_speechTexts.TryGetValue(speaker, out var tmp)) tmp.text = text;
        panel.transform.SetAsLastSibling();
        var cg = EnsureCG(panel);
        FadeIn(panel, cg);
    }

    private void OnSpeechClose(CourtData.NPCId speaker)
    {
        if (!_speechPanels.TryGetValue(speaker, out var panel)) return;
        var cg = EnsureCG(panel);
        FadeOut(panel, cg, () =>
        {
            CourtController.Instance?.NotifySpeechClosed();
        });
    }

    // ════  阿卡那菜单  ════════════════════════════════════════════════════════

    private void ShowAkanaMenu()
    {
        if (_akanaMenuPanel == null) return;
        RefreshAkanaCardStates();
        _akanaMenuPanel.transform.SetAsLastSibling();
        FadeIn(_akanaMenuPanel, _akanaMenuCG);
    }

    private void HideAkanaMenu(Action onComplete = null)
    {
        if (_akanaMenuPanel == null) { onComplete?.Invoke(); return; }
        FadeOut(_akanaMenuPanel, _akanaMenuCG, onComplete);
    }

    private void RefreshAkanaCardStates()
    {
        var cc = CourtController.Instance;
        foreach (var cardId in _akanaCardButtons.Keys)
        {
            bool available = cc != null && cc.IsCardAvailable(cardId);
            if (_akanaCardImages.TryGetValue(cardId, out var img))
                img.color = available ? BTN_NORMAL : CARD_UNAVAILABLE;
            if (_akanaCardButtons.TryGetValue(cardId, out var btn))
                btn.interactable = available;
        }
    }

    private void OnAkanaCardClicked(AkanaCardId cardId)
    {
        var cc = CourtController.Instance;
        if (cc == null || !cc.IsCardAvailable(cardId)) return;
        HideAkanaMenu(() => { cc.NotifyCardChosen(cardId); });
    }

    // ════  卡牌详情  ══════════════════════════════════════════════════════════

    private void ShowCardDetail(AkanaCardId cardId)
    {
        if (!_cardDetailPanels.TryGetValue(cardId, out var panel)) return;
        panel.transform.SetAsLastSibling();
        FadeIn(panel, _cardDetailCGs[cardId]);
    }

    private void HideCardDetail(AkanaCardId cardId, Action onComplete = null)
    {
        if (!_cardDetailPanels.TryGetValue(cardId, out var panel))
        { onComplete?.Invoke(); return; }
        FadeOut(panel, _cardDetailCGs[cardId], onComplete);
    }

    private void OnCardDetailBack(AkanaCardId cardId)
    {
        HideCardDetail(cardId, () => { CourtController.Instance?.NotifyCardBack(); });
    }

    private void OnCardDetailPlay(AkanaCardId cardId)
    {
        HideCardDetail(cardId, () => { CourtController.Instance?.NotifyCardPlay(); });
    }

    // ════  选择目标  ══════════════════════════════════════════════════════════

    private void ShowSelectTarget()
    {
        if (_selectTargetPanel == null) return;
        _selectTargetPanel.transform.SetAsLastSibling();
        FadeIn(_selectTargetPanel, _selectTargetCG);
    }

    private void OnTargetChosen(CourtData.NPCId targetId)
    {
        FadeOut(_selectTargetPanel, _selectTargetCG, () =>
        {
            CourtController.Instance?.NotifyTargetChosen(targetId);
        });
    }

    // ════  回合结算  ══════════════════════════════════════════════════════════

    private void ShowRoundResult(int persuadedCount)
    {
        int idx = Mathf.Clamp(persuadedCount, 0, 4);
        var panel = _roundResultPanels[idx];
        if (panel == null) return;
        panel.transform.SetAsLastSibling();
        var cg = EnsureCG(panel);
        FadeIn(panel, cg);
        DOVirtual.DelayedCall(1.8f, () => FadeOut(panel, cg)).SetUpdate(true);
    }

    // ════  胜利 / 失败  ════════════════════════════════════════════════════════

    private void ShowVictory()
    {
        if (_victoryPanel == null) return;
        _victoryPanel.transform.SetAsLastSibling();
        FadeIn(_victoryPanel, EnsureCG(_victoryPanel));
    }

    private void ShowDefeat()
    {
        if (_defeatPanel == null) return;
        _defeatPanel.transform.SetAsLastSibling();
        var cg = EnsureCG(_defeatPanel);
        FadeIn(_defeatPanel, cg);

        var rect = _defeatPanel.GetComponent<RectTransform>();
        if (rect != null)
        {
            rect.DOShakePosition(1.2f, new Vector3(60f, 60f, 0f), 35, 90f, false, true).SetUpdate(true);
            rect.DOShakeRotation(1.2f, new Vector3(0f, 0f, 10f), 35, 90f, true).SetUpdate(true);
        }

        DOVirtual.DelayedCall(2f, () =>
        {
            GameManager.Instance?.EnterPhase(GamePhase.MainMenu);
        }).SetUpdate(true);
    }

    private void OnVictoryConfirm()
    {
        GameManager.Instance?.EnterPhase(GamePhase.MainMenu);
    }

    // ════  NPC 属性变化  ════════════════════════════════════════════════════════

    private void OnNPCStatChanged(CourtData.NPCId id, int rational, int emotional)
    {
        RefreshRulePanelStats();
    }

    // ════  清理  ════════════════════════════════════════════════════════════

    private void Cleanup()
    {
        var cc = CourtController.Instance;
        if (cc != null)
        {
            cc.OnRulePanelRequested -= ShowRulePanel;
            cc.OnNPCSpeech -= ShowSpeech;
            cc.OnStateChanged -= OnCourtStateChanged;
            cc.OnCardSelected -= ShowCardDetail;
            cc.OnRoundResult -= ShowRoundResult;
            cc.OnVictory -= ShowVictory;
            cc.OnDefeat -= ShowDefeat;
            cc.OnNPCStatChanged -= OnNPCStatChanged;
        }

        // \u9500\u6bc1\u8fd0\u884c\u65f6\u521b\u5efa\u7684\u9762\u677f
        foreach (var go in _runtimeCreated)
            if (go != null) Destroy(go);
        _runtimeCreated.Clear();

        // prefab \u9762\u677f\u53ea\u9700\u6e05\u5f15\u7528\uff0c\u4e0d Destroy（\u968f\u573a\u666f\u5378\u8f7d\u81ea\u52a8\u9500\u6bc1\uff09
        _speechPanels.Clear();
        _speechTexts.Clear();
        _speechNames.Clear();
        _akanaCardButtons.Clear();
        _akanaCardImages.Clear();
        _cardDetailPanels.Clear();
        _cardDetailCGs.Clear();
        _ruleNPCStatTexts.Clear();

        _ruleButton = null;
        _rulePanel = null;
        _rulePanelCG = null;
        _akanaMenuPanel = null;
        _akanaMenuCG = null;
        _selectTargetPanel = null;
        _selectTargetCG = null;
        _victoryPanel = null;
        _defeatPanel = null;
        for (int i = 0; i < 5; i++) _roundResultPanels[i] = null;
        _isCourt = false;
    }

    private void OnDestroy()
    {
        if (UIManager.Instance != null)
            UIManager.Instance.OnSceneRootRegistered -= OnSceneLoaded;
        Cleanup();
        if (Instance == this) Instance = null;
    }

    // ════  UI 工具  ════════════════════════════════════════════════════════════

    /// <summary>\u5728\u6307\u5b9a\u5b50\u7269\u4f53\u4e0a\u67e5\u627e TMP \u6587\u672c\u7ec4\u4ef6\u3002</summary>
    private static TextMeshProUGUI FindTMP(Transform parent, string childName)
    {
        var child = parent.Find(childName);
        if (child == null) return null;
        var tmp = child.GetComponent<TextMeshProUGUI>();
        if (tmp != null) return tmp;
        // \u5c1d\u8bd5 InputField \u5185\u90e8\u7684 text \u7ec4\u4ef6
        var input = child.GetComponent<TMP_InputField>();
        if (input != null) return input.textComponent as TextMeshProUGUI;
        return child.GetComponentInChildren<TextMeshProUGUI>(true);
    }

    private static GameObject CreateFullscreenPanel(Transform parent, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        var bg = go.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.7f);
        bg.raycastTarget = true;
        return go;
    }

    private static GameObject CreatePanel(Transform parent, string name, float w, float h, Vector2 pos)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(w, h);
        rect.anchoredPosition = pos;
        return go;
    }

    private static Image SetImage(GameObject go, Color color)
    {
        var img = go.GetComponent<Image>();
        if (img == null) img = go.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = true;
        return img;
    }

    private static TextMeshProUGUI CreateTMP(Transform parent, string text, float fontSize,
        Color color, TextAlignmentOptions alignment,
        Vector2? anchoredPos = null, Vector2? sizeDelta = null)
    {
        var go = new GameObject("Text");
        go.transform.SetParent(parent, false);
        var rect = go.AddComponent<RectTransform>();
        if (sizeDelta.HasValue)
            rect.sizeDelta = sizeDelta.Value;
        else
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
        if (anchoredPos.HasValue)
            rect.anchoredPosition = anchoredPos.Value;

        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = color;
        tmp.alignment = alignment;
        tmp.enableWordWrapping = true;
        tmp.overflowMode = TextOverflowModes.Ellipsis;
        tmp.raycastTarget = false;
        ChineseFontProvider.ApplyFont(tmp);
        return tmp;
    }

    private static CanvasGroup EnsureCG(GameObject go)
    {
        var cg = go.GetComponent<CanvasGroup>();
        if (cg == null) cg = go.AddComponent<CanvasGroup>();
        return cg;
    }

    private static void HideImmediate(GameObject go, CanvasGroup cg)
    {
        cg.alpha = 0f;
        cg.interactable = false;
        cg.blocksRaycasts = false;
        go.SetActive(false);
    }

    private static void FadeIn(GameObject go, CanvasGroup cg)
    {
        go.SetActive(true);
        cg.alpha = 0f;
        cg.interactable = false;
        cg.blocksRaycasts = true;
        cg.DOFade(1f, FADE_DURATION).SetUpdate(true).SetEase(Ease.OutQuad)
            .OnComplete(() => { cg.interactable = true; });
    }

    private static void FadeOut(GameObject go, CanvasGroup cg, Action onComplete = null)
    {
        cg.interactable = false;
        cg.blocksRaycasts = false;
        cg.DOFade(0f, FADE_DURATION).SetUpdate(true).SetEase(Ease.InQuad)
            .OnComplete(() =>
            {
                go.SetActive(false);
                onComplete?.Invoke();
            });
    }

    private static string FormatNPCStatLine(string name, int rational, int emotional, string threshold)
    {
        return name + "  \u7406\u6027 " + rational + " / \u611f\u6027 " + emotional + "  " + threshold;
    }
}
