using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class SaveData
{
    public int version = 1;

    public PlayerStatsSave playerStats = new();
    public TimeSave time = new();
    public WorldSave world = new();
    public InventorySave inventory = new();
    public string currentLocationSceneName;

    public QuestJournal.Snapshot quests = new();
    public DialogueJournal.Snapshot dialogues = new();
}

[Serializable]
public sealed class PlayerStatsSave
{
    public int money;
    public int strength;
    public int intellect;
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