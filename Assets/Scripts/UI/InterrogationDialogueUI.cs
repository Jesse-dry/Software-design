using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

/// <summary>
/// 盘问对话 UI 控制器 — 驱动 UIRoot_ServerRoom 中的 "对话UI" 面板。
///
/// ══ Prefab 结构（UIRoot_ServerRoom ModalLayer 内）══
///   对话UI (root, Image 背景)
///     ├─ Image         (角色立绘，左侧)
///     ├─ 盘问内容       (TMP，问题文本)
///     ├─ Image         (标题区域背景)
///     ├─ 标题           (TMP，角色名)
///     ├─ ButtonToContinue  ("是" 按钮)
///     └─ ButtonToContinue  ("否" 按钮)
///
/// ══ 使用方式 ══
///   var ui = InterrogationDialogueUI.Instance;
///   ui.Show("安保主管", "你是今天来替班的维修员李工吧？", onYes, onNo);
///   ui.Hide();
///
/// ══ 不需要手动挂载。由 UIManager.InitializeGlobalUI() 自动创建（DDOL 模式）。══
/// </summary>
public class InterrogationDialogueUI : MonoBehaviour
{
    public static InterrogationDialogueUI Instance { get; private set; }

    // ── UI 引用 ──
    private GameObject _dialogueRoot;
    private TextMeshProUGUI _titleText;
    private TextMeshProUGUI _contentText;
    private Button _yesButton;
    private Button _noButton;

    // ── 回调 ──
    private Action _onYes;
    private Action _onNo;

    private bool _isBound = false;

    // ══════════════════════════════════════════════════════════════
    //  静态初始化（由 UIManager.InitializeGlobalUI 调用，仅执行一次）
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// 创建全局持久的 InterrogationDialogueUI 实例，并订阅场景加载事件。
    /// 每次场景切换后自动重新绑定，保证 "对话UI" 默认隐藏。
    /// </summary>
    public static InterrogationDialogueUI Initialize(Transform parent)
    {
        if (Instance != null) return Instance;

        var go = new GameObject("InterrogationDialogueUI_Logic");
        go.transform.SetParent(parent, false);
        Instance = go.AddComponent<InterrogationDialogueUI>();

        if (UIManager.Instance != null)
        {
            UIManager.Instance.OnSceneRootRegistered += Instance.OnSceneLoaded;

            // 防漏补丁：若场景根已存在（如直接从 ServerRoom 启动），立即触发绑定
            if (UIManager.Instance.HasSceneRoot && UIManager.Instance.modalLayer != null)
            {
                var existingRoot = UIManager.Instance.modalLayer
                    .GetComponentInParent<UISceneRoot>();
                if (existingRoot != null)
                {
                    Debug.Log("[InterrogationUI] 检测到场景已存在，立即补发绑定！");
                    Instance.OnSceneLoaded(existingRoot);
                }
            }
        }

        return Instance;
    }

    private void OnDestroy()
    {
        if (UIManager.Instance != null)
            UIManager.Instance.OnSceneRootRegistered -= OnSceneLoaded;
        if (Instance == this) Instance = null;
    }

    // ══════════════════════════════════════════════════════════════
    //  场景加载回调
    // ══════════════════════════════════════════════════════════════

    private void OnSceneLoaded(UISceneRoot root)
    {
        // 场景切换：重置绑定状态，查找新场景的 "对话UI" 面板并强制隐藏
        _isBound = false;
        _dialogueRoot = null;
        _titleText = null;
        _contentText = null;
        _yesButton = null;
        _noButton = null;
        _onYes = null;
        _onNo = null;

        TryBind();
    }

    /// <summary>
    /// 显示盘问对话框。
    /// </summary>
    /// <param name="title">角色名/标题</param>
    /// <param name="content">盘问内容文本</param>
    /// <param name="onYes">点击"是"的回调</param>
    /// <param name="onNo">点击"否"的回调（也用于 Esc 关闭）</param>
    public void Show(string title, string content, Action onYes, Action onNo)
    {
        if (!_isBound) TryBind();
        if (!_isBound)
        {
            Debug.LogWarning("[InterrogationUI] 未绑定到对话UI面板，回退到 onNo。");
            onNo?.Invoke();
            return;
        }

        _onYes = onYes;
        _onNo = onNo;

        if (_titleText != null) _titleText.text = title;
        if (_contentText != null) _contentText.text = content;

        _dialogueRoot.SetActive(true);
        _dialogueRoot.transform.SetAsLastSibling();

        // 显示 ModalBackground
        UIManager.Instance?.ShowModalBackground();
    }

    /// <summary>隐藏对话框</summary>
    public void Hide()
    {
        if (_dialogueRoot != null) _dialogueRoot.SetActive(false);
        UIManager.Instance?.HideModalBackground();
        _onYes = null;
        _onNo = null;
    }

    /// <summary>当前是否显示中</summary>
    public bool IsShowing => _dialogueRoot != null && _dialogueRoot.activeSelf;

    void Update()
    {
        // Esc 键等同于点击"否"
        if (IsShowing && Input.GetKeyDown(KeyCode.Escape))
        {
            OnNoClicked();
        }
    }

    // ══════════════════════════════════════════════════════════════
    //  按钮回调
    // ══════════════════════════════════════════════════════════════

    private void OnYesClicked()
    {
        var callback = _onYes;
        Hide();
        callback?.Invoke();
    }

    private void OnNoClicked()
    {
        var callback = _onNo;
        Hide();
        callback?.Invoke();
    }

    // ══════════════════════════════════════════════════════════════
    //  绑定 Prefab 中的 UI 元素
    // ══════════════════════════════════════════════════════════════

    private void TryBind()
    {
        if (_isBound) return;

        // 在 ModalLayer 中查找 "对话UI"
        RectTransform modalLayer = UIManager.Instance?.modalLayer;
        if (modalLayer == null) return;

        Transform dialogueT = null;
        for (int i = 0; i < modalLayer.childCount; i++)
        {
            var child = modalLayer.GetChild(i);
            if (child.name == "对话UI")
            {
                dialogueT = child;
                break;
            }
        }

        if (dialogueT == null)
        {
            Debug.Log("[InterrogationUI] ModalLayer 中无 '对话UI' 面板。");
            return;
        }

        _dialogueRoot = dialogueT.gameObject;

        // 查找 "标题" TMP
        var titleT = FindChild(dialogueT, "标题");
        if (titleT != null) _titleText = titleT.GetComponent<TextMeshProUGUI>();

        // 查找 "盘问内容" TMP
        var contentT = FindChild(dialogueT, "盘问内容");
        if (contentT != null) _contentText = contentT.GetComponent<TextMeshProUGUI>();

        // 查找两个 ButtonToContinue — 通过 Button 组件定位
        Button[] buttons = _dialogueRoot.GetComponentsInChildren<Button>(true);
        if (buttons.Length >= 2)
        {
            // 按 X 位置排序：左边的是"是"，右边的是"否"
            if (buttons[0].transform.localPosition.x <= buttons[1].transform.localPosition.x)
            {
                _yesButton = buttons[0];
                _noButton = buttons[1];
            }
            else
            {
                _yesButton = buttons[1];
                _noButton = buttons[0];
            }
        }
        else if (buttons.Length == 1)
        {
            _yesButton = buttons[0];
        }

        // 绑定按钮事件
        if (_yesButton != null)
        {
            _yesButton.onClick.RemoveAllListeners();
            _yesButton.onClick.AddListener(() => OnYesClicked());
        }
        if (_noButton != null)
        {
            _noButton.onClick.RemoveAllListeners();
            _noButton.onClick.AddListener(() => OnNoClicked());
        }

        // 初始隐藏
        _dialogueRoot.SetActive(false);

        _isBound = true;
        Debug.Log($"[InterrogationUI] 绑定成功 — 标题:{_titleText != null} 内容:{_contentText != null} 是:{_yesButton != null} 否:{_noButton != null}");
    }

    private static Transform FindChild(Transform parent, string name)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            var child = parent.GetChild(i);
            if (child.name == name) return child;
            var result = FindChild(child, name);
            if (result != null) return result;
        }
        return null;
    }
}
