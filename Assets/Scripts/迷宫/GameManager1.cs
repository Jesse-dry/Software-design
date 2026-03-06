using UnityEngine;

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
        Debug.Log("恭喜通关！"); // 这里可以以后换成 Win 按钮
    }
}