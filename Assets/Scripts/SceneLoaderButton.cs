using UnityEngine;

public class SceneLoaderButton : MonoBehaviour
{
    public SceneReference targetScene;

    public void Load()
    {
        LevelManager.Instance.LoadScene(targetScene.SceneName);
    }
}
