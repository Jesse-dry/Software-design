using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using System.Collections.Generic;
using TMPro;

/// <summary>
/// 阿卡那牌 HUD 控制器（全局持久，跨场景自动注入）。
///
/// ══ 架构说明 ══
///   完全复用 InGameMenuController 的注入模式：
///   ┌──────── [MANAGERS] (DontDestroyOnLoad) ─────────────────────────┐
///   │  UIManager.InitializeGlobalUI()                                 │
///   │    └─ AkanaHUDController.Initialize()  ← 创建一次，全局持久     │
///   └──────────────────────────────────────────────────────────────────┘
///
///   每次场景加载（UISceneRoot.Awake → UIManager.OnSceneRootRegistered）：
///   1. 在 HUDLayer 查找 "akana" 按钮 → 绑定点击→打开 akanaMenu
///   2. 在 OverlayLayer 查找 "akanaMenu" 面板 → 初始隐藏，按需显示
///   3. 在 akanaMenu 内查找 4 张卡牌按钮（圣杯/星币/宝剑/权杖）
///      → 已收集=正常颜色+可点击, 未收集=黑色+不可交互
///   4. 在 ModalLayer 查找 4 个牌描述面板（圣杯牌/星币牌/宝剑牌/权杖牌）
///      → 点击收集的卡牌→打开对应描述，面板内 Button 关闭描述
///   5. akanaMenu 内 "ButtonToContinue" → 关闭菜单
///
/// ══ UI 层级关系 ══
///   HUDLayer(10)：akana 按钮始终可见
///   OverlayLayer(50)：akanaMenu 打开时覆盖 HUD
///   ModalLayer(90)：卡牌描述打开时覆盖一切（含 ModalBackground 遮罩）
///
/// ══ 不需要手动挂载。由 Bootstrapper 自动创建。══
/// </summary>
public class AkanaHUDController : MonoBehaviour
{
    // ══════════════════════════════════════════════════════════════
    //  单例 + 初始化
    // ══════════════════════════════════════════════════════════════

    public static AkanaHUDController Instance { get; private set; }

    /// <summary>
    /// 由 UIManager.InitializeGlobalUI() 调用，仅执行一次。
    /// </summary>
    public static AkanaHUDController Initialize(Transform parent)
    {
        if (Instance != null) return Instance;

        var go = new GameObject("AkanaHUDController_Logic");
        go.transform.SetParent(parent, false);
        Instance = go.AddComponent<AkanaHUDController>();

        if (UIManager.Instance != null)
        {
            UIManager.Instance.OnSceneRootRegistered += Instance.OnSceneLoaded;

            // 防漏补丁
            if (UIManager.Instance.HasSceneRoot && UIManager.Instance.hudLayer != null)
            {
                var existingRoot = UIManager.Instance.hudLayer
                    .GetComponentInParent<UISceneRoot>();
                if (existingRoot != null)
                {
                    Debug.Log("[AkanaHUD] 检测到场景已存在，立即补发加载！");
                    Instance.OnSceneLoaded(existingRoot);
                }
            }
        }

        // 监听卡牌收集事件
        if (AkanaManager.Instance != null)
        {
            AkanaManager.Instance.OnCardCollected += Instance.OnCardCollected;
        }

        return Instance;
    }

    // ══════════════════════════════════════════════════════════════
    //  配置常量
    // ══════════════════════════════════════════════════════════════

    /// <summary>HUD 中阿卡那按钮名称</summary>
    private const string HUD_BUTTON_NAME = "akana";

    /// <summary>OverlayLayer 中阿卡那菜单名称</summary>
    private const string MENU_PANEL_NAME = "akanaMenu";

    /// <summary>akanaMenu 中关闭按钮名称</summary>
    private const string CLOSE_BUTTON_NAME = "ButtonToContinue";

    /// <summary>ModalLayer 中模态背景名称</summary>
    private const string MODAL_BG_NAME = "ModalBackground";

    /// <summary>卡牌描述面板内关闭按钮名称</summary>
    private const string CARD_CLOSE_BUTTON_NAME = "Button";

    /// <summary>未收集卡牌的颜色（纯黑遮盖）</summary>
    private static readonly Color UNCOLLECTED_COLOR = Color.black;

    /// <summary>已收集卡牌的正常颜色</summary>
    private static readonly Color COLLECTED_COLOR = Color.white;

    /// <summary>
    /// 四张卡牌的 ID 与 prefab 内 GameObject 名称映射。
    /// akanaMenu 内子物体名：圣杯、星币、宝剑、权杖
    /// ModalLayer 内子物体名：圣杯牌、星币牌、宝剑牌、权杖牌
    /// </summary>
    private static readonly AkanaCardId[] ALL_CARDS = {
        AkanaCardId.圣杯, AkanaCardId.星币, AkanaCardId.宝剑, AkanaCardId.权杖
    };

    // ══════════════════════════════════════════════════════════════
    //  动画参数
    // ══════════════════════════════════════════════════════════════

    [Header("== 动画配置 ==")]
    [SerializeField] private float fadeDuration = 0.25f;
    [SerializeField] private Ease showEase = Ease.OutBack;
    [SerializeField] private Ease hideEase = Ease.InQuad;

    // ══════════════════════════════════════════════════════════════
    //  运行时引用（每次场景加载重新赋值）
    // ══════════════════════════════════════════════════════════════

    private Button    _hudButton;           // HUDLayer 中的 akana 按钮
    private GameObject _menuPanel;          // OverlayLayer 中的 akanaMenu
    private CanvasGroup _menuCG;            // akanaMenu 的 CanvasGroup
    private RectTransform _menuRect;        // akanaMenu 的 RectTransform
    private Button    _closeButton;         // akanaMenu 中的 ButtonToContinue

    // 卡牌按钮映射：akanaMenu 中的 4 张卡牌
    private readonly Dictionary<AkanaCardId, Button> _cardButtons = new Dictionary<AkanaCardId, Button>();
    private readonly Dictionary<AkanaCardId, Image>  _cardImages  = new Dictionary<AkanaCardId, Image>();

    // 卡牌描述面板映射：ModalLayer 中的 4 个面板
    private readonly Dictionary<AkanaCardId, GameObject> _cardPanels = new Dictionary<AkanaCardId, GameObject>();
    private readonly Dictionary<AkanaCardId, Button>     _cardPanelCloseButtons = new Dictionary<AkanaCardId, Button>();

    // ModalBackground
    private GameObject _modalBackground;

    private bool _isMenuOpen = false;
    private bool _isCardDetailOpen = false;
    private bool _isAnimating = false;
    private AkanaCardId _openedCardDetail;

    /// <summary>关闭菜单后执行一次的回调（用于通关流程等外部驱动场景）</summary>
    private System.Action _onMenuHiddenOnce;

    /// <summary>关闭卡牌描述面板后执行一次的回调</summary>
    private System.Action _onCardDetailHiddenOnce;

    // ══════════════════════════════════════════════════════════════
    //  场景加载回调
    // ══════════════════════════════════════════════════════════════

    private void OnSceneLoaded(UISceneRoot root)
    {
        Debug.Log($"[AkanaHUD] 监听到新场景加载: {(root != null ? root.name : "null")}");

        // 清理旧引用
        CleanupPreviousScene();

        if (root == null) return;

        // 绑定 HUD 按钮
        if (root.hudLayer != null)
            BindHudButton(root.hudLayer);

        // 绑定菜单面板
        if (root.overlayLayer != null)
            SetupMenuPanel(root.overlayLayer);

        // 绑定 Modal 层的卡牌描述面板
        if (root.modalLayer != null)
            SetupCardDetailPanels(root.modalLayer);
    }

    // ══════════════════════════════════════════════════════════════
    //  HUD 按钮（akana）
    // ══════════════════════════════════════════════════════════════

    private void BindHudButton(Transform hudLayer)
    {
        var btnTransform = FindInChildren(hudLayer, HUD_BUTTON_NAME);
        if (btnTransform == null)
        {
            Debug.Log("[AkanaHUD] 当前场景 HUDLayer 中无 'akana' 按钮，跳过。");
            return;
        }

        _hudButton = btnTransform.GetComponent<Button>();
        if (_hudButton == null)
        {
            // 如果 prefab 没有 Button 组件，自动添加
            _hudButton = btnTransform.gameObject.AddComponent<Button>();
            Debug.Log("[AkanaHUD] 'akana' 物体缺少 Button 组件，已自动添加。");
        }

        _hudButton.onClick.AddListener(OnHudButtonClicked);
        Debug.Log("[AkanaHUD] 已绑定 HUD 'akana' 按钮。");
    }

    // ══════════════════════════════════════════════════════════════
    //  菜单面板设置（akanaMenu）
    // ══════════════════════════════════════════════════════════════

    private void SetupMenuPanel(Transform overlayLayer)
    {
        // 优先查找场景内已存在的 akanaMenu
        var menuTransform = FindInChildren(overlayLayer, MENU_PANEL_NAME);
        if (menuTransform == null)
        {
            Debug.Log("[AkanaHUD] OverlayLayer 中未找到 'akanaMenu'，当前场景无阿卡那牌菜单。");
            return;
        }

        _menuPanel = menuTransform.gameObject;
        _menuRect = _menuPanel.GetComponent<RectTransform>();

        // 确保有 CanvasGroup
        _menuCG = _menuPanel.GetComponent<CanvasGroup>();
        if (_menuCG == null) _menuCG = _menuPanel.AddComponent<CanvasGroup>();

        // 绑定关闭按钮
        _closeButton = FindButtonByName(_menuPanel.transform, CLOSE_BUTTON_NAME);
        if (_closeButton != null)
        {
            _closeButton.onClick.AddListener(OnCloseMenuClicked);
            Debug.Log("[AkanaHUD] 已绑定 akanaMenu 'ButtonToContinue' 关闭按钮。");
        }
        else
        {
            Debug.LogWarning("[AkanaHUD] akanaMenu 中未找到 'ButtonToContinue' 关闭按钮！");
        }

        // 绑定 4 张卡牌按钮
        _cardButtons.Clear();
        _cardImages.Clear();

        foreach (var cardId in ALL_CARDS)
        {
            string cardName = AkanaManager.GetCardDisplayName(cardId);
            var cardTransform = FindInChildren(_menuPanel.transform, cardName);

            if (cardTransform == null)
            {
                Debug.LogWarning($"[AkanaHUD] akanaMenu 中未找到卡牌 '{cardName}'！");
                continue;
            }

            var img = cardTransform.GetComponent<Image>();
            var btn = cardTransform.GetComponent<Button>();

            if (img != null) _cardImages[cardId] = img;
            if (btn != null)
            {
                _cardButtons[cardId] = btn;
                // 捕获 cardId 的闭包
                var capturedId = cardId;
                btn.onClick.AddListener(() => OnCardClicked(capturedId));
            }
        }

        // 初始化卡牌显示状态
        RefreshCardStates();

        // 强制初始隐藏
        _menuCG.alpha = 0f;
        _menuCG.interactable = false;
        _menuCG.blocksRaycasts = false;
        _menuPanel.SetActive(false);

        _isMenuOpen = false;
        _isAnimating = false;

        Debug.Log("[AkanaHUD] akanaMenu 面板初始化完成。");
    }

    // ══════════════════════════════════════════════════════════════
    //  卡牌描述面板设置（ModalLayer 中的 X牌）
    // ══════════════════════════════════════════════════════════════

    private void SetupCardDetailPanels(Transform modalLayer)
    {
        _cardPanels.Clear();
        _cardPanelCloseButtons.Clear();

        // 查找 ModalBackground
        var bgTransform = FindInChildren(modalLayer, MODAL_BG_NAME);
        if (bgTransform != null)
        {
            _modalBackground = bgTransform.gameObject;
        }

        foreach (var cardId in ALL_CARDS)
        {
            string panelName = AkanaManager.GetCardPanelName(cardId);
            var panelTransform = FindInChildren(modalLayer, panelName);

            if (panelTransform == null)
            {
                Debug.Log($"[AkanaHUD] ModalLayer 中未找到 '{panelName}'，该卡牌描述面板不可用。");
                continue;
            }

            var panelGO = panelTransform.gameObject;
            _cardPanels[cardId] = panelGO;

            // 查找面板内的关闭按钮
            var closeBtn = FindButtonByName(panelTransform, CARD_CLOSE_BUTTON_NAME);
            if (closeBtn != null)
            {
                _cardPanelCloseButtons[cardId] = closeBtn;
                var capturedId = cardId;
                closeBtn.onClick.AddListener(() => OnCardDetailCloseClicked(capturedId));
            }
            else
            {
                Debug.LogWarning($"[AkanaHUD] '{panelName}' 中未找到 'Button' 关闭按钮！");
            }

            // 初始隐藏
            panelGO.SetActive(false);
        }

        Debug.Log($"[AkanaHUD] 已设置 {_cardPanels.Count} 个卡牌描述面板。");
    }

    // ══════════════════════════════════════════════════════════════
    //  卡牌状态刷新
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// 根据 AkanaManager 的收集数据刷新 akanaMenu 中卡牌的显示状态。
    /// 已收集：正常颜色 + 可交互
    /// 未收集：黑色遮盖 + 不可交互
    /// </summary>
    public void RefreshCardStates()
    {
        var akana = AkanaManager.Instance;
        if (akana == null) return;

        foreach (var cardId in ALL_CARDS)
        {
            bool collected = akana.HasCard(cardId);

            // 更新图片颜色
            if (_cardImages.TryGetValue(cardId, out var img))
            {
                img.color = collected ? COLLECTED_COLOR : UNCOLLECTED_COLOR;
            }

            // 更新按钮交互性
            if (_cardButtons.TryGetValue(cardId, out var btn))
            {
                btn.interactable = collected;
            }
        }
    }

    // ══════════════════════════════════════════════════════════════
    //  按钮回调
    // ══════════════════════════════════════════════════════════════

    private void OnHudButtonClicked()
    {
        if (_isAnimating) return;

        if (_isMenuOpen)
            HideMenu();
        else
            ShowMenu();
    }

    private void OnCloseMenuClicked()
    {
        if (_isAnimating) return;
        HideMenu();
    }

    private void OnCardClicked(AkanaCardId cardId)
    {
        if (_isAnimating || _isCardDetailOpen) return;

        // 只有已收集的卡牌才可以查看
        if (AkanaManager.Instance == null || !AkanaManager.Instance.HasCard(cardId))
            return;

        ShowCardDetail(cardId);
    }

    private void OnCardDetailCloseClicked(AkanaCardId cardId)
    {
        HideCardDetail(cardId);
    }

    /// <summary>卡牌收集事件回调 — 实时刷新菜单（如果菜单是打开的）</summary>
    private void OnCardCollected(AkanaCardId cardId)
    {
        RefreshCardStates();
    }

    // ══════════════════════════════════════════════════════════════
    //  菜单显示 / 隐藏（DOTween 动画）
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// 打开 akanaMenu，关闭时自动执行 onClosed 回调（仅一次）。
    /// 用于通关流程：查看完牌后自动跳转下一步。
    /// </summary>
    public void ShowMenuWithCallback(System.Action onClosed)
    {
        _onMenuHiddenOnce = onClosed;
        ShowMenu();
    }

    public void ShowMenu()
    {
        if (_menuPanel == null || _isMenuOpen) return;

        // 每次打开菜单时刷新卡牌状态
        RefreshCardStates();

        _isMenuOpen = true;
        _isAnimating = true;

        _menuPanel.SetActive(true);
        _menuCG.alpha = 0f;
        _menuCG.interactable = false;
        _menuCG.blocksRaycasts = false;

        var seq = DOTween.Sequence().SetUpdate(true);
        seq.Join(_menuCG.DOFade(1f, fadeDuration).SetEase(showEase));
        seq.OnComplete(() =>
        {
            _menuCG.interactable = true;
            _menuCG.blocksRaycasts = true;
            _isAnimating = false;
        });
    }

    public void HideMenu()
    {
        if (_menuPanel == null || !_isMenuOpen) return;

        // 如果有卡牌描述面板打开，先关闭
        if (_isCardDetailOpen)
        {
            HideCardDetail(_openedCardDetail);
        }

        _isMenuOpen = false;
        _isAnimating = true;

        _menuCG.interactable = false;
        _menuCG.blocksRaycasts = false;

        var seq = DOTween.Sequence().SetUpdate(true);
        seq.Join(_menuCG.DOFade(0f, fadeDuration).SetEase(hideEase));
        seq.OnComplete(() =>
        {
            _menuPanel.SetActive(false);
            _isAnimating = false;

            // 触发一次性回调（通关 → 查看牌 → 关闭后跳转）
            var cb = _onMenuHiddenOnce;
            _onMenuHiddenOnce = null;
            cb?.Invoke();
        });
    }

    // ══════════════════════════════════════════════════════════════
    //  卡牌描述面板显示 / 隐藏
    // ══════════════════════════════════════════════════════════════

    private void ShowCardDetail(AkanaCardId cardId)
    {
        if (!_cardPanels.TryGetValue(cardId, out var panel)) return;

        _isCardDetailOpen = true;
        _openedCardDetail = cardId;

        // ── LLM 文本注入：如果大模型已生成文本，替换 prefab 原始内容 ──
        TryInjectLLMCardText(cardId, panel);

        // 显示模态背景
        if (_modalBackground != null)
        {
            _modalBackground.SetActive(true);
            var bgImg = _modalBackground.GetComponent<Image>();
            if (bgImg != null)
            {
                bgImg.color = new Color(0f, 0f, 0f, 0f);
                bgImg.DOFade(0.75f, fadeDuration).SetUpdate(true);
            }
        }

        panel.SetActive(true);

        // 淡入动画
        var cg = panel.GetComponent<CanvasGroup>();
        if (cg == null) cg = panel.AddComponent<CanvasGroup>();
        cg.alpha = 0f;
        cg.interactable = false;
        cg.blocksRaycasts = false;

        var seq = DOTween.Sequence().SetUpdate(true);
        seq.Join(cg.DOFade(1f, fadeDuration).SetEase(showEase));
        seq.OnComplete(() =>
        {
            cg.interactable = true;
            cg.blocksRaycasts = true;
        });

        Debug.Log($"[AkanaHUD] 显示卡牌描述: {AkanaManager.GetCardPanelName(cardId)}");
    }

    private void HideCardDetail(AkanaCardId cardId)
    {
        if (!_cardPanels.TryGetValue(cardId, out var panel)) return;

        _isCardDetailOpen = false;

        var cg = panel.GetComponent<CanvasGroup>();
        if (cg == null) cg = panel.AddComponent<CanvasGroup>();
        cg.interactable = false;
        cg.blocksRaycasts = false;

        var seq = DOTween.Sequence().SetUpdate(true);
        seq.Join(cg.DOFade(0f, fadeDuration).SetEase(hideEase));
        seq.OnComplete(() =>
        {
            panel.SetActive(false);
        });

        // 隐藏模态背景
        if (_modalBackground != null)
        {
            var bgImg = _modalBackground.GetComponent<Image>();
            if (bgImg != null)
            {
                bgImg.DOFade(0f, fadeDuration).SetUpdate(true)
                    .OnComplete(() => _modalBackground.SetActive(false));
            }
            else
            {
                _modalBackground.SetActive(false);
            }
        }

        Debug.Log($"[AkanaHUD] 隐藏卡牌描述: {AkanaManager.GetCardPanelName(cardId)}");

        // 触发一次性回调（通关查看牌后自动跳转）
        var cb = _onCardDetailHiddenOnce;
        _onCardDetailHiddenOnce = null;
        cb?.Invoke();
    }

    // ══════════════════════════════════════════════════════════════
    //  查询接口
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// 显示指定卡牌的描述面板，关闭后自动执行 onClosed 回调（仅一次）。
    /// 通关流程统一入口：收集卡牌 → Toast → Modal是否查看 → 是 → 此方法 → 关闭后下一步。
    /// </summary>
    public void ShowCardDetailWithCallback(AkanaCardId cardId, System.Action onClosed)
    {
        _onCardDetailHiddenOnce = onClosed;
        ShowCardDetail(cardId);
    }

    /// <summary>akanaMenu 当前是否打开</summary>
    public bool IsMenuOpen => _isMenuOpen;

    /// <summary>是否有卡牌描述面板打开</summary>
    public bool IsCardDetailOpen => _isCardDetailOpen;

    // ══════════════════════════════════════════════════════════════
    //  清理
    // ══════════════════════════════════════════════════════════════

    private void CleanupPreviousScene()
    {
        // 解绑 HUD 按钮
        if (_hudButton != null)
        {
            _hudButton.onClick.RemoveListener(OnHudButtonClicked);
            _hudButton = null;
        }

        // 解绑菜单关闭按钮
        if (_closeButton != null)
        {
            _closeButton.onClick.RemoveListener(OnCloseMenuClicked);
            _closeButton = null;
        }

        // 解绑卡牌按钮
        foreach (var kvp in _cardButtons)
        {
            if (kvp.Value != null)
                kvp.Value.onClick.RemoveAllListeners();
        }
        _cardButtons.Clear();
        _cardImages.Clear();

        // 解绑描述面板关闭按钮
        foreach (var kvp in _cardPanelCloseButtons)
        {
            if (kvp.Value != null)
                kvp.Value.onClick.RemoveAllListeners();
        }
        _cardPanels.Clear();
        _cardPanelCloseButtons.Clear();

        _menuPanel = null;
        _menuCG = null;
        _menuRect = null;
        _closeButton = null;
        _modalBackground = null;

        _isMenuOpen = false;
        _isCardDetailOpen = false;
        _isAnimating = false;
    }

    private void OnDestroy()
    {
        if (UIManager.Instance != null)
            UIManager.Instance.OnSceneRootRegistered -= OnSceneLoaded;

        if (AkanaManager.Instance != null)
            AkanaManager.Instance.OnCardCollected -= OnCardCollected;

        CleanupPreviousScene();
        Instance = null;
    }

    // ══════════════════════════════════════════════════════════════
    //  LLM 文本注入
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// 卡牌描述面板中的 "文本内容" 子物体名称（与 prefab 一致）。
    /// </summary>
    private const string CARD_TEXT_NODE_NAME = "文本内容";

    /// <summary>
    /// 如果 LLM 模式启用且文本已就绪，将面板内 "文本内容" 的 TMP_Text 替换为 LLM 生成文本。
    /// 否则保留 prefab 原始文本不变。
    /// </summary>
    private void TryInjectLLMCardText(AkanaCardId cardId, GameObject panel)
    {
        if (!LLMBridge.IsEnabled) return;

        string llmText = LLMBridge.GetCardText(cardId);
        if (string.IsNullOrEmpty(llmText)) return;

        var textNode = FindInChildren(panel.transform, CARD_TEXT_NODE_NAME);
        if (textNode == null)
        {
            Debug.LogWarning($"[AkanaHUD] 面板 '{panel.name}' 中未找到 '{CARD_TEXT_NODE_NAME}' 子物体！");
            return;
        }

        var tmpText = textNode.GetComponent<TMP_Text>();
        if (tmpText != null)
        {
            tmpText.text = llmText;
            Debug.Log($"[AkanaHUD] 已注入 LLM 文本到 {AkanaManager.GetCardPanelName(cardId)}");
        }
        else
        {
            Debug.LogWarning($"[AkanaHUD] '{CARD_TEXT_NODE_NAME}' 上未找到 TMP_Text 组件！");
        }
    }

    // ══════════════════════════════════════════════════════════════
    //  工具方法
    // ══════════════════════════════════════════════════════════════

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

    private static Button FindButtonByName(Transform parent, string name)
    {
        if (parent == null) return null;
        foreach (var btn in parent.GetComponentsInChildren<Button>(true))
        {
            if (btn.gameObject.name == name)
                return btn;
        }
        return null;
    }
}
