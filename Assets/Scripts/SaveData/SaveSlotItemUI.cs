using System;
using UnityEngine;
using UnityEngine.UI;

public sealed class SaveSlotItemUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Text slotTitleText;
    [SerializeField] private Text metaText;
    [SerializeField] private Button saveButton;
    [SerializeField] private Button loadButton;
    [SerializeField] private Button deleteButton;

    private string _slotId;
    private Action<string> _onSave;
    private Action<string> _onLoad;
    private Action<string> _onDelete;

    private void Awake()
    {
        if (saveButton != null)
            saveButton.onClick.AddListener(HandleSave);

        if (loadButton != null)
            loadButton.onClick.AddListener(HandleLoad);

        if (deleteButton != null)
            deleteButton.onClick.AddListener(HandleDelete);
    }

    private void OnDestroy()
    {
        if (saveButton != null)
            saveButton.onClick.RemoveListener(HandleSave);

        if (loadButton != null)
            loadButton.onClick.RemoveListener(HandleLoad);

        if (deleteButton != null)
            deleteButton.onClick.RemoveListener(HandleDelete);
    }

    public void Bind(
        SaveSlotInfo info,
        Action<string> onSave,
        Action<string> onLoad,
        Action<string> onDelete,
        bool allowSave = true)
    {
        _slotId = info != null ? info.slotId : null;
        _onSave = onSave;
        _onLoad = onLoad;
        _onDelete = onDelete;

        if (slotTitleText != null)
            slotTitleText.text = FormatSlotTitle(_slotId);

        // Read-only rows (autosaves) hide Save entirely so the player can't
        // overwrite them manually.
        if (saveButton != null)
            saveButton.gameObject.SetActive(allowSave);

        if (metaText != null)
        {
            if (info == null || !info.exists)
            {
                metaText.text = "Empty";
            }
            else
            {
                string savedAt = string.IsNullOrWhiteSpace(info.savedAtUtc) ? "-" : info.savedAtUtc;
                string location = string.IsNullOrWhiteSpace(info.currentLocationSceneName) ? "-" : info.currentLocationSceneName;
                string trackedQuest = string.IsNullOrWhiteSpace(info.trackedQuestId) ? "-" : info.trackedQuestId;

                metaText.text =
                    $"Saved: {savedAt}\n" +
                    $"Location: {location}\n" +
                    $"Tracked quest: {trackedQuest}";
            }
        }

        if (loadButton != null)
            loadButton.interactable = info != null && info.exists;

        if (deleteButton != null)
            deleteButton.interactable = info != null && info.exists;
    }

    /// <summary>"autosave_1" -> "Autosave 1", "slot_2" -> "Slot 2".</summary>
    private static string FormatSlotTitle(string slotId)
    {
        if (string.IsNullOrWhiteSpace(slotId))
            return "Empty Slot";

        var parts = slotId.Split('_');
        for (int i = 0; i < parts.Length; i++)
        {
            if (parts[i].Length > 0)
                parts[i] = char.ToUpperInvariant(parts[i][0]) + parts[i].Substring(1);
        }

        return string.Join(" ", parts);
    }

    private void HandleSave()
    {
        if (!string.IsNullOrWhiteSpace(_slotId))
            _onSave?.Invoke(_slotId);
    }

    private void HandleLoad()
    {
        if (!string.IsNullOrWhiteSpace(_slotId))
            _onLoad?.Invoke(_slotId);
    }

    private void HandleDelete()
    {
        if (!string.IsNullOrWhiteSpace(_slotId))
            _onDelete?.Invoke(_slotId);
    }
}