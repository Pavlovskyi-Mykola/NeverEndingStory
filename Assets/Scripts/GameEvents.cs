using System;

/// <summary>
/// A single, centralized event hub for cross-system game signals.
/// Keep this small and stable: quests, UI, analytics, etc. can listen here
/// without needing to subscribe to many different managers.
/// </summary>
public static class GameEvents
{
    // -----------------
    // Dialogue / NPC
    // -----------------

    /// <summary>Raised when a dialogue interaction with an NPC finishes.</summary>
    public static event Action<string, string> NpcTalked; // (npcId, dialogueId)

    public static void RaiseNpcTalked(string npcId, string dialogueId)
    {
        if (string.IsNullOrEmpty(npcId)) return;
        NpcTalked?.Invoke(npcId, dialogueId);
    }

    // -----------------
    // Location / Travel
    // -----------------

    /// <summary>Raised when the player enters (switches to) a location scene.</summary>
    public static event Action<string> LocationEntered; // (locationSceneName)

    public static void RaiseLocationEntered(string locationSceneName)
    {
        if (string.IsNullOrEmpty(locationSceneName)) return;
        LocationEntered?.Invoke(locationSceneName);
    }

    // -----------------
    // Time
    // -----------------

    public static event Action<DayOfWeek, TimeOfDay, TimeChangeSource> TimeChanged;

    public static void RaiseTimeChanged(DayOfWeek day, TimeOfDay phase, TimeChangeSource source)
    {
        TimeChanged?.Invoke(day, phase, source);
    }

    // -----------------
    // Player stats
    // -----------------

    public readonly struct StatsSnapshot
    {
        public readonly int Money;
        public readonly int Strength;
        public readonly int Intellect;

        public StatsSnapshot(int money, int strength, int intellect)
        {
            Money = money;
            Strength = strength;
            Intellect = intellect;
        }
    }


    // -----------------
    // Inventory
    // -----------------

    public readonly struct InventoryChange
    {
        public readonly string ItemId;
        public readonly int OldCount;
        public readonly int NewCount;
        public readonly InventoryManager Inventory;

        public InventoryChange(string itemId, int oldCount, int newCount, InventoryManager inventory)
        {
            ItemId = itemId;
            OldCount = oldCount;
            NewCount = newCount;
            Inventory = inventory;
        }
    }

    public static event Action<InventoryChange> InventoryChanged;

    public static void RaiseInventoryChanged(string itemId, int oldCount, int newCount, InventoryManager inventory)
    {
        InventoryChanged?.Invoke(new InventoryChange(itemId, oldCount, newCount, inventory));
    }

    // -----------------
    // World flags
    // -----------------

    public static event Action<StatsSnapshot> StatsChanged;

    public static void RaiseStatsChanged(int money, int strength, int intellect)
    {
        StatsChanged?.Invoke(new StatsSnapshot(money, strength, intellect));
    }

    //Now quests can re-check whenever flags change.
    public static event Action<string, bool> FlagChanged; // (key, value)

    public static void RaiseFlagChanged(string key, bool value)
    {
        if (string.IsNullOrEmpty(key)) return;
        FlagChanged?.Invoke(key, value);
    }
}