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

    public DialogueRouteSet Routes;

    [Header("Schedule")]
    public List<NpcScheduleEntry> Schedule = new List<NpcScheduleEntry>();

    public bool TryGetScheduleForLocation(DayOfWeek day, TimeOfDay phase, SceneReference location, out NpcScheduleEntry entry)
    {
        entry = default;

        if (location == null || !location.IsValid)
            return false;

        for (int i = 0; i < Schedule.Count; i++)
        {
            var e = Schedule[i];
            if (e.Absent) continue;

            if (!e.MatchesTime(day, phase))
                continue;

            if (!SceneRefEquals(e.LocationScene, location))
                continue;

            entry = e;
            return true;
        }

        return false;
    }

    private static bool SceneRefEquals(SceneReference a, SceneReference b)
    {
        if (a == null || b == null) return false;
        if (!a.IsValid || !b.IsValid) return false;
        return a.SceneName == b.SceneName;
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

    [Header("Dialogue")]
    public DialogueGraph Dialogue; // conversation to use at this time/location

    [Header("Optional")]
    public bool Absent; // if true, NPC is forced absent even if LocationScene is set

    public bool MatchesTime(DayOfWeek day, TimeOfDay phase)
    {
        bool dayOk = (Days & DayOfWeekMaskExtensions.From(day)) != 0;
        bool phaseOk = (Phases & DayPhaseMaskExtensions.From(phase)) != 0;
        return dayOk && phaseOk;
    }
}
