using UnityEngine;

public class MovingWall : MonoBehaviour
{
    public float speed = 2f;       // 移动速度
    public float distance = 3f;    // 移动的距离范围
    
    // 新增加的开关，用来决定斜线的方向
    public bool moveRightUp = true; // 勾选：左下到右上。不勾选：左上到右下。

    private Vector3 startPos; // 用来记住墙壁一开始的位置

    void Start()
    {
        startPos = transform.position;
    }

    void Update()
    {
        // 算出当前应该移动多少距离
        float offset = Mathf.Sin(Time.time * speed) * distance;

        if (moveRightUp)
        {
            // 撇向右上角：X轴和Y轴同时增加
            transform.position = startPos + new Vector3(offset, offset, 0);
        }
        else
        {
            // 捺向右下角：X轴增加，Y轴减少
            transform.position = startPos + new Vector3(offset, -offset, 0);
        }
    }
}