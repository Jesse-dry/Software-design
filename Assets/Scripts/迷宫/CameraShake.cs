using UnityEngine;

public class CameraShake : MonoBehaviour
{
    public bool enableShake = true;       
    public float shakeMagnitude = 0.2f;   
    public float shakeFrequency = 20f;    
    
    // --- 新增：把小球当成判断游戏是否进行的“信号灯” ---
    public GameObject playerBall; 

    private Vector3 originalPos;          

    void Start()
    {
        originalPos = transform.position;
    }

    void Update()
    {
        // 核心逻辑：检查小球是否存在，且当前是否显示在画面中
        bool isGamePlaying = false;
        if (playerBall != null && playerBall.activeInHierarchy)
        {
            isGamePlaying = true;
        }

        // 只有当“总开关开启” 且 “游戏正在进行” 时，才制造地震
        if (enableShake && isGamePlaying)
        {
            float shakeX = (Mathf.PerlinNoise(Time.time * shakeFrequency, 0f) * 2f - 1f) * shakeMagnitude;
            float shakeY = (Mathf.PerlinNoise(0f, Time.time * shakeFrequency) * 2f - 1f) * shakeMagnitude;
            transform.position = originalPos + new Vector3(shakeX, shakeY, 0f);
        }
        // 否则（停在 Start 界面，或者撞墙失败了），让相机平稳地滑回原位
        else if (transform.position != originalPos)
        {
            transform.position = Vector3.MoveTowards(transform.position, originalPos, 2f * Time.deltaTime);
        }
    }

    void OnDrawGizmosSelected()
    {
        if (!enableShake) return;

        Gizmos.color = Color.red;
        Vector3 centerPos = Application.isPlaying ? originalPos : transform.position;
        Vector3 boxSize = new Vector3(shakeMagnitude * 2f, shakeMagnitude * 2f, 0f);
        Gizmos.DrawWireCube(centerPos, boxSize);
    }
}