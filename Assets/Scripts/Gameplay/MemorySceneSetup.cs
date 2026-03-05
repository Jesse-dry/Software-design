using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Memory 场景初始化脚本。
/// 
/// 挂载到 Memory 场景中的空 GameObject 上（建议命名 "MemorySceneSetup"）。
/// 负责在场景加载后搭建完整的运行时结构：
///   - 创建不可见的 Player（Rigidbody2D + Collider + PlayerMovement + PlayerInteraction）
///   - 创建全屏背景 SpriteRenderer + MemoryParallaxBackground
///   - 创建 4 个记忆碎片（MemoryFragmentNode + SpriteRenderer + Collider）
///   - 创建终点门（AbyssPortal + AbyssPortalNode + Collider）
///   - 加载帧序列 Sprite（从 Resources 或手动赋值）
///   - 配置相机
/// 
/// 所有坐标可在 Inspector 中微调。
/// </summary>
public class MemorySceneSetup : MonoBehaviour
{
    [Header("== 兼容迁移 ==")]
    [Tooltip("启用后会在运行时清理旧版记忆场景对象，避免与新版流程冲突")]
    public bool removeLegacyMemoryObjects = true;

    [Header("== 帧序列 ==")]
    [Tooltip("背景帧序列 Sprite 数组（96 帧）。可在 Inspector 拖入，或通过 Resources 快捷加载")]
    public Sprite[] backgroundFrames;

    [Header("== 碎片资源 ==")]
    [Tooltip("碎片 Sprite")]
    public Sprite fragmentSprite;

    [Header("== 场景布局（Y 轴） ==")]
    [Tooltip("玩家起始 Y 坐标")]
    public float startY = 0f;

    [Tooltip("场景终点 Y 坐标（门的位置再往前一点）")]
    public float endY = 20f;

    [Tooltip("玩家移动速度")]
    public float playerSpeed = 4f;

    [Header("== 碎片位置 ==")]
    [Tooltip("4 个碎片的交互触发 Y 坐标（玩家走到这里时可交互）")]
    public float[] fragmentYPositions = { 3f, 7f, 11f, 15f };

    [Tooltip("4 个碎片的视觉显示坐标（在固定相机视野内的屏幕位置）")]
    public Vector2[] fragmentVisualPositions = {
        new Vector2(-4f, 2f),
        new Vector2(4f, -2f),
        new Vector2(-2f, 0f),
        new Vector2(2f, 3f)
    };

    [Header("== 碎片占位内容 ==")]
    public string[] fragmentTitles = {
        "碎片 #1",
        "碎片 #2",
        "碎片 #3",
        "碎片 #4"
    };

    [TextArea(2, 5)]
    public string[] fragmentBodies = {
        "模糊的记忆浮上心头……你看到了一扇旋转的门。",
        "有人在低语——「不要相信他们给你看的」。",
        "一张泛黄的合同，签名处被刻意模糊了。",
        "最后的记忆是一道刺眼的白光，然后是无尽的沉默。"
    };

    [Header("== 门（终点） ==")]
    [Tooltip("门的 Y 坐标")]
    public float portalY = 19f;

    [Header("== 碎片缩放 ==")]
    [Tooltip("碎片 Sprite 缩放")]
    public float fragmentScale = 0.5f;

    [Header("== 透视设置 ==")]
    [Tooltip("碎片的透视消失点（世界/屏幕坐标，通常是 (0,0)）")]
    public Vector2 perspectiveVanishingPoint = Vector2.zero;

    private bool initialized;

    void Start()
    {
        if (initialized) return;
        initialized = true;

        // 【关键】先修复 UI 层级，再初始化场景
        // UIRoot Prefab 若为空壳（无 Canvas / 无子层级），所有 UI 系统（Modal、Toast、Transition）
        // 的 Initialize() 会静默失败，导致交互后弹窗不显示、Toast 不出现、转场黑屏等问题。
        EnsureUILayers();

        SetupScene();
    }

    private void SetupScene()
    {
        if (removeLegacyMemoryObjects)
            CleanupLegacyMemoryObjects();

        EnsureEventSystem();

        // ─── 1. 创建 Player ────────────────────────────────────
        var playerGO = new GameObject("Player");
        playerGO.tag = "Player";
        playerGO.transform.position = new Vector3(0f, startY, 0f);
        playerGO.layer = LayerMask.NameToLayer("Default");

        var rb = playerGO.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        // 注意：不添加非触发 BoxCollider2D。
        // Memory 场景中玩家不可见且无物理障碍，仅需触发器进行交互检测。
        // 若同时存在非触发和触发 Collider 会产生双重 Trigger 事件导致交互异常。

        // Memory 场景中不需要 PlayerInput 组件：
        // - PlayerMovement 有内置键盘回退（W/S/方向键）
        // - PlayerInteraction 使用 Keyboard.current 直接检测 E/F 键
        // - 添加 PlayerInput 但无 InputActionAsset 会导致 actions 为 null，
        //   且 Interact 使用了 Hold 交互器（需长按才触发 performed），不适合即时交互

        var movement = playerGO.AddComponent<PlayerMovement>();
        movement.moveSpeed = playerSpeed;
        movement.singleAxisMode = true;
        movement.lockedAxis = PlayerMovement.MovementAxis.Vertical;

        playerGO.AddComponent<PlayerInteraction>();

        // 交互检测范围（Trigger 稍大一些）
        var interactTrigger = playerGO.AddComponent<CircleCollider2D>();
        interactTrigger.radius = 1.5f;
        interactTrigger.isTrigger = true;

        // ─── 2. 创建背景 ──────────────────────────────────────
        var bgGO = new GameObject("ParallaxBackground");
        var bgSR = bgGO.AddComponent<SpriteRenderer>();
        bgSR.sortingOrder = -100; // 最底层
        bgGO.transform.position = Vector3.zero;

        var parallax = bgGO.AddComponent<MemoryParallaxBackground>();
        parallax.SetFrames(backgroundFrames, playerGO.transform, startY, endY);

        // 如果有帧，先显示第一帧
        if (backgroundFrames != null && backgroundFrames.Length > 0)
        {
            bgSR.sprite = backgroundFrames[0];
        }

        int collectedCount = 0;

        // ─── 3. 创建 4 个碎片 ────────────────────────────────
        for (int i = 0; i < 4; i++)
        {
            string fragmentId = $"fragment_{i + 1}";
            if (MemoryFragmentNode.IsCollected(fragmentId))
            {
                collectedCount++;
                continue;
            }

            float logicY = i < fragmentYPositions.Length ? fragmentYPositions[i] : startY + (endY - startY) * (i + 1) / 5f;
            Vector2 visualPos = (i < fragmentVisualPositions.Length) ? fragmentVisualPositions[i] : (i % 2 == 0 ? new Vector2(-2, 0) : new Vector2(2, 0));

            // 父物体：逻辑交互点（Collider, MemoryFragmentNode Logic）
            var fragLogicGO = new GameObject($"MemoryFragment_{i + 1}_Logic");
            fragLogicGO.transform.position = new Vector3(0f, logicY, 0f); // 逻辑位置（Y 轴进度）

            // 逻辑组件
            var fragCol = fragLogicGO.AddComponent<CircleCollider2D>();
            fragCol.radius = 1.5f; // 交互范围
            fragCol.isTrigger = true;

            var fragNode = fragLogicGO.AddComponent<MemoryFragmentNode>();
            fragNode.SetFragmentId(fragmentId);
            fragNode.fragmentTitle = i < fragmentTitles.Length ? fragmentTitles[i] : $"碎片 #{i + 1}";
            fragNode.fragmentBody = i < fragmentBodies.Length ? fragmentBodies[i] : "一段破碎的记忆……";

            // 子物体：视觉表现（Sprite Renderer）
            var fragVisualGO = new GameObject("Visual");
            fragVisualGO.transform.SetParent(fragLogicGO.transform, false);
            
            // 关键：为了在 Logic Y (0, 20) 时让 Visual 出现在 屏幕(0,0) 的相对位置
            // 相机在 (0,0)，屏幕位置(visualPos) 假如是 (2, 0)
            // 那么 Visual 的世界坐标应该是 (2, 0)。
            // 父物体在 (0, 20)。
            // 本地位置 = (2, 0) - (0, 20) = (2, -20)。
            fragVisualGO.transform.position = new Vector3(visualPos.x, visualPos.y, 0f);

            var fragSR = fragVisualGO.AddComponent<SpriteRenderer>();
            fragSR.sprite = fragmentSprite;
            fragSR.sortingOrder = 10;
            fragVisualGO.transform.localScale = Vector3.one * fragmentScale;

            // 添加透视增强脚本
            var perspective = fragVisualGO.AddComponent<MemoryPerspectiveEffect>();
            perspective.playerLogicTransform = playerGO.transform;
            perspective.fragmentLogicRoot = fragLogicGO.transform; // 使用父物体作为判定进度的基准
            perspective.visualTransform = fragVisualGO.transform;
            
            // 使用新的世界坐标透视模式
            perspective.useWorldSpaceLerp = true;
            perspective.vanishingPoint = new Vector3(perspectiveVanishingPoint.x, perspectiveVanishingPoint.y, 0f);
            perspective.targetWorldPos = new Vector3(visualPos.x, visualPos.y, 0f);
            
            // 配置透视感觉
            perspective.appearDistance = 10f; // 提前显示
            perspective.fullDistance = 1.5f;  // 快碰到才最大
            perspective.minScale = fragmentScale * 0.1f; // 远处很小
            perspective.maxScale = fragmentScale;        // 近处正常
            perspective.centerBias = 0.2f;               // 远处聚集在中心

            // Visual 子物体的位置即为屏幕可见位置。
            // MemoryFragmentNode.OnPlayerEnter() 中已通过 transform.Find("Visual") 获取飘字锚点。
        }

        // ─── 4. 创建终点门 ───────────────────────────────────
        var portalGO = new GameObject("AbyssPortal");
        portalGO.transform.position = new Vector3(0f, portalY, 0f);

        var portalCol = portalGO.AddComponent<BoxCollider2D>();
        portalCol.size = new Vector2(3f, 2f);
        portalCol.isTrigger = true;

        var portal = portalGO.AddComponent<AbyssPortal>();
        portal.requiredFragments = 4;

        // 同步已收集碎片数量（避免跳过生成后门进度仍为 0）
        for (int i = 0; i < collectedCount; i++)
        {
            portal.CollectFragment();
        }

        portalGO.AddComponent<AbyssPortalNode>();

        // ─── 5. 相机设置 ──────────────────────────────────────
        var cam = Camera.main;
        if (cam != null)
        {
            // 确保相机固定在原点，移除跟随脚本
            var oldFollow = cam.GetComponent<CameraFollow>();
            if (oldFollow != null) Destroy(oldFollow);
            
            cam.transform.position = new Vector3(0f, 0f, -10f);
        }

        Debug.Log("[MemorySceneSetup] 场景初始化完成 — Player/Background/Fragments/Portal 已创建。");
    }

    private void CleanupLegacyMemoryObjects()
    {
        // 旧版玩家（只清理场景内已有对象，避免重复 Player）
        var players = FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None);
        for (int i = 0; i < players.Length; i++)
        {
            if (players[i] == null) continue;
            Destroy(players[i].gameObject);
        }

        // 旧版碎片与门
        var fragments = FindObjectsByType<MemoryFragmentNode>(FindObjectsSortMode.None);
        for (int i = 0; i < fragments.Length; i++)
        {
            if (fragments[i] == null) continue;
            Destroy(fragments[i].gameObject);
        }

        var portals = FindObjectsByType<AbyssPortal>(FindObjectsSortMode.None);
        for (int i = 0; i < portals.Length; i++)
        {
            if (portals[i] == null) continue;
            Destroy(portals[i].gameObject);
        }

        // 旧版背景
        var oldParallax = FindObjectsByType<MemoryParallaxBackground>(FindObjectsSortMode.None);
        for (int i = 0; i < oldParallax.Length; i++)
        {
            if (oldParallax[i] == null) continue;
            Destroy(oldParallax[i].gameObject);
        }

    }

    /// <summary>
    /// 确保场景中有 EventSystem（UI 按钮点击必需）。
    /// Bootstrapper 会在 [MANAGERS] 下创建持久化 EventSystem，此方法作为兜底保护，
    /// 仅在未检测到任何 EventSystem 时才补建（例如直接运行本场景、跳过 Bootstrapper 时）。
    /// </summary>
    private void EnsureEventSystem()
    {
        if (FindAnyObjectByType<EventSystem>() != null) return;

        var esGO = new GameObject("EventSystem");
        esGO.AddComponent<EventSystem>();

        // 优先使用 InputSystem 的 UI Input Module
        var moduleType = System.Type.GetType(
            "UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
        if (moduleType != null)
            esGO.AddComponent(moduleType);
        else
            esGO.AddComponent<StandaloneInputModule>();

        Debug.Log("[MemorySceneSetup] 运行时创建 EventSystem。");
    }

    // ═══════════════════════════════════════════════════════════════
    //  UI 层级修复
    //  UIRoot Prefab（Resources/Prefabs/UIroot）若为空壳（仅 Transform，
    //  无 Canvas、无 HUDLayer / ModalLayer 等子对象），UIManager 的
    //  BindLayersFromRoot() 会使所有层引用为 null，导致 ModalSystem、
    //  ToastSystem、TransitionSystem 全部静默失败。
    //  此方法在场景初始化前检测并重建完整的运行时 UI 层级。
    // ═══════════════════════════════════════════════════════════════

    private void EnsureUILayers()
    {
        var ui = UIManager.Instance;
        if (ui == null)
        {
            Debug.LogError("[MemorySceneSetup] UIManager.Instance 为 null，跳过 UI 层级检查。");
            return;
        }

        // 如果关键层已存在，无需重建
        if (ui.modalLayer != null && ui.hudLayer != null
            && ui.overlayLayer != null && ui.transitionLayer != null)
        {
            Debug.Log("[MemorySceneSetup] UI 层级检查通过。");
            return;
        }

        Debug.LogWarning(
            "[MemorySceneSetup] UIManager 层级缺失（UIRoot Prefab 可能为空壳），开始重建运行时 UI 结构……");

        // 移除有问题的 UIRoot
        var oldRoot = ui.transform.Find("UIRoot");
        if (oldRoot != null)
            Destroy(oldRoot.gameObject);

        // ── 创建完整的 Canvas 层级结构 ──────────────────────────
        var canvasGO = new GameObject("UIRoot");
        canvasGO.transform.SetParent(ui.transform, false);

        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 0;

        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGO.AddComponent<GraphicRaycaster>();

        // 按渲染顺序从低到高创建层
        ui.hudLayer        = CreateUILayer(canvasGO.transform, "HUDLayer", 10);
        ui.overlayLayer    = CreateUILayer(canvasGO.transform, "OverlayLayer", 50);
        ui.modalLayer      = CreateUILayer(canvasGO.transform, "ModalLayer", 90);
        ui.transitionLayer = CreateUILayer(canvasGO.transform, "TransitionLayer", 100);

        // 模态背景（全屏半透明黑遮罩，阻断输入）
        var bgGO = new GameObject("ModalBackground");
        bgGO.transform.SetParent(ui.modalLayer, false);
        bgGO.transform.SetAsFirstSibling();
        ui.modalBackground = bgGO.AddComponent<Image>();
        ui.modalBackground.color = new Color(0f, 0f, 0f, 0f);
        ui.modalBackground.raycastTarget = true;
        var bgRect = bgGO.GetComponent<RectTransform>();
        StretchFull(bgRect);
        ui.modalBackground.gameObject.SetActive(false);

        // 重新初始化所有子系统（它们的 Initialize() 依赖层引用非 null）
        ui.Transition?.Initialize();
        ui.Modal?.Initialize();
        ui.Toast?.Initialize();
        ui.HUD?.Initialize();
        ui.Dialogue?.Initialize();
        ui.ItemDisplay?.Initialize();

        Debug.Log("[MemorySceneSetup] UI 层级已重建完成。");
    }

    private static RectTransform CreateUILayer(Transform parent, string name, int sortOrder)
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

    private static void StretchFull(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
