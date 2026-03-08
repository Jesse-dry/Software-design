using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using System;
using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

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
    private const string CARD_TEXT_NODE_NAME = "文本内容";
    /// <summary>LLM 证词评分≥此值时给予 20% 加成</summary>
    private const int ARGUMENT_BONUS_THRESHOLD = 7;
    private const float ARGUMENT_BONUS_MULTIPLIER = 1.2f;

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

    // NPC 多轮发言文本槽：TextForInput(1), TextForInput (2), TextForInput(3)
    private readonly Dictionary<CourtData.NPCId, TextMeshProUGUI[]> _speechRoundTexts = new();
    /// <summary>每个 NPC 已发言次数（用于定位 TextForInput 槽位）</summary>
    private readonly Dictionary<CourtData.NPCId, int> _npcSpeechCount = new();

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

    // HUD 层 CanvasGroup（用于在模态面板激活时隐藏 HUD）
    private CanvasGroup _hudCG;

    // 运行时创建的 GO，清理时 Destroy
    private readonly List<GameObject> _runtimeCreated = new();

    private bool _isCourt = false;

    // ════  场景加载  ════════════════════════════════════════════════════════

    private void OnSceneLoaded(UISceneRoot root)
    {
        Debug.Log("[CourtUI] >>> OnSceneLoaded called. root=" + (root != null ? root.name : "NULL"));
        Cleanup();

        if (root == null) { Debug.LogWarning("[CourtUI] root is null, abort."); return; }

        // 不依赖 GameManager 阶段（Awake 时 GM.Start 尚未执行），改用场景根名判断
        bool isCourt = root.name.Contains("Court");
        Debug.Log("[CourtUI] root.name='" + root.name + "' isCourt=" + isCourt);
        if (!isCourt) return;
        _isCourt = true;
        _hudLayer = root.hudLayer;
        _overlayLayer = root.overlayLayer;
        _modalLayer = root.modalLayer;

        Debug.Log("[CourtUI] root.hudLayer=" + (root.hudLayer != null ? root.hudLayer.name : "NULL")
            + ", root.overlayLayer=" + (root.overlayLayer != null ? root.overlayLayer.name : "NULL")
            + ", root.modalLayer=" + (root.modalLayer != null ? root.modalLayer.name : "NULL"));

        if (_hudLayer == null)
        {
            var found = root.transform.Find("HUDLayer (1)") ?? root.transform.Find("HUDLayer");
            Debug.Log("[CourtUI] hudLayer fallback search: " + (found != null ? found.name : "NOT FOUND"));
            if (found != null) _hudLayer = found as RectTransform;
        }

        if (_hudLayer == null || _overlayLayer == null || _modalLayer == null)
        {
            Debug.LogWarning("[CourtUI] \u7f3a\u5c11\u5fc5\u8981\u7684 UI \u5c42\u3002 hud=" + (_hudLayer != null) + " overlay=" + (_overlayLayer != null) + " modal=" + (_modalLayer != null));
            return;
        }

        // 初始化 HUD 层 CanvasGroup
        _hudCG = EnsureCG(_hudLayer.gameObject);

        // Phase 0: \u5148\u9690\u85cf\u6240\u6709\u5c42\u7ea7\u5b50\u7269\u4f53\uff0c\u7531\u72b6\u6001\u673a\u6309\u9700\u663e\u793a
        Debug.Log("[CourtUI] Phase 0: HideAllLayerChildren...");
        HideAllLayerChildren();
        Debug.Log("[CourtUI] Phase 0 done.");

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

        // Phase 2: \u7ed1\u5b9a\u63a7\u5236\u5668\u4e8b\u4ef6
        Debug.Log("[CourtUI] Phase 2: BindCourtController...");
        BindCourtController();

        Debug.Log("[CourtUI] \u5ead\u5ba1 UI \u521d\u59cb\u5316\u5b8c\u6210\u3002\u6700\u7ec8 overlayLayer \u5b50\u7269\u4f53\u72b6\u6001:");
        for (int i = 0; i < _overlayLayer.childCount; i++)
        {
            var c = _overlayLayer.GetChild(i).gameObject;
            var cg2 = c.GetComponent<CanvasGroup>();
            Debug.Log("[CourtUI]   overlay[" + i + "] '" + c.name + "' active=" + c.activeSelf + " alpha=" + (cg2 != null ? cg2.alpha.ToString("F2") : "NO_CG"));
        }
        Debug.Log("[CourtUI] \u6700\u7ec8 modalLayer \u5b50\u7269\u4f53\u72b6\u6001:");
        for (int i = 0; i < _modalLayer.childCount; i++)
        {
            var c = _modalLayer.GetChild(i).gameObject;
            var cg2 = c.GetComponent<CanvasGroup>();
            Debug.Log("[CourtUI]   modal[" + i + "] '" + c.name + "' active=" + c.activeSelf + " alpha=" + (cg2 != null ? cg2.alpha.ToString("F2") : "NO_CG"));
        }
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
            if (child == null)
            {
                Debug.LogWarning("[CourtUI] \u89c4\u5219\u9762\u677f\u4e2d\u672a\u627e\u5230\u5b50\u7269\u4f53: '" + npcStatNames[i] + "'");
                continue;
            }
            // \u5217\u51fa\u5b50\u7269\u4f53\u6240\u6709 TMP \u7ec4\u4ef6
            var allTmp = child.GetComponentsInChildren<TextMeshProUGUI>(true);
            Debug.Log("[CourtUI] \u89c4\u5219\u9762\u677f '" + npcStatNames[i] + "' \u4e2d\u627e\u5230 " + allTmp.Length + " \u4e2a TMP:");
            for (int j = 0; j < allTmp.Length; j++)
                Debug.Log("[CourtUI]   TMP[" + j + "] go='" + allTmp[j].gameObject.name + "' text='" + allTmp[j].text + "'");

            var tmp = child.GetComponent<TextMeshProUGUI>();
            if (tmp == null) tmp = child.GetComponentInChildren<TextMeshProUGUI>(true);
            if (tmp != null) _ruleNPCStatTexts[npcIds[i]] = tmp;
        }
        Debug.Log("[CourtUI] BindRulePanel: \u6210\u529f\u7ed1\u5b9a " + _ruleNPCStatTexts.Count + " \u4e2a NPC \u5c5e\u6027\u6587\u672c");

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
        _speechRoundTexts.Clear();
        _npcSpeechCount.Clear();

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

            // \u591a\u8f6e\u53d1\u8a00\u6587\u672c\u69fd: TextForInput(1), TextForInput (2), TextForInput(3)
            var roundTexts = new TextMeshProUGUI[3];
            roundTexts[0] = FindTMPFuzzy(found, "TextForInput", "1");
            roundTexts[1] = FindTMPFuzzy(found, "TextForInput", "2");
            roundTexts[2] = FindTMPFuzzy(found, "TextForInput", "3");
            _speechRoundTexts[p.id] = roundTexts;
            _npcSpeechCount[p.id] = 0;

            int boundSlots = 0;
            for (int i = 0; i < 3; i++) if (roundTexts[i] != null) boundSlots++;
            Debug.Log($"[CourtUI] {p.name} \u591a\u8f6e\u6587\u672c\u69fd\u7ed1\u5b9a: {boundSlots}/3");

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
        Debug.Log("[CourtUI] HideAllLayerChildren: overlayLayer childCount=" + _overlayLayer.childCount + ", modalLayer childCount=" + _modalLayer.childCount);
        HideLayerChildren(_overlayLayer);
        HideLayerChildren(_modalLayer);
    }

    private void HideLayerChildren(Transform layer)
    {
        Debug.Log("[CourtUI]   HideLayerChildren(" + layer.name + ") count=" + layer.childCount);
        for (int i = 0; i < layer.childCount; i++)
        {
            var child = layer.GetChild(i).gameObject;
            bool wasBefore = child.activeSelf;
            var cg = EnsureCG(child);
            cg.alpha = 0f;
            cg.interactable = false;
            cg.blocksRaycasts = false;
            child.SetActive(false);
            Debug.Log("[CourtUI]     [" + i + "] '" + child.name + "' wasActive=" + wasBefore + " -> SetActive(false)");
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

        // Bug2: 绑定跳过/不出牌按钮
        var skipBtn = FindButton(found, "ButtonToContinue") ?? FindButton(found, "跳过") ?? FindButton(found, "Pass");
        if (skipBtn != null)
        {
            skipBtn.onClick.AddListener(OnAkanaSkip);
            Debug.Log("[CourtUI] 绑定 akanaMenu 跳过按钮: " + skipBtn.gameObject.name);
        }
        else
        {
            Debug.LogWarning("[CourtUI] 未找到 akanaMenu 跳过按钮，创建备用。");
            var skipGO = CreatePanel(found, "SkipBtn_RT", 180, 48, new Vector2(0, -250));
            SetImage(skipGO, BTN_NORMAL);
            var newBtn = skipGO.AddComponent<Button>();
            newBtn.targetGraphic = skipGO.GetComponent<Image>();
            CreateTMP(skipGO.transform, "不出牌", 22, TEXT_MAIN, TextAlignmentOptions.Center);
            newBtn.onClick.AddListener(OnAkanaSkip);
            _runtimeCreated.Add(skipGO);
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

        // LLM 事件
        cc.OnArgumentInputRequested += ShowArgumentInput;
        cc.OnArgumentScoreResult += ShowScoreFloat;

        Debug.Log("[CourtUI] \u5df2\u7ed1\u5b9a CourtController \u4e8b\u4ef6\u3002");
    }

    // ════  状态变化响应  ════════════════════════════════════════════════════

    private void OnCourtStateChanged(CourtState state)
    {
        // Bug1: 当模态/覆盖面板激活时隐藏 HUD，防止遮挡交互
        bool hideHud = state == CourtState.NPCSpeech || state == CourtState.AkanaMenu ||
                       state == CourtState.CardDetail || state == CourtState.SelectTarget ||
                       state == CourtState.RoundResult || state == CourtState.Victory ||
                       state == CourtState.Defeat;
        if (_hudCG != null)
        {
            _hudCG.alpha = hideHud ? 0f : 1f;
            _hudCG.blocksRaycasts = !hideHud;
            _hudCG.interactable = !hideHud;
        }

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
        Debug.Log("[CourtUI] RefreshRulePanelStats: cc=" + (cc != null ? "OK" : "NULL") + " statTexts=" + _ruleNPCStatTexts.Count);
        if (cc == null) return;

        foreach (var p in CourtData.NPCProfiles)
        {
            if (!_ruleNPCStatTexts.TryGetValue(p.id, out var tmp))
            {
                Debug.LogWarning("[CourtUI]   " + p.name + ": \u65e0\u5bf9\u5e94 TMP");
                continue;
            }
            var npc = cc.GetNPC(p.id);
            if (npc == null)
            {
                Debug.LogWarning("[CourtUI]   " + p.name + ": GetNPC \u8fd4\u56de null");
                continue;
            }

            // Bug4: 只注入 <理性值>/<感性值>
            tmp.text = npc.rational + "/" + npc.emotional;
            Debug.Log("[CourtUI]   " + p.name + ": \u5199\u5165 -> '" + tmp.text + "' go='" + tmp.gameObject.name + "'");
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

        // 确定本 NPC 的发言槽位（第几次发言 → TextForInput(N)）
        bool wroteToSlot = false;
        if (_speechRoundTexts.TryGetValue(speaker, out var roundTexts)
            && _npcSpeechCount.TryGetValue(speaker, out int count))
        {
            int slot = Mathf.Clamp(count, 0, roundTexts.Length - 1);
            if (roundTexts[slot] != null)
            {
                roundTexts[slot].text = text;
                wroteToSlot = true;
                Debug.Log($"[CourtUI] {speaker} 第{count + 1}次发言 → TextForInput({slot + 1})");
            }
            _npcSpeechCount[speaker] = count + 1;
        }

        // fallback: 写入旧的 TextForInput（若存在）
        if (!wroteToSlot && _speechTexts.TryGetValue(speaker, out var tmp))
            tmp.text = text;

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

    /// <summary>Bug2: 玩家在阿卡那菜单选择不出牌，直接进入下一回合。</summary>
    private void OnAkanaSkip()
    {
        HideAkanaMenu(() => { CourtController.Instance?.NotifySkipCard(); });
    }

    // ════  卡牌详情  ══════════════════════════════════════════════════════════

    private void ShowCardDetail(AkanaCardId cardId)
    {
        if (!_cardDetailPanels.TryGetValue(cardId, out var panel)) return;

        // LLM: 注入模型生成的卡牌文本到 "文本内容" 节点
        TryInjectLLMCardText(cardId, panel);

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
            cc.OnArgumentInputRequested -= ShowArgumentInput;
            cc.OnArgumentScoreResult -= ShowScoreFloat;
        }

        // \u9500\u6bc1\u8fd0\u884c\u65f6\u521b\u5efa\u7684\u9762\u677f
        foreach (var go in _runtimeCreated)
            if (go != null) Destroy(go);
        _runtimeCreated.Clear();

        // prefab \u9762\u677f\u53ea\u9700\u6e05\u5f15\u7528\uff0c\u4e0d Destroy（\u968f\u573a\u666f\u5378\u8f7d\u81ea\u52a8\u9500\u6bc1\uff09
        _speechPanels.Clear();
        _speechTexts.Clear();
        _speechNames.Clear();
        _speechRoundTexts.Clear();
        _npcSpeechCount.Clear();
        _akanaCardButtons.Clear();
        _akanaCardImages.Clear();
        _cardDetailPanels.Clear();
        _cardDetailCGs.Clear();
        _ruleNPCStatTexts.Clear();

        _ruleButton = null;
        _rulePanel = null;
        _rulePanelCG = null;
        _hudCG = null;
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

    // ════  LLM: 卡牌文本注入  ══════════════════════════════════════════════

    /// <summary>
    /// 将 LLMBridge 缓存的卡牌文本注入到面板中的 "文本内容" 子物体。
    /// LLM 未启用或文本不可用时静默跳过，不影响原始 prefab 文本。
    /// </summary>
    private void TryInjectLLMCardText(AkanaCardId cardId, GameObject panel)
    {
        if (!LLMBridge.IsEnabled) return;

        string llmText = LLMBridge.GetCardText(cardId);
        if (string.IsNullOrEmpty(llmText)) return;

        var textNode = FindInChildren(panel.transform, CARD_TEXT_NODE_NAME);
        if (textNode == null) return;

        var tmpText = textNode.GetComponent<TMP_Text>();
        if (tmpText != null)
        {
            tmpText.text = llmText;
            Debug.Log($"[CourtUI] LLM 卡牌文本已注入: {cardId}");
        }
    }

    // ════  LLM: 证词输入  ═══════════════════════════════════════════════════

    /// <summary>
    /// 出牌后弹出证词输入 UI，玩家提交后异步获取 LLM 评分，
    /// 完成后通知 CourtController 继续流程。
    /// 仅在 LLMBridge.IsEnabled 时由 OnArgumentInputRequested 事件触发。
    /// </summary>
    private void ShowArgumentInput(AkanaCardId cardId)
    {
        // 运行时创建输入面板
        var bg = CreateFullscreenPanel(_modalLayer, "ArgumentInputBG_RT");
        _runtimeCreated.Add(bg);

        var panel = CreatePanel(bg.transform, "ArgumentInputPanel",
            PANEL_WIDTH * 0.8f, PANEL_HEIGHT * 0.55f, Vector2.zero);
        SetImage(panel, BG_PANEL);

        // 提示文本
        CreateTMP(panel.transform,
            "请输入证词，模型将对证词质量评分。\n若高于7分，牌效将获20%加成。",
            20, TEXT_TITLE, TextAlignmentOptions.Center,
            anchoredPos: new Vector2(0, 90), sizeDelta: new Vector2(650, 80));

        // 输入框容器
        var inputGO = new GameObject("ArgumentInput");
        inputGO.transform.SetParent(panel.transform, false);
        var inputRect = inputGO.AddComponent<RectTransform>();
        inputRect.anchorMin = new Vector2(0.5f, 0.5f);
        inputRect.anchorMax = new Vector2(0.5f, 0.5f);
        inputRect.sizeDelta = new Vector2(620, 100);
        inputRect.anchoredPosition = new Vector2(0, 0);
        var inputBg = inputGO.AddComponent<Image>();
        inputBg.color = new Color(0.06f, 0.06f, 0.1f, 1f);

        // TextArea 子物体
        var textAreaGO = new GameObject("Text Area");
        textAreaGO.transform.SetParent(inputGO.transform, false);
        var textAreaRect = textAreaGO.AddComponent<RectTransform>();
        textAreaRect.anchorMin = Vector2.zero;
        textAreaRect.anchorMax = Vector2.one;
        textAreaRect.offsetMin = new Vector2(10, 5);
        textAreaRect.offsetMax = new Vector2(-10, -5);

        // 输入显示文本
        var inputTextGO = new GameObject("Text");
        inputTextGO.transform.SetParent(textAreaGO.transform, false);
        var inputTextRect = inputTextGO.AddComponent<RectTransform>();
        inputTextRect.anchorMin = Vector2.zero;
        inputTextRect.anchorMax = Vector2.one;
        inputTextRect.offsetMin = Vector2.zero;
        inputTextRect.offsetMax = Vector2.zero;
        var inputTmp = inputTextGO.AddComponent<TextMeshProUGUI>();
        inputTmp.fontSize = 18;
        inputTmp.color = TEXT_MAIN;
        inputTmp.enableWordWrapping = true;
        inputTmp.raycastTarget = false;
        ChineseFontProvider.ApplyFont(inputTmp);

        // Placeholder 文本
        var placeholderGO = new GameObject("Placeholder");
        placeholderGO.transform.SetParent(textAreaGO.transform, false);
        var phRect = placeholderGO.AddComponent<RectTransform>();
        phRect.anchorMin = Vector2.zero;
        phRect.anchorMax = Vector2.one;
        phRect.offsetMin = Vector2.zero;
        phRect.offsetMax = Vector2.zero;
        var phTmp = placeholderGO.AddComponent<TextMeshProUGUI>();
        phTmp.text = "在此输入你的证词论述...";
        phTmp.fontSize = 18;
        phTmp.color = TEXT_DIM;
        phTmp.fontStyle = FontStyles.Italic;
        phTmp.enableWordWrapping = true;
        phTmp.raycastTarget = false;
        ChineseFontProvider.ApplyFont(phTmp);

        // TMP_InputField 组件
        var inputField = inputGO.AddComponent<TMP_InputField>();
        inputField.textViewport = textAreaRect;
        inputField.textComponent = inputTmp;
        inputField.placeholder = phTmp;
        inputField.lineType = TMP_InputField.LineType.MultiLineNewline;
        inputField.characterLimit = 500;
        inputField.fontAsset = inputTmp.font;

        // 提交按钮
        var submitGO = CreatePanel(panel.transform, "SubmitBtn", 180, 45, new Vector2(0, -100));
        SetImage(submitGO, BTN_HIGHLIGHT);
        var submitBtn = submitGO.AddComponent<Button>();
        submitBtn.targetGraphic = submitGO.GetComponent<Image>();
        CreateTMP(submitGO.transform, "提交证词", 22, TEXT_MAIN, TextAlignmentOptions.Center);

        var capturedBg = bg;
        var capturedCard = cardId;
        submitBtn.onClick.AddListener(() =>
        {
            string argument = inputField.text;
            if (string.IsNullOrWhiteSpace(argument)) return; // 空文本不提交
            submitBtn.interactable = false;
            OnArgumentSubmitted(argument, capturedCard, capturedBg).Forget();
        });

        bg.transform.SetAsLastSibling();
        var cg = EnsureCG(bg);
        FadeIn(bg, cg);
        inputField.ActivateInputField();
    }

    /// <summary>
    /// 证词提交后：关闭输入面板 → 调用 LLM 评分 → 显示浮窗 → 通知 Controller。
    /// </summary>
    private async UniTaskVoid OnArgumentSubmitted(string argument, AkanaCardId cardId, GameObject inputBg)
    {
        // 关闭输入面板
        var cg = EnsureCG(inputBg);
        FadeOut(inputBg, cg);

        // 显示加载提示
        var loadingGO = CreatePanel(_modalLayer, "LoadingHint_RT", 300, 60, Vector2.zero);
        SetImage(loadingGO, BG_PANEL);
        CreateTMP(loadingGO.transform, "评分中...", 22, TEXT_TITLE, TextAlignmentOptions.Center);
        loadingGO.transform.SetAsLastSibling();
        var loadingCG = EnsureCG(loadingGO);
        FadeIn(loadingGO, loadingCG);
        _runtimeCreated.Add(loadingGO);

        // 异步调用 LLM 评分
        int score = await LLMBridge.EvaluateArgument(argument, cardId);

        // 关闭加载提示
        FadeOut(loadingGO, loadingCG);

        // 计算加成
        bool hasBonus = score >= ARGUMENT_BONUS_THRESHOLD;
        float multiplier = hasBonus ? ARGUMENT_BONUS_MULTIPLIER : 1f;

        // 显示评分浮窗
        ShowScoreFloat(score, hasBonus);

        // 等待浮窗显示一会儿再通知 Controller
        await UniTask.Delay(2200, ignoreTimeScale: true);

        CourtController.Instance?.NotifyArgumentResult(multiplier);
    }

    // ════  LLM: 评分浮窗  ═══════════════════════════════════════════════════

    /// <summary>
    /// 在画面中央偏上显示评分浮窗，2 秒后自动消失。
    /// </summary>
    private void ShowScoreFloat(int score, bool hasBonus)
    {
        string scoreLabel = score >= 0 ? score.ToString() : "?";
        string bonusText = hasBonus ? "  ✦ 20%加成!" : "";
        Color scoreColor = hasBonus ? new Color(0.3f, 0.95f, 0.5f, 1f) : TEXT_MAIN;

        var floatGO = CreatePanel(_modalLayer, "ScoreFloat_RT", 350, 90, new Vector2(0, 200));
        SetImage(floatGO, new Color(0.1f, 0.1f, 0.15f, 0.92f));
        CreateTMP(floatGO.transform, "模型评分", 16, TEXT_DIM, TextAlignmentOptions.Center,
            anchoredPos: new Vector2(0, 20), sizeDelta: new Vector2(300, 24));
        CreateTMP(floatGO.transform, scoreLabel + " / 10" + bonusText, 30, scoreColor, TextAlignmentOptions.Center,
            anchoredPos: new Vector2(0, -12), sizeDelta: new Vector2(340, 40));

        floatGO.transform.SetAsLastSibling();
        var cg = EnsureCG(floatGO);
        _runtimeCreated.Add(floatGO);

        // 淡入 → 停留 → 上浮淡出
        FadeIn(floatGO, cg);
        var rect = floatGO.GetComponent<RectTransform>();
        DOVirtual.DelayedCall(1.6f, () =>
        {
            rect.DOAnchorPosY(rect.anchoredPosition.y + 60f, 0.5f).SetUpdate(true);
            FadeOut(floatGO, cg);
        }).SetUpdate(true);
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

    /// <summary>
    /// 模糊查找带编号后缀的 TMP 子物体。
    /// 处理 Unity 层级中常见的命名不一致：
    ///   "TextForInput (1)", "TextForInput(1)", "TextForInput 1" 等。
    /// </summary>
    private static TextMeshProUGUI FindTMPFuzzy(Transform parent, string baseName, string number)
    {
        // 尝试多种可能的命名格式
        string[] candidates =
        {
            $"{baseName} ({number})",   // "TextForInput (1)"
            $"{baseName}({number})",    // "TextForInput(1)"
            $"{baseName}{number}",      // "TextForInput1"
            $"{baseName} {number}",     // "TextForInput 1"
        };

        foreach (var name in candidates)
        {
            var result = FindTMP(parent, name);
            if (result != null) return result;
        }

        // 深度搜索：遍历所有子物体，名称包含 baseName 和 number
        foreach (Transform child in parent.GetComponentsInChildren<Transform>(true))
        {
            if (child == parent) continue;
            if (child.name.Contains(baseName) && child.name.Contains(number))
            {
                var tmp = child.GetComponent<TextMeshProUGUI>();
                if (tmp != null) return tmp;
                var input = child.GetComponent<TMP_InputField>();
                if (input != null) return input.textComponent as TextMeshProUGUI;
                tmp = child.GetComponentInChildren<TextMeshProUGUI>(true);
                if (tmp != null) return tmp;
            }
        }

        return null;
    }

    /// <summary>递归查找子物体（按名称精确匹配）。</summary>
    private static Transform FindInChildren(Transform parent, string name)
    {
        if (parent == null) return null;
        foreach (Transform child in parent)
        {
            if (child.name == name) return child;
            var r = FindInChildren(child, name);
            if (r != null) return r;
        }
        return null;
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

    /// <summary>Bug4: 仅输出 理性值/感性值，不带说明文字。</summary>
    private static string FormatNPCStatLine(int rational, int emotional)
    {
        return rational + "/" + emotional;
    }
}
