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
        
        tryAgainButton.SetActive(true); // 显示 Try Again 按钮
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
        // 弹出胜利提示复用 Toast 系统
        if (UIManager.Instance != null && UIManager.Instance.Toast != null)
        {
            // 绿色正面反馈字
            UIManager.Instance.Toast.Show("破解成功！核心加密日志已下载", colorType: ToastColor.Positive);
        }

        // 停顿 2 秒
        yield return new WaitForSeconds(2.0f);

        //  获得重要证据（请根据你们背包/数据系统的实际代码调整！）
        // 假设你们的数据管家叫 DataManager，大概是这样写：
        // if (DataManager.Instance != null) { DataManager.Instance.AddEvidence("核心加密日志"); }

        //大管家切入法庭阶段！
        if (GameManager.Instance != null)
        {
            GameManager.Instance.EnterPhase(GamePhase.Court);
        }
    }
}