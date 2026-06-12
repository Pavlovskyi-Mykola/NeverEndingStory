using System;
using UnityEngine;

public enum ActionFailReason
{
    None,
    BlockedByDialogue,
    NotAvailableHere,
    WrongTimePhase,
    NotEnoughMoney,
    NotEnoughInfluence,
    NotEnoughStrategy,
    NotEnoughNetworking,
    NotEnoughReputation,
    MissingRequiredItem,
    MissingCostItem,
    BlockedByUI
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
    public int RequiredInfluence = 0;
    public int RequiredStrategy = 0;
    public int RequiredNetworking = 0;
    public int RequiredReputation = 0;

    [Header("Item Requirements (must have, not consumed)")]
    public ItemAmount[] RequiredItems = Array.Empty<ItemAmount>();

    [Header("Costs")]
    public int MoneyCost = 0;
    public ItemAmount[] ItemCosts = Array.Empty<ItemAmount>();

    [Header("Rewards")]
    public int MoneyReward = 0;
    public int InfluenceReward = 0;
    public int StrategyReward = 0;
    public int NetworkingReward = 0;
    public int ReputationReward = 0;
    public ItemAmount[] ItemRewards = Array.Empty<ItemAmount>();

    [Header("Time")]
    public TimeSkipMode TimeSkip = TimeSkipMode.None;

    public int GetRequirement(StatType stat) => stat switch
    {
        StatType.Money      => RequiredMoney,
        StatType.Influence  => RequiredInfluence,
        StatType.Strategy   => RequiredStrategy,
        StatType.Networking => RequiredNetworking,
        StatType.Reputation => RequiredReputation,
        _ => 0
    };

    public int GetReward(StatType stat) => stat switch
    {
        StatType.Money      => MoneyReward,
        StatType.Influence  => InfluenceReward,
        StatType.Strategy   => StrategyReward,
        StatType.Networking => NetworkingReward,
        StatType.Reputation => ReputationReward,
        _ => 0
    };
}