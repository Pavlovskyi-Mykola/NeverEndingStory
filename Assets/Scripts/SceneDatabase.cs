using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "SceneDatabase", menuName = "Game/Scene Database")]
public class SceneDatabase : ScriptableObject
{
    [Header("Core / Startup")]
    public SceneReference Bootstrap;
    public SceneReference UI;
    public SceneReference MainMenu;

    [Header("Safe Location (fallback)")]
    public SceneReference Home;

    [Header("Locations (with availability)")]
    public List<LocationEntry> Locations = new List<LocationEntry>();

    private Dictionary<string, LocationEntry> _bySceneName;

    private void OnEnable() => RebuildCache();
#if UNITY_EDITOR
    private void OnValidate() => RebuildCache();
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
}

[Serializable]
public struct LocationEntry
{
    public string Id;                 // optional: "home", "work_ff", etc.
    public SceneReference Scene;

    [Header("Availability (Option A)")]
    public DayPhaseMask AllowedPhases;     // Morning/Afternoon/Evening/Night
    public DayOfWeekMask AllowedDays;      // Mon..Sun

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
}
