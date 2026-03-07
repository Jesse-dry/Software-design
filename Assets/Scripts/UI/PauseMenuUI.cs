using UnityEngine;
using UnityEngine.UI;
using System;
using DG.Tweening; // 

[RequireComponent(typeof(CanvasGroup))]
public class PauseMenuUI : MonoBehaviour
{
    [Header("按钮引用")]
    public Button resumeButton;
    public Button mainMenuButton;
    public Button quitButton;

    [Header("动画配置")]
    public float fadeDuration = 0.2f;

    private CanvasGroup _canvasGroup;
    private Action _onResumeCallback;

    private void Awake()
    {
        _canvasGroup = GetComponent<CanvasGroup>();

        // 绑定按钮事件
        if (resumeButton != null) resumeButton.onClick.AddListener(OnResumeClicked);
        if (mainMenuButton != null) mainMenuButton.onClick.AddListener(OnMainMenuClicked);
        if (quitButton != null) quitButton.onClick.AddListener(OnQuitClicked);
    }

    // 由系统注入，同时负责安全的初始化
    public void Setup(Action onResume)
    {
        _onResumeCallback = onResume;

        // 在这里进行初始化最安全，绝不放在 Awake 里
        _canvasGroup.alpha = 0f;
        _canvasGroup.interactable = false;
        _canvasGroup.blocksRaycasts = false;
        gameObject.SetActive(false);
    }

    public void Show()
    {
        gameObject.SetActive(true);
        // DOTween 的 SetUpdate(true) 可以完美无视 Time.timeScale = 0
        _canvasGroup.DOFade(1f, fadeDuration).SetUpdate(true).OnComplete(() =>
        {
            _canvasGroup.interactable = true;
            _canvasGroup.blocksRaycasts = true;
        });
    }

    public void Hide()
    {
        _canvasGroup.interactable = false;
        _canvasGroup.blocksRaycasts = false;
        _canvasGroup.DOFade(0f, fadeDuration).SetUpdate(true).OnComplete(() =>
        {
            gameObject.SetActive(false);
        });
    }

    private void OnResumeClicked() => _onResumeCallback?.Invoke();

    private void OnMainMenuClicked()
    {
        Time.timeScale = 1f;
        if (GameManager.Instance != null) GameManager.Instance.EnterPhase(GamePhase.MainMenu);
        Hide();
    }

    private void OnQuitClicked()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}