using System;
using System.Collections.Generic;
using UnityEngine;

public enum DialogueConditionType
{
    // NOTE: values are serialized by index — only APPEND new entries, never reorder.
    // The "AtLeast" suffix on the numeric types is legacy naming; the actual operator
    // is now chosen by DialogueCondition.comparison (defaults to AtLeast for old data).
    MoneyAtLeast,
    InfluenceAtLeast,
    StrategyAtLeast,
    NetworkingAtLeast,
    ReputationAtLeast,
    TimeOfDayIs,
    FlagIsTrue,
    RelationshipAtLeast,
    FlagIsFalse,
    HasItem,
    QuestStateIs,
    HasSeenDialogue
}

/// <summary>How a numeric condition compares the live value against its target.</summary>
public enum DialogueComparison
{
    AtLeast, // >=
    AtMost,  // <=
    Equals   // ==
}

[Serializable]
public class DialogueCondition
{
    public DialogueConditionType type;

    [Tooltip("Operator for the numeric conditions (stats, relationship level, item count).")]
    public DialogueComparison comparison = DialogueComparison.AtLeast;

    public int intValue;
    public TimeOfDay timeOfDayValue;
    [FlagId] public string flagId;

    [Tooltip("Relationship: leave empty to check the NPC you're currently talking to.")]
    public string npcId;

    [ItemId] public string itemId;

    [QuestId] public string questId;
    public QuestRouteState questState = QuestRouteState.Active;

    [Tooltip("HasSeenDialogue: the conversation that must (or must not) have been seen.")]
    public DialogueGraph seenDialogue;
}

[Serializable]
public class DialogueConditionGroup
{
    [SerializeField] private List<DialogueCondition> all = new();

    public IReadOnlyList<DialogueCondition> All => all;

    public bool Evaluate()
    {
        for (int i = 0; i < all.Count; i++)
        {
            if (!EvaluateSingle(all[i]))
                return false;
        }

        return true;
    }

    private static bool EvaluateSingle(DialogueCondition c)
    {
        switch (c.type)
        {
            case DialogueConditionType.MoneyAtLeast:
                return PlayerStatsManager.Instance != null &&
                       Compare(PlayerStatsManager.Instance.Money, c.comparison, c.intValue);

            case DialogueConditionType.InfluenceAtLeast:
                return PlayerStatsManager.Instance != null &&
                       Compare(PlayerStatsManager.Instance.Influence, c.comparison, c.intValue);

            case DialogueConditionType.StrategyAtLeast:
                return PlayerStatsManager.Instance != null &&
                       Compare(PlayerStatsManager.Instance.Strategy, c.comparison, c.intValue);

            case DialogueConditionType.NetworkingAtLeast:
                return PlayerStatsManager.Instance != null &&
                       Compare(PlayerStatsManager.Instance.Networking, c.comparison, c.intValue);

            case DialogueConditionType.ReputationAtLeast:
                return PlayerStatsManager.Instance != null &&
                       Compare(PlayerStatsManager.Instance.Reputation, c.comparison, c.intValue);

            case DialogueConditionType.TimeOfDayIs:
                return TimeManager.Instance != null && TimeManager.Instance.TimeOfDay == c.timeOfDayValue;

            case DialogueConditionType.FlagIsTrue:
                // An unset flag id fails the condition rather than silently passing.
                return !string.IsNullOrWhiteSpace(c.flagId) && WorldFlags.Get(c.flagId);

            case DialogueConditionType.FlagIsFalse:
                // Unset flags read as false, so this also covers "flag was never set".
                return !string.IsNullOrWhiteSpace(c.flagId) && !WorldFlags.Get(c.flagId);

            case DialogueConditionType.RelationshipAtLeast:
                {
                    string npcId = !string.IsNullOrWhiteSpace(c.npcId)
                        ? c.npcId
                        : DialogueRunner.Instance != null ? DialogueRunner.Instance.CurrentNpcId : null;

                    return RelationshipManager.Instance != null &&
                           !string.IsNullOrEmpty(npcId) &&
                           Compare(RelationshipManager.Instance.GetLevel(npcId), c.comparison, c.intValue);
                }

            case DialogueConditionType.HasItem:
                return InventoryManager.Instance != null &&
                       !string.IsNullOrWhiteSpace(c.itemId) &&
                       Compare(InventoryManager.Instance.GetCount(c.itemId), c.comparison, c.intValue);

            case DialogueConditionType.QuestStateIs:
                {
                    if (string.IsNullOrWhiteSpace(c.questId))
                        return false;

                    var journal = QuestJournal.Instance;
                    QuestRouteState actual;
                    if (journal == null) actual = QuestRouteState.NotStarted;
                    else if (journal.IsActive(c.questId)) actual = QuestRouteState.Active;
                    else if (journal.IsCompleted(c.questId)) actual = QuestRouteState.Completed;
                    else actual = QuestRouteState.NotStarted;

                    return c.questState == QuestRouteState.Any || actual == c.questState;
                }

            case DialogueConditionType.HasSeenDialogue:
                return c.seenDialogue != null &&
                       DialogueJournal.Instance != null &&
                       DialogueJournal.Instance.HasSeenDialogue(c.seenDialogue.EffectiveDialogueId);

            default:
                return true;
        }
    }

    private static bool Compare(int actual, DialogueComparison cmp, int target)
    {
        switch (cmp)
        {
            case DialogueComparison.AtMost: return actual <= target;
            case DialogueComparison.Equals: return actual == target;
            default: return actual >= target; // AtLeast
        }
    }
}