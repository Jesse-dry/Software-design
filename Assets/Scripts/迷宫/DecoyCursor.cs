using UnityEngine;

public class DecoyCursor : MonoBehaviour
{
    // --- 新增：总开关 ---
    public bool enableInterference = true; // 勾选：开启干扰；取消勾选：关闭干扰

    public int decoyCount = 2;         
    public float offsetRadius = 0.5f;  
    public float lagSpeed = 15f;       
    public float decoyAlpha = 0.6f;    
    public float decoySize = 0.8f;     
    
    // --- 新增：强制乱动计时器 ---
    public float changeTargetTime = 0.2f; // 每隔 0.2 秒强制换一次乱动方向

    private Sprite shadowSprite;
    private GameObject[] decoys;
    private Vector2[] randomTargets;
    private float timer; // 我们内部使用的小秒表

    void Start()
    {
        shadowSprite = GetComponent<SpriteRenderer>().sprite;
        decoys = new GameObject[decoyCount];
        randomTargets = new Vector2[decoyCount]; 

        for (int i = 0; i < decoyCount; i++)
        {
            decoys[i] = new GameObject("DecoyShadow_" + i);
            
            // 1. 【修复】：让影子认小球为父物体。这样游戏没开始时，它就会跟着小球一起乖乖隐藏
            decoys[i].transform.SetParent(this.transform);
            
            SpriteRenderer sr = decoys[i].AddComponent<SpriteRenderer>();
            sr.sprite = shadowSprite;
            sr.color = new Color(1f, 1f, 1f, decoyAlpha); 
            sr.sortingOrder = -1; 
            
            // 2. 【核心修复】：因为已经是子物体了，直接填 decoySize 即可，Unity 会自动帮你按比例算好！
            decoys[i].transform.localScale = new Vector3(decoySize, decoySize, 1f);

            randomTargets[i] = Random.insideUnitCircle * offsetRadius;
        }
        
        timer = changeTargetTime; // 游戏开始时给秒表上满发条
    }

    void Update()
    {
        // 1. 根据开关，决定要不要显示影子
        for (int i = 0; i < decoyCount; i++)
        {
            if (decoys[i] != null)
            {
                decoys[i].SetActive(enableInterference);
            }
        }

        // 2. 如果开关关了，后面的代码就不运行了，直接休息
        if (!enableInterference) return;

        Vector2 realPos = transform.position;

        // 3. 倒计时逻辑：不管鼠标怎么动，时间一到必须换方向
        timer -= Time.deltaTime;
        if (timer <= 0f)
        {
            for (int i = 0; i < decoyCount; i++)
            {
                // 强制分配新的随机目标点
                randomTargets[i] = Random.insideUnitCircle * offsetRadius;
            }
            timer = changeTargetTime; // 重新定闹钟
        }

        // 4. 让影子永远都在追赶最新的目标
        for (int i = 0; i < decoyCount; i++)
        {
            Vector2 targetPos = realPos + randomTargets[i];
            decoys[i].transform.position = Vector3.Lerp(decoys[i].transform.position, targetPos, lagSpeed * Time.deltaTime);
        }
    }

    void OnDrawGizmosSelected()
    {
        // 如果关了干扰，连青色的辅助线也一起藏起来
        if (!enableInterference) return; 

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, offsetRadius);

        if (Application.isPlaying && decoys != null)
        {
            Gizmos.color = new Color(0, 1, 1, 0.3f);
            for (int i = 0; i < decoyCount; i++)
            {
                if (decoys[i] != null && decoys[i].activeSelf)
                {
                    Gizmos.DrawLine(transform.position, decoys[i].transform.position);
                }
            }
        }
    }
}