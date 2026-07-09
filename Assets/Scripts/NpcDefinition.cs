using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NpcDefinition", menuName = "Game/NPC Definition")]
public class NpcDefinition : ScriptableObject
{
    [Header("Identity")]
    public string NpcId;              // unique, stable id (e.g. "npc_anna")
    public string DisplayName;        // shown name (e.g. "Anna")
    [Tooltip("Portrait shown in UI — e.g. the phone Contacts app.")]
    public Sprite Portrait;

    [Header("Prefab")]
    public GameObject Prefab;         // visual instance for scenes

    [Header("Dialogue Routing")]
    [Tooltip("Which conversation this NPC has, by world state. Eligible rules play by tier/priority; if none resolve, Dialogue Fallback plays. Keep rules to SELECTION between whole conversations — put in-conversation logic in the graph's Branch nodes.")]
    public List<DialogueSelectorRule> DialogueRules = new();

    [Tooltip("Played when no rule resolves a graph.")]
    public DialogueGraph DialogueFallback;

    [Header("Dialogue Text Colors")]
    [Tooltip("Color of this NPC's dialogue body text.")]
    public Color DialogueTextColor = Color.white;
    [Tooltip("Color of this NPC's speaker name in the dialogue log.")]
    public Color DialogueNameColor = Color.white;

    // LEGACY — schedule merged into DialogueRules (per-rule Placement). Kept only
    // so old serialized data survives until the NPC Routing window migrates it.
    [HideInInspector]
    public List<NpcScheduleEntry> Schedule = new List<NpcScheduleEntry>();

    [Header("Relationship (0 = use RelationshipManager defaults)")]
    [Tooltip("Points needed per level for this NPC — higher = harder to win over.")]
    public int RelationshipPointsPerLevel = 0;
    public int RelationshipMaxLevel = 0;
}

/// <summary>LEGACY — replaced by DialogueSelectorRule.placement. Only read by the migration in the NPC Routing window.</summary>
[Serializable]
public struct NpcScheduleEntry
{
    public DayOfWeekMask Days;
    public DayPhaseMask Phases;
    public SceneReference LocationScene;
    public string SpawnPointKey;
    public bool Absent;
}
