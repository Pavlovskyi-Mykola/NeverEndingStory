using UnityEngine;

/// <summary>
/// Opens/closes/toggles a UIPanel by id through UIPanelManager. Wire one of the
/// public methods to a Button's OnClick. Resolves the manager singleton at click
/// time, so it works across scenes (e.g. a MainMenu button reaching the settings
/// panel that lives in the persistent UI scene) where direct inspector references
/// to another scene's objects aren't allowed.
/// </summary>
public sealed class UIPanelButton : MonoBehaviour
{
    [Tooltip("Panel id to act on. Matches UIPanel.PanelId (defaults to the panel GameObject's name).")]
    [SerializeField] private string panelId;

    [Tooltip("Log a warning if the manager or panel can't be found when clicked.")]
    [SerializeField] private bool warnIfMissing = true;

    /// <summary>Opens the panel. Hook to Button.onClick.</summary>
    public void Open()
    {
        if (!TryGetManager(out var manager)) return;

        if (!manager.Open(panelId) && warnIfMissing)
            Debug.LogWarning($"[UIPanelButton] No panel registered with id '{panelId}'.", this);
    }

    /// <summary>Closes the panel. Hook to Button.onClick.</summary>
    public void Close()
    {
        if (!TryGetManager(out var manager)) return;

        if (!manager.Close(panelId) && warnIfMissing)
            Debug.LogWarning($"[UIPanelButton] No panel registered with id '{panelId}'.", this);
    }

    /// <summary>Toggles the panel open/closed. Hook to Button.onClick.</summary>
    public void Toggle()
    {
        if (!TryGetManager(out var manager)) return;

        if (manager.TryGetPanel(panelId, out var panel))
            panel.Toggle();
        else if (warnIfMissing)
            Debug.LogWarning($"[UIPanelButton] No panel registered with id '{panelId}'.", this);
    }

    /// <summary>Opens a specific panel id, ignoring the serialized field. Useful for shared buttons.</summary>
    public void Open(string id)
    {
        if (!TryGetManager(out var manager)) return;

        if (!manager.Open(id) && warnIfMissing)
            Debug.LogWarning($"[UIPanelButton] No panel registered with id '{id}'.", this);
    }

    private bool TryGetManager(out UIPanelManager manager)
    {
        manager = UIPanelManager.Instance;

        if (manager == null && warnIfMissing)
            Debug.LogWarning("[UIPanelButton] UIPanelManager not found — is the UI scene loaded?", this);

        return manager != null;
    }
}
