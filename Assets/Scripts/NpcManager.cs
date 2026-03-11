using System.Collections.Generic;
using UnityEngine;

public class NpcManager : MonoBehaviour
{
    public static NpcManager Instance { get; private set; }

    public const string PlayerSpeakerId = "Player";

    public IReadOnlyList<NpcDefinition> Npcs => npcs;

    [Header("NPC Database")]
    [SerializeField] private List<NpcDefinition> npcs = new List<NpcDefinition>();

    private LocationNpcSpawner _activeSpawner;
    private SceneReference _currentLocation;

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

    private void OnEnable()
    {
        GameManager.InstanceReady += HandleGameManagerReady;
        if (GameManager.Instance != null)
            HandleGameManagerReady(GameManager.Instance);

        TimeManager.InstanceReady += HandleTimeManagerReady;
        if (TimeManager.Instance != null)
            HandleTimeManagerReady(TimeManager.Instance);
    }

    private void OnDisable()
    {
        GameManager.InstanceReady -= HandleGameManagerReady;
        if (GameManager.Instance != null)
            GameManager.Instance.LocationReady -= HandleLocationReady;

        TimeManager.InstanceReady -= HandleTimeManagerReady;
        if (TimeManager.Instance != null)
            TimeManager.Instance.OnTimeChanged -= HandleTimeChanged;
    }

    private void HandleGameManagerReady(GameManager gm)
    {
        // avoid double subscribe
        gm.LocationReady -= HandleLocationReady;
        gm.LocationReady += HandleLocationReady;

        // sync immediately
        if (gm.CurrentLocationRef != null && gm.CurrentLocationRef.IsValid)
            HandleLocationReady(gm.CurrentLocationRef);
    }
    private void HandleTimeManagerReady(TimeManager tm)
    {
        // avoid double subscribe
        tm.OnTimeChanged -= HandleTimeChanged;
        tm.OnTimeChanged += HandleTimeChanged;

        // Optional: immediate refresh using current time (good when scene loads before binding)
        RefreshAll();
    }


    private void HandleLocationReady(SceneReference location)
    {
        _currentLocation = location;

        // Find spawner in the newly active location scene
        _activeSpawner = FindFirstObjectByType<LocationNpcSpawner>();

        // If the scene has no spawner, just despawn any old stuff safely
        RefreshAll();
    }

    private void HandleTimeChanged(System.DayOfWeek day, TimeOfDay phase)
    {
        // Only refresh if we are currently inside a location with an active spawner
        if (_activeSpawner == null) return;

        RefreshAll();
    }

    public void RefreshAll()
    {
        if (TimeManager.Instance == null) return;
        if (_activeSpawner == null) return;
        if (_currentLocation == null || !_currentLocation.IsValid) return;

        var day = TimeManager.Instance.DayOfWeek;
        var phase = TimeManager.Instance.TimeOfDay;

        for (int i = 0; i < npcs.Count; i++)
        {
            var def = npcs[i];
            if (def == null) continue;

            if (def.Prefab == null || string.IsNullOrWhiteSpace(def.NpcId))
            {
                _activeSpawner.Despawn(def.NpcId);
                continue;
            }

            // ✅ Only ask: "Is this NPC scheduled to be HERE right now?"
            if (!def.TryGetScheduleForLocation(day, phase, _currentLocation, out var entry))
            {
                _activeSpawner.Despawn(def.NpcId);
                continue;
            }

            var instance = _activeSpawner.EnsureSpawned(def.NpcId, def.Prefab, entry.SpawnPointKey);

            if (instance != null)
            {
                var launcher = instance.GetComponent<NpcDialogueLauncher>();
                if (launcher != null)
                {
                    // locationId: use SceneReference guid/name/key, whatever your selector expects
                    string locationId = _currentLocation != null ? _currentLocation.SceneName : null;
                    var routeSet = def.Routes;

                    launcher.Init(def.NpcId, locationId, routeSet);
                }
            }
        }
    }

    public List<string> GetAllSpeakerIds()
    {
        var result = new List<string> { PlayerSpeakerId };

        for (int i = 0; i < npcs.Count; i++)
        {
            var def = npcs[i];
            if (def == null) continue;

            var id = def.NpcId;
            if (string.IsNullOrWhiteSpace(id)) continue;

            if (!result.Contains(id))
                result.Add(id);
        }

        return result;
    }
}
