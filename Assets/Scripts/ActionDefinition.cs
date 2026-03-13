using System;
using UnityEngine;

public enum ActionFailReason
{
    None,
    BlockedByDialogue,
    NotAvailableHere,
    WrongTimePhase,
    NotEnoughMoney,
    NotEnoughStrength,
    NotEnoughIntellect,
    MissingRequiredItem,
    MissingCostItem
}

public enum TimeSkipMode
{
    None,
    NextPhase
}

[CreateAssetMenu(fileName = "Action", menuName = "Game/Actions/Action Definition")]
public class ActionDefinition : ScriptableObject
{
    [Header("Identity")]
    public string ActionId;
    public string DisplayName;
    [TextArea(2, 5)] public string Description;

    [Header("Availability")]
    public bool RestrictByPhase = false;
    public TimeOfDay[] AllowedPhases = Array.Empty<TimeOfDay>();

    [Header("Stat Requirements")]
    public int RequiredMoney = 0;
    public int RequiredStrength = 0;
    public int RequiredIntellect = 0;

    [Header("Item Requirements (must have, not consumed)")]
    public ItemAmount[] RequiredItems = Array.Empty<ItemAmount>();

    [Header("Costs")]
    public int MoneyCost = 0;
    public ItemAmount[] ItemCosts = Array.Empty<ItemAmount>();

    [Header("Rewards")]
    public int MoneyReward = 0;
    public int StrengthReward = 0;
    public int IntellectReward = 0;
    public ItemAmount[] ItemRewards = Array.Empty<ItemAmount>();

    [Header("Time")]
    public TimeSkipMode TimeSkip = TimeSkipMode.None;
}