using UnityEngine;
using UnityEngine.Playables; 
using UnityEngine.SceneManagement;

public class CutsceneController : MonoBehaviour
{
    [Header("过场动画设置")]
    [Tooltip("拖入带有 Playable Director 组件的物体")]
    public PlayableDirector director;

    [Tooltip("动画播放完毕后要加载的下一个场景名称")]
    public string nextSceneName = "GameplayScene";

    private void Awake()
    {
        // 如果没有手动赋值，尝试在自身获取该组件
        if (director == null)
        {
            director = GetComponent<PlayableDirector>();
        }
    }

    private void OnEnable()
    {
        // 订阅（监听）Timeline 播放结束的事件
        if (director != null)
        {
            director.stopped += OnPlayableDirectorStopped;
        }
    }

    private void OnDisable()
    {
        // 取消订阅，防止内存泄漏
        if (director != null)
        {
            director.stopped -= OnPlayableDirectorStopped;
        }
    }

    // 当 Timeline 停止时会自动调用此方法
    private void OnPlayableDirectorStopped(PlayableDirector aDirector)
    {
        // 确认是当前的 director 停止了（防止多个director干扰）
        if (director == aDirector)
        {
            Debug.Log("过场动画结束，准备跳转游戏关卡！");
            // 这里也可以替换成上面我们写的异步加载，为了简单起见直接Load
            SceneManager.LoadScene(nextSceneName);
        }
    }
}