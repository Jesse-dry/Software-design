using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI; // 用于控制渐变 UI

public class AbyssPortal : MonoBehaviour
{
    public static AbyssPortal Instance; // 单例模式，方便碎片直接找到它

    [Header("过关条件")]
    public int requiredFragments = 2; // 需要收集几个碎片才能传送
    private int currentFragments = 0;

    [Header("潜渊过渡设置")]
    public string nextSceneName = "Abyss"; // 下一个剪影场景的名字
    public Image fadeImage; // 拖入一个覆盖全屏的黑色/深蓝色 UI Image
    public float fadeDuration = 2f; // 潜渊渐变的时长（秒）

    private bool isSubmerging = false;

    private void Awake()
    {
        Instance = this;
    }

    // 碎片被吃掉时调用这个方法
    public void CollectFragment()
    {
        currentFragments++;
        Debug.Log($"当前进度: {currentFragments} / {requiredFragments}");

        if (currentFragments >= requiredFragments)
        {
            Debug.Log("记忆碎片收集完毕，潜渊传送门已开启！");
            // 这里可以写代码让传送门发光，或者播放开启音效
        }
    }

    // 玩家碰到传送点
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player") && !isSubmerging)
        {
            if (currentFragments >= requiredFragments)
            {
                // 满足条件，开始潜渊！
                StartCoroutine(SubmergeRoutine());
            }
            else
            {
                Debug.Log("记忆碎片不足，传送门未激活。");
            }
        }
    }

    // 潜渊渐变协程
    private IEnumerator SubmergeRoutine()
    {
        isSubmerging = true;

        // 确保淡入UI是激活的
        if (fadeImage != null)
        {
            fadeImage.gameObject.SetActive(true);
            Color imgColor = fadeImage.color;
            float timer = 0f;

            // 逐渐把 Alpha（透明度）从 0 变成 1，实现屏幕变黑
            while (timer < fadeDuration)
            {
                timer += Time.deltaTime;
                imgColor.a = Mathf.Lerp(0f, 1f, timer / fadeDuration);
                fadeImage.color = imgColor;
                yield return null;
            }
        }

        // 渐变黑屏结束后，加载剪影场景
        SceneManager.LoadScene(nextSceneName);
    }
}