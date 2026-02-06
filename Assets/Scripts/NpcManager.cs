using System.Collections.Generic;
using UnityEngine;

public class NpcManager : MonoBehaviour
{
    public static NpcManager Instance { get; private set; }

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

        // If it already exists, bind immediately
        if (GameManager.Instance != null)
            HandleGameManagerReady(GameManager.Instance);

        if (TimeManager.Instance != null)
            TimeManager.Instance.OnTimeChanged += HandleTimeChanged;
    }

    private void OnDisable()
    {
        GameManager.InstanceReady -= HandleGameManagerReady;

        if (GameManager.Instance != null)
            GameManager.Instance.LocationReady -= HandleLocationReady;

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
        RefreshAll();
    }

    public void RefreshAll()
    {
        // If we have no spawner in current scene, ensure nothing stays spawned
        if (_activeSpawner == null || _activeSpawner.LocationScene == null || !_activeSpawner.LocationScene.IsValid)
        {
            // If you want, you can keep a reference to previous spawner and despawn there.
            // For now: best-effort cleanup in current spawner only.
            return;
        }

        if (TimeManager.Instance == null) return;

        var day = TimeManager.Instance.DayOfWeek;
        var phase = TimeManager.Instance.TimeOfDay;

        for (int i = 0; i < npcs.Count; i++)
        {
            var def = npcs[i];
            if (def == null) continue;
            if (string.IsNullOrWhiteSpace(def.NpcId)) continue;
            if (def.Prefab == null)
            {
                _activeSpawner.Despawn(def.NpcId);
                continue;
            }

            // No match => absent
            if (!def.TryGetScheduleFor(day, phase, out var entry))
            {
                _activeSpawner.Despawn(def.NpcId);
                continue;
            }

            // Must match current location
            if (!SceneReferenceEquals(entry.LocationScene, _activeSpawner.LocationScene))
            {
                _activeSpawner.Despawn(def.NpcId);
                continue;
            }

            _activeSpawner.EnsureSpawned(def.NpcId, def.Prefab, entry.SpawnPointKey);
        }
    }

    /// <summary>
    /// You may need to adjust this depending on how your SceneReference is implemented.
    /// Common options: compare SceneName, ScenePath, or BuildIndex.
    /// </summary>
    private bool SceneReferenceEquals(SceneReference a, SceneReference b)
    {
        if (a == null || b == null) return false;
        if (!a.IsValid || !b.IsValid) return false;

        // ✅ Most SceneReference wrappers expose a SceneName or similar.
        // If yours uses a different property, change this one line.
        return a.SceneName == b.SceneName;
    }
}
