using UnityEngine;
using TMPro;
using System.Linq;
using System.Collections.Generic;

/// <summary>
/// 中文字体统一供应器 — 全项目唯一入口。
///
/// 【设计目标】
///   1. 所有 UI 系统（Modal / Toast / Dialogue / HUD / ItemDisplay）
///      以及世界空间文字（MemoryNodeBase）统一从此处获取字体。
///   2. 优先使用美术放置的 TMP 字体资源（SDF），
///      找不到时回退到运行时从 OS 动态创建，保证开发期可用。
///   3. 同时为 TMP（TextMeshProUGUI）和旧版 TextMesh 提供字体。
///
/// 【美术工作流】
///   ● TMP 字体（推荐 — 用于所有 Canvas UI 文字）：
///     1. 将中文 .ttf/.otf 放到 Assets/Fonts/（如 NotoSansSC-Regular.ttf）
///     2. 用 TMP Font Asset Creator 生成 SDF Font Asset
///        - Atlas Resolution: 4096×4096
///        - Atlas Population Mode: Dynamic（运行时按需渲染字形）
///        - Character Set: Custom Characters 或 Unicode Range
///     3. 将生成的 TMP_FontAsset 放到 Resources/Fonts/ChineseTMP
///        （或任何 Resources 子路径，在 TmpFontResourcePath 中配置）
///     4. 在 TMP Settings → Default Font Asset 中也指定此字体
///        （路径：Assets/TextMesh Pro/Resources/TMP Settings.asset）
///
///   ● 旧版字体（TextMesh — 仅用于 Memory 场景世界空间提示文字）：
///     可选：放一个 .ttf 到 Resources/Fonts/ChineseFont
///     若无则自动从 OS 加载。
///
/// 【程序使用】
///   // TMP
///   ChineseFontProvider.ApplyFont(myTmpText);
///
///   // TextMesh（世界空间）
///   myTextMesh.font = ChineseFontProvider.GetLegacyFont();
/// </summary>
public static class ChineseFontProvider
{
    // ── 可配置的 Resources 路径 ──────────────────────────────────
    /// <summary>TMP Font Asset 的 Resources 加载路径（不含扩展名）</summary>
    public const string TmpFontResourcePath = "Fonts/ChineseTMP";

    /// <summary>旧版 Font 的 Resources 加载路径（不含扩展名）</summary>
    public const string LegacyFontResourcePath = "Fonts/ChineseFont";

    // ── 缓存 ─────────────────────────────────────────────────────
    private static TMP_FontAsset _tmpFont;
    private static Font _legacyFont;
    private static bool _tmpSearched;
    private static bool _legacySearched;

    // ── OS 候选字体名（按优先级） ────────────────────────────────
    private static readonly string[] CandidateFonts =
    {
        "Microsoft YaHei",   // Windows 常用中文
        "微软雅黑",
        "SimHei",            // Windows 黑体
        "黑体",
        "PingFang SC",       // macOS / iOS
        "Hiragino Sans GB",  // macOS
        "Noto Sans CJK SC",  // Linux / Android
        "Source Han Sans SC",// 思源
        "Arial Unicode MS",  // 通用 Unicode
        "Arial",             // 最终兜底
    };

    // ══════════════════════════════════════════════════════════════
    //  TMP 字体（TextMeshProUGUI / TextMeshPro）
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// 获取 TMP 中文字体资源。
    /// 优先从 Resources 加载美术制作的 SDF Font Asset，
    /// 找不到时运行时从 OS 字体动态创建。
    /// </summary>
    public static TMP_FontAsset GetTmpFont()
    {
        if (_tmpFont != null) return _tmpFont;
        if (_tmpSearched) return _tmpFont; // 已搜过但没找到资源，返回缓存（可能是运行时生成的）

        _tmpSearched = true;

        // 1. 尝试从 Resources 加载美术制作的 SDF Font Asset
        _tmpFont = Resources.Load<TMP_FontAsset>(TmpFontResourcePath);
        if (_tmpFont != null)
        {
            Debug.Log($"[ChineseFontProvider] 从 Resources 加载 TMP 字体: {TmpFontResourcePath}");
            return _tmpFont;
        }

        // 2. 从 TMP Settings 的默认字体获取备选
        var settings = TMP_Settings.defaultFontAsset;
        if (settings != null && CanRenderChinese(settings))
        {
            _tmpFont = settings;
            Debug.Log("[ChineseFontProvider] 使用 TMP Settings 默认字体");
            return _tmpFont;
        }

        // 3. 运行时从 OS 字体创建（开发期回退）
        _tmpFont = CreateTmpFromOS();
        return _tmpFont;
    }

    /// <summary>
    /// 便捷方法：将 TMP 中文字体应用到指定文本组件。
    /// 用来替换各子系统中分散的 ApplyChineseFont()。
    /// </summary>
    public static void ApplyFont(TMP_Text tmp)
    {
        if (tmp == null) return;
        var font = GetTmpFont();
        if (font != null) tmp.font = font;
    }

    // ══════════════════════════════════════════════════════════════
    //  旧版 Font（TextMesh 世界空间文字）
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// 获取旧版 Font（用于 TextMesh 世界空间提示文字）。
    /// 优先从 Resources 加载，找不到时从 OS 动态创建。
    /// </summary>
    public static Font GetLegacyFont()
    {
        if (_legacyFont != null) return _legacyFont;
        if (_legacySearched) return _legacyFont;

        _legacySearched = true;

        // 1. 从 Resources 加载
        _legacyFont = Resources.Load<Font>(LegacyFontResourcePath);
        if (_legacyFont != null)
        {
            Debug.Log($"[ChineseFontProvider] 从 Resources 加载旧版字体: {LegacyFontResourcePath}");
            return _legacyFont;
        }

        // 2. OS 动态字体
        _legacyFont = CreateLegacyFontFromOS();
        return _legacyFont;
    }

    // ══════════════════════════════════════════════════════════════
    //  内部实现
    // ══════════════════════════════════════════════════════════════

    private static TMP_FontAsset CreateTmpFromOS()
    {
        var installed = new HashSet<string>(
            Font.GetOSInstalledFontNames(), System.StringComparer.OrdinalIgnoreCase);

        string chosen = CandidateFonts.FirstOrDefault(c => installed.Contains(c)) ?? "Arial";

        var osFont = Font.CreateDynamicFontFromOSFont(chosen, 36);
        if (osFont == null)
        {
            Debug.LogWarning("[ChineseFontProvider] 无法创建 OS 字体，回退到 Arial");
            osFont = Font.CreateDynamicFontFromOSFont("Arial", 36);
        }

        var tmpFont = TMP_FontAsset.CreateFontAsset(osFont);
        if (tmpFont != null)
        {
            tmpFont.name = $"ChineseTMP_Runtime_{chosen}";
            tmpFont.atlasPopulationMode = AtlasPopulationMode.Dynamic;
            Debug.Log($"[ChineseFontProvider] 运行时创建 TMP 字体: {chosen} (Dynamic Atlas)");
        }

        return tmpFont;
    }

    private static Font CreateLegacyFontFromOS()
    {
        var font = Font.CreateDynamicFontFromOSFont(CandidateFonts, 64);
        if (font == null)
            font = Font.CreateDynamicFontFromOSFont("Arial", 64);

        if (font != null)
            Debug.Log($"[ChineseFontProvider] 运行时创建旧版字体: {font.name}");

        return font;
    }

    /// <summary>粗略检测一个 TMP_FontAsset 是否能渲染中文字符</summary>
    private static bool CanRenderChinese(TMP_FontAsset font)
    {
        if (font == null) return false;
        // 尝试查找 "的" (U+7684) — 最高频汉字
        return font.HasCharacter('\u7684', searchFallbacks: true, tryAddCharacter: true);
    }

    /// <summary>
    /// 清除缓存（仅用于编辑器热重载或测试）。
    /// </summary>
    public static void ClearCache()
    {
        _tmpFont = null;
        _legacyFont = null;
        _tmpSearched = false;
        _legacySearched = false;
    }
}
