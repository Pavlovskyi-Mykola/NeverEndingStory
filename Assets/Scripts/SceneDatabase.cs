using System;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[CreateAssetMenu(fileName = "SceneDatabase", menuName = "Game/Scene Database")]
public class SceneDatabase : ScriptableObject
{
    private static SceneDatabase _instance;

    /// <summary>
    /// Global access to the single SceneDatabase asset.
    /// In Editor, lazily finds and loads it if not yet loaded.
    /// In runtime builds, this assumes the asset is referenced somewhere so Unity loads it.
    /// </summary>
    public static SceneDatabase Instance
    {
        get
        {
#if UNITY_EDITOR
            if (_instance == null)
            {
                var guids = AssetDatabase.FindAssets("t:SceneDatabase");
                if (guids != null && guids.Length > 0)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guids[0]);
                    _instance = AssetDatabase.LoadAssetAtPath<SceneDatabase>(path);
                }
            }
#endif
            return _instance;
        }
    }

    [Header("Core / Startup")]
    public SceneReference Bootstrap;
    public SceneReference UI;
    public SceneReference MainMenu;

    [Header("Safe Location (fallback)")]
    public SceneReference Home;

    [Header("Locations (with availability)")]
    public List<LocationEntry> Locations = new List<LocationEntry>();

    private Dictionary<string, LocationEntry> _bySceneName;

    private void OnEnable()
    {
        // Ensure Instance is set as soon as Unity loads this asset (Editor + Runtime).
        _instance = this;
        RebuildCache();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // Keep cache fresh while editing.
        // Also keep Instance stable if asset gets reloaded.
        _instance = this;
        RebuildCache();
    }
#endif

    private void RebuildCache()
    {
        _bySceneName = new Dictionary<string, LocationEntry>(StringComparer.Ordinal);

        foreach (var loc in Locations)
        {
            if (loc.Scene == null || !loc.Scene.IsValid) continue;

            var key = loc.Scene.SceneName;
            if (string.IsNullOrEmpty(key)) continue;

            // last one wins if duplicates exist
            _bySceneName[key] = loc;
        }
    }

    public bool TryGetLocation(SceneReference sceneRef, out LocationEntry entry)
    {
        entry = default;

        if (sceneRef == null || !sceneRef.IsValid) return false;
        if (_bySceneName == null) RebuildCache();

        return _bySceneName.TryGetValue(sceneRef.SceneName, out entry);
    }

    /// <summary>
    /// If a scene is not present in Locations list, we treat it as unrestricted
    /// (useful for UI/Bootstrap/MainMenu or any non-location scenes).
    ///
    /// If it IS present in Locations list, it is restricted-by-default and must explicitly allow day+phase.
    /// </summary>
    public bool IsAllowedNow(SceneReference sceneRef, DayOfWeek day, TimeOfDay phase)
    {
        if (!TryGetLocation(sceneRef, out var entry))
            return true;

        return entry.IsAllowed(day, phase);
    }
    public bool CanEnterNow(SceneReference sceneRef, DayOfWeek day, TimeOfDay phase, InventoryManager inventory)
    {
        if (!TryGetLocation(sceneRef, out var entry))
            return true;

        return entry.CanEnter(day, phase, inventory);
    }
}

[Serializable]
public struct LocationEntry
{
    public string Id;                 // optional: "home", "work_ff", etc.
    public SceneReference Scene;

    [Header("Availability (Option A)")]
    public DayPhaseMask AllowedPhases;     // Morning/Afternoon/Evening/Night
    public DayOfWeekMask AllowedDays;      // Mon..Sun

    [Header("Inventory Requirements")]
    public ItemAmount[] RequiredItems;

    // ✅ RESTRICTED BY DEFAULT:
    // - if entry exists but masks are None => location is CLOSED
    public bool IsAllowed(DayOfWeek day, TimeOfDay phase)
    {
        if (AllowedDays == DayOfWeekMask.None) return false;
        if (AllowedPhases == DayPhaseMask.None) return false;

        var dayOk = AllowedDays.HasFlag(DayOfWeekMaskExtensions.From(day));
        var phaseOk = AllowedPhases.HasFlag(DayPhaseMaskExtensions.From(phase));

        return dayOk && phaseOk;
    }

    public bool HasRequiredItems(InventoryManager inventory)
    {
        if (RequiredItems == null || RequiredItems.Length == 0)
            return true;

        if (inventory == null)
            return false;

        for (int i = 0; i < RequiredItems.Length; i++)
        {
            var req = RequiredItems[i];

            if (req == null || string.IsNullOrWhiteSpace(req.ItemId))
                continue;

            int count = req.Count <= 0 ? 1 : req.Count;

            if (!inventory.HasItem(req.ItemId, count))
                return false;
        }

        return true;
    }

    public bool CanEnter(DayOfWeek day, TimeOfDay phase, InventoryManager inventory)
    {
        return IsAllowed(day, phase) && HasRequiredItems(inventory);
    }

}
