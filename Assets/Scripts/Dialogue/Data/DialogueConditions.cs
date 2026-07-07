using System;
using System.Collections.Generic;
using UnityEngine;

public enum DialogueConditionType
{
    MoneyAtLeast,
    InfluenceAtLeast,
    StrategyAtLeast,
    NetworkingAtLeast,
    ReputationAtLeast,
    TimeOfDayIs,
    FlagIsTrue,
    RelationshipAtLeast
}

[Serializable]
public class DialogueCondition
{
    public DialogueConditionType type;

    public int intValue;
    public TimeOfDay timeOfDayValue;
    [FlagId] public string flagId;

    [Tooltip("RelationshipAtLeast: leave empty to check the NPC you're currently talking to.")]
    public string npcId;
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
                return PlayerStatsManager.Instance != null && PlayerStatsManager.Instance.Money >= c.intValue;

            case DialogueConditionType.InfluenceAtLeast:
                return PlayerStatsManager.Instance != null && PlayerStatsManager.Instance.Influence >= c.intValue;

            case DialogueConditionType.StrategyAtLeast:
                return PlayerStatsManager.Instance != null && PlayerStatsManager.Instance.Strategy >= c.intValue;

            case DialogueConditionType.NetworkingAtLeast:
                return PlayerStatsManager.Instance != null && PlayerStatsManager.Instance.Networking >= c.intValue;

            case DialogueConditionType.ReputationAtLeast:
                return PlayerStatsManager.Instance != null && PlayerStatsManager.Instance.Reputation >= c.intValue;

            case DialogueConditionType.TimeOfDayIs:
                return TimeManager.Instance != null && TimeManager.Instance.TimeOfDay == c.timeOfDayValue;

            case DialogueConditionType.FlagIsTrue:
                // An unset flag id fails the condition rather than silently passing.
                return !string.IsNullOrWhiteSpace(c.flagId) && WorldFlags.Get(c.flagId);

            case DialogueConditionType.RelationshipAtLeast:
                {
                    string npcId = !string.IsNullOrWhiteSpace(c.npcId)
                        ? c.npcId
                        : DialogueRunner.Instance != null ? DialogueRunner.Instance.CurrentNpcId : null;

                    return RelationshipManager.Instance != null &&
                           !string.IsNullOrEmpty(npcId) &&
                           RelationshipManager.Instance.GetLevel(npcId) >= c.intValue;
                }

            default:
                return true;
        }
    }
}