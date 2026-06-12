using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed class SaveLoadPanelUI : MonoBehaviour
{
    [Header("Root")]
    [SerializeField] private GameObject root;
    [SerializeField] private Button closeButton;

    [Header("Slots")]
    [SerializeField] private List<SaveSlotItemUI> slotItems = new();

    [Tooltip("Read-only rows for the autosave rotation: load/delete only, no Save button. Slot ids come from SaveLoadManager.AutosaveSlotIds.")]
    [SerializeField] private List<SaveSlotItemUI> autosaveSlotItems = new();

    [Header("Config")]
    [SerializeField] private bool hideOnStart = true;
    [SerializeField] private bool refreshOnEnable = true;
    [SerializeField] private string[] slotIds = { "slot_1", "slot_2", "slot_3" };

    private void Awake()
    {
        if (root == null)
            root = gameObject;

        if (closeButton != null)
            closeButton.onClick.AddListener(Close);

        if (hideOnStart)
            root.SetActive(false);
    }

    private void OnDestroy()
    {
        if (closeButton != null)
            closeButton.onClick.RemoveListener(Close);
    }

    private void OnEnable()
    {
        if (refreshOnEnable)
            Refresh();
    }

    public void Open()
    {
        if (root != null)
            root.SetActive(true);

        Refresh();
    }

    public void Close()
    {
        if (root != null)
            root.SetActive(false);
    }

    public void Toggle()
    {
        if (root == null)
            return;

        bool next = !root.activeSelf;
        root.SetActive(next);

        if (next)
            Refresh();
    }

    public void Refresh()
    {
        var manager = SaveLoadManager.Instance;
        if (manager == null)
        {
            Debug.LogWarning("[SaveLoadPanelUI] SaveLoadManager not found.");
            return;
        }

        var infos = manager.GetAllSlotInfos(slotIds);

        for (int i = 0; i < slotItems.Count; i++)
        {
            if (slotItems[i] == null)
                continue;

            SaveSlotInfo info = i < infos.Count ? infos[i] : new SaveSlotInfo
            {
                slotId = i < slotIds.Length ? slotIds[i] : $"slot_{i + 1}",
                exists = false
            };

            slotItems[i].Bind(info, HandleSave, HandleLoad, HandleDelete);
        }

        // Autosave rows: load/delete only, ids owned by the manager.
        var autosaveIds = manager.AutosaveSlotIds;

        for (int i = 0; i < autosaveSlotItems.Count; i++)
        {
            if (autosaveSlotItems[i] == null)
                continue;

            string slotId = autosaveIds != null && i < autosaveIds.Count ? autosaveIds[i] : $"autosave_{i + 1}";

            autosaveSlotItems[i].Bind(
                manager.GetSlotInfo(slotId),
                onSave: null,
                onLoad: HandleLoad,
                onDelete: HandleDelete,
                allowSave: false);
        }
    }

    private void HandleSave(string slotId)
    {
        if (SaveLoadManager.Instance == null)
            return;

        SaveLoadManager.Instance.SetActiveSlot(slotId);
        SaveLoadManager.Instance.SaveGame(slotId, "manual");
        Refresh();
    }

    private async void HandleLoad(string slotId)
    {
        if (SaveLoadManager.Instance == null)
            return;

        SaveLoadManager.Instance.SetActiveSlot(slotId);
        await SaveLoadManager.Instance.LoadGame(slotId);
        Refresh();
    }

    private void HandleDelete(string slotId)
    {
        if (SaveLoadManager.Instance == null)
            return;

        SaveLoadManager.Instance.DeleteSaveFile(slotId);
        Refresh();
    }
}