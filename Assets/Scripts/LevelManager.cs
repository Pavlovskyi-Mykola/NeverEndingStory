using UnityEngine;
using UnityEngine.SceneManagement;
using System;
using System.Collections;

public class LevelManager : MonoBehaviour
{
    public static LevelManager Instance { get; private set; }


    public event Action<string> OnSceneLoaded = delegate { };
    public event Action<string> OnSceneUnloaded = delegate { };

    [Header("Scene Data")]
    public SceneDatabase sceneDatabase;

    private bool _isLoaded = false;

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

    //maybe i'll need this for later on
    //private bool _shouldLoad = false;
    public void LoadScene (string sceneName)
    {
        if (!_isLoaded)
        {
            SceneManager.LoadSceneAsync (sceneName, LoadSceneMode.Additive);
            OnSceneLoaded?.Invoke(sceneName);
            _isLoaded = true;
        }
    }

    public void UnloadScene (string sceneName)
    {
        if (_isLoaded)
        {
            SceneManager.UnloadSceneAsync(sceneName);
            OnSceneUnloaded?.Invoke(sceneName);
            _isLoaded = false;
        }
    }
}
