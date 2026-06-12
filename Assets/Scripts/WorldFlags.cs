using System;
using System.Collections.Generic;
using UnityEngine;

public static class WorldFlags
{
    // -------------------------
    // Known flag ids
    // Keep these searchable and grouped.
    // Format: "category.subject.state"
    // -------------------------

    public static class Dialogue
    {
        public const string MetBob = "dialogue.bob.met";
        public const string BobIntroSeen = "dialogue.bob.intro_seen";
    }

    public static class Location
    {
        public const string MarketUnlocked = "location.market.unlocked";
        public const string ForestUnlocked = "location.forest.unlocked";
    }

    public static class Npc
    {
        public const string BobMovedToPark = "npc.bob.moved_to_park";
    }

    public static class Quest
    {
        public const string AnnaStarted = "quest.anna.started";
        public const string AnnaCompleted = "quest.anna.completed";
    }

    public static class Event
    {
        public const string FirstRentPaid = "event.first_rent_paid";
    }

    // -------------------------
    // Runtime helper API
    // -------------------------

    private static HashSet<string> _knownIds;

    /// <summary>True when the id is declared as a const in this catalog. Used to catch typos in data-driven flag ids.</summary>
    public static bool IsKnown(string flagId)
    {
        if (string.IsNullOrWhiteSpace(flagId)) return false;

        if (_knownIds == null)
        {
            _knownIds = new HashSet<string>(StringComparer.Ordinal);
            CollectConstStrings(typeof(WorldFlags), _knownIds);
        }

        return _knownIds.Contains(flagId);
    }

    private static void CollectConstStrings(Type type, HashSet<string> results)
    {
        const System.Reflection.BindingFlags bindingFlags =
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Static |
            System.Reflection.BindingFlags.FlattenHierarchy;

        foreach (var field in type.GetFields(bindingFlags))
        {
            if (field.FieldType != typeof(string)) continue;
            if (!field.IsLiteral || field.IsInitOnly) continue;

            if (field.GetRawConstantValue() is string s && !string.IsNullOrWhiteSpace(s))
                results.Add(s);
        }

        foreach (var nested in type.GetNestedTypes(bindingFlags))
            CollectConstStrings(nested, results);
    }

    public static bool Has(string flagId)
    {
        return WorldState.Instance != null && WorldState.Instance.HasFlag(flagId);
    }

    public static bool Get(string flagId, bool defaultValue = false)
    {
        return WorldState.Instance != null && WorldState.Instance.GetFlag(flagId, defaultValue);
    }

    public static void Set(string flagId, bool value = true)
    {
        if (WorldState.Instance == null) return;
        WorldState.Instance.SetFlag(flagId, value);
    }

    public static void Clear(string flagId)
    {
        if (WorldState.Instance == null) return;
        WorldState.Instance.ClearFlag(flagId);
    }

    public static void Apply(IEnumerable<FlagChange> changes)
    {
        if (changes == null) return;

        foreach (var change in changes)
        {
            if (change == null) continue;
            change.Apply();
        }
    }
}

[Serializable]
public sealed class FlagChange
{
    [Tooltip("Use a centralized id from WorldFlags.cs. Example: location.market.unlocked")]
    public string FlagId;

    [Tooltip("Usually true. Use false when you want to clear/disable a world state.")]
    public bool Value = true;

    public void Apply()
    {
        if (string.IsNullOrWhiteSpace(FlagId)) return;
        WorldFlags.Set(FlagId, Value);
    }
}