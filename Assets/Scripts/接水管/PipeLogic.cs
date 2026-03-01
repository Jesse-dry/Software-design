using UnityEngine;

public class PipeLogic : MonoBehaviour
{
    [Header("特殊属性")]
    public bool isStartPoint = false; // 勾选后，它是起点
    public bool isEndPoint = false;   // 【新增】勾选后，它是终点！
    public bool canRotate = true;     // 是否能被点击旋转

    [Header("开口设置 (初始状态)")]
    public bool up;
    public bool down;
    public bool left;
    public bool right;

    [HideInInspector] // 这个变量还在，但不需要在 Unity 面板里显示了
    public bool isFilled = false; 

    private float targetRotation;
    private float rotationSpeed = 10f;

    void Start()
    {
        targetRotation = transform.eulerAngles.z;
    }

    void OnMouseDown()
    {
        if (!canRotate) return; // 不能旋转的物体点它没反应

        targetRotation += 90f;

        // 逻辑旋转
        bool tempUp = up;
        up = right;
        right = down;
        down = left;
        left = tempUp;

        if (LevelJudge.Instance != null)
        {
            LevelJudge.Instance.CheckPath();
        }
    }

    void Update()
    {
        // 只有平滑旋转的动画，去掉了变色的代码
        float currentRotation = Mathf.MoveTowardsAngle(transform.eulerAngles.z, targetRotation, rotationSpeed);
        transform.rotation = Quaternion.Euler(0, 0, currentRotation);
    }
}