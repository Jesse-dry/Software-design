using UnityEngine;
using System.Collections;

public class GameManager1 : MonoBehaviour
{
    // 在面板里拖入对应的物体
    public GameObject startButton;
    public GameObject tryAgainButton;
    public GameObject mazeEnvironment; // 迷宫大包
    public GameObject playerBall; // 跟着鼠标的小球

    void Start()
    {
        // 游戏刚打开时：只显示 Start 按钮
        startButton.SetActive(true);
        tryAgainButton.SetActive(false);
        mazeEnvironment.SetActive(false);
        playerBall.SetActive(false);
    }

    // 这个方法给按钮点击时使用
    public void StartTheGame()
    {
        startButton.SetActive(false);
        tryAgainButton.SetActive(false);
        
        mazeEnvironment.SetActive(true); // 显示所有墙壁和终点
        playerBall.SetActive(true); // 显示小球
    }

    // 这个方法给小球撞墙时使用
    public void GameOver()
    {
        mazeEnvironment.SetActive(false); // 隐藏所有墙壁和终点
        playerBall.SetActive(false); // 隐藏小球
        
        // 【修改】使用 FailEffectController 显示失败特效（抖动 + 重载）
        if (FailEffectController.Instance != null)
        {
            FailEffectController.Instance.ShowFailEffect();
        }
        else
        {
            // 降级：显示 Try Again 按钮
            tryAgainButton.SetActive(true);
        }
    }

    // 碰到终点时使用
    public void WinGame()
    {
        mazeEnvironment.SetActive(false);
        playerBall.SetActive(false);
        Debug.Log("恭喜通关！准备切入法庭！");

        // 开启胜利转场协程
        StartCoroutine(VictoryAndTransition());
    }

    private IEnumerator VictoryAndTransition()
    {
        // 弹出胜利提示
        if (UIManager.Instance?.Toast != null)
            UIManager.Instance.Toast.Show("破解成功！核心加密日志已下载", colorType: ToastColor.Positive);

        yield return new WaitForSeconds(2.0f);

        // 获得阿卡那牌 — 圣杯
        if (AkanaManager.Instance != null)
            AkanaManager.Instance.CollectCard(AkanaCardId.圣杯);

        // Toast 提示获得卡牌
        if (UIManager.Instance?.Toast != null)
            UIManager.Instance.Toast.Show("获得了【圣杯】阿卡那牌！", colorType: ToastColor.Positive);

        yield return new WaitForSeconds(1.0f);

        // 询问是否查看圣杯牌说明 → 关闭后进入法庭
        AkanaVictoryHelper.AskViewCard(
            AkanaCardId.圣杯,
            "恭喜获得【圣杯】阿卡那牌，是否查看牌面内容？",
            onFinished: () =>
            {
                Debug.Log("[DecodeGame] 迷宫通关，进入 Court！");
                if (GameManager.Instance != null)
                    GameManager.Instance.EnterPhase(GamePhase.Court);
            }
        );
    }
}