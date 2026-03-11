using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public bool IsInDialogue { get; private set; }

    [Header("Database")]
    [SerializeField] private SceneDatabase sceneDatabase;

    [Header("Startup")]
    [SerializeField] private bool loadUIOnStart = true;
    [SerializeField] private bool loadMainMenuOnStart = true;

    // ✅ Current location as reference (single source of truth)
    public SceneReference CurrentLocationRef { get; private set; }

    // ✅ Convenience: scene name (read-only)
    public string CurrentLocation => CurrentLocationRef != null ? CurrentLocationRef.SceneName : null;

    // Track loaded scenes by name
    private readonly HashSet<string> _loaded = new HashSet<string>();

    public event Action<string> SceneLoadStarted;
    public event Action<string> SceneLoadCompleted;
    public event Action<string> SceneUnloadStarted;
    public event Action<string> SceneUnloadCompleted;
    public event Action<SceneReference> LocationLoadStarted;
    public event Action<SceneReference> LocationReady; // <- scene loaded + active + CurrentLocationRef updated
    public static event System.Action<GameManager> InstanceReady;

    private bool _isForcingRelocation;

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        InstanceReady?.Invoke(this);
        CacheAlreadyLoadedScenes();
    }

    private void OnEnable()
    {
        if (TimeManager.Instance != null)
            TimeManager.Instance.OnTimeChanged += HandleTimeChanged;
        if (DialogueRunner.Instance != null)
            DialogueRunner.Instance.OnDialogueStateChanged += HandleDialogueState;
    }

    private void OnDisable()
    {
        if (TimeManager.Instance != null)
            TimeManager.Instance.OnTimeChanged -= HandleTimeChanged;
        if (DialogueRunner.Instance != null)
            DialogueRunner.Instance.OnDialogueStateChanged -= HandleDialogueState;
    }

    private async void Start()
    {
        if (loadUIOnStart && sceneDatabase != null && sceneDatabase.UI != null && sceneDatabase.UI.IsValid)
            await Load(sceneDatabase.UI, setActive: false);

        if (loadMainMenuOnStart && sceneDatabase != null && sceneDatabase.MainMenu != null && sceneDatabase.MainMenu.IsValid)
        {
            await Load(sceneDatabase.MainMenu, setActive: true);
            CurrentLocationRef = sceneDatabase.MainMenu; // ✅ set reference
        }
        // Treat initial active scene as an entered location too.
        if (CurrentLocationRef != null && CurrentLocationRef.IsValid)
        {
            LocationLoadStarted?.Invoke(CurrentLocationRef);
            LocationReady?.Invoke(CurrentLocationRef);
            GameEvents.RaiseLocationEntered(CurrentLocationRef.SceneName);
        }
    }

    private void CacheAlreadyLoadedScenes()
    {
        _loaded.Clear();
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var sc = SceneManager.GetSceneAt(i);
            if (sc.IsValid() && sc.isLoaded && !string.IsNullOrEmpty(sc.name))
                _loaded.Add(sc.name);
        }
    }

    public SceneDatabase Scenes => sceneDatabase;

    public bool IsLoaded(SceneReference sceneRef) =>
        sceneRef != null && sceneRef.IsValid && _loaded.Contains(sceneRef.SceneName);

    public Task Load(SceneReference sceneRef, bool setActive = false) =>
        Load(sceneRef?.SceneName, setActive);

    public Task Unload(SceneReference sceneRef) =>
        Unload(sceneRef?.SceneName);

    public async Task Load(string sceneName, bool setActive = false)
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogError("Load failed: sceneName is null/empty.");
            return;
        }

        if (_loaded.Contains(sceneName))
        {
            if (setActive)
                SetActive(sceneName);
            return;
        }

        SceneLoadStarted?.Invoke(sceneName);

        var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
        if (op == null)
        {
            Debug.LogError($"Load failed: LoadSceneAsync returned null for '{sceneName}'. Is it in Build Settings?");
            return;
        }

        op.allowSceneActivation = true;

        while (!op.isDone)
            await Task.Yield();

        _loaded.Add(sceneName);

        if (setActive)
            SetActive(sceneName);

        SceneLoadCompleted?.Invoke(sceneName);
    }

    public async Task Unload(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogError("Unload failed: sceneName is null/empty.");
            return;
        }

        if (!_loaded.Contains(sceneName))
            return;

        SceneUnloadStarted?.Invoke(sceneName);

        var scene = SceneManager.GetSceneByName(sceneName);
        if (!scene.IsValid() || !scene.isLoaded)
        {
            _loaded.Remove(sceneName);
            SceneUnloadCompleted?.Invoke(sceneName);
            return;
        }

        // Don't unload the active scene without switching away first
        if (SceneManager.GetActiveScene().name == sceneName)
        {
            foreach (var s in _loaded)
            {
                if (s != sceneName)
                {
                    SetActive(s);
                    break;
                }
            }
        }

        var op = SceneManager.UnloadSceneAsync(scene);
        if (op == null)
        {
            Debug.LogError($"Unload failed: UnloadSceneAsync returned null for '{sceneName}'.");
            return;
        }

        while (!op.isDone)
            await Task.Yield();

        _loaded.Remove(sceneName);

        SceneUnloadCompleted?.Invoke(sceneName);
    }

    public bool SetActive(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
            return false;

        var sc = SceneManager.GetSceneByName(sceneName);
        if (!sc.IsValid() || !sc.isLoaded)
            return false;

        SceneManager.SetActiveScene(sc);
        return true;
    }

    public async Task SwitchTo(SceneReference target, params SceneReference[] scenesToUnload)
    {
        if (target == null || !target.IsValid)
        {
            Debug.LogError("SwitchTo failed: target is invalid.");
            return;
        }

        await Load(target, setActive: true);

        if (scenesToUnload == null) return;
        foreach (var s in scenesToUnload)
        {
            if (s != null && s.IsValid && s.SceneName != target.SceneName)
                await Unload(s);
        }
    }

    public async Task SwitchLocation(SceneReference targetLocation)
    {
        if (targetLocation == null || !targetLocation.IsValid)
            return;

        LocationLoadStarted?.Invoke(targetLocation);

        // ✅ Optional but recommended: also enforce restriction on manual travel
        if (sceneDatabase != null && TimeManager.Instance != null)
        {
            var day = TimeManager.Instance.DayOfWeek;
            var phase = TimeManager.Instance.TimeOfDay;

            if (!sceneDatabase.IsAllowedNow(targetLocation, day, phase))
            {
                Debug.Log($"Blocked travel to '{targetLocation.SceneName}' at {day}/{phase}.");
                return;
            }
        }

        // unload previous location if any
        if (CurrentLocationRef != null && CurrentLocationRef.IsValid)
            await Unload(CurrentLocationRef);

        await Load(targetLocation, setActive: true);

        // ✅ store reference
        CurrentLocationRef = targetLocation;

        // ✅ THIS is the key hook NPC system will listen to
        LocationReady?.Invoke(CurrentLocationRef);
        GameEvents.RaiseLocationEntered(CurrentLocationRef.SceneName);
    }

    // ✅ Failsafe: if current location becomes invalid after time skip -> force Home
    private async void HandleTimeChanged(DayOfWeek day, TimeOfDay phase)
    {
        if (_isForcingRelocation)
            return;

        if (sceneDatabase == null || sceneDatabase.Home == null || !sceneDatabase.Home.IsValid)
            return;

        if (CurrentLocationRef == null || !CurrentLocationRef.IsValid)
            return;

        // If we're already home, no action
        if (CurrentLocationRef.SceneName == sceneDatabase.Home.SceneName)
            return;

        // Only gameplay locations should be listed/restricted; SceneDatabase decides.
        if (!sceneDatabase.IsAllowedNow(CurrentLocationRef, day, phase))
        {
            _isForcingRelocation = true;
            try
            {
                Debug.Log($"Location '{CurrentLocationRef.SceneName}' became restricted at {day}/{phase}. Forcing Home.");
                await SwitchLocation(sceneDatabase.Home);
            }
            finally
            {
                _isForcingRelocation = false;
            }
        }
    }

    private void HandleDialogueState(bool inDialogue)
    {
        IsInDialogue = inDialogue;

        // Force UI to refresh availability immediately
        if (ActionService.Instance != null)
            ActionService.Instance.NotifyStateChanged();
    }
}
