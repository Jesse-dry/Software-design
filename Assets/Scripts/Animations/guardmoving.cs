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
// 翻转人物朝向（连带手电筒一起）
    void Flip()
    {
        Vector3 scaler = transform.localScale;
        
        // 1. 记住你原本设置的真实大小（比如你放大了 3 倍，就记住 3），防止变形
        float originalSize = Mathf.Abs(scaler.x);
        
        // 2. 结合你朝左的原图来翻转
        if (movingRight)
        {
            // 向右走时：需要镜像翻转（变成负数），让手电筒和人一起朝右
            scaler.x = -originalSize; 
        }
        else
        {
            // 向左走时：保持原图状态（变成正数），让手电筒和人一起朝左
            scaler.x = originalSize;
        }
        
        transform.localScale = scaler;
    }
}