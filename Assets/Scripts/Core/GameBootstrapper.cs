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
///   2. 在 Asset 里填写场景名、起始阶段等配置。
///   3. 直接 Play，所有 Manager 均自动创建完毕。
/// </summary>
public static class GameBootstrapper
{
    private const string CONFIG_PATH = "Config/GameBootConfig";

    // BeforeSceneLoad：在第一个场景 Awake/Start 之前执行
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        // ── 1. 加载配置 ──────────────────────────────────────────────
        GameBootConfig config = Resources.Load<GameBootConfig>(CONFIG_PATH);

        if (config == null)
        {
            Debug.LogError(
                "[Bootstrapper] 找不到 GameBootConfig！\n" +
                $"请将配置文件放到 Assets/Resources/{CONFIG_PATH}.asset");
            // 降级：使用默认值继续，避免游戏直接崩溃
            config = ScriptableObject.CreateInstance<GameBootConfig>();
        }

        Log(config, "[Bootstrapper] 开始初始化...");

        // ── 2. 创建根容器 ─────────────────────────────────────────────
        var root = new GameObject("[MANAGERS]");
        Object.DontDestroyOnLoad(root);

        // ── 3. 按依赖顺序创建各 Manager ──────────────────────────────
        //   DataManager 最先，其他 Manager 可能依赖它
        CreateManager<DataManager>(root, "DataManager");
        CreateManager<SceneController>(root, "SceneController", sc =>
        {
            // 将配置里的场景名注入 SceneController
            sc.InjectConfig(config.bootScene, config.abyssScene, config.courtScene);
        });
        CreateManager<GameManager>(root, "GameManager", gm =>
        {
            gm.InjectConfig(config.startPhase);
        });

        Log(config, "[Bootstrapper] 所有 Manager 初始化完成。");
    }

    // ── 辅助方法 ─────────────────────────────────────────────────────

    /// <summary>
    /// 在 root 下创建一个挂载 T 组件的子对象，并在添加后执行可选的初始化回调。
    /// </summary>
    private static T CreateManager<T>(
        GameObject root,
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
        go.transform.SetParent(root.transform);
        T manager = go.AddComponent<T>();
        onCreated?.Invoke(manager);
        return manager;
    }

    private static void Log(GameBootConfig config, string msg)
    {
        if (config != null && config.verboseLog)
            Debug.Log(msg);
    }
}
