using System;
using UnityEngine;
using UnityEngine.UI;

public sealed class QuestListItemUI : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private Text label;
    [SerializeField] private Image background;
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color selectedColor = new Color(0.85f, 0.85f, 0.65f, 1f);

    private string _questId;
    private Action<string> _onClick;

    public string QuestId => _questId;

    public void Bind(string questId, string title, Action<string> onClick, bool isSelected)
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

        SetSelected(isSelected);
    }

    public void SetSelected(bool isSelected)
    {
        if (background != null)
            background.color = isSelected ? selectedColor : normalColor;
    }

    private void Reset()
    {
        button = GetComponent<Button>();

        if (label == null)
            label = GetComponentInChildren<Text>();

        if (background == null)
            background = GetComponent<Image>();
    }
}