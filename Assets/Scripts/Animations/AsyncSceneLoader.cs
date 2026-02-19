using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI; // 用于 Slider 进度条
using TMPro; // 用于 TextMeshPro 文本

public class AsyncSceneLoader : MonoBehaviour
{
    [Header("加载界面 UI")]
    public GameObject loadingPanel; // 包含进度条的加载背景面板
    public Slider progressBar;      // 进度条组件
    public TextMeshProUGUI progressText; // 进度百分比文本

    // 绑定到“开始”按钮上的方法
    public void LoadLevel(string sceneName)
    {
        // 显示加载界面
        if (loadingPanel != null) loadingPanel.SetActive(true);

        // 开启协程进行异步加载
        StartCoroutine(LoadSceneCoroutine(sceneName));
    }

    private IEnumerator LoadSceneCoroutine(string sceneName)
    {
        // 开启异步加载
        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName);

        // 只要没有加载完成，就一直循环
        while (!operation.isDone)
        {
            // Unity的加载进度 progress 最大只会到 0.9（剩下的0.1是场景激活阶段）
            // 我们用除以0.9的方式，把0~0.9映射到0~1，方便UI显示
            float progress = Mathf.Clamp01(operation.progress / 0.9f);

            // 更新 UI
            if (progressBar != null) progressBar.value = progress;
            if (progressText != null) progressText.text = (progress * 100f).ToString("F0") + "%";

            // 等待下一帧继续检测
            yield return null;
        }
    }
}
