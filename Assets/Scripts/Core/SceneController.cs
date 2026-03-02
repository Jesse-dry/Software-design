using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class SceneController : MonoBehaviour
{
    public static SceneController Instance { get; private set; }

    [Header("Scene Names")]
    [SerializeField] private string bootScene  = "Boot";
    [SerializeField] private string abyssScene = "Abyss";
    [SerializeField] private string courtScene = "Court";

    private bool isLoading = false;

    // =========================
    // 配置注入（由 GameBootstrapper 调用）
    // =========================

    /// <summary>
    /// 由 GameBootstrapper 在 Start() 之前注入场景名配置，
    /// 优先级高于 Inspector 里的 SerializeField 默认值。
    /// </summary>
    public void InjectConfig(string boot, string abyss, string court)
    {
        if (!string.IsNullOrEmpty(boot))  bootScene  = boot;
        if (!string.IsNullOrEmpty(abyss)) abyssScene = abyss;
        if (!string.IsNullOrEmpty(court)) courtScene = court;
    }

    private void Awake()
    {
        // ����
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // =========================
    // ����ӿڣ�ֻ�������
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
    // ���ļ����߼�
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

        // ���ｫ�����Խӣ�
        // - ��������
        // - Loading UI
        // - ���ֹ���

        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName);

        while (!operation.isDone)
        {
            // �Ժ���԰ѽ��ȴ��� UI
            yield return null;
        }

        Debug.Log($"[SceneController] Scene Loaded: {sceneName}");

        isLoading = false;
    }
}
