using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

public sealed class SaveLoadManager : MonoBehaviour
{
    public static SaveLoadManager Instance { get; private set; }

    [Header("Startup")]
    [SerializeField] private bool loadOnStart = false;
    [SerializeField] private bool saveOnApplicationQuit = false;
    [SerializeField] private string activeSlotId = "slot_1";

    [Header("Autosave")]
    [SerializeField] private bool autosaveEnabled = true;
    [SerializeField] private bool autosaveOnTimeChange = true;
    [SerializeField] private bool autosaveOnLocationChange = true;
    [SerializeField] private bool autosaveOnQuestChange = true;
    [SerializeField] private bool autosaveOnTrackedQuestChange = true;
    [SerializeField] private float autosaveDelay = 1.5f;
    [SerializeField] private bool autosaveOnCareerChange = true;

    [Tooltip("Autosaves rotate through these slots (oldest gets overwritten) and never touch the manual slots.")]
    [SerializeField] private string[] autosaveSlotIds = { "autosave_1", "autosave_2" };

    [Header("Debug")]
    [SerializeField] private bool prettyPrintJson = true;
    [SerializeField] private bool verboseLogs = true;

    private bool _isLoading;
    private float _autosaveAt = -1f;
    private bool _autosavePending;

    public string ActiveSlotId => activeSlotId;
    public bool IsLoading => _isLoading;
    public IReadOnlyList<string> AutosaveSlotIds => autosaveSlotIds;

    private string SaveFolder => Path.Combine(Application.persistentDataPath, "Saves");

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
        GameEvents.TimeChanged += HandleTimeChanged;
        GameEvents.LocationEntered += HandleLocationEntered;

        // InstanceReady covers singletons that wake up after us; the Instance
        // check covers ones that woke up first.
        QuestManager.InstanceReady += HandleQuestManagerReady;
        if (QuestManager.Instance != null)
            HandleQuestManagerReady(QuestManager.Instance);

        QuestJournal.InstanceReady += HandleQuestJournalReady;
        if (QuestJournal.Instance != null)
            HandleQuestJournalReady(QuestJournal.Instance);

        CareerManager.InstanceReady += HandleCareerManagerReady;
        if (CareerManager.Instance != null)
            HandleCareerManagerReady(CareerManager.Instance);
    }

    private void OnDisable()
    {
        GameEvents.TimeChanged -= HandleTimeChanged;
        GameEvents.LocationEntered -= HandleLocationEntered;

        QuestManager.InstanceReady -= HandleQuestManagerReady;
        if (QuestManager.Instance != null)
            QuestManager.Instance.OnQuestStateChanged -= HandleQuestStateChanged;

        QuestJournal.InstanceReady -= HandleQuestJournalReady;
        if (QuestJournal.Instance != null)
            QuestJournal.Instance.OnTrackedQuestChanged -= HandleTrackedQuestChanged;

        CareerManager.InstanceReady -= HandleCareerManagerReady;
        if (CareerManager.Instance != null)
            CareerManager.Instance.OnCareerStateChanged -= HandleCareerStateChanged;
    }

    private void HandleQuestManagerReady(QuestManager qm)
    {
        qm.OnQuestStateChanged -= HandleQuestStateChanged;
        qm.OnQuestStateChanged += HandleQuestStateChanged;
    }

    private void HandleQuestJournalReady(QuestJournal journal)
    {
        journal.OnTrackedQuestChanged -= HandleTrackedQuestChanged;
        journal.OnTrackedQuestChanged += HandleTrackedQuestChanged;
    }

    private void HandleCareerManagerReady(CareerManager career)
    {
        career.OnCareerStateChanged -= HandleCareerStateChanged;
        career.OnCareerStateChanged += HandleCareerStateChanged;
    }

    private async void Start()
    {
        if (loadOnStart && HasSave(activeSlotId))
            await LoadGame(activeSlotId);
    }

    private void Update()
    {
        if (_autosavePending && !_isLoading && Time.unscaledTime >= _autosaveAt)
        {
            _autosavePending = false;

            // The request was made in gameplay, but the player may have left it
            // (quit to menu) during the delay.
            if (IsGameplayActive())
                SaveGame(GetNextAutosaveSlotId(), "autosave");
        }
    }

    private void OnApplicationQuit()
    {
        // Quit-saves go to the autosave rotation too — quitting from the main menu
        // (or anywhere) must never overwrite a manual slot with unwanted state.
        if (saveOnApplicationQuit && !_isLoading && IsGameplayActive())
            SaveGame(GetNextAutosaveSlotId(), "quit");
    }

    private static bool IsGameplayActive()
    {
        return GameManager.Instance != null && GameManager.Instance.IsInGameplay;
    }

    /// <summary>
    /// Immediately saves to the next autosave slot if the player is in gameplay.
    /// Returns true if a save was written. Use for explicit "save before quit".
    /// </summary>
    public bool TryAutosave(string label = "autosave")
    {
        if (_isLoading || !IsGameplayActive())
            return false;

        SaveGame(GetNextAutosaveSlotId(), label);
        return true;
    }

    /// <summary>Autosaves rotate: first empty autosave slot, otherwise the oldest one.</summary>
    private string GetNextAutosaveSlotId()
    {
        if (autosaveSlotIds == null || autosaveSlotIds.Length == 0)
            return "autosave_1";

        string oldest = null;
        DateTime oldestTime = DateTime.MaxValue;

        for (int i = 0; i < autosaveSlotIds.Length; i++)
        {
            string slotId = autosaveSlotIds[i];
            if (string.IsNullOrWhiteSpace(slotId))
                continue;

            string path = GetSavePath(slotId);
            if (!File.Exists(path))
                return slotId;

            DateTime writeTime = File.GetLastWriteTimeUtc(path);
            if (writeTime < oldestTime)
            {
                oldestTime = writeTime;
                oldest = slotId;
            }
        }

        return oldest ?? "autosave_1";
    }

    public void SetActiveSlot(string slotId)
    {
        if (string.IsNullOrWhiteSpace(slotId))
            return;

        activeSlotId = slotId.Trim();
    }

    public bool HasSave(string slotId)
    {
        return File.Exists(GetSavePath(slotId));
    }

    public string GetSavePath(string slotId)
    {
        if (string.IsNullOrWhiteSpace(slotId))
            slotId = "slot_1";

        return Path.Combine(SaveFolder, $"{slotId}.json");
    }

    public List<SaveSlotInfo> GetAllSlotInfos(params string[] slotIds)
    {
        var result = new List<SaveSlotInfo>();

        if (slotIds == null || slotIds.Length == 0)
            slotIds = new[] { "slot_1", "slot_2", "slot_3" };

        foreach (var slotId in slotIds)
            result.Add(GetSlotInfo(slotId));

        return result;
    }

    public SaveSlotInfo GetSlotInfo(string slotId)
    {
        var info = new SaveSlotInfo
        {
            slotId = slotId,
            filePath = GetSavePath(slotId),
            exists = false,
            version = 0
        };

        if (!File.Exists(info.filePath))
            return info;

        try
        {
            string json = File.ReadAllText(info.filePath);
            var data = JsonUtility.FromJson<SaveData>(json);
            if (data == null)
                return info;

            info.exists = true;
            info.savedAtUtc = data.savedAtUtc;
            info.currentLocationSceneName = data.currentLocationSceneName;
            info.version = data.version;
            info.trackedQuestId = data.quests != null ? data.quests.trackedQuestId : null;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[SaveLoadManager] Failed to read slot info for '{slotId}'.\n{ex}");
        }

        return info;
    }

    [ContextMenu("Save Active Slot")]
    public void SaveGame()
    {
        SaveGame(activeSlotId, "manual");
    }

    public void SaveGame(string slotId, string label = null)
    {
        try
        {
            var data = BuildSaveData(slotId, label);
            string json = JsonUtility.ToJson(data, prettyPrintJson);

            if (!Directory.Exists(SaveFolder))
                Directory.CreateDirectory(SaveFolder);

            // Write to a temp file first so a crash mid-write can't corrupt the slot.
            string path = GetSavePath(slotId);
            string tmpPath = path + ".tmp";
            File.WriteAllText(tmpPath, json);

            if (File.Exists(path))
                File.Replace(tmpPath, path, null);
            else
                File.Move(tmpPath, path);

            if (verboseLogs)
                Debug.Log($"[SaveLoadManager] Saved slot '{slotId}' to: {path}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SaveLoadManager] Save failed.\n{ex}");
        }
    }

    [ContextMenu("Load Active Slot")]
    public async void LoadGameContextMenu()
    {
        await LoadGame(activeSlotId);
    }

    public async Task<bool> LoadGame(string slotId)
    {
        string path = GetSavePath(slotId);

        if (!File.Exists(path))
        {
            if (verboseLogs)
                Debug.Log($"[SaveLoadManager] No save file for slot '{slotId}'.");
            return false;
        }

        try
        {
            _isLoading = true;

            string json = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(json))
                return false;

            var data = JsonUtility.FromJson<SaveData>(json);
            if (data == null)
                return false;

            data = MigrateIfNeeded(data);
            SetActiveSlot(slotId);

            await RestoreSaveData(data);

            if (verboseLogs)
                Debug.Log($"[SaveLoadManager] Loaded slot '{slotId}' from: {path}");

            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SaveLoadManager] Load failed.\n{ex}");
            return false;
        }
        finally
        {
            _isLoading = false;
        }
    }

    [ContextMenu("Delete Active Slot")]
    public void DeleteSaveFile()
    {
        DeleteSaveFile(activeSlotId);
    }

    public void DeleteSaveFile(string slotId)
    {
        try
        {
            string path = GetSavePath(slotId);
            if (File.Exists(path))
            {
                File.Delete(path);
                if (verboseLogs)
                    Debug.Log($"[SaveLoadManager] Deleted slot '{slotId}': {path}");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SaveLoadManager] Delete failed.\n{ex}");
        }
    }

    public void ClearCurrentSlotAndRuntimeState()
    {
        DeleteSaveFile(activeSlotId);
        ResetRuntimeState();
    }

    /// <summary>
    /// Resets all gameplay managers to a fresh-game state without touching save files.
    /// Used by "Start New Game".
    /// </summary>
    public void ResetRuntimeState()
    {
        // A dialogue running across a reset would keep stale graph state and leave
        // IsInDialogue stuck; abort skips trailing commands/NpcTalked on purpose.
        if (DialogueRunner.Instance != null)
            DialogueRunner.Instance.AbortDialogue();

        // Fresh-game stats come from the manager's inspector defaults, not a
        // zeroed save object (new PlayerStatsSave() would wipe starting stats).
        if (PlayerStatsManager.Instance != null)
            PlayerStatsManager.Instance.ResetToFreshState();

        if (InventoryManager.Instance != null)
            InventoryManager.Instance.RestoreState(new InventorySave());

        if (WorldState.Instance != null)
            WorldState.Instance.RestoreState(new WorldSave());

        if (DialogueJournal.Instance != null)
            DialogueJournal.Instance.RestoreSnapshot(new DialogueJournal.Snapshot());

        if (QuestJournal.Instance != null)
            QuestJournal.Instance.RestoreSnapshot(new QuestJournal.Snapshot());

        if (CareerManager.Instance != null)
            CareerManager.Instance.RestoreSnapshot(new CareerManager.Snapshot());

        if (RelationshipManager.Instance != null)
            RelationshipManager.Instance.RestoreSnapshot(new RelationshipManager.Snapshot());

        // Before the time reset below: its System TimeChanged re-anchors the
        // calendar/random-event clocks to the fresh phase 0.
        if (CalendarManager.Instance != null)
            CalendarManager.Instance.RestoreSnapshot(new CalendarManager.Snapshot());

        if (RandomEventManager.Instance != null)
            RandomEventManager.Instance.RestoreSnapshot(new RandomEventManager.Snapshot());

        if (TimeManager.Instance != null)
            TimeManager.Instance.RestoreState(new TimeSave
            {
                dayOfWeek = (int)DayOfWeek.Monday,
                timeOfDay = (int)TimeOfDay.Morning
            });
    }

    /// <summary>True if any save file exists in any slot (manual or autosave).</summary>
    public bool HasAnySave() => GetMostRecentSlotId() != null;

    /// <summary>The slot id of the most recently written save across all files, or null if none.</summary>
    public string GetMostRecentSlotId()
    {
        if (!Directory.Exists(SaveFolder))
            return null;

        string newest = null;
        DateTime newestTime = DateTime.MinValue;

        foreach (var path in Directory.GetFiles(SaveFolder, "*.json"))
        {
            // GetFiles("*.json") can over-match on Windows; confirm the real extension.
            if (!string.Equals(Path.GetExtension(path), ".json", StringComparison.OrdinalIgnoreCase))
                continue;

            DateTime t = File.GetLastWriteTimeUtc(path);
            if (t > newestTime)
            {
                newestTime = t;
                newest = path;
            }
        }

        return newest != null ? Path.GetFileNameWithoutExtension(newest) : null;
    }

    /// <summary>Loads the most recent save (any slot). Returns false if there are none.</summary>
    public async Task<bool> ContinueLatest()
    {
        string slotId = GetMostRecentSlotId();
        if (string.IsNullOrEmpty(slotId))
        {
            if (verboseLogs)
                Debug.Log("[SaveLoadManager] ContinueLatest: no saves found.");
            return false;
        }

        return await LoadGame(slotId);
    }

    private void HandleTimeChanged(DayOfWeek day, TimeOfDay phase, TimeChangeSource source)
    {
        if (autosaveEnabled && autosaveOnTimeChange && source != TimeChangeSource.System)
            RequestAutosave();
    }

    private void HandleLocationEntered(string locationSceneName)
    {
        if (autosaveEnabled && autosaveOnLocationChange)
            RequestAutosave();
    }

    private void HandleQuestStateChanged()
    {
        if (autosaveEnabled && autosaveOnQuestChange)
            RequestAutosave();
    }

    private void HandleTrackedQuestChanged()
    {
        if (autosaveEnabled && autosaveOnTrackedQuestChange)
            RequestAutosave();
    }

    private void RequestAutosave()
    {
        if (_isLoading)
            return;

        // Never autosave outside gameplay: launching to the main menu raises
        // LocationEntered, which must not write a fresh default state anywhere.
        if (!IsGameplayActive())
            return;

        _autosavePending = true;
        _autosaveAt = Time.unscaledTime + Mathf.Max(0.1f, autosaveDelay);
    }

    private SaveData BuildSaveData(string slotId, string label)
    {
        return new SaveData
        {
            version = SaveVersions.Current,
            slotId = slotId,
            saveLabel = string.IsNullOrWhiteSpace(label) ? "manual" : label,
            savedAtUtc = DateTime.UtcNow.ToString("O"),

            playerStats = PlayerStatsManager.Instance != null
                ? PlayerStatsManager.Instance.CaptureState()
                : new PlayerStatsSave(),

            inventory = InventoryManager.Instance != null
                ? InventoryManager.Instance.CaptureState()
                : new InventorySave(),

            time = TimeManager.Instance != null
                ? TimeManager.Instance.CaptureState()
                : new TimeSave(),

            world = WorldState.Instance != null
                ? WorldState.Instance.CaptureState()
                : new WorldSave(),

            currentLocationSceneName = GameManager.Instance != null
                ? GameManager.Instance.CurrentLocation
                : null,

            quests = QuestJournal.Instance != null
                ? QuestJournal.Instance.CaptureSnapshot()
                : new QuestJournal.Snapshot(),

            dialogues = DialogueJournal.Instance != null
                ? DialogueJournal.Instance.CaptureSnapshot()
                : new DialogueJournal.Snapshot(),

            career = CareerManager.Instance != null
                ? CareerManager.Instance.CaptureSnapshot()
                : new CareerManager.Snapshot(),

            relationships = RelationshipManager.Instance != null
                ? RelationshipManager.Instance.CaptureSnapshot()
                : new RelationshipManager.Snapshot(),

            calendar = CalendarManager.Instance != null
                ? CalendarManager.Instance.CaptureSnapshot()
                : new CalendarManager.Snapshot(),

            randomEvents = RandomEventManager.Instance != null
                ? RandomEventManager.Instance.CaptureSnapshot()
                : new RandomEventManager.Snapshot()
        };
    }

    private async Task RestoreSaveData(SaveData data)
    {
        if (data == null)
            return;

        // Abort (not Close) any running dialogue first: Close would execute trailing
        // commands and raise NpcTalked, mutating the state we are about to restore.
        if (DialogueRunner.Instance != null)
            DialogueRunner.Instance.AbortDialogue();

        if (PlayerStatsManager.Instance != null)
            PlayerStatsManager.Instance.RestoreState(data.playerStats);

        if (WorldState.Instance != null)
            WorldState.Instance.RestoreState(data.world);

        if (InventoryManager.Instance != null)
            InventoryManager.Instance.RestoreState(data.inventory);

        if (TimeManager.Instance != null)
            TimeManager.Instance.RestoreState(data.time);

        if (DialogueJournal.Instance != null)
            DialogueJournal.Instance.RestoreSnapshot(data.dialogues);

        if (QuestJournal.Instance != null)
            QuestJournal.Instance.RestoreSnapshot(data.quests);

        if (CareerManager.Instance != null)
            CareerManager.Instance.RestoreSnapshot(data.career);

        if (RelationshipManager.Instance != null)
            RelationshipManager.Instance.RestoreSnapshot(data.relationships);

        // After TimeManager.RestoreState: the calendar and random events anchor
        // their clocks to the restored TotalPhasesElapsed.
        if (CalendarManager.Instance != null)
            CalendarManager.Instance.RestoreSnapshot(data.calendar);

        if (RandomEventManager.Instance != null)
            RandomEventManager.Instance.RestoreSnapshot(data.randomEvents);

        if (GameManager.Instance != null && !string.IsNullOrWhiteSpace(data.currentLocationSceneName))
            await GameManager.Instance.RestoreLocationBySceneName(data.currentLocationSceneName);
    }

    private SaveData MigrateIfNeeded(SaveData data)
    {
        if (data == null)
            return new SaveData();

        if (data.version <= 0)
            data.version = SaveVersions.Initial;

        if (data.career == null)
            data.career = new CareerManager.Snapshot();

        // v8: pre-calendar saves have no event history — start empty.
        if (data.calendar == null)
            data.calendar = new CalendarManager.Snapshot();

        if (data.randomEvents == null)
            data.randomEvents = new RandomEventManager.Snapshot();

        if (data.version < SaveVersions.TrackedQuestAndSlots)
        {
            // v1 -> v2
            if (data.quests == null)
                data.quests = new QuestJournal.Snapshot();

            if (string.IsNullOrWhiteSpace(data.slotId))
                data.slotId = activeSlotId;

            if (string.IsNullOrWhiteSpace(data.savedAtUtc))
                data.savedAtUtc = DateTime.UtcNow.ToString("O");

            if (string.IsNullOrWhiteSpace(data.saveLabel))
                data.saveLabel = "migrated_v1";
        }

        data.version = SaveVersions.Current;
        return data;
    }

    private void HandleCareerStateChanged()
    {
        if (autosaveEnabled && autosaveOnCareerChange)
            RequestAutosave();
    }
}
