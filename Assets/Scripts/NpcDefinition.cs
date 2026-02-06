using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NpcDefinition", menuName = "Game/NPC Definition")]
public class NpcDefinition : ScriptableObject
{
    [Header("Identity")]
    public string NpcId;              // unique, stable id (e.g. "npc_anna")
    public string DisplayName;        // shown name (e.g. "Anna")

    [Header("Prefab")]
    public GameObject Prefab;         // visual instance for scenes

    [Header("Schedule")]
    public List<NpcScheduleEntry> Schedule = new List<NpcScheduleEntry>();

    /// <summary>
    /// Returns the first matching schedule entry for the given day/phase.
    /// If none matches -> NPC is absent.
    /// </summary>
    public bool TryGetScheduleFor(DayOfWeek day, TimeOfDay phase, out NpcScheduleEntry entry)
    {
        for (int i = 0; i < Schedule.Count; i++)
        {
            var e = Schedule[i];
            if (e.Matches(day, phase))
            {
                entry = e;
                return true;
            }
        }

        entry = default;
        return false;
    }
}

[Serializable]
public struct NpcScheduleEntry
{
    [Header("When")]
    public DayOfWeekMask Days;     // reuse your existing masks
    public DayPhaseMask Phases;

    [Header("Where")]
    public SceneReference LocationScene;   // scene = location (your current model)
    [SpawnPointKey]
    public string SpawnPointKey;           // e.g. "Door", "Table01"

    [Header("Optional")]
    public bool Absent; // if true, NPC is forced absent even if LocationScene is set

    public bool Matches(DayOfWeek day, TimeOfDay phase)
    {
        if (Absent) return false;
        if (Days == DayOfWeekMask.None) return false;
        if (Phases == DayPhaseMask.None) return false;

        var dayOk = Days.HasFlag(DayOfWeekMaskExtensions.From(day));
        var phaseOk = Phases.HasFlag(DayPhaseMaskExtensions.From(phase));
        return dayOk && phaseOk;
    }
}
