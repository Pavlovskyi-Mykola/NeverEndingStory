using UnityEngine;

public class SceneLoaderButton : MonoBehaviour
{
    [SerializeField] private SceneType sceneToLoad;

    public void LoadSceneAsync()
    {
        var sceneDatabase = SceneManager.Instance.sceneDatabase;
        SceneReference sceneRef = sceneDatabase.GetScene(sceneToLoad);
        SceneManager.Instance.LoadSceneAsync(sceneRef);
    }
}
