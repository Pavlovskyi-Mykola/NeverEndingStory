using System;
using System.Collections.Generic;
using UnityEngine;

public enum DialogueConditionType
{
    MoneyAtLeast,
    StrengthAtLeast,
    IntellectAtLeast,
    TimeOfDayIs,
    FlagIsTrue,
    HasItem,
    ItemCountAtLeast
}

[Serializable]
public class DialogueCondition
{
    public DialogueConditionType type;

    // Generic fields
    public int intValue;
    public TimeOfDay timeOfDayValue;
    public string stringValue; // flag id or item id
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
                       PlayerStatsManager.Instance.Money >= c.intValue;

            case DialogueConditionType.StrengthAtLeast:
                return PlayerStatsManager.Instance != null &&
                       PlayerStatsManager.Instance.Strength >= c.intValue;

            case DialogueConditionType.IntellectAtLeast:
                return PlayerStatsManager.Instance != null &&
                       PlayerStatsManager.Instance.Intellect >= c.intValue;

            case DialogueConditionType.TimeOfDayIs:
                return TimeManager.Instance != null &&
                       TimeManager.Instance.TimeOfDay == c.timeOfDayValue;

            case DialogueConditionType.FlagIsTrue:
                return WorldState.Instance != null &&
                       WorldState.Instance.HasFlag(c.stringValue);

            case DialogueConditionType.HasItem:
                return InventoryManager.Instance != null &&
                       InventoryManager.Instance.HasItem(c.stringValue);

            case DialogueConditionType.ItemCountAtLeast:
                return InventoryManager.Instance != null &&
                       InventoryManager.Instance.GetCount(c.stringValue) >= c.intValue;

            default:
                return true;
        }
    }
}