using UnityEngine;
using UnityEngine.Video;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem; 

/// <summary>
/// 【已废弃】旧版视频过场控制器。
/// 场景跳转逻辑已迁移到 CutsceneController（协程驱动）。
/// 保留此文件避免场景序列化引用丢失，但所有运行时逻辑已禁用。
/// </summary>
public class VideoCutsceneController : MonoBehaviour
{
    [Tooltip("拖入挂载了 Video Player 的物体")]
    public VideoPlayer videoPlayer;

    [Tooltip("视频播完后要跳转的下一个场景")]
    public string nextSceneName = "GameplayScene";

    void Start()
    {
        // 【已废弃】禁用自身，防止任何旧逻辑干扰新的 CutsceneController
        Debug.Log("[VideoCutsceneController] 已废弃，自动禁用。场景流程由 CutsceneController 控制。");
        enabled = false;
    }
}
