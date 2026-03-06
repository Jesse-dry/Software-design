using UnityEngine;

public class MovingWall1 : MonoBehaviour
{
    public bool moveHorizontal = false; 
    
    // --- 新增：起止方向（反向移动）开关 ---
    public bool reverseDirection = false; // 勾选：先向左/下； 不勾选：先向右/上
    
    public float speed = 2f;            
    public float distance = 3f;         

    private Vector3 startPos;           

    void Start()
    {
        startPos = transform.position;
    }

    void Update()
    {
        float offset = Mathf.Sin(Time.time * speed) * distance;

        // 核心魔法：如果勾选了反向，就把算出来的偏移量直接倒过来
        if (reverseDirection)
        {
            offset = -offset;
        }

        if (moveHorizontal)
        {
            transform.position = startPos + new Vector3(offset, 0, 0);
        }
        else
        {
            transform.position = startPos + new Vector3(0, offset, 0);
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Vector3 centerPos = Application.isPlaying ? startPos : transform.position;

        Vector3 point1;
        Vector3 point2;

        if (moveHorizontal)
        {
            point1 = centerPos + new Vector3(distance, 0, 0);
            point2 = centerPos + new Vector3(-distance, 0, 0);
        }
        else
        {
            point1 = centerPos + new Vector3(0, distance, 0);
            point2 = centerPos + new Vector3(0, -distance, 0);
        }

        // 画出绿色的移动轨道
        Gizmos.DrawLine(point1, point2);
        Gizmos.DrawWireSphere(point1, 0.1f);
        Gizmos.DrawWireSphere(point2, 0.1f);

        // --- 新增辅助线：用红色实心球标出最开始移动的终点方向 ---
        Gizmos.color = Color.red;
        Vector3 firstTargetPos;
        
        if (moveHorizontal)
        {
            // 如果是水平，反转就是向左(-distance)，不反转就是向右(distance)
            firstTargetPos = centerPos + new Vector3(reverseDirection ? -distance : distance, 0, 0);
        }
        else
        {
            // 如果是垂直，反转就是向下(-distance)，不反转就是向上(distance)
            firstTargetPos = centerPos + new Vector3(0, reverseDirection ? -distance : distance, 0);
        }
        
        // 画出这个醒目的红色实心球
        Gizmos.DrawSphere(firstTargetPos, 0.1f);
    }
}