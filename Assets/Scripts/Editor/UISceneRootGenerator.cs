#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using System.IO;

/// <summary>
/// 编辑器工具 — 一键生成各场景的 UISceneRoot Prefab。
///
/// 菜单：Tools → UI → Generate UISceneRoot Prefabs
///
/// 生成逻辑：
///   为每个需要 UI 的场景创建一个 UISceneRoot Prefab，
///   包含 Canvas + CanvasScaler + GraphicRaycaster + UISceneRoot 组件，
///   以及 HUDLayer / OverlayLayer / ModalLayer 三个子层。
///
///   不同场景可按需增加专属子元素（如 Memory 的碎片计数 HUD）。
///
/// 生成目录：Assets/Prefabs/UI/
///
/// 美术工作流：
///   1. 运行此工具生成基础 Prefab
///   2. 在 Prefab Mode 中微调布局和样式
///   3. 将 Prefab 拖入对应场景的 Hierarchy
/// </summary>
public static class UISceneRootGenerator
{
    private const string OUTPUT_DIR = "Assets/Prefabs/UI";

    // ── 场景定义（场景名 → 是否需要特殊定制） ───────────────────
    private static readonly SceneDef[] Scenes =
    {
        new("CutsceneScene", "UIRoot_Cutscene",  SceneUIProfile.Minimal),
        new("Memory",        "UIRoot_Memory",    SceneUIProfile.Memory),
        new("Abyss",         "UIRoot_Abyss",     SceneUIProfile.Exploration),
        new("Court",         "UIRoot_Court",     SceneUIProfile.Court),
        new("接水管",        "UIRoot_PipePuzzle", SceneUIProfile.MiniGame),
        new("机房",          "UIRoot_ServerRoom", SceneUIProfile.Exploration),
        new("水管房间",      "UIRoot_PipeRoom",  SceneUIProfile.MiniGame),
        new("走廊v0.2",      "UIRoot_Corridor",  SceneUIProfile.Exploration),
    };

    private enum SceneUIProfile
    {
        Minimal,        // 仅基础三层（过场动画等）
        Memory,         // 碎片收集 HUD + 模态弹窗
        Exploration,    // 通用探索（HUD + 对话 + 道具查看）
        Court,          // 庭审（证据面板 + 辩论 UI）
        MiniGame,       // 小游戏（简化 HUD + 结果弹窗）
    }

    private struct SceneDef
    {
        public string sceneName;
        public string prefabName;
        public SceneUIProfile profile;

        public SceneDef(string scene, string prefab, SceneUIProfile profile)
        {
            sceneName = scene;
            prefabName = prefab;
            this.profile = profile;
        }
    }

    // ══════════════════════════════════════════════════════════════
    //  菜单入口
    // ══════════════════════════════════════════════════════════════

    [MenuItem("Tools/UI/Generate All UISceneRoot Prefabs", priority = 100)]
    public static void GenerateAll()
    {
        EnsureDirectory(OUTPUT_DIR);

        int created = 0;
        foreach (var def in Scenes)
        {
            string path = $"{OUTPUT_DIR}/{def.prefabName}.prefab";
            if (File.Exists(path))
            {
                Debug.Log($"[UISceneRootGenerator] 已存在，跳过: {path}");
                continue;
            }

            var root = BuildPrefab(def);
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            created++;
            Debug.Log($"[UISceneRootGenerator] 已生成: {path}");
        }

        AssetDatabase.Refresh();
        Debug.Log($"[UISceneRootGenerator] 完成！新建 {created} 个 Prefab，路径: {OUTPUT_DIR}/");
    }

    [MenuItem("Tools/UI/Generate UISceneRoot — Memory Only", priority = 101)]
    public static void GenerateMemoryOnly()
    {
        EnsureDirectory(OUTPUT_DIR);
        var def = new SceneDef("Memory", "UIRoot_Memory", SceneUIProfile.Memory);
        string path = $"{OUTPUT_DIR}/{def.prefabName}.prefab";

        var root = BuildPrefab(def);
        PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);

        AssetDatabase.Refresh();
        Debug.Log($"[UISceneRootGenerator] 已生成/覆盖: {path}");
    }

    // ══════════════════════════════════════════════════════════════
    //  Prefab 构建
    // ══════════════════════════════════════════════════════════════

    private static GameObject BuildPrefab(SceneDef def)
    {
        // ── 根 Canvas ──
        var rootGO = new GameObject(def.prefabName);
        var canvas = rootGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 0;

        var scaler = rootGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        rootGO.AddComponent<GraphicRaycaster>();

        // ── 三个标准层 ──
        var hudLayer     = CreateLayer(rootGO.transform, "HUDLayer",     10);
        var overlayLayer = CreateLayer(rootGO.transform, "OverlayLayer", 50);
        var modalLayer   = CreateLayer(rootGO.transform, "ModalLayer",   90);

        // ── ModalBackground（半透明遮罩，初始隐藏） ──
        var modalBg = CreateModalBackground(modalLayer);

        // ── UISceneRoot 组件 ──
        var sceneRoot = rootGO.AddComponent<UISceneRoot>();
        sceneRoot.hudLayer     = hudLayer;
        sceneRoot.overlayLayer = overlayLayer;
        sceneRoot.modalLayer   = modalLayer;

        // ── 按场景类型添加专属子元素 ──
        switch (def.profile)
        {
            case SceneUIProfile.Memory:
                BuildMemoryExtras(hudLayer, modalLayer);
                break;
            case SceneUIProfile.Court:
                BuildCourtExtras(hudLayer, overlayLayer);
                break;
            case SceneUIProfile.MiniGame:
                BuildMiniGameExtras(hudLayer, overlayLayer);
                break;
            case SceneUIProfile.Exploration:
                BuildExplorationExtras(hudLayer);
                break;
            case SceneUIProfile.Minimal:
                // 仅保留基础三层
                break;
        }

        return rootGO;
    }

    // ── 标准层创建 ───────────────────────────────────────────────

    private static RectTransform CreateLayer(Transform parent, string name, int sortOrder)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rect = go.AddComponent<RectTransform>();
        StretchFull(rect);

        var layerCanvas = go.AddComponent<Canvas>();
        layerCanvas.overrideSorting = true;
        layerCanvas.sortingOrder = sortOrder;
        go.AddComponent<GraphicRaycaster>();

        return rect;
    }

    private static GameObject CreateModalBackground(RectTransform modalLayer)
    {
        var go = new GameObject("ModalBackground");
        go.transform.SetParent(modalLayer, false);
        go.transform.SetAsFirstSibling();
        var rect = go.AddComponent<RectTransform>();
        StretchFull(rect);
        var img = go.AddComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0f);
        img.raycastTarget = true;
        go.SetActive(false);
        return go;
    }

    // ══════════════════════════════════════════════════════════════
    //  场景专属子元素
    // ══════════════════════════════════════════════════════════════

    /// <summary>Memory 场景：底部交互提示栏占位</summary>
    private static void BuildMemoryExtras(RectTransform hudLayer, RectTransform modalLayer)
    {
        // ── 底部提示栏（"按 E 交互"类通用提示） ──
        var promptBar = new GameObject("InteractionPrompt");
        promptBar.transform.SetParent(hudLayer, false);
        var pbRect = promptBar.AddComponent<RectTransform>();
        pbRect.anchorMin = new Vector2(0.5f, 0);
        pbRect.anchorMax = new Vector2(0.5f, 0);
        pbRect.pivot = new Vector2(0.5f, 0);
        pbRect.anchoredPosition = new Vector2(0, 40);
        pbRect.sizeDelta = new Vector2(300, 40);
        var pbBg = promptBar.AddComponent<Image>();
        pbBg.color = new Color(0.02f, 0.02f, 0.05f, 0.5f);
        promptBar.SetActive(false); // 默认隐藏，运行时由代码控制

        var promptText = new GameObject("PromptText");
        promptText.transform.SetParent(promptBar.transform, false);
        var ptRect = promptText.AddComponent<RectTransform>();
        StretchFull(ptRect);
        var pt = promptText.AddComponent<Text>();
        pt.text = "按 E 交互";
        pt.fontSize = 18;
        pt.color = new Color(0.85f, 0.85f, 0.9f);
        pt.alignment = TextAnchor.MiddleCenter;
    }

    /// <summary>Court 场景：证据栏 + 辩论状态</summary>
    private static void BuildCourtExtras(RectTransform hudLayer, RectTransform overlayLayer)
    {
        // ── 庭审状态栏（顶部居中） ──
        var statusBar = new GameObject("CourtStatusBar");
        statusBar.transform.SetParent(hudLayer, false);
        var sbRect = statusBar.AddComponent<RectTransform>();
        sbRect.anchorMin = new Vector2(0.5f, 1);
        sbRect.anchorMax = new Vector2(0.5f, 1);
        sbRect.pivot = new Vector2(0.5f, 1);
        sbRect.anchoredPosition = new Vector2(0, -10);
        sbRect.sizeDelta = new Vector2(600, 50);
        var sbBg = statusBar.AddComponent<Image>();
        sbBg.color = new Color(0.05f, 0.02f, 0.05f, 0.7f);

        var stateText = new GameObject("StateText");
        stateText.transform.SetParent(statusBar.transform, false);
        var stRect = stateText.AddComponent<RectTransform>();
        StretchFull(stRect);
        var st = stateText.AddComponent<Text>();
        st.text = "庭审进行中";
        st.fontSize = 24;
        st.color = new Color(0.9f, 0.85f, 0.4f);
        st.alignment = TextAnchor.MiddleCenter;

        // ── 证据提交面板占位（左侧） ──
        var evidencePanel = new GameObject("EvidencePanel");
        evidencePanel.transform.SetParent(overlayLayer, false);
        var epRect = evidencePanel.AddComponent<RectTransform>();
        epRect.anchorMin = new Vector2(0, 0);
        epRect.anchorMax = new Vector2(0.3f, 0.7f);
        epRect.offsetMin = new Vector2(20, 80);
        epRect.offsetMax = new Vector2(-10, -80);
        var epBg = evidencePanel.AddComponent<Image>();
        epBg.color = new Color(0.03f, 0.03f, 0.06f, 0.85f);
        evidencePanel.SetActive(false); // 默认隐藏

        var epTitle = new GameObject("Title");
        epTitle.transform.SetParent(evidencePanel.transform, false);
        var etRect = epTitle.AddComponent<RectTransform>();
        etRect.anchorMin = new Vector2(0, 1);
        etRect.anchorMax = new Vector2(1, 1);
        etRect.pivot = new Vector2(0.5f, 1);
        etRect.anchoredPosition = Vector2.zero;
        etRect.sizeDelta = new Vector2(0, 40);
        var et = epTitle.AddComponent<Text>();
        et.text = "证据列表";
        et.fontSize = 20;
        et.color = new Color(0.85f, 0.85f, 0.9f);
        et.alignment = TextAnchor.MiddleCenter;
    }

    /// <summary>小游戏场景：简化 HUD（计时器 + 结果提示占位）</summary>
    private static void BuildMiniGameExtras(RectTransform hudLayer, RectTransform overlayLayer)
    {
        // ── 计时器（顶部居中） ──
        var timerPanel = new GameObject("TimerPanel");
        timerPanel.transform.SetParent(hudLayer, false);
        var tpRect = timerPanel.AddComponent<RectTransform>();
        tpRect.anchorMin = new Vector2(0.5f, 1);
        tpRect.anchorMax = new Vector2(0.5f, 1);
        tpRect.pivot = new Vector2(0.5f, 1);
        tpRect.anchoredPosition = new Vector2(0, -15);
        tpRect.sizeDelta = new Vector2(160, 50);
        var tpBg = timerPanel.AddComponent<Image>();
        tpBg.color = new Color(0.02f, 0.02f, 0.05f, 0.6f);

        var timerText = new GameObject("TimerText");
        timerText.transform.SetParent(timerPanel.transform, false);
        var ttRect = timerText.AddComponent<RectTransform>();
        StretchFull(ttRect);
        var tt = timerText.AddComponent<Text>();
        tt.text = "00:00";
        tt.fontSize = 28;
        tt.color = new Color(0.9f, 0.9f, 0.95f);
        tt.alignment = TextAnchor.MiddleCenter;

        // ── 结果面板占位（居中，初始隐藏） ──
        var resultPanel = new GameObject("ResultPanel");
        resultPanel.transform.SetParent(overlayLayer, false);
        var rpRect = resultPanel.AddComponent<RectTransform>();
        rpRect.anchorMin = new Vector2(0.2f, 0.3f);
        rpRect.anchorMax = new Vector2(0.8f, 0.7f);
        rpRect.offsetMin = Vector2.zero;
        rpRect.offsetMax = Vector2.zero;
        var rpBg = resultPanel.AddComponent<Image>();
        rpBg.color = new Color(0.05f, 0.03f, 0.08f, 0.9f);
        resultPanel.SetActive(false);

        var resultText = new GameObject("ResultText");
        resultText.transform.SetParent(resultPanel.transform, false);
        var rtRect = resultText.AddComponent<RectTransform>();
        StretchFull(rtRect);
        var rt = resultText.AddComponent<Text>();
        rt.text = "通关！";
        rt.fontSize = 36;
        rt.color = new Color(0.4f, 0.9f, 0.5f);
        rt.alignment = TextAnchor.MiddleCenter;
    }

    /// <summary>探索场景：通用 HUD（交互提示 + 状态显示）</summary>
    private static void BuildExplorationExtras(RectTransform hudLayer)
    {
        // ── 交互提示栏（底部居中） ──
        var promptBar = new GameObject("InteractionPrompt");
        promptBar.transform.SetParent(hudLayer, false);
        var pbRect = promptBar.AddComponent<RectTransform>();
        pbRect.anchorMin = new Vector2(0.5f, 0);
        pbRect.anchorMax = new Vector2(0.5f, 0);
        pbRect.pivot = new Vector2(0.5f, 0);
        pbRect.anchoredPosition = new Vector2(0, 40);
        pbRect.sizeDelta = new Vector2(300, 40);
        var pbBg = promptBar.AddComponent<Image>();
        pbBg.color = new Color(0.02f, 0.02f, 0.05f, 0.5f);
        promptBar.SetActive(false);

        var promptText = new GameObject("PromptText");
        promptText.transform.SetParent(promptBar.transform, false);
        var ptRect = promptText.AddComponent<RectTransform>();
        StretchFull(ptRect);
        var pt = promptText.AddComponent<Text>();
        pt.text = "提示文本";
        pt.fontSize = 18;
        pt.color = new Color(0.85f, 0.85f, 0.9f);
        pt.alignment = TextAnchor.MiddleCenter;
    }

    // ══════════════════════════════════════════════════════════════
    //  工具方法
    // ══════════════════════════════════════════════════════════════

    private static void StretchFull(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void EnsureDirectory(string path)
    {
        if (!AssetDatabase.IsValidFolder(path))
        {
            // 逐级创建
            string[] parts = path.Split('/');
            string current = parts[0]; // "Assets"
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}

/// <summary>
/// 编辑器工具 — 中文 TMP 字体设置助手。
///
/// 菜单：Tools → UI → Setup Chinese TMP Font
///
/// 帮助美术快速配置中文 TMP 字体：
///   1. 检查 Resources/Fonts/ 是否已有 TMP Font Asset
///   2. 如果有 .ttf 但没有 Font Asset，提示创建步骤
///   3. 检查 TMP Settings 默认字体是否支持中文
/// </summary>
public static class ChineseFontSetup
{
    [MenuItem("Tools/UI/Setup Chinese TMP Font (Help)", priority = 200)]
    public static void ShowHelp()
    {
        string msg =
            "=== 中文 TMP 字体配置指南 ===\n\n" +
            "目标路径: Assets/Resources/Fonts/ChineseTMP.asset\n\n" +
            "步骤：\n" +
            "1. 准备中文字体文件（推荐 Noto Sans SC / 思源黑体）\n" +
            "   → 放到 Assets/Fonts/ 目录\n\n" +
            "2. 打开 Window → TextMeshPro → Font Asset Creator\n" +
            "   → Source Font File: 选择你的 .ttf/.otf\n" +
            "   → Atlas Resolution: 4096 x 4096\n" +
            "   → Atlas Population Mode: Dynamic\n" +
            "   → Character Set: Unicode Range\n" +
            "   → Unicode Range: 20-7E,2000-206F,3000-303F,4E00-9FFF,FF00-FFEF\n" +
            "     （基础拉丁 + 常用中文 + 全角标点）\n" +
            "   → 点击 Generate Font Atlas → Save\n\n" +
            "3. 将生成的 .asset 移动到:\n" +
            "   Assets/Resources/Fonts/ChineseTMP.asset\n\n" +
            "4.（可选）在 TMP Settings 中设为默认字体:\n" +
            "   Assets/TextMesh Pro/Resources/TMP Settings.asset\n" +
            "   → Default Font Asset: 选择你的字体\n\n" +
            "5. 运行游戏验证中文显示。\n\n" +
            "=== 运行时回退 ===\n" +
            "如果不配置以上步骤，ChineseFontProvider 会\n" +
            "自动从 OS 动态创建字体（仅适合开发期）。\n" +
            "发布前务必完成 SDF 字体配置！";

        EditorUtility.DisplayDialog("中文 TMP 字体配置", msg, "知道了");
        Debug.Log(msg);
    }

    [MenuItem("Tools/UI/Validate Chinese Font Setup", priority = 201)]
    public static void ValidateSetup()
    {
        bool hasFont = false;
        string fontPath = "Assets/Resources/Fonts/ChineseTMP.asset";

        if (File.Exists(fontPath))
        {
            var asset = AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>(fontPath);
            if (asset != null)
            {
                hasFont = true;
                Debug.Log($"[FontValidation] ✓ TMP 中文字体已配置: {fontPath}");
            }
        }

        if (!hasFont)
        {
            Debug.LogWarning(
                "[FontValidation] ✗ 未找到 TMP 中文字体资源。\n" +
                $"期望路径: {fontPath}\n" +
                "请运行 Tools → UI → Setup Chinese TMP Font (Help) 查看配置步骤。\n" +
                "运行时将回退到 OS 动态字体（仅适合开发期）。");
        }

        // 检查 TMP Settings
        var settings = AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>(
            "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
        Debug.Log(settings != null
            ? "[FontValidation] TMP 默认字体: LiberationSans SDF（不支持中文，建议替换）"
            : "[FontValidation] 未找到 TMP 默认字体配置");

        // 检查 .ttf 文件
        var ttfFiles = Directory.GetFiles("Assets/Fonts", "*.ttf", SearchOption.AllDirectories);
        var otfFiles = Directory.Exists("Assets/Fonts")
            ? Directory.GetFiles("Assets/Fonts", "*.otf", SearchOption.AllDirectories)
            : new string[0];

        if (ttfFiles.Length > 0 || otfFiles.Length > 0)
        {
            Debug.Log($"[FontValidation] 发现字体文件 ({ttfFiles.Length} ttf, {otfFiles.Length} otf) in Assets/Fonts/");
        }
        else
        {
            Debug.Log("[FontValidation] Assets/Fonts/ 中无 .ttf/.otf 文件。需要先放入中文字体。");
        }
    }
}
#endif
