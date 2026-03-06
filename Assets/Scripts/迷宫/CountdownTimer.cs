using UnityEngine;
using UnityEngine;
using UnityEngine.UI; 
using TMPro; // 1. 【核心修改】必须加这一行，才能识别新版文字

public class CountdownTimer : MonoBehaviour
{
    public float totalTime = 60f; 
    
    // 2. 【核心修改】将 Text 改为 TextMeshProUGUI
    public TextMeshProUGUI timeText; 

    public GameObject tryAgainScreen; 
    public GameObject playerBall;     

    private bool isRunning = true; 
    private float initialTime; // 用来记住你设定的初始时间

    void Start()
    {
        // 游戏开始时记下初始时间，方便以后重置
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
                
                if (tryAgainScreen != null)
                {
                    tryAgainScreen.SetActive(true);
                }
                
                if (playerBall != null)
                {
                    playerBall.SetActive(false);
                }
            }

            // 更新文字显示
            if (timeText != null)
            {
                timeText.text =  Mathf.CeilToInt(totalTime).ToString();
            }
        }
    }

    // --- 额外赠送：重置倒计时的功能 ---
    // 你可以把这个方法绑定在 Try Again 按钮上
    public void ResetTimer()
    {
        totalTime = initialTime;
        isRunning = true;
    }
}