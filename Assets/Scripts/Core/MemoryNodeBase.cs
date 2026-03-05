using UnityEngine;

/// <summary>
/// Memory 场景交互节点抽象基类。
/// 提供统一交互接口 + 世界空间持久提示文字管理。
/// 子类：MemoryFragmentNode（碎片）、AbyssPortalNode（传送门）。
///
/// 提示文字使用 Unity 原生 TextMesh（非 TMP），通过 OS 动态字体
/// 自动支持中文，无需额外导入 TMP 字体资源。
/// </summary>
public abstract class MemoryNodeBase : MonoBehaviour
{
    // ── 提示样式（可在子类 Inspector 覆盖） ──────────────────────
    [Header("== 提示文字样式 ==")]
    [Tooltip("字体大小（CharacterSize）")]
    public float promptCharSize = 0.15f;

    [Tooltip("字号（FontSize，影响清晰度）")]
    public int promptFontSize = 64;

    [Tooltip("文字颜色")]
    public Color promptColor = new Color(0.95f, 0.95f, 1f, 0.95f);

    [Tooltip("排序层级（越大越前）")]
    public int promptSortingOrder = 200;

    // ── 抽象接口 ─────────────────────────────────────────────────

    /// <summary>玩家按交互键时调用</summary>
    public abstract void Interact();

    /// <summary>玩家进入交互范围</summary>
    public virtual void OnPlayerEnter(GameObject player) { }

    /// <summary>玩家离开交互范围</summary>
    public virtual void OnPlayerExit(GameObject player) { }

    // ── 世界空间持久提示文字 ──────────────────────────────────────

    private GameObject _promptGO;
    private TextMesh _promptTextMesh;

    // 缓存系统中文字体（全局共享）
    private static Font _cachedFont;

    /// <summary>
    /// 获取支持中文的 OS 动态字体。
    /// 按优先级尝试：微软雅黑 → 黑体 → Arial Unicode MS → Arial。
    /// </summary>
    private static Font GetChineseFont()
    {
        if (_cachedFont != null) return _cachedFont;

        string[] candidates = {
            "Microsoft YaHei",  // Windows 常用中文
            "SimHei",           // Windows 黑体
            "PingFang SC",      // macOS/iOS
            "Noto Sans CJK SC", // Linux / Android
            "Arial Unicode MS", // 通用 Unicode
            "Arial"             // 最终兜底
        };

        _cachedFont = Font.CreateDynamicFontFromOSFont(candidates, 64);

        if (_cachedFont == null)
            _cachedFont = Font.CreateDynamicFontFromOSFont("Arial", 64);

        return _cachedFont;
    }

    /// <summary>
    /// 在指定世界坐标显示持久提示文字（不自动消失，需手动 HidePrompt）。
    /// 多次调用只更新文字和位置，不重复创建。
    /// </summary>
    protected void ShowPrompt(string text, Vector3 worldPosition)
    {
        if (_promptGO == null)
        {
            _promptGO = new GameObject($"{gameObject.name}_Prompt");

            _promptTextMesh = _promptGO.AddComponent<TextMesh>();
            _promptTextMesh.font = GetChineseFont();
            _promptTextMesh.characterSize = promptCharSize;
            _promptTextMesh.fontSize = promptFontSize;
            _promptTextMesh.alignment = TextAlignment.Center;
            _promptTextMesh.anchor = TextAnchor.MiddleCenter;
            _promptTextMesh.color = promptColor;
            _promptTextMesh.richText = false;

            // 设置 MeshRenderer 材质为字体材质 + 排序
            var mr = _promptGO.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                mr.material = _promptTextMesh.font.material;
                mr.sortingOrder = promptSortingOrder;
            }
        }

        _promptTextMesh.text = text;
        _promptGO.transform.position = worldPosition;
        _promptGO.SetActive(true);
    }

    /// <summary>隐藏提示文字（可再次 ShowPrompt 恢复）</summary>
    protected void HidePrompt()
    {
        if (_promptGO != null) _promptGO.SetActive(false);
    }

    /// <summary>彻底销毁提示文字 GameObject</summary>
    protected void DestroyPrompt()
    {
        if (_promptGO != null)
        {
            Destroy(_promptGO);
            _promptGO = null;
            _promptTextMesh = null;
        }
    }

    protected virtual void OnDestroy()
    {
        DestroyPrompt();
    }
}