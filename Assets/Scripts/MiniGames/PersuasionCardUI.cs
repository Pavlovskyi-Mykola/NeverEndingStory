using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// One argument card in the Persuasion mini-game — a base argument (reusable)
/// or an intel/leverage card (single use, visually distinct, shows its cost).
/// </summary>
public sealed class PersuasionCardUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Button button;
    [SerializeField] private Image background;
    [SerializeField] private TMP_Text titleLabel;
    [SerializeField] private TMP_Text subLabel;

    [Header("Colors")]
    [SerializeField] private Color baseColor = Color.white;
    [SerializeField] private Color intelColor = new Color(1f, 0.9f, 0.65f);
    [SerializeField] private Color usedColor = new Color(0.85f, 0.85f, 0.85f, 0.6f);

    private Action _onClicked;
    private bool _used;

    private void Awake()
    {
        if (button == null) button = GetComponent<Button>();
        if (button != null) button.onClick.AddListener(HandleClick);
    }

    private void OnDestroy()
    {
        if (button != null) button.onClick.RemoveListener(HandleClick);
    }

    public void Bind(string title, string subtitle, bool isIntel, Action onClicked)
    {
        _onClicked = onClicked;
        _used = false;

        if (titleLabel != null) titleLabel.text = title;

        if (subLabel != null)
        {
            subLabel.text = subtitle ?? string.Empty;
            subLabel.gameObject.SetActive(!string.IsNullOrEmpty(subtitle));
        }

        if (background != null) background.color = isIntel ? intelColor : baseColor;
        if (button != null) button.interactable = true;
    }

    /// <summary>Permanently disables the card (intel cards after playing).</summary>
    public void SetUsed()
    {
        _used = true;
        if (button != null) button.interactable = false;
        if (background != null) background.color = usedColor;
    }

    public void SetInteractable(bool value)
    {
        if (button != null && !_used)
            button.interactable = value;
    }

    private void HandleClick()
    {
        if (_used) return;
        _onClicked?.Invoke();
    }
}
