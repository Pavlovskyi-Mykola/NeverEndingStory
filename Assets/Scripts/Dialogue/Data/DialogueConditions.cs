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
    FlagIsTrue
}

[Serializable]
public class DialogueCondition
{
    public DialogueConditionType type;

    public int intValue;
    public TimeOfDay timeOfDayValue;
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

            default:
                return true;
        }
    }
}