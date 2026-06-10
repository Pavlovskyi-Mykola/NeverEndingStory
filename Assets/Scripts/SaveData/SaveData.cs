using System;
using System.Collections.Generic;

[Serializable]
public sealed class SaveData
{
    public int version = SaveVersions.Current;

    public string slotId;
    public string saveLabel;
    public string savedAtUtc;

    public PlayerStatsSave playerStats = new();
    public TimeSave time = new();
    public WorldSave world = new();
    public InventorySave inventory = new();
    public string currentLocationSceneName;

    public QuestJournal.Snapshot quests = new();
    public DialogueJournal.Snapshot dialogues = new();
    public CareerManager.Snapshot career = new CareerManager.Snapshot();
}

public static class SaveVersions
{
    public const int Initial = 1;
    public const int TrackedQuestAndSlots = 2;
    public const int CareerProgression = 3;
    public const int CorporateStats = 4;

    public const int Current = CorporateStats;
}

[Serializable]
public sealed class SaveSlotInfo
{
    public string slotId;
    public string filePath;
    public bool exists;
    public string savedAtUtc;
    public string currentLocationSceneName;
    public string trackedQuestId;
    public int version;
}

[Serializable]
public sealed class PlayerStatsSave
{
    public int money;
    public int influence;
    public int strategy;
    public int networking;
    public int reputation;
}

[Serializable]
public sealed class TimeSave
{
    public int dayOfWeek;
    public int timeOfDay;
}

[Serializable]
public sealed class InventorySave
{
    public List<InventoryEntrySave> items = new();
}

[Serializable]
public sealed class InventoryEntrySave
{
    public string itemId;
    public int count;
}

[Serializable]
public sealed class WorldSave
{
    public List<FlagEntrySave> flags = new();
}

[Serializable]
public sealed class FlagEntrySave
{
    public string key;
    public bool value;
}