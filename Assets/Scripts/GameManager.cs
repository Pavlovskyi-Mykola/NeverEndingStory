using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public enum GameState { Playing, Paused, Cutscene }
    public GameState CurrentState { get; private set; }

    private void Awake()
    {
        if (Instance != null) Destroy(gameObject);
        else Instance = this;

        DontDestroyOnLoad(gameObject);
    }

    public void SetState(GameState state)
    {
        CurrentState = state;
        // Trigger UI/logic changes
    }
}
