using UnityEngine;
using TMPro;

public class MemoryUIManager : MonoBehaviour
{
    public static MemoryUIManager Instance;

    [Header("UI")]
    public GameObject memoryPanel;
    public TMPro.TextMeshProUGUI memoryText;

    private PlayerMovement playerMovement;

    void Awake()
    {
        Instance = this;
        playerMovement = Object.FindAnyObjectByType<PlayerMovement>();
        memoryPanel.SetActive(false);
    }

    public void ShowMemory(MemoryFragmentNode node)
    {
        playerMovement.Freeze();
        memoryText.text = node.memoryContent;
        memoryPanel.SetActive(true);
    }

    public void CloseMemory()
    {
        memoryPanel.SetActive(false);
        playerMovement.Unfreeze();
    }
}