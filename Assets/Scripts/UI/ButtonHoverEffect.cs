using UnityEngine;
using TMPro;
using UnityEngine.EventSystems;

public class ButtonHoverEffect : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private TextMeshProUGUI btnText;
    private string originalText;

    void Start()
    {
        btnText = GetComponentInChildren<TextMeshProUGUI>();
        originalText = btnText.text;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        // ÐüÍ£Ê±Ìí¼ÓÐÞÊÎ·û£¬±ÈÈç [ > START < ]
        btnText.text = "[ " + originalText + " ]";
        btnText.color = Color.green;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        // »Ö¸´Ô­×´
        btnText.text = originalText;
        btnText.color = Color.white;
    }
}