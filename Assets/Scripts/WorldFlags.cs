using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Static facade over WorldState boolean flags.
/// Flag ids are declared in the FlagDatabase asset (Assets > Create > Game > Flag Database)
/// and picked via dropdowns in the editor — adding a flag needs no code change.
/// Convention for ids: "category.subject.state", e.g. location.market.unlocked
///
/// For code access, typo-proof constants are generated into WorldFlags.Generated.cs
/// (Tools > Game > Generate World Flag Constants, or the button on the FlagDatabase asset),
/// e.g. WorldFlags.Npc.Bob.Met
/// </summary>
public static partial class WorldFlags
{
    /// <summary>True when the id is declared in the FlagDatabase asset. Used to catch typos in data-driven flag ids.</summary>
    public static bool IsKnown(string flagId)
    {
        var db = FlagDatabase.Instance;
        return db != null && db.IsKnown(flagId);
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
    [Tooltip("Pick an id declared in the FlagDatabase asset. Example: location.market.unlocked")]
    [FlagId] public string FlagId;

    [Tooltip("Usually true. Use false when you want to clear/disable a world state.")]
    public bool Value = true;

    public void Apply()
    {
        if (string.IsNullOrWhiteSpace(FlagId)) return;
        WorldFlags.Set(FlagId, Value);
    }
}
