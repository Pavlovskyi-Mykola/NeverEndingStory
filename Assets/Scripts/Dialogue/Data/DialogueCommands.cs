using System;
using UnityEngine;

public enum DialogueCommandType
{
    AddMoney,
    SpendMoney,
    AddInfluence,
    AddStrategy,
    AddNetworking,
    AddReputation,
    AdvanceTimePhase,
    SetFlag,
    AddItem,
    RemoveItem,
    ConsumeItem,
    StartQuest
}

[Serializable]
public class DialogueCommand
{
    public DialogueCommandType type;

    public int amount = 1;

    public string flagId;
    public bool flagValue;

    [ItemId] public string itemId;

    public string questId;

    public void Execute()
    {
        switch (type)
        {
            case DialogueCommandType.AddMoney:
                if (PlayerStatsManager.Instance != null)
                    PlayerStatsManager.Instance.AddMoney(amount);
                break;

            case DialogueCommandType.SpendMoney:
                if (PlayerStatsManager.Instance != null)
                    PlayerStatsManager.Instance.TrySpendMoney(amount);
                break;

            case DialogueCommandType.AddInfluence:
                if (PlayerStatsManager.Instance != null)
                    PlayerStatsManager.Instance.AddInfluence(amount);
                break;

            case DialogueCommandType.AddStrategy:
                if (PlayerStatsManager.Instance != null)
                    PlayerStatsManager.Instance.AddStrategy(amount);
                break;

            case DialogueCommandType.AddNetworking:
                if (PlayerStatsManager.Instance != null)
                    PlayerStatsManager.Instance.AddNetworking(amount);
                break;

            case DialogueCommandType.AddReputation:
                if (PlayerStatsManager.Instance != null)
                    PlayerStatsManager.Instance.AddReputation(amount);
                break;

            case DialogueCommandType.AdvanceTimePhase:
                if (TimeManager.Instance != null)
                    TimeManager.Instance.AdvancePhase(TimeChangeSource.Dialogue);
                break;

            case DialogueCommandType.SetFlag:
                if (WorldState.Instance != null && !string.IsNullOrWhiteSpace(flagId))
                    WorldState.Instance.SetFlag(flagId, flagValue);
                break;

            case DialogueCommandType.AddItem:
                if (InventoryManager.Instance != null && !string.IsNullOrWhiteSpace(itemId))
                    InventoryManager.Instance.AddItem(itemId, Mathf.Max(1, amount));
                break;

            case DialogueCommandType.RemoveItem:
                if (InventoryManager.Instance != null && !string.IsNullOrWhiteSpace(itemId))
                    InventoryManager.Instance.RemoveItem(itemId, Mathf.Max(1, amount));
                break;

            case DialogueCommandType.ConsumeItem:
                if (InventoryManager.Instance != null && !string.IsNullOrWhiteSpace(itemId))
                    InventoryManager.Instance.TryConsume(itemId, Mathf.Max(1, amount));
                break;

            case DialogueCommandType.StartQuest:
                if (QuestManager.Instance != null && !string.IsNullOrWhiteSpace(questId))
                    QuestManager.Instance.StartQuest(questId);
                break;
        }
    }
}