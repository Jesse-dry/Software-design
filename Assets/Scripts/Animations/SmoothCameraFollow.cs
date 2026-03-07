using UnityEngine;

public class SmoothCameraFollow : MonoBehaviour
{
    [Header("跟随目标")]
    public Transform target; // 把你的玩家主角拖到这里

    [Header("跟随配置")]
    public float smoothSpeed = 5f; // 数值越大跟得越紧
    public Vector3 offset = new Vector3(0, 0, -10f); // 2D 相机 Z 轴必须是负数！

    private void LateUpdate() // 必须在 LateUpdate 里执行，防止画面抖动
    {
        if (target == null) return;

        // 计算目标位置
        Vector3 desiredPosition = target.position + offset;

        // 使用 Lerp（线性插值）实现丝滑的跟随效果
        Vector3 smoothedPosition = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime);

        // 更新相机位置
        transform.position = smoothedPosition;
    }
}
