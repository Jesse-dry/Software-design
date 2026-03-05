using UnityEngine;

/// <summary>
/// 记忆场景透视帧背景。
/// 将玩家 Y 坐标映射到帧序列索引（0~frameCount-1），
/// 实现"景随人动"的透视推进效果。
/// 
/// 使用方式：
///   1. 在场景中创建一个带 SpriteRenderer 的 GameObject
///   2. 挂载此脚本
///   3. 将帧序列 Sprite 数组拖入 frames
///   4. 设置 playerTransform 指向玩家
///   5. 设置 minY / maxY 为玩家的移动范围
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class MemoryParallaxBackground : MonoBehaviour
{
    [Header("帧序列")]
    [Tooltip("按顺序排列的背景帧 Sprite 数组（0 = 最远/起始, N = 最近/终点）")]
    public Sprite[] frames;

    [Header("目标")]
    [Tooltip("跟踪的玩家 Transform")]
    public Transform playerTransform;

    [Header("Y 轴映射范围")]
    [Tooltip("玩家起始位置 Y（对应第 0 帧）")]
    public float minY = 0f;

    [Tooltip("玩家终点位置 Y（对应最后一帧）")]
    public float maxY = 20f;

    private SpriteRenderer sr;
    private int lastFrameIndex = -1;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
    }

    void LateUpdate()
    {
        if (playerTransform == null || frames == null || frames.Length == 0)
            return;

        // 将玩家 Y 坐标映射到 [0, 1] 范围
        float t = Mathf.InverseLerp(minY, maxY, playerTransform.position.y);
        t = Mathf.Clamp01(t);

        // 映射到帧索引
        int frameIndex = Mathf.Clamp(
            Mathf.FloorToInt(t * frames.Length),
            0,
            frames.Length - 1
        );

        // 仅在帧变化时更新 Sprite（性能优化）
        if (frameIndex != lastFrameIndex)
        {
            sr.sprite = frames[frameIndex];
            lastFrameIndex = frameIndex;
        }
    }

    /// <summary>
    /// 运行时动态设置帧序列（可供 MemorySceneSetup 调用）
    /// </summary>
    public void SetFrames(Sprite[] newFrames, Transform player, float yMin, float yMax)
    {
        frames = newFrames;
        playerTransform = player;
        minY = yMin;
        maxY = yMax;
        lastFrameIndex = -1;
    }
}
