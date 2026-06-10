using System;
using UnityEngine;

public class ActionService : MonoBehaviour
{
    public static ActionService Instance { get; private set; }

    public event Action OnActionStateChanged;

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        GameEvents.InventoryChanged += HandleInventoryChanged;
    }

    private void OnDisable()
    {
        GameEvents.InventoryChanged -= HandleInventoryChanged;
    }

    private void HandleInventoryChanged(GameEvents.InventoryChange _)
    {
        NotifyStateChanged();
    }

    public bool CanExecute(ActionDefinition action, out ActionFailReason reason)
    {
        if (action == null)
        {
            reason = ActionFailReason.NotAvailableHere;
            return false;
        }

        if (GameManager.Instance != null && GameManager.Instance.IsInDialogue)
        {
            reason = ActionFailReason.BlockedByDialogue;
            return false;
        }

        reason = ActionFailReason.None;

        var stats = PlayerStatsManager.Instance;
        if (stats == null)
        {
            reason = ActionFailReason.NotAvailableHere;
            return false;
        }

        var inventory = InventoryManager.Instance;

        if (action.RestrictByPhase && TimeManager.Instance != null)
        {
            var phase = TimeManager.Instance.TimeOfDay;
            bool ok = false;

            if (action.AllowedPhases != null)
            {
                for (int i = 0; i < action.AllowedPhases.Length; i++)
                {
                    if (action.AllowedPhases[i] == phase)
                    {
                        ok = true;
                        break;
                    }
                }
            }

            if (!ok)
            {
                reason = ActionFailReason.WrongTimePhase;
                return false;
            }
        }

        if (stats.Money < action.RequiredMoney)
        {
            reason = ActionFailReason.NotEnoughMoney;
            return false;
        }

        if (stats.Influence < action.RequiredInfluence)
        {
            reason = ActionFailReason.NotEnoughInfluence;
            return false;
        }

        if (stats.Strategy < action.RequiredStrategy)
        {
            reason = ActionFailReason.NotEnoughStrategy;
            return false;
        }

        if (stats.Networking < action.RequiredNetworking)
        {
            reason = ActionFailReason.NotEnoughNetworking;
            return false;
        }

        if (stats.Reputation < action.RequiredReputation)
        {
            reason = ActionFailReason.NotEnoughReputation;
            return false;
        }

        if (action.RequiredItems != null)
        {
            for (int i = 0; i < action.RequiredItems.Length; i++)
            {
                var req = action.RequiredItems[i];
                if (req == null || string.IsNullOrWhiteSpace(req.ItemId))
                    continue;

                int count = Mathf.Max(1, req.Count);

                if (inventory == null || !inventory.HasItem(req.ItemId, count))
                {
                    reason = ActionFailReason.MissingRequiredItem;
                    return false;
                }
            }
        }

        if (stats.Money < action.MoneyCost)
        {
            reason = ActionFailReason.NotEnoughMoney;
            return false;
        }

        if (action.ItemCosts != null)
        {
            for (int i = 0; i < action.ItemCosts.Length; i++)
            {
                var cost = action.ItemCosts[i];
                if (cost == null || string.IsNullOrWhiteSpace(cost.ItemId))
                    continue;

                int count = Mathf.Max(1, cost.Count);

                if (inventory == null || !inventory.HasItem(cost.ItemId, count))
                {
                    reason = ActionFailReason.MissingCostItem;
                    return false;
                }
            }
        }

        return true;
    }

    public bool Execute(ActionDefinition action, out ActionFailReason reason)
    {
        if (GameManager.Instance != null && GameManager.Instance.IsInDialogue)
        {
            reason = ActionFailReason.BlockedByDialogue;
            return false;
        }

        if (!CanExecute(action, out reason))
            return false;

        var stats = PlayerStatsManager.Instance;
        var inventory = InventoryManager.Instance;

        if (action.MoneyCost > 0)
            stats.TrySpendMoney(action.MoneyCost);

        if (action.ItemCosts != null && inventory != null)
        {
            for (int i = 0; i < action.ItemCosts.Length; i++)
            {
                var cost = action.ItemCosts[i];
                if (cost == null || string.IsNullOrWhiteSpace(cost.ItemId))
                    continue;

                inventory.TryConsume(cost.ItemId, Mathf.Max(1, cost.Count));
            }
        }

        if (action.MoneyReward > 0)
            stats.AddMoney(action.MoneyReward);

        if (action.InfluenceReward > 0)
            stats.AddInfluence(action.InfluenceReward);

        if (action.StrategyReward > 0)
            stats.AddStrategy(action.StrategyReward);

        if (action.NetworkingReward > 0)
            stats.AddNetworking(action.NetworkingReward);

        if (action.ReputationReward > 0)
            stats.AddReputation(action.ReputationReward);

        if (action.ItemRewards != null && inventory != null)
        {
            for (int i = 0; i < action.ItemRewards.Length; i++)
            {
                var reward = action.ItemRewards[i];
                if (reward == null || string.IsNullOrWhiteSpace(reward.ItemId))
                    continue;

                inventory.AddItem(reward.ItemId, Mathf.Max(1, reward.Count));
            }
        }

        if (action.TimeSkip == TimeSkipMode.NextPhase &&
            TimeManager.Instance != null)
        {
            TimeManager.Instance.AdvancePhase(TimeChangeSource.Action);
        }

        return true;
    }

    public void NotifyStateChanged()
    {
        OnActionStateChanged?.Invoke();
    }
}