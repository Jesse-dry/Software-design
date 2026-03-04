using UnityEngine;

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
    [Tooltip("4 个碎片分布的 Y 坐标")]
    public float[] fragmentYPositions = { 3f, 7f, 11f, 15f };

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

    private bool initialized;

    void Start()
    {
        if (initialized) return;
        initialized = true;
        SetupScene();
    }

    private void SetupScene()
    {
        if (removeLegacyMemoryObjects)
            CleanupLegacyMemoryObjects();

        // ─── 1. 创建 Player ────────────────────────────────────
        var playerGO = new GameObject("Player");
        playerGO.tag = "Player";
        playerGO.transform.position = new Vector3(0f, startY, 0f);
        playerGO.layer = LayerMask.NameToLayer("Default");

        var rb = playerGO.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        var playerCol = playerGO.AddComponent<BoxCollider2D>();
        playerCol.size = new Vector2(0.5f, 0.5f);
        playerCol.isTrigger = false;

        // Input System 需要 PlayerInput 组件
        var playerInput = playerGO.AddComponent<UnityEngine.InputSystem.PlayerInput>();
        playerInput.defaultActionMap = "Player";

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

        // ─── 3. 创建 4 个碎片 ────────────────────────────────
        for (int i = 0; i < 4; i++)
        {
            float y = i < fragmentYPositions.Length ? fragmentYPositions[i] : startY + (endY - startY) * (i + 1) / 5f;

            var fragGO = new GameObject($"MemoryFragment_{i + 1}");
            fragGO.transform.position = new Vector3(0f, y, 0f);

            var fragSR = fragGO.AddComponent<SpriteRenderer>();
            fragSR.sprite = fragmentSprite;
            fragSR.sortingOrder = 10;
            fragGO.transform.localScale = Vector3.one * fragmentScale;

            var fragCol = fragGO.AddComponent<CircleCollider2D>();
            fragCol.radius = 1.5f / fragmentScale; // 补偿缩放
            fragCol.isTrigger = true;

            var fragNode = fragGO.AddComponent<MemoryFragmentNode>();
            fragNode.fragmentTitle = i < fragmentTitles.Length ? fragmentTitles[i] : $"碎片 #{i + 1}";
            fragNode.fragmentBody = i < fragmentBodies.Length ? fragmentBodies[i] : "一段破碎的记忆……";
        }

        // ─── 4. 创建终点门 ───────────────────────────────────
        var portalGO = new GameObject("AbyssPortal");
        portalGO.transform.position = new Vector3(0f, portalY, 0f);

        var portalCol = portalGO.AddComponent<BoxCollider2D>();
        portalCol.size = new Vector2(3f, 2f);
        portalCol.isTrigger = true;

        var portal = portalGO.AddComponent<AbyssPortal>();
        portal.requiredFragments = 4;

        portalGO.AddComponent<AbyssPortalNode>();

        // ─── 5. 相机设置 ──────────────────────────────────────
        var cam = Camera.main;
        if (cam != null)
        {
            // 移除已有的 CameraFollow（如果有），重新添加配置
            var oldFollow = cam.GetComponent<CameraFollow>();
            if (oldFollow != null) Destroy(oldFollow);

            var follow = cam.gameObject.AddComponent<CameraFollow>();
            follow.target = playerGO.transform;
            follow.smoothSpeed = 5f;
            follow.offset = new Vector3(0f, 0f, -10f);
            follow.useBounds = true;
            follow.minBounds = new Vector2(-1f, startY);
            follow.maxBounds = new Vector2(1f, endY);
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

        // 旧版独立 UI 管理器（仅场景级）
        var legacyUi = FindObjectsByType<MemoryUIManager>(FindObjectsSortMode.None);
        for (int i = 0; i < legacyUi.Length; i++)
        {
            if (legacyUi[i] == null) continue;
            Destroy(legacyUi[i].gameObject);
        }
    }
}
