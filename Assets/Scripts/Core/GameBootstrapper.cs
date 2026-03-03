using UnityEngine;

/// <summary>
/// 游戏自动初始化启动器。
/// 
/// 原理：
///   [RuntimeInitializeOnLoadMethod] 让 Unity 在任何场景加载 **之前** 自动调用 Bootstrap()，
///   无需在 Hierarchy 里手动放置任何 Manager GameObject。
///
/// 使用方式：
///   1. 在 Project 窗口右键 → Create → Game → Boot Config，
///      将生成的 Asset 保存到 Assets/Resources/Config/GameBootConfig.asset。
///   2. 在 Asset 里配置场景名、起始阶段等。
///   3. 直接 Play，所有 Manager 均自动创建完毕。
///
/// 团队开发说明：
///   - 新增场景：在 GameBootConfig 中添加场景名字段
///   - 新增 Manager：在 Bootstrap() 的 "创建核心 Manager" 区域添加
///   - 场景流程：修改 GameBootConfig.startPhase 来决定从哪个阶段开始
/// </summary>
public static class GameBootstrapper
{
    private const string CONFIG_PATH = "Config/GameBootConfig";
    private const string ROOT_NAME = "[MANAGERS]";

    // BeforeSceneLoad：在第一个场景 Awake/Start 之前执行
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        // 防止热重载时重复初始化
        if (GameObject.Find(ROOT_NAME) != null)
        {
            Debug.Log("[Bootstrapper] 管理器根对象已存在，跳过初始化。");
            return;
        }

        // ── 1. 加载配置 ──────────────────────────────────────────────
        GameBootConfig config = LoadConfig();
        Log(config, "[Bootstrapper] 开始初始化...");

        // ── 2. 创建根容器 ─────────────────────────────────────────────
        var root = new GameObject(ROOT_NAME);
        Object.DontDestroyOnLoad(root);

        // ── 3. 创建核心 Manager ───────────────────────────────────────
        // 按依赖顺序创建，后面的可能依赖前面的
        
        // DataManager：数据管理（证据、话题等）
        CreateManager<DataManager>(root, "DataManager");
        
        // SceneController：场景切换控制
        CreateManager<SceneController>(root, "SceneController", sc =>
        {
            sc.InjectConfig(
                config.bootScene, 
                config.mainMenuScene,
                config.cutsceneScene,
                config.memoryScene,
                config.abyssScene, 
                config.courtScene
            );
        });
        
        // GameManager：游戏阶段状态机
        CreateManager<GameManager>(root, "GameManager", gm =>
        {
            gm.InjectConfig(config.startPhase);
        });

        Log(config, "[Bootstrapper] 所有 Manager 初始化完成。");
        Log(config, $"[Bootstrapper] 起始阶段: {config.startPhase}");
    }

    // ── 辅助方法 ─────────────────────────────────────────────────────

    /// <summary>
    /// 加载配置文件，找不到则创建默认配置
    /// </summary>
    private static GameBootConfig LoadConfig()
    {
        var config = Resources.Load<GameBootConfig>(CONFIG_PATH);

        if (config == null)
        {
            Debug.LogWarning(
                "[Bootstrapper] 找不到 GameBootConfig！\n" +
                $"请创建配置文件：Assets → Create → Game → Boot Config，\n" +
                $"保存到 Assets/Resources/{CONFIG_PATH}.asset\n" +
                $"使用默认配置继续...");
            
            config = ScriptableObject.CreateInstance<GameBootConfig>();
        }

        return config;
    }

    /// <summary>
    /// 在 parent 下创建一个挂载 T 组件的子对象，并在添加后执行可选的初始化回调。
    /// </summary>
    private static T CreateManager<T>(
        GameObject parent,
        string name,
        System.Action<T> onCreated = null) where T : UnityEngine.Component
    {
        // 防止重复创建（热重载 / 编辑器多次进入 Play 时可能触发）
        T existing = Object.FindFirstObjectByType<T>();
        if (existing != null)
        {
            Debug.LogWarning($"[Bootstrapper] {typeof(T).Name} 已存在，跳过创建。");
            return existing;
        }

        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        T manager = go.AddComponent<T>();
        // 父对象已标记 DontDestroyOnLoad，子对象自动继承
        
        onCreated?.Invoke(manager);
        return manager;
    }

    private static void Log(GameBootConfig config, string msg)
    {
        if (config != null && config.verboseLog)
            Debug.Log(msg);
    }
}
