using System;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

public sealed class SaveLoadManager : MonoBehaviour
{
    public static SaveLoadManager Instance { get; private set; }

    [Header("Startup")]
    [SerializeField] private bool loadOnStart = false;
    [SerializeField] private bool saveOnApplicationQuit = false;

    [Header("Debug")]
    [SerializeField] private string fileName = "savegame.json";
    [SerializeField] private bool prettyPrintJson = true;
    [SerializeField] private bool verboseLogs = true;

    private bool _isLoading;

    public string SavePath => Path.Combine(Application.persistentDataPath, fileName);
    public bool HasSave => File.Exists(SavePath);
    public bool IsLoading => _isLoading;

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

    private async void Start()
    {
        if (loadOnStart && HasSave)
            await LoadGame();
    }

    private void OnApplicationQuit()
    {
        if (saveOnApplicationQuit && !_isLoading)
            SaveGame();
    }

    [ContextMenu("Save Game")]
    public void SaveGame()
    {
        try
        {
            var data = BuildSaveData();
            string json = JsonUtility.ToJson(data, prettyPrintJson);

            string dir = Path.GetDirectoryName(SavePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            File.WriteAllText(SavePath, json);

            if (verboseLogs)
                Debug.Log($"[SaveLoadManager] Save written to: {SavePath}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SaveLoadManager] Save failed.\n{ex}");
        }
    }

    [ContextMenu("Load Game")]
    public async void LoadGameContextMenu()
    {
        await LoadGame();
    }

    public async Task<bool> LoadGame()
    {
        if (!HasSave)
        {
            if (verboseLogs)
                Debug.Log("[SaveLoadManager] No save file found.");
            return false;
        }

        try
        {
            _isLoading = true;

            string json = File.ReadAllText(SavePath);
            if (string.IsNullOrWhiteSpace(json))
            {
                Debug.LogWarning("[SaveLoadManager] Save file is empty.");
                return false;
            }

            var data = JsonUtility.FromJson<SaveData>(json);
            if (data == null)
            {
                Debug.LogWarning("[SaveLoadManager] Failed to deserialize save.");
                return false;
            }

            await RestoreSaveData(data);

            if (verboseLogs)
                Debug.Log($"[SaveLoadManager] Save loaded from: {SavePath}");

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

    [ContextMenu("Delete Save File")]
    public void DeleteSaveFile()
    {
        try
        {
            if (File.Exists(SavePath))
            {
                File.Delete(SavePath);
                if (verboseLogs)
                    Debug.Log($"[SaveLoadManager] Save deleted: {SavePath}");
            }
            else if (verboseLogs)
            {
                Debug.Log("[SaveLoadManager] No save file to delete.");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SaveLoadManager] Delete save failed.\n{ex}");
        }
    }

    private SaveData BuildSaveData()
    {
        return new SaveData
        {
            version = 1,
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
                : new DialogueJournal.Snapshot()
        };
    }

    private async Task RestoreSaveData(SaveData data)
    {
        if (data == null)
            return;

        // 1. base systems
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

        // 2. location last, because scene switching is async
        if (GameManager.Instance != null && !string.IsNullOrWhiteSpace(data.currentLocationSceneName))
            await GameManager.Instance.RestoreLocationBySceneName(data.currentLocationSceneName);
    }
}