using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    public GameState CurrentState { get; private set; }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        ChangeState(GameState.Exploration);
    }

    public void ChangeState(GameState newState)
    {
        CurrentState = newState;
        Debug.Log("Game State -> " + newState);

        if (SystemManager.Instance != null)
        {
            SystemManager.Instance.OnGameStateChanged(newState);
        }
    }
}
