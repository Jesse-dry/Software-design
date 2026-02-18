using UnityEngine;
using TMPro;
using UnityEngine.EventSystems;

public class TitleInteraction : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private TextMeshProUGUI titleText;
    public Color highlightColor = Color.green;
    private Color originalColor;

    void Start()
    {
        titleText = GetComponent<TextMeshProUGUI>();
        originalColor = titleText.color;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        // 悬停时触发快速闪烁或变色
        titleText.color = highlightColor;
        // 可以在这里触发一个 Glitch 音效
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        titleText.color = originalColor;
    }
}