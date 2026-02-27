using System;
using UnityEngine;
using UnityEngine.UI;

public sealed class QuestListItemUI : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private Text label;

    private string _questId;
    private Action<string> _onClick;

    public void Bind(string questId, string title, Action<string> onClick)
    {
        _questId = questId;
        _onClick = onClick;

        if (label != null)
            label.text = string.IsNullOrEmpty(title) ? questId : title;

        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => _onClick?.Invoke(_questId));
        }
    }

    private void Reset()
    {
        button = GetComponent<Button>();
        if (label == null) label = GetComponentInChildren<Text>();
    }
}