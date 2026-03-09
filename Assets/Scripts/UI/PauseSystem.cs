using UnityEngine;

public class PauseSystem : MonoBehaviour
{
    public static PauseSystem Instance { get; private set; }

    private PauseMenuUI _pauseUI;
    private bool _isPaused = false;

    public static PauseSystem Initialize(Transform parent)
    {
        if (Instance != null) return Instance;

        GameObject go = new GameObject("PauseSystem_Logic");
        go.transform.SetParent(parent, false);
        Instance = go.AddComponent<PauseSystem>();

        if (UIManager.Instance != null)
        {
            // 监听后续的场景切换
            UIManager.Instance.OnSceneRootRegistered += Instance.OnSceneLoaded;

            // 【生命周期防漏补丁】：如果当前场景已经注册过了，立刻手动加载一次！
            if (UIManager.Instance.HasSceneRoot && UIManager.Instance.overlayLayer != null)
            {
                Debug.Log("[PauseSystem] 检测到场景已存在，立即补发加载！");
                Instance.LoadUIPrefab(UIManager.Instance.overlayLayer);
            }
        }

        return Instance;
    }

    private void OnSceneLoaded(UISceneRoot root)
    {
        Debug.Log($"[PauseSystem] 监听到新场景加载: {(root != null ? root.name : "null")}");
        if (root != null && root.overlayLayer != null)
        {
            LoadUIPrefab(root.overlayLayer);
        }
    }

    private void LoadUIPrefab(Transform parentLayer)
    {
        if (_pauseUI != null) Destroy(_pauseUI.gameObject);

        GameObject prefab = Resources.Load<GameObject>("UI/PausePanel");
        if (prefab != null)
        {
            GameObject uiGo = Instantiate(prefab, parentLayer, false);

            RectTransform rect = uiGo.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                rect.localScale = Vector3.one;
                rect.anchoredPosition = Vector2.zero;
            }

            _pauseUI = uiGo.GetComponent<PauseMenuUI>();
            if (_pauseUI != null)
            {
                _pauseUI.Setup(ResumeGame);
                Debug.Log("[PauseSystem] 暂停菜单预制体加载成功并绑定！");
            }
        }
        else
        {
            Debug.LogError("[PauseSystem] 致命错误：找不到预制体 Resources/UI/PausePanel");
        }
    }

    private void Update()
    {
        bool escPressed = false;
        if (Input.GetKeyDown(KeyCode.Escape)) escPressed = true;

#if ENABLE_INPUT_SYSTEM
        else if (UnityEngine.InputSystem.Keyboard.current != null &&
                 UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            escPressed = true;
        }
#endif

        if (!escPressed) return;

        // ════════════════════════════════════════════════════════════
        //  MainMenu 场景：ESC → 退出游戏
        //  以「当前激活场景名」为准，避免 GamePhase 尚未切换到 MainMenu
        //  时（如 startPhase=Boot 且 EnterPhase(MainMenu) 被注释）误判。
        // ════════════════════════════════════════════════════════════
        bool isMainMenu =
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "MainMenu" ||
            (GameManager.Instance != null && GameManager.Instance.IsInMainMenu());

        if (isMainMenu)
        {
            Debug.Log("[PauseSystem] ESC → 主菜单场景，退出游戏");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
            return;
        }

        // ════════════════════════════════════════════════════════════
        //  其他场景：ESC → 切换游戏内主菜单（InGameMenuController）
        // ════════════════════════════════════════════════════════════
        if (InGameMenuController.Instance != null)
        {
            Debug.Log("[PauseSystem] ESC → 切换游戏内主菜单");
            InGameMenuController.Instance.ToggleMenu();
        }
        else
        {
            Debug.LogWarning("[PauseSystem] ESC 按下但 InGameMenuController 不可用");
        }
    }

    public void TogglePause()
    {
        if (_pauseUI == null)
        {
            Debug.LogError("[PauseSystem] 呼叫失败：_pauseUI 为空！菜单根本没生成！");
            return;
        }

        _isPaused = !_isPaused;
        Debug.Log($"[PauseSystem] 执行暂停切换，当前 _isPaused = {_isPaused}");

        if (_isPaused)
        {
            Time.timeScale = 0f;
            _pauseUI.Show();
        }
        else
        {
            ResumeGame();
        }
    }

    private void ResumeGame()
    {
        _isPaused = false;
        Time.timeScale = 1f;
        if (_pauseUI != null) _pauseUI.Hide();
    }

    private void OnDestroy()
    {
        if (UIManager.Instance != null)
        {
            UIManager.Instance.OnSceneRootRegistered -= OnSceneLoaded;
        }
    }
}