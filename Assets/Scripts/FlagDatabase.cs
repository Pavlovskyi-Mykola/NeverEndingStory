using System;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public enum FlagEntitySource
{
    ManualList = 0, // entities typed into the template (e.g. tutorial panel names)
    Npcs = 1,       // all NpcDefinition assets (NpcId)
    Locations = 2   // SceneDatabase locations (Id, or SceneName when Id is empty)
}

[CreateAssetMenu(fileName = "FlagDatabase", menuName = "Game/Flag Database")]
public class FlagDatabase : ScriptableObject
{
    private static FlagDatabase _instance;

    /// <summary>
    /// Global access to the single FlagDatabase asset.
    /// In Editor, lazily finds and loads it if not yet loaded.
    /// In runtime builds, this assumes the asset is referenced somewhere so Unity loads it.
    /// </summary>
    public static FlagDatabase Instance
    {
        get
        {
#if UNITY_EDITOR
            if (_instance == null)
            {
                var guids = AssetDatabase.FindAssets("t:FlagDatabase");
                if (guids != null && guids.Length > 0)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guids[0]);
                    _instance = AssetDatabase.LoadAssetAtPath<FlagDatabase>(path);
                }
            }
#endif
            return _instance;
        }
    }

    [Tooltip("Generated flags: one id per entity per state, as '<category>.<entity>.<state>'. " +
             "Entities come from the chosen source, so e.g. adding an NPC automatically adds its flags.")]
    public List<FlagCategoryTemplate> Templates = new();

    [Tooltip("Hand-made flags that don't fit a template — one-off story beats like 'event.first_rent_paid'.")]
    public List<FlagDefinition> Flags = new();

    private HashSet<string> _customIds;
    private HashSet<string> _generatedIds;
#if UNITY_EDITOR
    private double _generatedBuiltAt = double.NegativeInfinity;
#endif

    private void OnEnable()
    {
        // Ensure Instance is set as soon as Unity loads this asset (Editor + Runtime).
        _instance = this;
        RebuildCustomCache();
    }

#if UNITY_EDITOR
    private void Reset()
    {
        SeedDefaultTemplates();
    }

    private void OnValidate()
    {
        // Keep caches fresh while editing; keep Instance stable if asset gets reloaded.
        _instance = this;
        RebuildCustomCache();
        _generatedIds = null;
    }
#endif

    public bool IsKnown(string flagId)
    {
        if (string.IsNullOrWhiteSpace(flagId)) return false;

        if (_customIds == null) RebuildCustomCache();
        if (_customIds.Contains(flagId)) return true;

        EnsureGeneratedCache();
        return _generatedIds.Contains(flagId);
    }

    /// <summary>All valid flag ids: generated (templates × entities × states) plus custom ones.</summary>
    public List<string> GetAllFlagIds()
    {
        if (_customIds == null) RebuildCustomCache();
        EnsureGeneratedCache();

        var result = new List<string>(_generatedIds.Count + _customIds.Count);
        result.AddRange(_generatedIds);
        result.AddRange(_customIds);
        return result;
    }

    private void RebuildCustomCache()
    {
        _customIds = new HashSet<string>(StringComparer.Ordinal);

        if (Flags == null)
            return;

        for (int i = 0; i < Flags.Count; i++)
        {
            var def = Flags[i];
            if (def != null && !string.IsNullOrWhiteSpace(def.Id))
                _customIds.Add(def.Id.Trim());
        }
    }

    private void EnsureGeneratedCache()
    {
#if UNITY_EDITOR
        // Entity lists change as assets are edited; refresh at most every couple of seconds.
        double now = EditorApplication.timeSinceStartup;
        if (_generatedIds != null && now - _generatedBuiltAt < 2.0d)
            return;
        _generatedBuiltAt = now;
#else
        if (_generatedIds != null)
            return;
#endif

        _generatedIds = new HashSet<string>(StringComparer.Ordinal);

        if (Templates == null)
            return;

        var entities = new List<string>();

        for (int t = 0; t < Templates.Count; t++)
        {
            var template = Templates[t];
            if (template == null) continue;
            if (string.IsNullOrWhiteSpace(template.Category)) continue;
            if (template.States == null || template.States.Count == 0) continue;

            entities.Clear();
            CollectEntities(template, entities);

            string category = template.Category.Trim();

            for (int e = 0; e < entities.Count; e++)
            {
                string entity = entities[e];
                if (string.IsNullOrWhiteSpace(entity)) continue;
                entity = entity.Trim();

                for (int s = 0; s < template.States.Count; s++)
                {
                    string state = template.States[s];
                    if (string.IsNullOrWhiteSpace(state)) continue;

                    _generatedIds.Add($"{category}.{entity}.{state.Trim()}");
                }
            }
        }
    }

    private static void CollectEntities(FlagCategoryTemplate template, List<string> results)
    {
        switch (template.EntitySource)
        {
            case FlagEntitySource.ManualList:
                if (template.Entities != null)
                    results.AddRange(template.Entities);
                break;

            case FlagEntitySource.Npcs:
                CollectNpcIds(results);
                break;

            case FlagEntitySource.Locations:
                CollectLocationIds(results);
                break;
        }
    }

    private static void CollectNpcIds(List<string> results)
    {
#if UNITY_EDITOR
        // Asset scan: works in edit mode, where the scene-bound NpcManager does not exist.
        var guids = AssetDatabase.FindAssets("t:NpcDefinition");
        for (int i = 0; i < guids.Length; i++)
        {
            var def = AssetDatabase.LoadAssetAtPath<NpcDefinition>(AssetDatabase.GUIDToAssetPath(guids[i]));
            if (def != null && !string.IsNullOrWhiteSpace(def.NpcId))
                results.Add(def.NpcId);
        }
#else
        var nm = NpcManager.Instance;
        if (nm == null || nm.Npcs == null) return;

        for (int i = 0; i < nm.Npcs.Count; i++)
        {
            var def = nm.Npcs[i];
            if (def != null && !string.IsNullOrWhiteSpace(def.NpcId))
                results.Add(def.NpcId);
        }
#endif
    }

    private static void CollectLocationIds(List<string> results)
    {
        var db = SceneDatabase.Instance;
        if (db == null || db.Locations == null) return;

        for (int i = 0; i < db.Locations.Count; i++)
        {
            var loc = db.Locations[i];

            if (!string.IsNullOrWhiteSpace(loc.Id))
                results.Add(loc.Id);
            else if (loc.Scene != null && loc.Scene.IsValid)
                results.Add(loc.Scene.SceneName);
        }
    }

#if UNITY_EDITOR
    /// <summary>Editor-only: adds a custom flag id if not already known and keeps the list sorted. Caller handles Undo/dirty marking.</summary>
    public bool AddFlagEditor(string flagId)
    {
        flagId = flagId?.Trim();

        if (string.IsNullOrEmpty(flagId) || IsKnown(flagId))
            return false;

        Flags.Add(new FlagDefinition { Id = flagId });
        Flags.Sort((a, b) => string.CompareOrdinal(a?.Id, b?.Id));
        RebuildCustomCache();
        return true;
    }

    /// <summary>Editor-only: fills in the standard templates when the asset has none yet.</summary>
    public void SeedDefaultTemplates()
    {
        if (Templates != null && Templates.Count > 0)
            return;

        Templates = new List<FlagCategoryTemplate>
        {
            new FlagCategoryTemplate
            {
                Category = "npc",
                EntitySource = FlagEntitySource.Npcs,
                States = new List<string> { "met", "intro_seen", "befriended", "upset", "helped" },
                Description = "Social state per NPC. 'met' is set on first conversation."
            },
            new FlagCategoryTemplate
            {
                Category = "location",
                EntitySource = FlagEntitySource.Locations,
                States = new List<string> { "unlocked", "visited" },
                Description = "Discovery/unlock state per location."
            },
            new FlagCategoryTemplate
            {
                Category = "tutorial",
                EntitySource = FlagEntitySource.ManualList,
                Entities = new List<string> { "inventory", "journal", "save_load", "dialogue", "time" },
                States = new List<string> { "seen" },
                Description = "First-time hints for UI panels and features."
            }
        };
    }
#endif
}

[Serializable]
public sealed class FlagCategoryTemplate
{
    [Tooltip("First id segment, e.g. 'npc' generates npc.<entity>.<state>")]
    public string Category;

    [Tooltip("Where entities come from. Npcs/Locations read the real game data, so new content gets its flags automatically.")]
    public FlagEntitySource EntitySource = FlagEntitySource.ManualList;

    [Tooltip("Only used when Entity Source is Manual List.")]
    public List<string> Entities = new();

    [Tooltip("One flag per entity per state, e.g. 'met' generates npc.bob.met")]
    public List<string> States = new();

    [TextArea(1, 2)] public string Description;
}

[Serializable]
public sealed class FlagDefinition
{
    public string Id;
    [TextArea(1, 2)] public string Description;
}
