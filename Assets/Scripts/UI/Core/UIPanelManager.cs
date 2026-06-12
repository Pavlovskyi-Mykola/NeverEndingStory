using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Central coordinator for UIPanels. Lives on an always-active object in the UI scene.
/// Tracks open panels in open order, enforces layer exclusivity, maintains hide-groups,
/// the gameplay-block flag and an optional raycast-blocking backdrop.
/// Panels report themselves via OnEnable/OnDisable, so plain SetActive calls stay valid.
/// </summary>
public sealed class UIPanelManager : MonoBehaviour
{
    public static UIPanelManager Instance { get; private set; }

    [Header("Backdrop (optional)")]
    [Tooltip("Full-screen raycast-blocking image. Placed directly behind the topmost open panel that has 'Blocks UI Behind'. Leave empty to disable.")]
    [SerializeField] private GameObject backdrop;

    [Header("Input")]
    [Tooltip("Escape closes the topmost open panel (if that panel allows it).")]
    [SerializeField] private bool handleEscapeKey = true;

    private readonly Dictionary<string, UIPanel> _registry = new();
    private readonly List<UIPanel> _openPanels = new(); // open order: first = bottom, last = top
    private HashSet<string> _hiddenGroups = new();
    private bool _gameplayBlocked;

    // Panels whose OnEnable ran before the manager woke up.
    private static readonly List<UIPanel> PendingShown = new();

    /// <summary>(groupId, hidden) — raised when a hide-group starts/stops being hidden.</summary>
    public static event Action<string, bool> HideGroupChanged;

    /// <summary>Raised when the gameplay-blocking state flips.</summary>
    public static event Action<bool> GameplayBlockedChanged;

    /// <summary>True while any open panel has BlocksGameplay. Gameplay systems (movement, NPC interaction, hotkeys) should early-out on this.</summary>
    public static bool IsGameplayBlocked => Instance != null && Instance._gameplayBlocked;

    public static bool IsGroupHidden(string groupId) =>
        Instance != null && !string.IsNullOrEmpty(groupId) && Instance._hiddenGroups.Contains(groupId);

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (backdrop != null)
            backdrop.SetActive(false);

        RegisterScenePanels();

        for (int i = 0; i < PendingShown.Count; i++)
        {
            var panel = PendingShown[i];
            if (panel != null && panel.IsOpen)
                HandleShown(panel);
        }
        PendingShown.Clear();
    }

    private void OnEnable() => SceneManager.sceneLoaded += HandleSceneLoaded;
    private void OnDisable() => SceneManager.sceneLoaded -= HandleSceneLoaded;

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        if (handleEscapeKey && Input.GetKeyDown(KeyCode.Escape))
            CloseTopmost();
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode) => RegisterScenePanels();

    private void RegisterScenePanels()
    {
        var panels = FindObjectsByType<UIPanel>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < panels.Length; i++)
            Register(panels[i]);
    }

    public void Register(UIPanel panel)
    {
        if (panel == null) return;
        _registry[panel.PanelId] = panel;
    }

    public void Unregister(UIPanel panel)
    {
        if (panel == null) return;

        if (_registry.TryGetValue(panel.PanelId, out var existing) && existing == panel)
            _registry.Remove(panel.PanelId);

        if (_openPanels.Remove(panel))
            RecomputeState();
    }

    public bool TryGetPanel(string panelId, out UIPanel panel) =>
        _registry.TryGetValue(panelId, out panel) && panel != null;

    public bool Open(string panelId)
    {
        if (!TryGetPanel(panelId, out var panel)) return false;
        panel.Open();
        return true;
    }

    public bool Close(string panelId)
    {
        if (!TryGetPanel(panelId, out var panel)) return false;
        panel.Close();
        return true;
    }

    public UIPanel Topmost => _openPanels.Count > 0 ? _openPanels[_openPanels.Count - 1] : null;

    public int OpenCount => _openPanels.Count;

    public bool IsAnyOpen(UIPanelLayer layer)
    {
        for (int i = 0; i < _openPanels.Count; i++)
        {
            if (_openPanels[i] != null && _openPanels[i].Layer == layer)
                return true;
        }
        return false;
    }

    /// <summary>Closes the topmost open panel if it allows Escape-closing. Also usable as a backdrop Button handler.</summary>
    public bool CloseTopmost()
    {
        var top = Topmost;
        if (top == null || !top.CloseOnEscape)
            return false;

        top.Close();
        return true;
    }

    public void CloseAll(UIPanelLayer? layer = null)
    {
        for (int i = _openPanels.Count - 1; i >= 0; i--)
        {
            var panel = _openPanels[i];
            if (panel != null && (!layer.HasValue || panel.Layer == layer.Value))
                panel.Close(); // removes itself from the list via NotifyHidden

            if (i > _openPanels.Count)
                i = _openPanels.Count;
        }
    }

    internal static void NotifyShown(UIPanel panel)
    {
        if (panel == null) return;

        if (Instance == null)
        {
            if (!PendingShown.Contains(panel))
                PendingShown.Add(panel);
            return;
        }

        Instance.HandleShown(panel);
    }

    internal static void NotifyHidden(UIPanel panel)
    {
        if (panel == null || Instance == null) return;
        Instance.HandleHidden(panel);
    }

    internal static void NotifyDestroyed(UIPanel panel)
    {
        if (panel == null || Instance == null) return;
        Instance.Unregister(panel);
    }

    private void HandleShown(UIPanel panel)
    {
        Register(panel);

        // Re-shown while already tracked: move to top of the stack.
        _openPanels.Remove(panel);

        if (panel.ExclusiveInLayer && panel.Layer != UIPanelLayer.System)
        {
            for (int i = _openPanels.Count - 1; i >= 0; i--)
            {
                var other = _openPanels[i];
                if (other != null && other != panel && other.Layer == panel.Layer)
                    other.Close(); // removes itself from the list via NotifyHidden

                if (i > _openPanels.Count)
                    i = _openPanels.Count;
            }
        }

        _openPanels.Add(panel);
        RecomputeState();
    }

    private void HandleHidden(UIPanel panel)
    {
        if (_openPanels.Remove(panel))
            RecomputeState();
    }

    private void RecomputeState()
    {
        _openPanels.RemoveAll(p => p == null);

        bool blocked = false;
        for (int i = 0; i < _openPanels.Count; i++)
        {
            if (_openPanels[i].BlocksGameplay)
            {
                blocked = true;
                break;
            }
        }

        if (blocked != _gameplayBlocked)
        {
            _gameplayBlocked = blocked;
            GameplayBlockedChanged?.Invoke(blocked);
        }

        var desired = new HashSet<string>();
        for (int i = 0; i < _openPanels.Count; i++)
        {
            var groups = _openPanels[i].HideGroupsWhileOpen;
            for (int g = 0; g < groups.Count; g++)
            {
                if (!string.IsNullOrEmpty(groups[g]))
                    desired.Add(groups[g]);
            }
        }

        if (!desired.SetEquals(_hiddenGroups))
        {
            var previous = _hiddenGroups;
            _hiddenGroups = desired; // swap before raising so IsGroupHidden is consistent in callbacks

            foreach (var group in desired)
            {
                if (!previous.Contains(group))
                    HideGroupChanged?.Invoke(group, true);
            }

            foreach (var group in previous)
            {
                if (!desired.Contains(group))
                    HideGroupChanged?.Invoke(group, false);
            }
        }

        UpdateBackdrop();
    }

    private void UpdateBackdrop()
    {
        if (backdrop == null) return;

        UIPanel top = null;
        for (int i = _openPanels.Count - 1; i >= 0; i--)
        {
            if (_openPanels[i].BlocksUIBehind)
            {
                top = _openPanels[i];
                break;
            }
        }

        if (top == null)
        {
            backdrop.SetActive(false);
            return;
        }

        var backdropTransform = backdrop.transform;
        var panelTransform = top.transform;

        if (backdropTransform.parent != panelTransform.parent)
            backdropTransform.SetParent(panelTransform.parent, false);

        // SetSiblingIndex removes the transform before re-inserting it: when the backdrop
        // currently sits before the panel, the panel shifts down by one on removal —
        // compensate, or the backdrop lands after the panel and renders on top of it.
        int targetIndex = panelTransform.GetSiblingIndex();
        if (backdropTransform.GetSiblingIndex() < targetIndex)
            targetIndex -= 1;

        backdropTransform.SetSiblingIndex(targetIndex);
        backdrop.SetActive(true);
    }
}
