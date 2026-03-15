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
        if (SaveLoadManager.Instance == null)
        {
            Debug.LogWarning("[SaveLoadPanelUI] SaveLoadManager not found.");
            return;
        }

        var infos = SaveLoadManager.Instance.GetAllSlotInfos(slotIds);

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