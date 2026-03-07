using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class ChaosHUDAdapter : MonoBehaviour
{
    [Header("配置")]
    [Tooltip("在 HUDSystem 中注册的键值，必须对应代码调用")]
    public string barKey = "chaos";

    private Image fillImage;
    private int lastChaosValue = -1;

    private void Start()
    {
        fillImage = GetComponent<Image>();

        // 1. 将当前的 Image 注册到 HUD 系统中
        UIManager.Instance.HUD.RegisterValueBar(barKey, fillImage);

        // 2. 初始同步一下当前混乱值
        if (ChaosManager.Instance != null)
        {
            lastChaosValue = ChaosManager.Instance.CurrentChaos;
            UpdateHUD(lastChaosValue, ChaosManager.Instance.MaxChaos, null);
        }
    }
   
    private void OnEnable()
    {
        if (ChaosManager.Instance != null)
            ChaosManager.Instance.OnChaosChanged += OnChaosDataChanged;
    }

    private void OnDisable()
    {
        if (ChaosManager.Instance != null)
            ChaosManager.Instance.OnChaosChanged -= OnChaosDataChanged;
    }


    private void OnChaosDataChanged(int currentValue, int maxValue)
    {
        int delta = currentValue - lastChaosValue;
        string deltaText = "";

        // 生成飘字文本
        if (delta > 0) deltaText = $"+{delta} 混乱";
        else if (delta < 0) deltaText = $"{delta} 混乱"; // 负数自带负号

        UpdateHUD(currentValue, maxValue, deltaText);
        lastChaosValue = currentValue;
    }

    private void UpdateHUD(int currentValue, int maxValue, string deltaText)
    {
        float normalized = (float)currentValue / maxValue;
        // 调用 API
        UIManager.Instance.HUD.SetValue(barKey, normalized, deltaText);
    }
}