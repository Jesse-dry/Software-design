using UnityEngine;

public class PlayerHide : MonoBehaviour
{
    [Header("图层设置")]
    public int normalLayer = 5;  
    public int hideLayer = -1;   

    private bool isNearVase = false; 
    public bool isHiding = false;   
    private Transform vaseTransform; 

    private SpriteRenderer spriteRenderer;
    private playercontroller moveScript; 
    
    // 💡 1. 声明动画组件
    private Animator animator;

    void Start()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        moveScript = GetComponent<playercontroller>();
        
        // 💡 2. 获取动画组件
        animator = GetComponent<Animator>();
        
        spriteRenderer.sortingOrder = normalLayer;
    }

    void Update()
    {
        if (isNearVase && Input.GetKeyDown(KeyCode.H))
        {
            if (isHiding == false)
            {
                // —— 【开始躲藏】 ——
                isHiding = true;
                spriteRenderer.sortingOrder = hideLayer; 
                transform.position = new Vector3(vaseTransform.position.x, transform.position.y, transform.position.z);
                
                if (moveScript != null) moveScript.enabled = false;

                // 💡 3. 关键修改：躲藏时强制停止走路动画，切换为站立状态
                if (animator != null) animator.SetBool("isWalking", false);

                // 藏进花瓶后，把这个花瓶的提示牌藏起来
                Transform ui = vaseTransform.Find("H_Prompt");
                if (ui != null) ui.gameObject.SetActive(false);
            }
            else
            {
                // —— 【出来，解除躲藏】 ——
                isHiding = false;
                spriteRenderer.sortingOrder = normalLayer; 
                if (moveScript != null) moveScript.enabled = true;

                // 钻出来后，重新显示这个花瓶的提示牌
                Transform ui = vaseTransform.Find("H_Prompt");
                if (ui != null) ui.gameObject.SetActive(true);
            }
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Vase")) 
        {
            isNearVase = true;
            vaseTransform = other.transform; 

            Transform ui = other.transform.Find("H_Prompt");
            if (ui != null) ui.gameObject.SetActive(true);
        }
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Vase"))
        {
            Transform ui = other.transform.Find("H_Prompt");
            if (ui != null) ui.gameObject.SetActive(false);

            isNearVase = false;
            vaseTransform = null;
        }
    }
}