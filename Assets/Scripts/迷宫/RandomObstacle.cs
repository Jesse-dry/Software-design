using UnityEngine;

public class RandomObstacle : MonoBehaviour
{
    public int difficulty = 1;      
    public float baseSpeed = 2f;    
    public float moveRadius = 3f;   

    private Vector2 startPos;       
    private Vector2 targetPos;      

    void Start()
    {
        startPos = transform.position; 
        GetNewTarget();                
    }

    void Update()
    {
        float currentSpeed = baseSpeed + (difficulty - 1);
        transform.position = Vector2.MoveTowards(transform.position, targetPos, currentSpeed * Time.deltaTime);

        if (Vector2.Distance(transform.position, targetPos) < 0.1f)
        {
            GetNewTarget(); 
        }
    }

    void GetNewTarget()
    {
        float randomX = startPos.x + Random.Range(-moveRadius, moveRadius);
        float randomY = startPos.y + Random.Range(-moveRadius, moveRadius);
        targetPos = new Vector2(randomX, randomY);
    }

    // --- 下面是新加的“透视眼镜”功能 ---
    // 这个特殊的方法专门用来在 Unity 画面里画辅助线
    void OnDrawGizmosSelected()
    {
        // 1. 准备一支黄色的画笔
        Gizmos.color = Color.yellow;
        
        // 确定要在哪里画圈（如果游戏没运行，就在当前位置画；如果运行了，就在它出生的起点画）
        Vector2 center = Application.isPlaying ? startPos : (Vector2)transform.position;
        
        // 画一个空心的圆圈，半径就是你的 moveRadius
        Gizmos.DrawWireSphere(center, moveRadius);

        // 2. 如果游戏正在运行，再用红色的笔，画一条从当前位置连向“目标点”的线
        if (Application.isPlaying)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position, targetPos);
        }
    }
}