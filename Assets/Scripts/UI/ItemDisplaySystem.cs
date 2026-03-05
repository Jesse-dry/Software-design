using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using TMPro;
using System;
using System.Collections.Generic;

/// <summary>
/// 道具展示系统（挂在 UIManager 上，跨场景持久）。
/// 
/// 不是传统背包 — 只负责"展示已收集的卡牌/碎片"。
/// 可通过快捷键（默认 Tab）呼出/收起。
/// 
/// 面板布局：
///   左侧为卡牌网格，右侧为选中卡牌的详情。
///   面板出现在 OverlayLayer（不完全打断游戏，但遮挡视线）。
/// 
/// 入场效果可在 Inspector 选择：
///   - SlideFromLeft: 从左侧滑入
///   - FadeIn: 整体淡入
///   - Expand: 从中心展开
/// 
/// 使用示例：
///   UIManager.Instance.ItemDisplay.Toggle();        // 切换显隐
///   UIManager.Instance.ItemDisplay.AddItem(data);   // 新增道具（带收集动画）
///   UIManager.Instance.ItemDisplay.RefreshAll();     // 刷新列表
/// </summary>
public class ItemDisplaySystem : MonoBehaviour
{
    // ── Inspector 配置 ───────────────────────────────────────────
    [Header("== 面板配置 ==")]
    [Tooltip("道具面板 Prefab。需包含：ItemGrid (GridLayoutGroup), DetailPanel, DetailIcon, DetailTitle, DetailDesc, CloseButton。留空则运行时创建。")]
    [SerializeField] private GameObject panelPrefab;

    [Tooltip("单个卡牌槽位 Prefab。需包含：Icon (Image), Title (TMP_Text)。留空则运行时创建。")]
    [SerializeField] private GameObject cardSlotPrefab;

    [Tooltip("面板入场动画")]
    [SerializeField] private ItemPanelAnimation panelAnimation = ItemPanelAnimation.SlideFromLeft;

    [Tooltip("动画时长")]
    [SerializeField] private float animDuration = 0.4f;

    [Header("== 快捷键 ==")]
    [Tooltip("呼出/收起快捷键")]
    [SerializeField] private KeyCode toggleKey = KeyCode.Tab;

    [Header("== 收集通知 ==")]
    [Tooltip("收集新道具时是否通过 Toast 提示")]
    [SerializeField] private bool toastOnCollect = true;

    [Tooltip("收集时卡牌入列动画时长")]
    [SerializeField] private float collectAnimDuration = 0.6f;

    // ── 状态 ─────────────────────────────────────────────────────
    private bool isOpen;
    public bool IsOpen => isOpen;

    private GameObject panelInstance;
    private CanvasGroup panelCG;
    private RectTransform panelRect;
    private Transform itemGrid;
    private Image detailIcon;
    private TMP_Text detailTitle;
    private TMP_Text detailDesc;

    private readonly List<ItemData> collectedItems = new();
    private readonly Dictionary<string, GameObject> slotMap = new();

    /// <summary>当前选中的道具</summary>
    public ItemData SelectedItem { get; private set; }

    /// <summary>道具收集事件</summary>
    public event Action<ItemData> OnItemCollected;

    // ══════════════════════════════════════════════════════════════
    //  初始化
    // ══════════════════════════════════════════════════════════════

    public void Initialize()
    {
        var overlayLayer = UIManager.Instance?.overlayLayer;
        if (overlayLayer == null) return;

        if (panelPrefab != null)
        {
            panelInstance = Instantiate(panelPrefab, overlayLayer);
        }
        else
        {
            panelInstance = CreateRuntimePanel(overlayLayer);
        }

        panelInstance.name = "ItemDisplayPanel";
        panelRect = panelInstance.GetComponent<RectTransform>();
        panelCG = panelInstance.GetComponent<CanvasGroup>();
        if (panelCG == null) panelCG = panelInstance.AddComponent<CanvasGroup>();

        // 绑定子元素
        itemGrid     = panelInstance.transform.Find("ItemGrid");
        detailIcon   = panelInstance.transform.Find("DetailPanel/DetailIcon")?.GetComponent<Image>();
        detailTitle  = panelInstance.transform.Find("DetailPanel/DetailTitle")?.GetComponent<TMP_Text>();
        detailDesc   = panelInstance.transform.Find("DetailPanel/DetailDesc")?.GetComponent<TMP_Text>();

        var closeBtn = panelInstance.transform.Find("CloseButton")?.GetComponent<Button>();
        closeBtn?.onClick.AddListener(() => Close());

        // 初始隐藏
        panelCG.alpha = 0f;
        panelCG.interactable = false;
        panelCG.blocksRaycasts = false;
        panelInstance.SetActive(false);
        isOpen = false;
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            Toggle();
        }
    }

    // ══════════════════════════════════════════════════════════════
    //  公开 API
    // ══════════════════════════════════════════════════════════════

    /// <summary>切换面板显隐</summary>
    public void Toggle()
    {
        if (isOpen) Close();
        else Open();
    }

    /// <summary>打开道具面板</summary>
    public void Open()
    {
        if (isOpen || panelInstance == null) return;
        isOpen = true;
        panelInstance.SetActive(true);
        RefreshAll();
        PlayOpenAnim();
    }

    /// <summary>关闭道具面板</summary>
    public void Close()
    {
        if (!isOpen || panelInstance == null) return;
        PlayCloseAnim(() =>
        {
            isOpen = false;
            panelInstance.SetActive(false);
        });
    }

    /// <summary>
    /// 添加一个已收集的道具。
    /// 如果面板正在显示，会播放入列动画。
    /// </summary>
    public void AddItem(ItemData item)
    {
        if (item == null || collectedItems.Exists(i => i.id == item.id)) return;

        item.isCollected = true;
        item.collectTime = Time.time;
        collectedItems.Add(item);
        OnItemCollected?.Invoke(item);

        if (toastOnCollect)
        {
            UIManager.Instance?.Toast?.Show(
                $"获得：{item.title}",
                ToastStyle.FadeSlideUp, 2.5f, ToastColor.Positive);
        }

        if (isOpen)
        {
            CreateSlot(item, true);
        }
    }

    /// <summary>检查是否已收集某道具</summary>
    public bool HasItem(string id)
    {
        return collectedItems.Exists(i => i.id == id);
    }

    /// <summary>获取所有已收集道具</summary>
    public List<ItemData> GetAllItems()
    {
        return new List<ItemData>(collectedItems);
    }

    /// <summary>刷新整个网格</summary>
    public void RefreshAll()
    {
        if (itemGrid == null) return;

        // 清理旧槽位
        foreach (var kv in slotMap)
            if (kv.Value != null) Destroy(kv.Value);
        slotMap.Clear();

        // 重建
        foreach (var item in collectedItems)
            CreateSlot(item, false);
    }

    /// <summary>选中某个道具并显示详情</summary>
    public void SelectItem(ItemData item)
    {
        SelectedItem = item;
        if (detailIcon != null)  detailIcon.sprite = item?.icon;
        if (detailTitle != null) detailTitle.text = item?.title ?? "";
        if (detailDesc != null)  detailDesc.text = item?.description ?? "";
    }

    // ══════════════════════════════════════════════════════════════
    //  槽位
    // ══════════════════════════════════════════════════════════════

    private void CreateSlot(ItemData item, bool animate)
    {
        if (itemGrid == null) return;

        GameObject slot;
        if (cardSlotPrefab != null)
        {
            slot = Instantiate(cardSlotPrefab, itemGrid);
        }
        else
        {
            slot = CreateRuntimeSlot(itemGrid);
        }

        slot.name = $"Slot_{item.id}";
        slotMap[item.id] = slot;

        // 绑定数据
        var icon = slot.transform.Find("Icon")?.GetComponent<Image>();
        var title = slot.transform.Find("Title")?.GetComponent<TMP_Text>();
        if (icon != null && item.icon != null) icon.sprite = item.icon;
        if (title != null) title.text = item.title;

        // 点击选中
        var btn = slot.GetComponent<Button>();
        if (btn == null) btn = slot.AddComponent<Button>();
        btn.onClick.AddListener(() => SelectItem(item));

        // 收集入列动画
        if (animate)
        {
            var cg = slot.GetComponent<CanvasGroup>();
            if (cg == null) cg = slot.AddComponent<CanvasGroup>();
            cg.alpha = 0f;
            slot.transform.localScale = Vector3.one * 1.3f;

            DOTween.Sequence()
                .Append(cg.DOFade(1f, collectAnimDuration))
                .Join(slot.transform.DOScale(1f, collectAnimDuration).SetEase(Ease.OutBack))
                .SetUpdate(true);
        }
    }

    // ══════════════════════════════════════════════════════════════
    //  动画
    // ══════════════════════════════════════════════════════════════

    private void PlayOpenAnim()
    {
        panelCG.interactable = true;
        panelCG.blocksRaycasts = true;

        switch (panelAnimation)
        {
            case ItemPanelAnimation.SlideFromLeft:
                panelCG.alpha = 0f;
                panelRect.anchoredPosition += Vector2.left * 300;
                DOTween.Sequence()
                    .Join(panelCG.DOFade(1f, animDuration))
                    .Join(panelRect.DOAnchorPosX(panelRect.anchoredPosition.x + 300, animDuration)
                        .SetEase(Ease.OutQuart))
                    .SetUpdate(true);
                break;

            case ItemPanelAnimation.FadeIn:
                panelCG.alpha = 0f;
                panelCG.DOFade(1f, animDuration).SetUpdate(true);
                break;

            case ItemPanelAnimation.Expand:
                panelCG.alpha = 0f;
                panelRect.localScale = Vector3.one * 0.5f;
                DOTween.Sequence()
                    .Join(panelCG.DOFade(1f, animDuration))
                    .Join(panelRect.DOScale(1f, animDuration).SetEase(Ease.OutBack))
                    .SetUpdate(true);
                break;
        }
    }

    private void PlayCloseAnim(Action onComplete)
    {
        DOTween.Sequence()
            .Append(panelCG.DOFade(0f, animDuration * 0.5f))
            .SetUpdate(true)
            .OnComplete(() =>
            {
                panelCG.interactable = false;
                panelCG.blocksRaycasts = false;
                onComplete?.Invoke();
            });
    }

    // ══════════════════════════════════════════════════════════════
    //  运行时创建（Prefab 缺失时回退）
    // ══════════════════════════════════════════════════════════════

    private GameObject CreateRuntimePanel(Transform parent)
    {
        var panel = new GameObject("ItemDisplayPanel");
        panel.transform.SetParent(parent, false);
        var rect = panel.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.offsetMin = new Vector2(60, 60);
        rect.offsetMax = new Vector2(-60, -60);
        var bg = panel.AddComponent<Image>();
        bg.color = new Color(0.03f, 0.03f, 0.06f, 0.92f);

        // ItemGrid
        var gridGO = new GameObject("ItemGrid");
        gridGO.transform.SetParent(panel.transform, false);
        var gridRect = gridGO.AddComponent<RectTransform>();
        gridRect.anchorMin = new Vector2(0, 0);
        gridRect.anchorMax = new Vector2(0.55f, 1f);
        gridRect.offsetMin = new Vector2(20, 20);
        gridRect.offsetMax = new Vector2(-10, -50);
        var grid = gridGO.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(100, 130);
        grid.spacing = new Vector2(12, 12);
        grid.padding = new RectOffset(10, 10, 10, 10);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 4;

        // DetailPanel
        var detailGO = new GameObject("DetailPanel");
        detailGO.transform.SetParent(panel.transform, false);
        var detailRect = detailGO.AddComponent<RectTransform>();
        detailRect.anchorMin = new Vector2(0.55f, 0);
        detailRect.anchorMax = new Vector2(1f, 1f);
        detailRect.offsetMin = new Vector2(10, 20);
        detailRect.offsetMax = new Vector2(-20, -50);
        var detailBg = detailGO.AddComponent<Image>();
        detailBg.color = new Color(0.05f, 0.05f, 0.08f, 0.8f);

        // DetailIcon
        var iconGO = new GameObject("DetailIcon");
        iconGO.transform.SetParent(detailGO.transform, false);
        var iconRect = iconGO.AddComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0.5f, 1f);
        iconRect.anchorMax = new Vector2(0.5f, 1f);
        iconRect.pivot = new Vector2(0.5f, 1f);
        iconRect.anchoredPosition = new Vector2(0, -20);
        iconRect.sizeDelta = new Vector2(120, 160);
        iconGO.AddComponent<Image>().color = new Color(0.1f, 0.1f, 0.15f, 0.5f);

        // DetailTitle
        CreateTMP(detailGO.transform, "DetailTitle", "", 24,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0, -190), new Vector2(250, 30));

        // DetailDesc
        CreateTMP(detailGO.transform, "DetailDesc", "", 16,
            new Vector2(0f, 0f), new Vector2(1f, 1f),
            new Vector2(10, 10), new Vector2(-10, -230), TextAlignmentOptions.TopLeft);

        // CloseButton
        var closeBtnGO = new GameObject("CloseButton");
        closeBtnGO.transform.SetParent(panel.transform, false);
        var btnRect = closeBtnGO.AddComponent<RectTransform>();
        btnRect.anchorMin = new Vector2(1, 1);
        btnRect.anchorMax = new Vector2(1, 1);
        btnRect.pivot = new Vector2(1, 1);
        btnRect.anchoredPosition = new Vector2(-10, -10);
        btnRect.sizeDelta = new Vector2(40, 40);
        var btnImg = closeBtnGO.AddComponent<Image>();
        btnImg.color = new Color(0.3f, 0.1f, 0.1f, 0.8f);
        closeBtnGO.AddComponent<Button>().targetGraphic = btnImg;
        CreateTMP(closeBtnGO.transform, "X", "X", 22,
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        return panel;
    }

    private GameObject CreateRuntimeSlot(Transform parent)
    {
        var slot = new GameObject("Slot");
        slot.transform.SetParent(parent, false);
        var rect = slot.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(100, 130);
        var bg = slot.AddComponent<Image>();
        bg.color = new Color(0.08f, 0.08f, 0.12f, 0.9f);

        // Icon
        var iconGO = new GameObject("Icon");
        iconGO.transform.SetParent(slot.transform, false);
        var iconRect = iconGO.AddComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0.1f, 0.25f);
        iconRect.anchorMax = new Vector2(0.9f, 0.95f);
        iconRect.offsetMin = Vector2.zero;
        iconRect.offsetMax = Vector2.zero;
        iconGO.AddComponent<Image>().color = new Color(0.15f, 0.15f, 0.2f, 0.6f);

        // Title
        CreateTMP(slot.transform, "Title", "", 12,
            new Vector2(0, 0), new Vector2(1, 0.25f),
            Vector2.zero, Vector2.zero);

        return slot;
    }

    private void CreateTMP(Transform parent, string name, string text, float fontSize,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax,
        TextAlignmentOptions align = TextAlignmentOptions.Center)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = new Color(0.8f, 0.8f, 0.85f);
        tmp.alignment = align;
        tmp.overflowMode = TextOverflowModes.Ellipsis;
    }
}

/// <summary>道具面板入场动画</summary>
public enum ItemPanelAnimation
{
    [Tooltip("从左侧滑入")]
    SlideFromLeft,
    [Tooltip("整体淡入")]
    FadeIn,
    [Tooltip("从中心展开")]
    Expand,
}
