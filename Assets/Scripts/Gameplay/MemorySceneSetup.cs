using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using DG.Tweening;

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
        "当你读取到这段数据时，零号法庭的记忆复现程序已经启动。我是 13 号档案员，也就是你现在重演的「罪徒」。接下来的记忆，藏着我看到的全部真相，也是你活下去的唯一依仗……",
        "【永序记忆】，将客户的记忆完整抽取并加密托管在离线服务器中，客户大脑将彻底清除这段记忆。你是工号 13，永序中心金牌记忆搬运工，身处归档区，安保系统已经全面启动……你的核心目标：活着逃出这栋大楼……",
        "《绝对静默》：档案员仅作为数据的容器与搬运工。严禁查看、严禁拷贝、严禁对客户记忆产生任何主观解读。数据即是数据，无关善恶。",
        "他们说数据即是数据，无关善恶。但我看到了恶，就无法无视。绝对静默，从来不是我们闭嘴的理由。当你收回记忆，就有了扳倒他们的武器……"
    };

    [Header("== 门（终点） ==")]
    [Tooltip("门的 Y 坐标")]
    public float portalY = 19f;

    [Tooltip("门需要的碎片数量")]
    public int portalRequiredFragments = 4;

    [Tooltip("碎片不足时的提示文字（门）")]
    public string portalInsufficientText = "碎片不足，继续探索吧";

    [Tooltip("碎片集齐后按 E 的提示文字（门）")]
    public string portalReadyText = "按 E 进入深渊";

    [Tooltip("碎片不足时尝试进入深渊的 Toast 文字")]
    public string portalInsufficientToastText = "记忆碎片不足，继续探索吧。";

    [Tooltip("进入深渊的确认弹窗文字")]
    public string portalConfirmText = "潜入深渊，探寻真相？";

    [Header("== 碎片缩放 ==")]
    [Tooltip("碎片 Sprite 缩放")]
    public float fragmentScale = 0.5f;

    [Header("== 透视设置 ==")]
    [Tooltip("碎片的透视消失点（世界/屏幕坐标，通常是 (0,0)）")]
    public Vector2 perspectiveVanishingPoint = Vector2.zero;

    // ────────────────────────────────────────────────────────────────────
    //  交互文字
    //  以下字段在运行时写入动态创建的 MemoryFragmentNode，便于直接在 Inspector 修改
    // ────────────────────────────────────────────────────────────────────

    [Header("== 交互文字 ==")]
    [Tooltip("玩家靠近碎片时的操作提示文字")]
    public string fragmentInteractPromptText = "按 E 交互";

    [Tooltip("碎片弹窗关闭按钮文字")]
    public string fragmentCloseButtonText = "关闭";

    // ────────────────────────────────────────────────────────────────────
    //  提示文字样式
    //  对应 MemoryNodeBase 中的 promptXxx 字段，运行时写入动态创建的碎片节点
    // ────────────────────────────────────────────────────────────────────

    [Header("== 提示文字样式 ==")]
    [Tooltip("世界空间文字大小（CharacterSize）")]
    [Range(0.05f, 0.5f)]
    public float fragmentPromptCharSize = 0.15f;

    [Tooltip("字号（FontSize，影响清晰度）")]
    [Range(16, 128)]
    public int fragmentPromptFontSize = 64;

    [Tooltip("提示文字颜色")]
    public Color fragmentPromptColor = new Color(0.95f, 0.95f, 1f, 0.95f);

    [Tooltip("提示文字的 MeshRenderer 排序层级（越大越靠前）")]
    public int fragmentPromptSortingOrder = 200;

    // ────────────────────────────────────────────────────────────────────
    //  UI 效果调节
    //  以下参数在场景初始化时自动应用到 UIManager 对应子系统
    //  修改后无需改代码，保存场景即生效
    // ────────────────────────────────────────────────────────────────────

    [Header("== UI 覆盖开关 ==")]
    [Tooltip("启用后 MemorySceneSetup 的'碎片文字动画'参数会在运行时覆盖 UIManager Prefab 中 DialoguePlayer 的同名设置。\n"
           + "关闭（默认）则完全以 Prefab 为准，不做任何修改。")]
    public bool overrideDialogueSettings = false;

    [Tooltip("启用后 MemorySceneSetup 的'转场效果'参数会在运行时覆盖 UIManager Prefab 中 TransitionSystem 的同名设置。\n"
           + "关闭（默认）则完全以 Prefab 为准，不做任何修改。")]
    public bool overrideTransitionSettings = false;

    [Tooltip("启用后 MemorySceneSetup 的'按钮样式'参数会在运行时覆盖 UIManager Prefab 中 ModalSystem 的按钮样式设置。\n"
           + "关闭（默认）则完全以 Prefab 为准，不做任何修改。")]
    public bool overrideButtonStyle = false;

    [Header("== 碎片文字动画 ==")]
    [Tooltip("碎片对话框的文字出现效果\n"
           + "  Typewriter     — 逐字打出（默认）\n"
           + "  Decode         — 乱码逐字解码\n"
           + "  FadeInPerChar  — 每字淡入\n"
           + "  GlitchLoop     — 持续故障抖动\n"
           + "  Wave           — 波浪起伏")]
    public TextEffectType fragmentTextEffect = TextEffectType.Typewriter;

    [Tooltip("打字机每个字符的停留时间（秒），仅 Typewriter / Decode / FadeInPerChar 有效\n0.01 = 极快，0.1 = 较慢，0.2 = 可明显感到停顿")]
    [Range(0.01f, 0.2f)]
    public float fragmentCharDelay = 0.04f;

    [Tooltip("说话人名字颜色（如《深竭瘁00山》等 NPC 名）")]
    public Color fragmentSpeakerColor = new Color(0.4f, 0.9f, 0.65f, 1f);

    [Tooltip("碎片正文颜色")]
    public Color fragmentBodyColor = new Color(0.85f, 0.85f, 0.9f, 1f);

    [Tooltip("对话框淡入 / 淡出时长（秒）")]
    [Range(0.1f, 0.8f)]
    public float fragmentPanelFadeDuration = 0.3f;

    [Header("== 背景渲染 ==")]
    [Tooltip("背景 Sprite 颜色叠加（白色 = 原色，偏暗 = 整体变暗）\n"
           + "小投巧：红色通道小于其他通道可调出蓝紫教堂感")]
    public Color backgroundTint = Color.white;

    [Tooltip("背景的整体缩放（1 = 原始大小）\n"
           + "调大可防止个别分辨率下出现黑边")]
    [Range(0.5f, 4f)]
    public float backgroundScale = 1f;

    [Tooltip("背景 Sprite 排序层（颜小越靠后，默认 -100）")]
    public int backgroundSortingOrder = -100;

    [Header("== 转场效果 ==")]
    [Tooltip("转场类型\n"
           + "  FadeBlack  — 黑幕淡入淡出（默认）\n"
           + "  FadeWhite  — 白光闪烁（记忆闪回感）\n"
           + "  GlitchFade — 故障批动 + 淡入淡出（数字世界感）")]
    public TransitionType memoryTransitionType = TransitionType.FadeBlack;

    [Tooltip("转场淡入淡出的默认时长（秒），影响进入和开局转入 Memory 后的黑幕消散速度")]
    [Range(0.1f, 3f)]
    public float transitionDuration = 1f;

    [Tooltip("淡入缓动曲线（黑幕出现时）")]
    public Ease transitionFadeInEase = Ease.InQuad;

    [Tooltip("淡出缓动曲线（黑幕消散时）")]
    public Ease transitionFadeOutEase = Ease.OutQuad;

    [Tooltip("[GlitchFade] 抗扩抖动强度，越大位移越明显")]
    [Range(1f, 20f)]
    public float transitionGlitchIntensity = 5f;

    [Tooltip("[GlitchFade] Glitch 持续时间（秒），超过此时长后过渡到平滑淡入淡出")]
    [Range(0.1f, 2f)]
    public float transitionGlitchDuration = 0.5f;

    [Tooltip("[GlitchFade] Glitch 帧间回调间隔（秒，越小越高频）")]
    [Range(0.01f, 0.2f)]
    public float transitionGlitchFrequency = 0.05f;

    // ────────────────────────────────────────────────────────────────────
    //  按钮样式（覆盖 ModalSystem 中的按钮外观）
    //  设置 overrideButtonStyle = true 后以下参数生效
    // ────────────────────────────────────────────────────────────────────

    [Header("== 按钮样式 ==")]
    [Tooltip("按钮大小覆盖（设为 (0,0) 保留原始大小）")]
    public Vector2 buttonSize = Vector2.zero;

    [Tooltip("悬停缩放倍率")]
    [Range(1f, 1.3f)]
    public float buttonHoverScale = 1.08f;

    [Tooltip("按下缩放倍率")]
    [Range(0.85f, 1f)]
    public float buttonPressScale = 0.95f;

    [Tooltip("按钮常态背景色")]
    public Color buttonNormalColor = new Color(0.15f, 0.15f, 0.2f, 1f);

    [Tooltip("按钮悬停背景色")]
    public Color buttonHoverColor = new Color(0.25f, 0.25f, 0.35f, 1f);

    [Tooltip("按钮按下背景色")]
    public Color buttonPressColor = new Color(0.1f, 0.3f, 0.2f, 1f);

    [Tooltip("按钮常态文字颜色")]
    public Color buttonNormalTextColor = new Color(0.7f, 0.9f, 0.7f, 1f);

    [Tooltip("按钮悬停文字颜色")]
    public Color buttonHoverTextColor = new Color(0.9f, 1f, 0.9f, 1f);

    [Tooltip("是否启用发光边框效果")]
    public bool buttonGlowOutline = false;

    private bool initialized;

    void Start()
    {
        if (initialized) return;
        initialized = true;

        // 【关键】先修复 UI 层级，再初始化场景
        // UIRoot Prefab 若为空壳（无 Canvas / 无子层级），所有 UI 系统（Modal、Toast、Transition）
        // 的 Initialize() 会静默失败，导致交互后弹窗不显示、Toast 不出现、转场黑屏等问题。
        EnsureUILayers();

        // 将 Inspector 中设置的 UI 效果参数应用到各子系统
        ApplyUIEffectSettings();

        SetupScene();
    }

    private void SetupScene()
    {
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
        bgSR.sortingOrder = backgroundSortingOrder;
        bgSR.color        = backgroundTint;
        bgGO.transform.position   = Vector3.zero;
        bgGO.transform.localScale = Vector3.one * backgroundScale;

        var parallax = bgGO.AddComponent<MemoryParallaxBackground>();
        parallax.SetFrames(backgroundFrames, playerGO.transform, startY, endY);

        // 如果有帧，先显示第一帧
        if (backgroundFrames != null && backgroundFrames.Length > 0)
        {
            bgSR.sprite = backgroundFrames[0];
        }

        int alreadyCollected = 0;

        // ─── 3. 创建 4 个碎片 ────────────────────────────────
        for (int i = 0; i < 4; i++)
        {
            string fragmentId = $"fragment_{i + 1}";
            if (MemoryFragmentNode.IsCollected(fragmentId))
            {
                alreadyCollected++;
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

            // 应用交互文字（由 MemorySceneSetup Inspector 统一配置）
            fragNode.interactPromptText  = fragmentInteractPromptText;
            fragNode.modalCloseButtonText = fragmentCloseButtonText;

            // 应用提示文字样式（继承自 MemoryNodeBase，运行时写入）
            fragNode.promptCharSize     = fragmentPromptCharSize;
            fragNode.promptFontSize     = fragmentPromptFontSize;
            fragNode.promptColor        = fragmentPromptColor;
            fragNode.promptSortingOrder = fragmentPromptSortingOrder;

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
        portal.requiredFragments = portalRequiredFragments;
        portal.insufficientToastText = portalInsufficientToastText;
        portal.confirmText = portalConfirmText;

        // 同步已收集碎片数量（避免跳过生成后门进度仍为 0）
        for (int i = 0; i < alreadyCollected; i++)
        {
            portal.CollectFragment();
        }

        var portalNode = portalGO.AddComponent<AbyssPortalNode>();
        portalNode.readyText = portalReadyText;
        portalNode.insufficientText = portalInsufficientText;

        // ─── 5. 相机设置 ──────────────────────────────────────
        var cam = Camera.main;
        if (cam != null)
        {
            // 确保相机固定在原点，移除跟随脚本
            var oldFollow = cam.GetComponent<CameraFollow>();
            if (oldFollow != null) Destroy(oldFollow);

            cam.transform.position = new Vector3(0f, 0f, -10f);

            // 【Bug Fix】重置 Viewport Rect 为全屏。
            // 从 Cutscene/VideoPlayer 场景过来时，主相机的 rect 可能被修改为非 (0,0,1,1)，
            // 导致世界空间内容仅渲染在屏幕子区域，四周出现相机背景色（蓝色）空白边，
            // 而 ScreenSpace-Overlay 的 UISceneRoot Canvas 仍然全屏，产生"两个画布不对齐"的效果。
            cam.rect = new Rect(0f, 0f, 1f, 1f);
        }

        // ─── 6. 背景自适应缩放 ────────────────────────────────
        // 根据主相机正交尺寸 + 屏幕宽高比自动撑满背景。
        // Inspector 的 backgroundScale 作为额外倍率（保留微调能力，默认 1 = 刚好铺满不留边）。
        if (cam != null && backgroundFrames != null && backgroundFrames.Length > 0
            && backgroundFrames[0] != null)
        {
            FitBackgroundToCamera(bgSR, bgGO, cam);
        }

        Debug.Log("[MemorySceneSetup] 场景初始化完成 — Player/Background/Fragments/Portal 已创建。");
    }



    /// <summary>
    /// 自动计算背景 SpriteRenderer 的缩放，使其刚好铺满主相机正交视野。
    /// 以"cover 模式"处理：取宽/高所需缩放的较大值，保证无黑边。
    /// Inspector 的 backgroundScale 作为额外倍率叠加（> 1 可留安全边距）。
    /// </summary>
    private void FitBackgroundToCamera(SpriteRenderer sr, GameObject bgGO, Camera cam)
    {
        if (sr.sprite == null) return;

        // 相机正交视野的世界空间尺寸
        float camHalfHeight = cam.orthographicSize;
        float camHalfWidth  = camHalfHeight * cam.aspect;

        // Sprite 在 scale=1 时的世界空间半尺寸
        Rect spriteRect = sr.sprite.rect;
        float ppu        = sr.sprite.pixelsPerUnit;
        float spriteHalfH = (spriteRect.height / ppu) * 0.5f;
        float spriteHalfW = (spriteRect.width  / ppu) * 0.5f;

        if (spriteHalfH <= 0f || spriteHalfW <= 0f) return;

        // cover 模式：取两轴所需缩放的最大值，保证无黑边
        float scaleH = camHalfHeight / spriteHalfH;
        float scaleW = camHalfWidth  / spriteHalfW;
        float autoScale = Mathf.Max(scaleH, scaleW);

        // 叠加 Inspector 的额外倍率
        bgGO.transform.localScale = Vector3.one * (autoScale * backgroundScale);

        Debug.Log($"[MemorySceneSetup] 背景自适应缩放: autoScale={autoScale:F3} × Inspector={backgroundScale} = {autoScale * backgroundScale:F3}");
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
    //  UI 场景根检查
    //  新架构下每个场景应放置挂有 UISceneRoot 的 Canvas Prefab，
    //  UISceneRoot.Awake() 会自动向 UIManager 注册场景层引用。
    //  此方法仅做安全检查，不再运行时重建 UI 层级。
    // ═══════════════════════════════════════════════════════════════

    private void EnsureUILayers()
    {
        var ui = UIManager.Instance;
        if (ui == null)
        {
            Debug.LogError("[MemorySceneSetup] UIManager.Instance 为 null，跳过 UI 检查。");
            return;
        }

        if (!ui.HasSceneRoot)
        {
            Debug.LogWarning(
                "[MemorySceneSetup] 未检测到 UISceneRoot。\n" +
                "请在场景中放置挂有 UISceneRoot 的 Canvas Prefab（如 UIRoot_Memory）。\n" +
                "场景子系统（HUD / Modal / Dialogue / ItemDisplay）将无法正常工作。");
        }
        else if (ui.modalLayer == null || ui.hudLayer == null || ui.overlayLayer == null)
        {
            Debug.LogWarning(
                "[MemorySceneSetup] UISceneRoot 已注册但部分层引用为空，" +
                "请检查 UISceneRoot Inspector 中 HUDLayer / OverlayLayer / ModalLayer 是否已正确赋值。");
        }
        else
        {
            Debug.Log("[MemorySceneSetup] UI 场景根检查通过。");
        }
    }

    // ―――――――――――――――――――――――――――――――――――――――――――――――――――――――――――――――
    //  UI 效果参数应用
    // ―――――――――――――――――――――――――――――――――――――――――――――――――――――――――――――――

    /// <summary>
    /// 将 Inspector 字段中设置的效果参数应用到 UIManager 对应子系统。
    /// 在 EnsureUILayers() 之后、SetupScene() 之前调用，确保 UIManager 已初始化。
    /// </summary>
    private void ApplyUIEffectSettings()
    {
        var ui = UIManager.Instance;
        if (ui == null) return;

        // ── 碎片文字效果（DialoguePlayer）────────────────────────
        // 仅在 overrideDialogueSettings = true 时才写入，否则以 Prefab 序列化值为准
        if (overrideDialogueSettings)
        {
            if (ui.Dialogue != null)
            {
                ui.Dialogue.Configure(
                    effect:       fragmentTextEffect,
                    newCharDelay: fragmentCharDelay,
                    speakerCol:   fragmentSpeakerColor,
                    bodyCol:      fragmentBodyColor,
                    newPanelAnim: fragmentPanelFadeDuration);

                Debug.Log("[MemorySceneSetup] 对话效果已覆盖: " + fragmentTextEffect);
            }
            else
            {
                Debug.LogWarning("[MemorySceneSetup] UIManager.Dialogue 为 null，跳过文字效果应用。");
            }
        }
        else
        {
            Debug.Log("[MemorySceneSetup] overrideDialogueSettings=false，对话效果以 Prefab 设置为准。");
        }

        // ── 转场效果（TransitionSystem）───────────────────────────
        // 仅在 overrideTransitionSettings = true 时才写入，否则以 Prefab 序列化值为准
        if (overrideTransitionSettings)
        {
            if (ui.Transition != null)
            {
                ui.Transition.Configure(
                    type:              memoryTransitionType,
                    duration:          transitionDuration,
                    inEase:            transitionFadeInEase,
                    outEase:           transitionFadeOutEase,
                    newGlitchIntensity: transitionGlitchIntensity,
                    newGlitchFrequency: transitionGlitchFrequency,
                    newGlitchDuration:  transitionGlitchDuration);

                Debug.Log("[MemorySceneSetup] 转场效果已覆盖: " + memoryTransitionType);
            }
            else
            {
                Debug.LogWarning("[MemorySceneSetup] UIManager.Transition 为 null，跳过转场效果应用。");
            }
        }
        else
        {
            Debug.Log("[MemorySceneSetup] overrideTransitionSettings=false，转场效果以 Prefab 设置为准。");
        }

        // ── 按钮样式（ModalSystem）────────────────────────────────
        // 仅在 overrideButtonStyle = true 时才写入，否则以 Prefab 序列化值为准
        if (overrideButtonStyle)
        {
            if (ui.Modal != null)
            {
                ui.Modal.ConfigureButtonStyle(
                    enabled:    true,
                    size:       buttonSize,
                    hoverScale: buttonHoverScale,
                    pressScale: buttonPressScale,
                    normalBg:   buttonNormalColor,
                    hoverBg:    buttonHoverColor,
                    pressBg:    buttonPressColor,
                    normalText: buttonNormalTextColor,
                    hoverText:  buttonHoverTextColor,
                    glow:       buttonGlowOutline);

                Debug.Log("[MemorySceneSetup] 按钮样式已覆盖。");
            }
            else
            {
                Debug.LogWarning("[MemorySceneSetup] UIManager.Modal 为 null，跳过按钮样式应用。");
            }
        }
        else
        {
            Debug.Log("[MemorySceneSetup] overrideButtonStyle=false，按钮样式以 Prefab 设置为准。");
        }
    }


}
