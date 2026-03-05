using UnityEngine;

public class LightDetection : MonoBehaviour
{
    private AudioSource audioSource; // 喇叭
    private bool hasFailed = false;  // 【新增】防刷屏开关：是否已经触发过失败了？

    void Start()
    {
        audioSource = GetComponent<AudioSource>();
    }

    // 1. 刚跨入光束的一瞬间检查
    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player")) CheckAndFail(other.gameObject);
    }

    // 2. 【新增】一直待在光束里时，持续不断地检查（专门抓从花瓶里突然冒出来的人！）
    void OnTriggerStay2D(Collider2D other)
    {
        if (other.CompareTag("Player")) CheckAndFail(other.gameObject);
    }

    // 3. 刚撞到保安身体的一瞬间检查
    void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Player")) CheckAndFail(collision.gameObject);
    }

    // 4. 【新增】一直贴着保安身体时，持续检查
    void OnCollisionStay2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Player")) CheckAndFail(collision.gameObject);
    }

    // ============ 【通用的失败惩罚逻辑】 ============
    void CheckAndFail(GameObject playerObj)
    {
        // 如果已经失败定格了，就不再重复执行下面的代码，防止声音重叠
        if (hasFailed) return; 

        PlayerHide hideScript = playerObj.GetComponent<PlayerHide>();
        
        // 如果主角正躲在花瓶后面，当做无事发生，直接结束
        if (hideScript != null && hideScript.isHiding)
        {
            return; 
        }

        // ====== 发现主角没有躲藏！立刻执行抓捕！ ======

        hasFailed = true; // 把开关关上，确认已经抓捕

        // 1. 声音处理
        if (audioSource != null) audioSource.Play();
        GameObject mainCamera = GameObject.FindGameObjectWithTag("MainCamera");
        if (mainCamera != null)
        {
            AudioSource bgmSource = mainCamera.GetComponent<AudioSource>();
            if (bgmSource != null) bgmSource.Stop(); 
        }

        // 2. 定住主角
        playercontroller playerMove = playerObj.GetComponent<playercontroller>();
        if (playerMove != null) playerMove.enabled = false; 
        Animator playerAnim = playerObj.GetComponent<Animator>();
        if (playerAnim != null) playerAnim.enabled = false;

        // 3. 定住所有的保安
        guardmoving[] allGuards = FindObjectsOfType<guardmoving>();
        foreach (guardmoving guard in allGuards)
        {
            guard.enabled = false;
            Animator guardAnim = guard.GetComponent<Animator>();
            if (guardAnim != null) guardAnim.enabled = false;
        }

        // 4. 关掉所有监控闪烁
        CameraBlink[] allCameras = FindObjectsOfType<CameraBlink>();
        foreach (CameraBlink cam in allCameras)
        {
            cam.enabled = false;
        }

        Debug.Log("被抓到了！全场冻结！");
    }
}