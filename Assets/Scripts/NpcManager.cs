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

        QuestManager.InstanceReady += HandleQuestManagerReady;
        if (QuestManager.Instance != null)
            HandleQuestManagerReady(QuestManager.Instance);
    }

    private void OnDisable()
    {
        GameManager.InstanceReady -= HandleGameManagerReady;
        if (GameManager.Instance != null)
            GameManager.Instance.LocationReady -= HandleLocationReady;

        TimeManager.InstanceReady -= HandleTimeManagerReady;
        if (TimeManager.Instance != null)
            TimeManager.Instance.OnTimeChanged -= HandleTimeChanged;

        QuestManager.InstanceReady -= HandleQuestManagerReady;
        if (QuestManager.Instance != null)
            QuestManager.Instance.OnQuestStateChanged -= HandleQuestStateChanged;
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

    private void HandleQuestManagerReady(QuestManager qm)
    {
        // avoid double subscribe
        qm.OnQuestStateChanged -= HandleQuestStateChanged;
        qm.OnQuestStateChanged += HandleQuestStateChanged;
    }

    private void HandleQuestStateChanged()
    {
        // Placement rules can be quest-gated (NPC moves/disappears during a
        // quest), so presence must re-resolve when quest state changes too.
        if (_activeSpawner == null) return;

        RefreshAll();
    }

    public void RefreshAll()
    {
        if (TimeManager.Instance == null) return;
        if (_activeSpawner == null) return;
        if (_currentLocation == null || !_currentLocation.IsValid) return;

        string locationId = _currentLocation.SceneName;

        for (int i = 0; i < npcs.Count; i++)
        {
            var def = npcs[i];
            if (def == null) continue;

            if (def.Prefab == null || string.IsNullOrWhiteSpace(def.NpcId))
            {
                _activeSpawner.Despawn(def.NpcId);
                continue;
            }

            // Presence is derived from the same rules that route dialogue: the
            // winning placement rule decides where (and whether) the NPC stands.
            var ctx = DialogueSelectorContext.From(def.NpcId, locationId);

            if (!DialogueSelector.TryResolvePlacement(def, ctx, out var location, out var spawnPointKey) ||
                !string.Equals(location.SceneName, locationId, System.StringComparison.OrdinalIgnoreCase))
            {
                _activeSpawner.Despawn(def.NpcId);
                continue;
            }

            var instance = _activeSpawner.EnsureSpawned(def.NpcId, def.Prefab, spawnPointKey);

            if (instance != null)
            {
                var launcher = instance.GetComponent<NpcDialogueLauncher>();
                if (launcher != null)
                    launcher.Init(def.NpcId, locationId, def);
            }
        }
    }

    public NpcDefinition GetById(string npcId)
    {
        if (string.IsNullOrWhiteSpace(npcId))
            return null;

        for (int i = 0; i < npcs.Count; i++)
        {
            var def = npcs[i];
            if (def == null) continue;

            if (string.Equals(def.NpcId, npcId, System.StringComparison.OrdinalIgnoreCase))
                return def;
        }

        return null;
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
