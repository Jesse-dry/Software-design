using UnityEngine;

public class InteractionPromptUI : MonoBehaviour
{
    [SerializeField] private GameObject promptRoot;

    void Awake()
    {
        Hide();
    }

    public void Show()
    {
        if (promptRoot == null) return;
        promptRoot.SetActive(true);
    }

    public void Hide()
    {
        if (promptRoot == null) return;
        promptRoot.SetActive(false);
    }
}
