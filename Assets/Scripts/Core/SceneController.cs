using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

/// <summary>
/// 场景切换控制器。
/// 
/// 负责：
///   - 管理所有场景的名称配置
///   - 提供场景加载接口
///   - 处理加载过渡（渐变黑屏等）
/// 
/// 团队开发说明：
///   - 新增场景：在 InjectConfig 中添加参数，并添加对应的 LoadXxx() 方法
///   - 过渡效果：在 LoadSceneCoroutine 中扩展（Loading UI、进度条等）
/// </summary>
public class SceneController : MonoBehaviour
{
    public static SceneController Instance { get; private set; }

    // 场景名称配置（由 Bootstrapper 注入）
    private string bootScene = "Boot";
    private string mainMenuScene = "MainMenu";
    private string cutsceneScene = "CutsceneScene";
    private string memoryScene = "Memory";
    private string abyssScene = "Abyss";
    private string courtScene = "Court";
    private string DecodeGameScene = "DecodeGame";
    private string corridorScene = "Corridor";
    private string pipeRoomScene = "PipeRoom";
    private string pipePuzzleScene = "PipePuzzle";
    private string serverRoomScene = "ServerRoom";

    private bool isLoading = false;

    // =========================
    // 生命周期
    // =========================

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        // 由 GameBootstrapper 创建时，父对象已标记 DontDestroyOnLoad，子对象自动继承
        if (transform.parent == null)
            DontDestroyOnLoad(gameObject);
    }

    // =========================
    // 配置注入（由 GameBootstrapper 调用）
    // =========================

    /// <summary>
    /// 由 GameBootstrapper 在 Start() 之前注入场景名配置，
    /// 优先级高于 Inspector 里的 SerializeField 默认值。
    /// </summary>
    public void InjectConfig(
        string boot, 
        string mainMenu,
        string cutscene,
        string memory,
        string abyss, 
        string court,
        string corridor,
        string decodeGame,
        string pipeRoom,
        string pipePuzzle,
        string serverRoom)
    {
        if (!string.IsNullOrEmpty(boot)) bootScene = boot;
        if (!string.IsNullOrEmpty(mainMenu)) mainMenuScene = mainMenu;
        if (!string.IsNullOrEmpty(cutscene)) cutsceneScene = cutscene;
        if (!string.IsNullOrEmpty(memory)) memoryScene = memory;
        if (!string.IsNullOrEmpty(abyss)) abyssScene = abyss;
        if (!string.IsNullOrEmpty(court)) courtScene = court;
        if (!string.IsNullOrEmpty(corridor)) corridorScene = corridor;
        if (!string.IsNullOrEmpty(decodeGame)) DecodeGameScene = decodeGame;
        if (!string.IsNullOrEmpty(pipeRoom)) pipeRoomScene = pipeRoom;
        if (!string.IsNullOrEmpty(pipePuzzle)) pipePuzzleScene = pipePuzzle;
        if (!string.IsNullOrEmpty(serverRoom)) serverRoomScene = serverRoom;
    }

    // =========================
    // 场景加载接口
    // =========================

    public void LoadBoot()
    {
        LoadScene(bootScene);
    }

    public void LoadMainMenu()
    {
        LoadScene(mainMenuScene);
    }

    public void LoadCutscene()
    {
        LoadScene(cutsceneScene);
    }

    public void LoadMemory()
    {
        LoadScene(memoryScene);
    }

    public void LoadAbyss()
    {
        LoadScene(abyssScene);
    }

    public void LoadCourt()
    {
        LoadScene(courtScene);
    }

    public void LoadDecodeGame()
    {
        LoadScene(DecodeGameScene);
    }

    public void LoadCorridor()
    {
        LoadScene(corridorScene);
    }

    public void LoadPipeRoom()
    {
        LoadScene(pipeRoomScene);
    }

    public void LoadPipePuzzle()
    {
        LoadScene(pipePuzzleScene);
    }

    public void LoadServerRoom()
    {
        LoadScene(serverRoomScene);
    }

    // =========================
    // 核心加载逻辑
    // =========================

    private void LoadScene(string sceneName)
    {
        if (isLoading)
        {
            Debug.LogWarning($"[SceneController] 正在加载场景中，忽略请求: {sceneName}");
            return;
        }

        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogError("[SceneController] 场景名称为空！");
            return;
        }

        StartCoroutine(LoadSceneCoroutine(sceneName));
    }

    private IEnumerator LoadSceneCoroutine(string sceneName)
    {
        isLoading = true;

        Debug.Log($"[SceneController] 开始加载场景: {sceneName}");

        // TODO: 这里可以扩展过渡效果
        // - 渐变黑屏
        // - Loading UI
        // - 进度条显示

        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName);

        while (!operation.isDone)
        {
            // 进度：operation.progress (0~0.9)
            yield return null;
        }

        Debug.Log($"[SceneController] 场景加载完成: {sceneName}");

        isLoading = false;
    }

    // =========================
    // 工具方法
    // =========================

    /// <summary>
    /// 获取当前场景名称
    /// </summary>
    public string GetCurrentSceneName()
    {
        return SceneManager.GetActiveScene().name;
    }

    /// <summary>
    /// 检查是否正在加载场景
    /// </summary>
    public bool IsLoading()
    {
        return isLoading;
    }
}
