using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Two-state phone widget for the HUD. One always-visible phone object that your
/// animation moves between its docked corner spot and the screen centre:
///
///   Corner  — the default: docked bottom-left. The phone body shows its base
///             face (time/date), which lives permanently on the phone root.
///   Center  — opened: moved up/centred, with the apps screen active on top.
///
/// The opened state is a real <see cref="UIPanel"/> sitting ON the apps-screen
/// child (active only while open). This component is a thin bridge: tap →
/// panel.Open(); the panel's Opened/Closed events drive an optional Animator bool
/// and the <see cref="onOpen"/>/<see cref="onClose"/> events your travel animation
/// hooks into. Everything policy-shaped — gameplay blocking, Escape, exclusivity,
/// click-outside-closes — is governed by the UIPanel's own inspector:
/// set Blocks Gameplay / Close On Escape there, and for click-outside enable
/// Blocks UI Behind and wire the manager's backdrop Button to
/// UIPanelManager.CloseTopmost.
///
/// Because every close path (Escape, backdrop click, another exclusive panel,
/// CloseAll) funnels through the panel's Closed event, the phone always animates
/// back down no matter why it closed. The apps screen deactivating instantly on
/// close reveals the base face while the phone slides down — intended.
///
/// Keep the Animator on this always-active phone root, not on the apps screen —
/// that object deactivates the moment the close starts.
/// </summary>
[DisallowMultipleComponent]
public sealed class PhonePanel : MonoBehaviour
{
    public enum PhoneState { Corner, Center }

    [Header("Panel")]
    [Tooltip("UIPanel ON the apps-screen child (active only while open). Gameplay blocking, Escape, exclusivity and click-outside (Blocks UI Behind + manager backdrop → CloseTopmost) are governed by its inspector.")]
    [SerializeField] private UIPanel panel;

    [Header("Interaction")]
    [Tooltip("Button that opens the phone — the tap area on the docked phone body. Optional; you can also call Open() yourself.")]
    [SerializeField] private Button tapToOpenButton;

    [Header("Animation (optional)")]
    [Tooltip("Animator driven on state changes — put it on this always-active phone root so the close animation isn't cut off. Leave empty to rely purely on the events below.")]
    [SerializeField] private Animator animator;

    [Tooltip("Bool parameter set true while centred, false while docked. Empty = don't touch the Animator.")]
    [SerializeField] private string openedBoolParam = "Opened";

    [Header("Events — attach your animations here")]
    [Tooltip("Corner → Center. Play the phone's 'move up to centre' animation.")]
    public UnityEvent onOpen;

    [Tooltip("Center → Corner. Play the phone's 'return to corner' animation.")]
    public UnityEvent onClose;

    public PhoneState State { get; private set; } = PhoneState.Corner;
    public bool IsOpen => State == PhoneState.Center;

    /// <summary>Raised after a state change is applied.</summary>
    public event Action<PhoneState> StateChanged;

    private void Awake()
    {
        if (panel == null)
            Debug.LogWarning("[PhonePanel] No UIPanel assigned — the phone can't open. Put a UIPanel on the apps-screen child and assign it.", this);

        if (tapToOpenButton != null)
        {
            tapToOpenButton.onClick.RemoveListener(Open);
            tapToOpenButton.onClick.AddListener(Open);
        }
    }

    private void OnEnable()
    {
        // The UIPanel drives our state, so an external force-close (Escape,
        // backdrop click, exclusivity, CloseAll) also brings the phone back down.
        if (panel != null)
        {
            panel.Opened += HandlePanelOpened;
            panel.Closed += HandlePanelClosed;
        }
    }

    private void Start()
    {
        // Snap to the current state on load without firing open/close animations.
        ApplyState(panel != null && panel.IsOpen ? PhoneState.Center : PhoneState.Corner, animate: false);
    }

    private void OnDisable()
    {
        if (panel != null)
        {
            panel.Opened -= HandlePanelOpened;
            panel.Closed -= HandlePanelClosed;
        }
    }

    private void OnDestroy()
    {
        if (tapToOpenButton != null)
            tapToOpenButton.onClick.RemoveListener(Open);
    }

    // -----------------------
    // Public API (OnClick-friendly)
    // -----------------------

    /// <summary>Corner → Center. No-op if already centred.</summary>
    public void Open()
    {
        if (panel != null)
            panel.Open(); // Opened event → HandlePanelOpened → state + animation
    }

    /// <summary>Center → Corner. No-op if already docked.</summary>
    public void Close()
    {
        if (panel != null)
            panel.Close();
    }

    public void Toggle()
    {
        if (panel == null) return;

        if (panel.IsOpen) panel.Close();
        else panel.Open();
    }

    // -----------------------
    // Panel-driven callbacks
    // -----------------------

    private void HandlePanelOpened(UIPanel _) => ApplyState(PhoneState.Center, animate: true);
    private void HandlePanelClosed(UIPanel _) => ApplyState(PhoneState.Corner, animate: true);

    // -----------------------
    // Internals
    // -----------------------

    private void ApplyState(PhoneState next, bool animate)
    {
        State = next;
        bool centred = next == PhoneState.Center;

        if (animator != null && !string.IsNullOrEmpty(openedBoolParam))
            animator.SetBool(openedBoolParam, centred);

        if (animate)
        {
            if (centred) onOpen?.Invoke();
            else onClose?.Invoke();
        }

        StateChanged?.Invoke(next);
    }
}
