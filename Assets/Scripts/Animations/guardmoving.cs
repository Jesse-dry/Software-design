using UnityEngine;

public class guardmoving : MonoBehaviour
{
    [Header("移动设置")]
    public float moveSpeed = 2.0f;    // 移动速度
    public float moveDistance = 5.0f; // 往返的单程距离

    private Vector3 startPosition;    // 起始位置
    private bool movingRight = true;  // 当前是否向右移动
    private SpriteRenderer spriteRenderer;

    void Start()
    {
        // 记录出生点
        startPosition = transform.position;
        spriteRenderer = GetComponent<SpriteRenderer>();
        Flip();
    }

    void Update()
    {
        // 1. 计算当前相对于起始点的位移
        float distanceMoved = transform.position.x - startPosition.x;

        // 2. 检查是否到达边界并转向
        if (movingRight && distanceMoved >= moveDistance)
        {
            movingRight = false;
            Flip();
        }
        else if (!movingRight && distanceMoved <= -moveDistance)
        {
            movingRight = true;
            Flip();
        }

        // 3. 执行移动
        float direction = movingRight ? 1 : -1;
        transform.Translate(Vector2.right * direction * moveSpeed * Time.deltaTime);
    }

    // 翻转人物朝向
    void Flip()
    {
        // 方式 A：直接翻转 SpriteRenderer 的镜像（简单推荐）
        if (spriteRenderer != null)
        {
            spriteRenderer.flipX = movingRight;
        }
        
        /* 方式 B：如果你有子物体，建议翻转 Scale
        Vector3 scaler = transform.localScale;
        scaler.x *= -1;
        transform.localScale = scaler;
        */
    }
}