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

    /// <summary>
    /// Raised when a finished dialogue was marked as relationship-building
    /// (DialogueGraph.CountsAsRelationshipTalk). Subset of NpcTalked.
    /// </summary>
    public static event Action<string> RelationshipTalk; // (npcId)

    public static void RaiseRelationshipTalk(string npcId)
    {
        if (string.IsNullOrEmpty(npcId)) return;
        RelationshipTalk?.Invoke(npcId);
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
        public readonly int Influence;
        public readonly int Strategy;
        public readonly int Networking;
        public readonly int Reputation;

        public StatsSnapshot(int money, int influence, int strategy, int networking, int reputation)
        {
            Money = money;
            Influence = influence;
            Strategy = strategy;
            Networking = networking;
            Reputation = reputation;
        }
    }

    public static event Action<StatsSnapshot> StatsChanged;

    public static void RaiseStatsChanged(int money, int influence, int strategy, int networking, int reputation)
    {
        StatsChanged?.Invoke(new StatsSnapshot(money, influence, strategy, networking, reputation));
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
    // Mini games
    // -----------------

    /// <summary>Raised when a mini-game finishes (any outcome, including Failed).</summary>
    public static event Action<string, MiniGameTier> MiniGameCompleted; // (miniGameId, tier)

    public static void RaiseMiniGameCompleted(string miniGameId, MiniGameTier tier)
    {
        if (string.IsNullOrEmpty(miniGameId)) return;
        MiniGameCompleted?.Invoke(miniGameId, tier);
    }

    // -----------------
    // World flags
    // -----------------

    public static event Action<string, bool> FlagChanged; // (key, value)

    public static void RaiseFlagChanged(string key, bool value)
    {
        if (string.IsNullOrEmpty(key)) return;
        FlagChanged?.Invoke(key, value);
    }
}