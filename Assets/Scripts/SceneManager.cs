using UnityEngine;
using UnityEngine.SceneManagement;
using System;
using System.Collections;
using NUnit.Framework;
using System.Collections.Generic;
using System.Threading.Tasks;

public class SceneManager : MonoBehaviour
{
    public static SceneManager Instance { get; private set; }


    public event Action<string> OnSceneLoaded = delegate { };
    public event Action<string> OnSceneUnloaded = delegate { };

    [Header("Scene Data")]
    public SceneDatabase sceneDatabase;
    public int maxActiveScenes = 5;

    private List<SceneReference> _activeScenes = new();

    private bool _isSceneLoaded = false;

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

    public async void LoadSceneAsync (SceneReference sceneRef)
    {
        if (!_isSceneLoaded)
        {
            AsyncOperation asyncOperation = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(sceneRef.SceneName, LoadSceneMode.Additive);
            while (!asyncOperation.isDone)
                await Task.Yield();

            OnSceneLoaded?.Invoke(sceneRef.SceneName);
            _isSceneLoaded = true;
            _activeScenes.Add(sceneRef);
        }
    }

    public async void UnloadSceneAsync (SceneReference sceneRef)
    {
        if (_isSceneLoaded)
        {
            AsyncOperation asyncOperation = UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync(sceneRef.SceneName);
            while (!asyncOperation.isDone)
                await Task.Yield(); 
            
            OnSceneUnloaded?.Invoke(sceneRef.SceneName);
            _isSceneLoaded = false;
            _activeScenes.Remove(sceneRef);
        }
    }
}
