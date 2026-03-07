using UnityEngine;

public class ItemFloatEffect : MonoBehaviour
{
    [Header("浮动配置")]
    [Tooltip("上下浮动的速度（越大越快）")]
    public float floatSpeed = 2.0f;

    [Tooltip("上下浮动的幅度（越大弹得越高）")]
    public float floatAmplitude = 0.15f;

    // 记录物品的初始位置
    private Vector3 _startPos;

    private void Start()
    {
        // 游戏开始时，记住它被摆放的位置
        _startPos = transform.position;
    }

    private void Update()
    {
        // 利用 Mathf.Sin (正弦波) 制作极其丝滑的上下呼吸浮动效果
        float newY = _startPos.y + Mathf.Sin(Time.time * floatSpeed) * floatAmplitude;

        // 更新位置（保持 X 和 Z 不变，只改变 Y）
        transform.position = new Vector3(_startPos.x, newY, _startPos.z);
    }
}