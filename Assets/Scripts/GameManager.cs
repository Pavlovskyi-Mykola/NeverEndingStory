using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum TravelBlockReason
{
    FloorLocked,  // career tier hasn't unlocked this floor
    TimeWindow,   // location is closed at the current day/phase
    MissingItems  // entry requirements (items) not met
}

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

    // Scenes with an async load/unload in flight — guards against loading a
    // duplicate additive instance when the same scene is requested twice.
    private readonly HashSet<string> _loading = new HashSet<string>();
    private readonly HashSet<string> _unloading = new HashSet<string>();

    private bool _isSwitchingLocation;

    /// <summary>True once the player is in an actual location (not the main menu). Gates autosaves.</summary>
    public bool IsInGameplay
    {
        get
        {
            if (CurrentLocationRef == null || !CurrentLocationRef.IsValid)
                return false;

            if (sceneDatabase != null && sceneDatabase.MainMenu != null && sceneDatabase.MainMenu.IsValid &&
                CurrentLocationRef.SceneName == sceneDatabase.MainMenu.SceneName)
                return false;

            return true;
        }
    }

    public event Action<string> SceneLoadStarted;
    public event Action<string> SceneLoadCompleted;
    public event Action<string> SceneUnloadStarted;
    public event Action<string> SceneUnloadCompleted;
    public event Action<SceneReference> LocationLoadStarted;
    public event Action<SceneReference> LocationReady; // <- scene loaded + active + CurrentLocationRef updated
    public event Action<SceneReference, TravelBlockReason> TravelBlocked; // rejected travel attempt (player feedback)
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
        // InstanceReady covers the case where the singletons wake up after us;
        // checking Instance covers the case where they woke up first.
        TimeManager.InstanceReady += HandleTimeManagerReady;
        if (TimeManager.Instance != null)
            HandleTimeManagerReady(TimeManager.Instance);

        DialogueRunner.InstanceReady += HandleDialogueRunnerReady;
        if (DialogueRunner.Instance != null)
            HandleDialogueRunnerReady(DialogueRunner.Instance);
    }

    private void OnDisable()
    {
        TimeManager.InstanceReady -= HandleTimeManagerReady;
        if (TimeManager.Instance != null)
            TimeManager.Instance.OnTimeChanged -= HandleTimeChanged;

        DialogueRunner.InstanceReady -= HandleDialogueRunnerReady;
        if (DialogueRunner.Instance != null)
            DialogueRunner.Instance.OnDialogueStateChanged -= HandleDialogueState;
    }

    private void HandleTimeManagerReady(TimeManager tm)
    {
        tm.OnTimeChanged -= HandleTimeChanged;
        tm.OnTimeChanged += HandleTimeChanged;
    }

    private void HandleDialogueRunnerReady(DialogueRunner runner)
    {
        runner.OnDialogueStateChanged -= HandleDialogueState;
        runner.OnDialogueStateChanged += HandleDialogueState;
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

    public async Task RestoreLocationBySceneName(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
            return;

        var target = FindSceneReferenceByName(sceneName);
        if (target == null || !target.IsValid)
        {
            Debug.LogWarning($"[GameManager] Could not restore location '{sceneName}'.");
            return;
        }

        // force: entry gates (time windows, career floors) must not block restoring
        // a save — the position was valid when the player saved.
        await SwitchLocation(target, force: true);
    }

    private SceneReference FindSceneReferenceByName(string sceneName)
    {
        if (sceneDatabase == null || string.IsNullOrWhiteSpace(sceneName))
            return null;

        if (sceneDatabase.MainMenu != null &&
            sceneDatabase.MainMenu.IsValid &&
            string.Equals(sceneDatabase.MainMenu.SceneName, sceneName, StringComparison.Ordinal))
            return sceneDatabase.MainMenu;

        if (sceneDatabase.Home != null &&
            sceneDatabase.Home.IsValid &&
            string.Equals(sceneDatabase.Home.SceneName, sceneName, StringComparison.Ordinal))
            return sceneDatabase.Home;

        if (sceneDatabase.UI != null &&
            sceneDatabase.UI.IsValid &&
            string.Equals(sceneDatabase.UI.SceneName, sceneName, StringComparison.Ordinal))
            return sceneDatabase.UI;

        if (sceneDatabase.Locations != null)
        {
            for (int i = 0; i < sceneDatabase.Locations.Count; i++)
            {
                var loc = sceneDatabase.Locations[i];
                if (loc.Scene == null || !loc.Scene.IsValid)
                    continue;

                if (string.Equals(loc.Scene.SceneName, sceneName, StringComparison.Ordinal))
                    return loc.Scene;
            }
        }

        return null;
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

        // If the same scene is already loading, wait for that load instead of
        // starting a second one (additive duplicates of the same scene).
        while (_loading.Contains(sceneName))
            await Task.Yield();

        if (_loaded.Contains(sceneName))
        {
            if (setActive)
                SetActive(sceneName);
            return;
        }

        _loading.Add(sceneName);
        try
        {
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
        finally
        {
            _loading.Remove(sceneName);
        }
    }

    public async Task Unload(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogError("Unload failed: sceneName is null/empty.");
            return;
        }

        // If the same scene is already unloading, wait for that instead of
        // issuing a second unload.
        while (_unloading.Contains(sceneName))
            await Task.Yield();

        if (!_loaded.Contains(sceneName))
            return;

        _unloading.Add(sceneName);
        try
        {
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
        finally
        {
            _unloading.Remove(sceneName);
        }
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

    public async Task SwitchLocation(SceneReference targetLocation, bool force = false)
    {
        if (targetLocation == null || !targetLocation.IsValid)
            return;

        // Ignore travel spam while a switch is in progress (double-clicked buttons).
        if (_isSwitchingLocation)
            return;

        // All validation happens before LocationLoadStarted, so listeners
        // (loading screens, faders) never see a start without a matching ready.
        if (!force)
        {
            if (IsBlockedByCareer(targetLocation))
            {
                Debug.Log($"Blocked travel to '{targetLocation.SceneName}' because floor is not unlocked yet.");
                TravelBlocked?.Invoke(targetLocation, TravelBlockReason.FloorLocked);
                return;
            }

            if (sceneDatabase != null && TimeManager.Instance != null)
            {
                var day = TimeManager.Instance.DayOfWeek;
                var phase = TimeManager.Instance.TimeOfDay;
                var inventory = InventoryManager.Instance;

                if (!sceneDatabase.CanEnterNow(targetLocation, day, phase, inventory))
                {
                    // Time window and item requirements are both checked by CanEnterNow;
                    // re-check the time window alone to report the right reason.
                    var reason = sceneDatabase.IsAllowedNow(targetLocation, day, phase)
                        ? TravelBlockReason.MissingItems
                        : TravelBlockReason.TimeWindow;

                    Debug.Log($"Blocked travel to '{targetLocation.SceneName}' due to requirements ({reason}).");
                    TravelBlocked?.Invoke(targetLocation, reason);
                    return;
                }
            }
        }

        _isSwitchingLocation = true;
        try
        {
            LocationLoadStarted?.Invoke(targetLocation);

            var previous = CurrentLocationRef;

            // Load the target first: if it fails, the player still stands in the
            // old location instead of nowhere.
            await Load(targetLocation, setActive: true);

            if (!_loaded.Contains(targetLocation.SceneName))
            {
                Debug.LogError($"SwitchLocation: failed to load '{targetLocation.SceneName}', staying in '{previous?.SceneName ?? "<none>"}'.");
                return;
            }

            CurrentLocationRef = targetLocation;

            if (previous != null && previous.IsValid && previous.SceneName != targetLocation.SceneName)
                await Unload(previous);

            if (CareerManager.Instance != null)
                CareerManager.Instance.SetCurrentFloor(targetLocation.SceneName);

            LocationReady?.Invoke(CurrentLocationRef);
            GameEvents.RaiseLocationEntered(CurrentLocationRef.SceneName);
        }
        finally
        {
            _isSwitchingLocation = false;
        }
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
                await SwitchLocation(sceneDatabase.Home, force: true);
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

    private bool IsBlockedByCareer(SceneReference targetLocation)
    {
        if (targetLocation == null || !targetLocation.IsValid)
            return true;

        if (CareerManager.Instance == null)
            return false;

        return !CareerManager.Instance.IsFloorUnlocked(targetLocation.SceneName);
    }
}
