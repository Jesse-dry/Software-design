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
    // 【核心修改】超时失败 → 失败特效 → 重载
    // ==========================================
    private IEnumerator FailureAndTransition()
    {
        // 1. 弹出警告提示 (GlitchFlash 闪烁效果)
        if (UIManager.Instance != null && UIManager.Instance.Toast != null)
        {
            UIManager.Instance.Toast.Show("破解超时！安保系统已锁定！", ToastStyle.GlitchFlash);
        }

        // 2. 增加混乱值惩罚
        if (ChaosManager.Instance != null)
        {
            ChaosManager.Instance.AddChaos(30, "破解终端超时");
        }

        // 3. 使用 FailEffectController 显示失败特效（抖动 + 重载场景）
        if (FailEffectController.Instance != null)
        {
            FailEffectController.Instance.ShowFailEffect();
            yield break; // FailEffectController 内部处理重载
        }

        // 降级：直接等待后强行进入法庭
        yield return new WaitForSeconds(2.0f);
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