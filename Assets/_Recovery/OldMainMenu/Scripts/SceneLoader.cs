using UnityEngine;
using UnityEngine.SceneManagement; 

public class SceneLoader : MonoBehaviour
{
    [Tooltip("想要加载的新场景的名称")]
    public string sceneName = "CutsceneScene";

    // 绑定给开始按钮的方法
    public void LoadGameScene()
    {
        // 场景跳转代码
        SceneManager.LoadScene(sceneName);
    }
}