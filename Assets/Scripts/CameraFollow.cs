using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [Header("跟随设置")]
    public Transform target;
    public float smoothSpeed = 5f;
    public Vector3 offset = new Vector3(0, 0, -10f);

    [Header("边界限制 (填入地图边缘的坐标)")]
    public bool useBounds = true;     // 边界开关，打勾就开启
    public Vector2 minBounds;         // 左下角极限位置 (最小的 X 和 Y)
    public Vector2 maxBounds;         // 右上角极限位置 (最大的 X 和 Y)

    void LateUpdate()
    {
        if (target != null)
        {
            // 1. 算出相机原本想去的位置
            Vector3 desiredPosition = target.position + offset;

            // 2. 【新增】如果开启了边界，就把相机死死卡在设定好的范围内
            if (useBounds)
            {
                // Mathf.Clamp 的作用就是：如果数字超出了设定的最大/最小值，就强制让它等于最大/最小值
                desiredPosition.x = Mathf.Clamp(desiredPosition.x, minBounds.x, maxBounds.x);
                desiredPosition.y = Mathf.Clamp(desiredPosition.y, minBounds.y, maxBounds.y);
            }
            
            // 3. 平滑移动
            Vector3 smoothedPosition = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime);
            transform.position = smoothedPosition;
        }
    }
}