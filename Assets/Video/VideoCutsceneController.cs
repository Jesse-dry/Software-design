using UnityEngine;
using UnityEngine.Video;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem; 

public class VideoCutsceneController : MonoBehaviour
{
    [Tooltip("拖入挂载了 Video Player 的物体")]
    public VideoPlayer videoPlayer;

    [Tooltip("视频播完后要跳转的下一个场景")]
    public string nextSceneName = "GameplayScene";

    // 状态锁，防止玩家疯狂按键导致场景被加载多次
    private bool isTransitioning = false;

    void Start()
    {
        if (videoPlayer == null)
        {
            videoPlayer = GetComponent<VideoPlayer>();
            

        }

        // 订阅视频播放结束事件
        if (videoPlayer != null)
        {
            videoPlayer.loopPointReached += OnVideoEnd;
        }
    }

    // 视频自然播放结束时触发
    void OnVideoEnd(VideoPlayer vp)
    {
        GoToNextScene();
    }

    void Update()
    {
        // 如果已经开始跳转，停止检测输入
        if (isTransitioning) return;

        // 1. 检测键盘是否有任意键按下
        bool keyboardPressed = Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame;

        // 2. 检测鼠标左键是否点击
        bool mouseClicked = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;

        // 如果满足任意一个条件，跳过动画
        if (keyboardPressed || mouseClicked)
        {
            Debug.Log("玩家跳过动画，切入主游戏...");
            GoToNextScene();
        }
    }

    private void GoToNextScene()
    {
        isTransitioning = true;
        SceneManager.LoadScene(nextSceneName);
    }

    void OnDestroy()
    {
        // 注销事件
        if (videoPlayer != null)
        {
            videoPlayer.loopPointReached -= OnVideoEnd;
        }
    }
}
