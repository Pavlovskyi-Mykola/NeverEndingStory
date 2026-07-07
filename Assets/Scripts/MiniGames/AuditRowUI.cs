using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// One expense line in the Audit mini-game: item label + reported vs actual amounts.
/// Click = flag it as suspicious. Each row resolves once (flagged or cleared).
/// </summary>
public sealed class AuditRowUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Button button;
    [SerializeField] private Image background;
    [SerializeField] private TMP_Text itemLabel;
    [SerializeField] private TMP_Text reportedLabel;
    [SerializeField] private TMP_Text actualLabel;

    [Header("Colors")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color flaggedColor = new Color(1f, 0.86f, 0.5f);    // discrepancy found
    [SerializeField] private Color clearedColor = new Color(0.85f, 0.88f, 0.92f); // inspected, was clean

    public bool IsDiscrepancy { get; private set; }
    public bool Resolved { get; private set; }

    private Action<AuditRowUI> _onClicked;

    private void Awake()
    {
        if (button == null) button = GetComponent<Button>();
        if (button != null) button.onClick.AddListener(HandleClick);
    }

    private void OnDestroy()
    {
        if (button != null) button.onClick.RemoveListener(HandleClick);
    }

    public void Bind(string item, int reported, int actual, bool isDiscrepancy, Action<AuditRowUI> onClicked)
    {
        IsDiscrepancy = isDiscrepancy;
        Resolved = false;
        _onClicked = onClicked;

        if (itemLabel != null) itemLabel.text = item;
        if (reportedLabel != null) reportedLabel.text = $"${reported:N0}";
        if (actualLabel != null) actualLabel.text = $"${actual:N0}";
        if (background != null) background.color = normalColor;
        if (button != null) button.interactable = true;
    }

    /// <summary>Locks the row after it has been clicked and colors it by what it was.</summary>
    public void Resolve()
    {
        Resolved = true;
        if (button != null) button.interactable = false;
        if (background != null) background.color = IsDiscrepancy ? flaggedColor : clearedColor;
    }

    public void SetInteractable(bool value)
    {
        if (button != null && !Resolved)
            button.interactable = value;
    }

    private void HandleClick()
    {
        if (Resolved) return;
        _onClicked?.Invoke(this);
    }
}
