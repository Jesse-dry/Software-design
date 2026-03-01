using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class SceneController : MonoBehaviour
{
    public static SceneController Instance { get; private set; }

    [Header("Scene Names")]
    [SerializeField] private string bootScene = "Boot";
    [SerializeField] private string abyssScene = "Abyss";
    [SerializeField] private string courtScene = "Court";

    private bool isLoading = false;

    private void Awake()
    {
        // 单例
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // =========================
    // 对外接口（只能走这里）
    // =========================

    public void LoadBoot()
    {
        LoadScene(bootScene);
    }

    public void LoadAbyss()
    {
        LoadScene(abyssScene);
    }

    public void LoadCourt()
    {
        LoadScene(courtScene);
    }

    // =========================
    // 核心加载逻辑
    // =========================

    private void LoadScene(string sceneName)
    {
        if (isLoading)
        {
            Debug.LogWarning("[SceneController] Scene is already loading.");
            return;
        }

        StartCoroutine(LoadSceneCoroutine(sceneName));
    }

    private IEnumerator LoadSceneCoroutine(string sceneName)
    {
        isLoading = true;

        Debug.Log($"[SceneController] Loading Scene: {sceneName}");

        // 这里将来可以接：
        // - 淡出动画
        // - Loading UI
        // - 音乐过渡

        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName);

        while (!operation.isDone)
        {
            // 以后可以把进度传给 UI
            yield return null;
        }

        Debug.Log($"[SceneController] Scene Loaded: {sceneName}");

        isLoading = false;
    }
}
