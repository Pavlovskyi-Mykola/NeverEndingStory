using UnityEngine;

/// <summary>
/// Quits the application. Hook Quit() to the Exit page's confirm button.
/// In the editor it stops play mode instead of closing.
/// Application.Quit raises OnApplicationQuit, so SaveLoadManager's quit-save
/// (if enabled and in gameplay) still runs.
/// </summary>
public sealed class QuitGame : MonoBehaviour
{
    public void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
