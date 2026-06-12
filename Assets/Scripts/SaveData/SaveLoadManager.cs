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

    [Header("Debug")]
    [SerializeField] private bool prettyPrintJson = true;
    [SerializeField] private bool verboseLogs = true;

    private bool _isLoading;
    private float _autosaveAt = -1f;
    private bool _autosavePending;

    public string ActiveSlotId => activeSlotId;
    public bool IsLoading => _isLoading;

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

        if (QuestManager.Instance != null)
            QuestManager.Instance.OnQuestStateChanged += HandleQuestStateChanged;

        if (QuestJournal.Instance != null)
            QuestJournal.Instance.OnTrackedQuestChanged += HandleTrackedQuestChanged;
        if (CareerManager.Instance != null)
            CareerManager.Instance.OnCareerStateChanged += HandleCareerStateChanged;
    }

    private void OnDisable()
    {
        GameEvents.TimeChanged -= HandleTimeChanged;
        GameEvents.LocationEntered -= HandleLocationEntered;

        if (QuestManager.Instance != null)
            QuestManager.Instance.OnQuestStateChanged -= HandleQuestStateChanged;

        if (QuestJournal.Instance != null)
            QuestJournal.Instance.OnTrackedQuestChanged -= HandleTrackedQuestChanged;
        if (CareerManager.Instance != null)
            CareerManager.Instance.OnCareerStateChanged -= HandleCareerStateChanged;
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
            SaveGame(activeSlotId, "autosave");
        }
    }

    private void OnApplicationQuit()
    {
        if (saveOnApplicationQuit && !_isLoading)
            SaveGame(activeSlotId, "quit");
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

            string path = GetSavePath(slotId);
            File.WriteAllText(path, json);

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
        // A dialogue running across a reset would keep stale graph state and leave
        // IsInDialogue stuck; abort skips trailing commands/NpcTalked on purpose.
        if (DialogueRunner.Instance != null)
            DialogueRunner.Instance.AbortDialogue();

        DeleteSaveFile(activeSlotId);

        if (PlayerStatsManager.Instance != null)
            PlayerStatsManager.Instance.RestoreState(new PlayerStatsSave());

        if (InventoryManager.Instance != null)
            InventoryManager.Instance.RestoreState(new InventorySave());

        if (WorldState.Instance != null)
            WorldState.Instance.RestoreState(new WorldSave());

        if (DialogueJournal.Instance != null)
            DialogueJournal.Instance.RestoreSnapshot(new DialogueJournal.Snapshot());

        if (QuestJournal.Instance != null)
            QuestJournal.Instance.RestoreSnapshot(new QuestJournal.Snapshot());

        if (TimeManager.Instance != null)
            TimeManager.Instance.RestoreState(new TimeSave
            {
                dayOfWeek = (int)DayOfWeek.Monday,
                timeOfDay = (int)TimeOfDay.Morning
            });
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
                : new CareerManager.Snapshot()
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
