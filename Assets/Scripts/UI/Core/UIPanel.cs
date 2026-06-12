using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public enum UIPanelLayer
{
    HUD = 0,     // always-on overlays (tracked quest, stats, time)
    Window = 10, // regular panels (inventory, journal, character sheet)
    Popup = 20,  // small popups on top of windows (item details, context menus)
    Modal = 30,  // must be resolved before anything else (dialogue, save/load, confirmations)
    System = 40  // loading screens, faders — never closed by Escape or exclusivity
}

/// <summary>
/// Attach to a panel root GameObject (e.g. PANEL_Inventory). Reports open/close to
/// UIPanelManager via OnEnable/OnDisable, so plain SetActive calls from existing
/// scripts keep working — no special open API is required.
/// </summary>
[DisallowMultipleComponent]
public sealed class UIPanel : MonoBehaviour
{
    [Header("Identity")]
    [Tooltip("Unique id. Defaults to the GameObject name when empty.")]
    [SerializeField] private string panelId;
    [SerializeField] private UIPanelLayer layer = UIPanelLayer.Window;

    [Header("Behaviour")]
    [Tooltip("Opening this panel closes other open panels on the same layer.")]
    [SerializeField] private bool exclusiveInLayer = true;

    [Tooltip("While open, gameplay input should be ignored. Systems check UIPanelManager.IsGameplayBlocked.")]
    [SerializeField] private bool blocksGameplay = true;

    [Tooltip("While open and topmost, the manager's raycast-blocking backdrop is placed behind this panel so UI underneath can't be clicked.")]
    [SerializeField] private bool blocksUIBehind = false;

    [Tooltip("Escape closes this panel when it is the topmost open panel.")]
    [SerializeField] private bool closeOnEscape = true;

    [Header("Hide Groups (optional)")]
    [Tooltip("UIVisibilityGroup elements with these group ids are hidden while this panel is open and restored when it closes.")]
    [SerializeField] private List<string> hideGroupsWhileOpen = new();

    [Header("Events")]
    public UnityEvent onOpened;
    public UnityEvent onClosed;

    public string PanelId => string.IsNullOrEmpty(panelId) ? name : panelId;
    public UIPanelLayer Layer => layer;
    public bool ExclusiveInLayer => exclusiveInLayer;
    public bool BlocksGameplay => blocksGameplay;
    public bool BlocksUIBehind => blocksUIBehind;
    public bool CloseOnEscape => closeOnEscape;
    public IReadOnlyList<string> HideGroupsWhileOpen => hideGroupsWhileOpen;

    public bool IsOpen => gameObject.activeInHierarchy;

    public event Action<UIPanel> Opened;
    public event Action<UIPanel> Closed;

    public void Open() => gameObject.SetActive(true);
    public void Close() => gameObject.SetActive(false);
    public void Toggle() => gameObject.SetActive(!gameObject.activeSelf);

    private void OnEnable()
    {
        UIPanelManager.NotifyShown(this);
        Opened?.Invoke(this);
        onOpened?.Invoke();
    }

    private void OnDisable()
    {
        UIPanelManager.NotifyHidden(this);
        Closed?.Invoke(this);
        onClosed?.Invoke();
    }

    private void OnDestroy()
    {
        UIPanelManager.NotifyDestroyed(this);
    }
}
