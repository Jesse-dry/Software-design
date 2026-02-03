using UnityEngine;

public class SystemManager : MonoBehaviour
{
    public static SystemManager Instance;

    [Header("Systems")]
    public ExplorationSystem explorationSystem;
    public CourtSystem courtSystem;
    public NarrativeSystem narrativeSystem;
    public UISystem uiSystem;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void OnGameStateChanged(GameState state)
    {
        if (explorationSystem != null)
            explorationSystem.OnEnter(state);

        if (courtSystem != null)
            courtSystem.OnEnter(state);

        if (narrativeSystem != null)
            narrativeSystem.OnEnter(state);

        if (uiSystem != null)
            uiSystem.OnEnter(state);
    }
}
