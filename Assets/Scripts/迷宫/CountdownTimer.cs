using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections; // 用来支持延迟协程

public class CountdownTimer : MonoBehaviour
{
    public float totalTime = 60f;
    public TextMeshProUGUI timeText;
    public GameObject tryAgainScreen;
    public GameObject playerBall;

    private bool isRunning = true;
    private float initialTime;

    void Start()
    {
        initialTime = totalTime;
    }

    void Update()
    {
        if (isRunning && totalTime > 0)
        {
            totalTime -= Time.deltaTime;

            if (totalTime <= 0)
            {
                totalTime = 0;
                isRunning = false;

                // 【修改】隐藏小球，且不再显示 Try Again 屏幕（因为要强制转场了）
                if (tryAgainScreen != null) tryAgainScreen.SetActive(false);
                if (playerBall != null) playerBall.SetActive(false);

                // 开启失败转场协程
                StartCoroutine(FailureAndTransition());
            }

            if (timeText != null)
            {
                timeText.text = Mathf.CeilToInt(totalTime).ToString();
            }
        }
    }

    // ==========================================
    // 【核心新增】失败并强行带入法庭的逻辑
    // ==========================================
    private IEnumerator FailureAndTransition()
    {
        // 1. 弹出警告提示 (GlitchFlash 闪烁效果最适合这种警报)
        if (UIManager.Instance != null && UIManager.Instance.Toast != null)
        {
            UIManager.Instance.Toast.Show("破解超时！安保系统已锁定！", ToastStyle.GlitchFlash);
        }

        // 2. 增加混乱值惩罚
        if (ChaosManager.Instance != null)
        {
            ChaosManager.Instance.AddChaos(30, "破解终端超时");
        }

        // 3. 停顿 2 秒，让玩家看到红字警告和混乱值增加
        yield return new WaitForSeconds(2.0f);

        // 4. 强行拉入法庭阶段！
        if (GameManager.Instance != null)
        {
            GameManager.Instance.EnterPhase(GamePhase.Court);
        }
    }

    public void ResetTimer()
    {
        totalTime = initialTime;
        isRunning = true;
    }
}