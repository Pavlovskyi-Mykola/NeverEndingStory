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
    BlockedByUI,
    NotEnoughEnergy,
    NotAtLocation,
    RequiredQuestNotActive,
    MiniGameUnavailable
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

    [Tooltip("Scene names or SceneDatabase location ids where this action is available. Empty = anywhere.")]
    public string[] AllowedLocations = Array.Empty<string>();

    [Tooltip("Only available while this quest is active. Empty = always available.")]
    [QuestId] public string RequiredActiveQuestId;
    [Tooltip("Optional: the quest must also be on this exact step id.")]
    public string RequiredActiveStepId;

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

    [Header("Energy")]
    [Tooltip("Energy spent on execute. Rough scale: heavy work 4, standard 2, light 1, free 0.")]
    public int EnergyCost = 0;
    [Tooltip("Energy restored on execute (e.g. Drink Coffee). Applied after the cost, clamped at max.")]
    public int EnergyRestore = 0;

    [Header("Rewards")]
    public int MoneyReward = 0;
    public int InfluenceReward = 0;
    public int StrategyReward = 0;
    public int NetworkingReward = 0;
    public int ReputationReward = 0;
    public ItemAmount[] ItemRewards = Array.Empty<ItemAmount>();

    [Tooltip("World flags set when the action succeeds (any non-failed tier for mini-game actions) — e.g. intel.bob.expense_fraud after a successful audit.")]
    public FlagChange[] SuccessFlags = Array.Empty<FlagChange>();

    [Header("Time")]
    public TimeSkipMode TimeSkip = TimeSkipMode.None;

    [Header("Mini Game (optional)")]
    [Tooltip("When set, executing launches this mini-game: costs are paid up front, rewards apply on completion scaled by the result tier, and the time skip runs afterward regardless of outcome.")]
    public MiniGameDefinition MiniGame;
    [Tooltip("Stat-reward multiplier per tier. Item rewards and Energy Restore are granted in full on any success; nothing is granted on a fail.")]
    public float BronzeRewardMultiplier = 0.5f;
    public float SilverRewardMultiplier = 1f;
    public float GoldRewardMultiplier = 1.5f;

    public float GetRewardMultiplier(MiniGameTier tier) => tier switch
    {
        MiniGameTier.Bronze => BronzeRewardMultiplier,
        MiniGameTier.Silver => SilverRewardMultiplier,
        MiniGameTier.Gold   => GoldRewardMultiplier,
        _ => 0f
    };

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