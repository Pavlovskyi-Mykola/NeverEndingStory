using System;
using UnityEngine;

public enum DialogueCommandType
{
    AddMoney,
    SpendMoney,
    AddStrength,
    AddIntellect,
    AdvanceTimePhase,
    SetFlag,
    AddItem,
    RemoveItem,
    ConsumeItem
}

[Serializable]
public class DialogueCommand
{
    public DialogueCommandType type;

    public int intValue;
    public bool boolValue;     // flag value
    public string flagId;

    [ItemId] public string itemId;

    public void Execute()
    {
        switch (type)
        {
            case DialogueCommandType.AddMoney:
                if (PlayerStatsManager.Instance != null)
                    PlayerStatsManager.Instance.AddMoney(intValue);
                break;

            case DialogueCommandType.SpendMoney:
                if (PlayerStatsManager.Instance != null)
                    PlayerStatsManager.Instance.TrySpendMoney(intValue);
                break;

            case DialogueCommandType.AddStrength:
                if (PlayerStatsManager.Instance != null)
                    PlayerStatsManager.Instance.AddStrength(intValue);
                break;

            case DialogueCommandType.AddIntellect:
                if (PlayerStatsManager.Instance != null)
                    PlayerStatsManager.Instance.AddIntellect(intValue);
                break;

            case DialogueCommandType.AdvanceTimePhase:
                if (TimeManager.Instance != null)
                    TimeManager.Instance.AdvancePhase(TimeChangeSource.Dialogue);
                break;

            case DialogueCommandType.SetFlag:
                if (WorldState.Instance != null)
                    WorldState.Instance.SetFlag(flagId, boolValue);
                break;

            case DialogueCommandType.AddItem:
                if (InventoryManager.Instance != null)
                    InventoryManager.Instance.AddItem(itemId, Mathf.Max(1, intValue));
                break;

            case DialogueCommandType.RemoveItem:
                if (InventoryManager.Instance != null)
                    InventoryManager.Instance.RemoveItem(itemId, Mathf.Max(1, intValue));
                break;

            case DialogueCommandType.ConsumeItem:
                if (InventoryManager.Instance != null)
                    InventoryManager.Instance.TryConsume(itemId, Mathf.Max(1, intValue));
                break;
        }
    }
}