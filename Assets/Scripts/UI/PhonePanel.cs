using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Two-state phone widget for the HUD.
///
///   Corner  — the default: small, docked bottom-left, showing the time/date screen.
///   Center  — opened: larger, centred, showing the apps screen.
///
/// Tapping the phone while in Corner opens it (Corner → Center, apps screen).
/// Escape, or a click outside the phone, while in Center returns it to Corner
/// (Center → Corner, time/date screen).
///
/// The component only swaps which screen is shown, drives an optional Animator
/// bool, and raises <see cref="onOpen"/>/<see cref="onClose"/> — hook your
/// appear/disappear animations to those events (or animate off the Animator bool).
/// It does not move or scale anything itself, so the visual transition is entirely
/// yours to author. Keep that animation on the always-on phone root, not on the
/// panel object, since the panel object deactivates the instant it closes.
///
/// Block-system integration (assign <see cref="panel"/>):
///   The centred phone is represented by a real <see cref="UIPanel"/> living on a
///   child that is active only while open. Opening = panel.Open(), closing =
///   panel.Close(); the component mirrors the panel's Opened/Closed events into its
///   visuals, so an external force-close (layer exclusivity, CloseAll) stays in
///   sync. Because it's a genuine UIPanel, gameplay blocking, Escape handling and
///   "closed/blocked by other panels" are governed by the UIPanel inspector — set
///   its Layer / Blocks Gameplay / Exclusive In Layer / Close On Escape there.
///
/// Self-managed fallback (no <see cref="panel"/> assigned):
///   The phone still opens/closes visually and consumes Escape via
///   UIPanelManager's interceptor stack, but does not block gameplay. Handy before
///   the UIPanel is wired in the scene.
/// </summary>
[DisallowMultipleComponent]
public sealed class PhonePanel : MonoBehaviour
{
    public enum PhoneState { Corner, Center }

    [Header("Panel-system integration")]
    [Tooltip("UIPanel representing the OPENED (centred) phone. Put it on a child that is active only while open. " +
             "Assign this to make the open phone a first-class panel — gameplay blocking, Escape, and exclusivity " +
             "are then governed by that UIPanel's own inspector fields. Leave empty for the self-managed fallback.")]
    [SerializeField] private UIPanel panel;

    [Header("Screens (swapped per state)")]
    [Tooltip("Shown while docked in the corner — the current time/date screen.")]
    [SerializeField] private GameObject cornerScreen;

    [Tooltip("Shown while opened in the centre — the apps screen.")]
    [SerializeField] private GameObject appsScreen;

    [Header("Interaction")]
    [Tooltip("Button that opens the phone. Put it on the corner widget; clicking it while docked opens the phone. Optional — you can also call Open() yourself.")]
    [SerializeField] private Button tapToOpenButton;

    [Tooltip("Full-screen raycast catcher enabled only while centred. Clicking it (outside the phone) closes the phone. Put a transparent Image with a Button on it, sitting behind the phone in the hierarchy. " +
             "If you assign a UIPanel with Blocks UI Behind + the manager backdrop, you can skip this and wire that backdrop to UIPanelManager.CloseTopmost instead.")]
    [SerializeField] private GameObject clickOutsideCatcher;

    [Tooltip("Self-managed mode only: Escape closes the phone while centred. When a UIPanel is assigned, Escape is governed by that UIPanel's own Close On Escape instead.")]
    [SerializeField] private bool closeOnEscape = true;

    [Header("Animation (optional)")]
    [Tooltip("Animator driven on state changes. Leave empty to rely purely on the events below. Keep it on the always-on phone root.")]
    [SerializeField] private Animator animator;

    [Tooltip("Bool parameter set true while centred, false while docked. Empty = don't touch the Animator.")]
    [SerializeField] private string openedBoolParam = "Opened";

    [Header("Events — attach your animations here")]
    [Tooltip("Corner → Center. Play the phone's 'slide up / grow' (appear) animation.")]
    public UnityEvent onOpen;

    [Tooltip("Center → Corner. Play the phone's 'shrink / dock' (disappear) animation.")]
    public UnityEvent onClose;

    public PhoneState State { get; private set; } = PhoneState.Corner;
    public bool IsOpen => State == PhoneState.Center;

    /// <summary>Raised after a state change is applied.</summary>
    public event Action<PhoneState> StateChanged;

    private bool _escapeRegistered;

    private void Awake()
    {
        if (tapToOpenButton != null)
        {
            tapToOpenButton.onClick.RemoveListener(Open);
            tapToOpenButton.onClick.AddListener(Open);
        }

        // If the click-outside catcher carries its own Button, auto-wire it so you
        // only have to drop the object in — no manual OnClick hookup needed.
        if (clickOutsideCatcher != null &&
            clickOutsideCatcher.TryGetComponent<Button>(out var catcherButton))
        {
            catcherButton.onClick.RemoveListener(Close);
            catcherButton.onClick.AddListener(Close);
        }
    }

    private void OnEnable()
    {
        // Panel mode: let the UIPanel drive our visuals so an external force-close
        // (exclusivity / CloseAll) keeps the phone's screens and catcher in sync.
        if (panel != null)
        {
            panel.Opened += HandlePanelOpened;
            panel.Closed += HandlePanelClosed;
        }
    }

    private void Start()
    {
        // Snap to the current state on load without firing open/close animations.
        bool startOpen = panel != null ? panel.IsOpen : false;
        ApplyState(startOpen ? PhoneState.Center : PhoneState.Corner, animate: false);
    }

    private void OnDisable()
    {
        if (panel != null)
        {
            panel.Opened -= HandlePanelOpened;
            panel.Closed -= HandlePanelClosed;
        }

        // Never leave a dangling interceptor if the HUD is torn down while centred.
        SetEscapeInterceptor(false);
    }

    private void OnDestroy()
    {
        if (tapToOpenButton != null)
            tapToOpenButton.onClick.RemoveListener(Open);

        if (clickOutsideCatcher != null &&
            clickOutsideCatcher.TryGetComponent<Button>(out var catcherButton))
            catcherButton.onClick.RemoveListener(Close);
    }

    // -----------------------
    // Public API (OnClick-friendly)
    // -----------------------

    /// <summary>Corner → Center. No-op if already centred.</summary>
    public void Open()
    {
        // Panel mode: activating the panel fires Opened → HandlePanelOpened → visuals.
        if (panel != null)
        {
            panel.Open();
            return;
        }

        if (State == PhoneState.Center) return;
        ApplyState(PhoneState.Center, animate: true);
    }

    /// <summary>Center → Corner. No-op if already docked.</summary>
    public void Close()
    {
        if (panel != null)
        {
            panel.Close();
            return;
        }

        if (State == PhoneState.Corner) return;
        ApplyState(PhoneState.Corner, animate: true);
    }

    public void Toggle()
    {
        bool open = panel != null ? panel.IsOpen : State == PhoneState.Center;
        if (open) Close();
        else Open();
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

        // Swap screens. The centred phone shows apps; the docked phone shows time/date.
        if (appsScreen != null && appsScreen.activeSelf != centred)
            appsScreen.SetActive(centred);
        if (cornerScreen != null && cornerScreen.activeSelf == centred)
            cornerScreen.SetActive(!centred);

        // The catcher only exists to swallow outside clicks while centred.
        if (clickOutsideCatcher != null && clickOutsideCatcher.activeSelf != centred)
            clickOutsideCatcher.SetActive(centred);

        // Self-managed Escape only — in panel mode the UIPanel/manager own Escape.
        if (panel == null)
            SetEscapeInterceptor(centred && closeOnEscape);

        if (animator != null && !string.IsNullOrEmpty(openedBoolParam))
            animator.SetBool(openedBoolParam, centred);

        if (animate)
        {
            if (centred) onOpen?.Invoke();
            else onClose?.Invoke();
        }

        StateChanged?.Invoke(next);
    }

    private void SetEscapeInterceptor(bool active)
    {
        var manager = UIPanelManager.Instance;
        if (manager == null || active == _escapeRegistered)
            return;

        if (active)
            manager.PushEscapeInterceptor(HandleEscape);
        else
            manager.RemoveEscapeInterceptor(HandleEscape);

        _escapeRegistered = active;
    }

    private bool HandleEscape()
    {
        if (State != PhoneState.Center)
            return false; // not our Escape to consume

        Close();
        return true; // consumed — don't let the manager close anything behind us
    }
}
