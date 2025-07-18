using UnityEngine;
using UnityEngine.SceneManagement;
using System;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public event Action<SceneReference> OnSceneLoaded = delegate { };

    [Header("Scene Data")]
    public SceneDatabase sceneDatabase;

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

    }

    public void LoadScene (SceneReference sceneToLoad)
    {
        if (IsSceneLoaded(sceneToLoad.SceneName))
            return;

        SceneManager.LoadScene(sceneToLoad.SceneName);

        OnSceneLoaded?.Invoke(sceneToLoad);
    }

    private bool IsSceneLoaded(string sceneName)
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var loadedScene = SceneManager.GetSceneAt(i);
            if (loadedScene.name == sceneName)
                return true;
        }
        return false;
    }

}



