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

    // 💡 注意：这里删掉了原来的 public GameObject hPromptUI

    void Start()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        moveScript = GetComponent<playercontroller>();
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

                // 💡 藏进花瓶后，把这个花瓶的提示牌藏起来
                Transform ui = vaseTransform.Find("H_Prompt");
                if (ui != null) ui.gameObject.SetActive(false);
            }
            else
            {
                // —— 【出来，解除躲藏】 ——
                isHiding = false;
                spriteRenderer.sortingOrder = normalLayer; 
                if (moveScript != null) moveScript.enabled = true;

                // 💡 钻出来后，重新显示这个花瓶的提示牌
                Transform ui = vaseTransform.Find("H_Prompt");
                if (ui != null) ui.gameObject.SetActive(true);
            }
        }
    }

    // 当主角走入花瓶的感应区
    void OnTriggerEnter2D(Collider2D other)
    {
        // 如果碰到的确实是花瓶
        if (other.CompareTag("Vase")) 
        {
            isNearVase = true;
            vaseTransform = other.transform; 

            // 💡 关键：只在这个被碰到的花瓶身上，寻找名字叫 "H_Prompt" 的提示牌，并打开它
            Transform ui = other.transform.Find("H_Prompt");
            if (ui != null) ui.gameObject.SetActive(true);
        }
    }

    // 当主角离开花瓶的感应区
    void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Vase"))
        {
            // 💡 关键：离开时，把刚刚那个花瓶身上的提示牌关掉
            Transform ui = other.transform.Find("H_Prompt");
            if (ui != null) ui.gameObject.SetActive(false);

            isNearVase = false;
            vaseTransform = null;
        }
    }
}