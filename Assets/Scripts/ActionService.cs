using System;
using UnityEngine;

public class ActionService : MonoBehaviour
{
    public static ActionService Instance { get; private set; }
    public static event Action<ActionService> InstanceReady;

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
        InstanceReady?.Invoke(this);
    }

    private void OnEnable()
    {
        // Single notification surface: everything that can change action
        // availability funnels into OnActionStateChanged, so buttons only need
        // to listen here.
        GameEvents.InventoryChanged += HandleInventoryChanged;
        GameEvents.StatsChanged += HandleStatsChanged;
        GameEvents.TimeChanged += HandleTimeChanged;
        UIPanelManager.GameplayBlockedChanged += HandleGameplayBlockedChanged;

        // Energy lives outside StatsSnapshot, so it needs its own hook for buttons
        // to gray out when it runs low.
        PlayerStatsManager.InstanceReady += HandleStatsManagerReady;
        if (PlayerStatsManager.Instance != null)
            HandleStatsManagerReady(PlayerStatsManager.Instance);
    }

    private void OnDisable()
    {
        GameEvents.InventoryChanged -= HandleInventoryChanged;
        GameEvents.StatsChanged -= HandleStatsChanged;
        GameEvents.TimeChanged -= HandleTimeChanged;
        UIPanelManager.GameplayBlockedChanged -= HandleGameplayBlockedChanged;

        PlayerStatsManager.InstanceReady -= HandleStatsManagerReady;
        if (PlayerStatsManager.Instance != null)
            PlayerStatsManager.Instance.OnEnergyChanged -= NotifyStateChanged;
    }

    private void HandleStatsManagerReady(PlayerStatsManager stats)
    {
        stats.OnEnergyChanged -= NotifyStateChanged;
        stats.OnEnergyChanged += NotifyStateChanged;
    }

    private void HandleGameplayBlockedChanged(bool blocked)
    {
        NotifyStateChanged();
    }

    private void HandleInventoryChanged(GameEvents.InventoryChange _)
    {
        NotifyStateChanged();
    }

    private void HandleStatsChanged(GameEvents.StatsSnapshot _)
    {
        NotifyStateChanged();
    }

    private void HandleTimeChanged(DayOfWeek day, TimeOfDay phase, TimeChangeSource source)
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

        if (UIPanelManager.IsGameplayBlocked)
        {
            reason = ActionFailReason.BlockedByUI;
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

        for (int i = 0; i < StatTypes.All.Length; i++)
        {
            var stat = StatTypes.All[i];
            if (stats.Get(stat) < action.GetRequirement(stat))
            {
                reason = StatToFailReason(stat);
                return false;
            }
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

        if (action.EnergyCost > 0 && stats.Energy < action.EnergyCost)
        {
            reason = ActionFailReason.NotEnoughEnergy;
            return false;
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
        if (!CanExecute(action, out reason))
            return false;

        var stats = PlayerStatsManager.Instance;
        var inventory = InventoryManager.Instance;

        // CanExecute just verified affordability in this same frame, so these
        // should never fail — if one does, something changed state in between.
        if (action.MoneyCost > 0 && !stats.TrySpendMoney(action.MoneyCost))
            Debug.LogWarning($"[ActionService] '{action.name}': money spend failed after CanExecute passed.");

        if (action.EnergyCost > 0 && !stats.TrySpendEnergy(action.EnergyCost))
            Debug.LogWarning($"[ActionService] '{action.name}': energy spend failed after CanExecute passed.");

        if (action.ItemCosts != null && inventory != null)
        {
            for (int i = 0; i < action.ItemCosts.Length; i++)
            {
                var cost = action.ItemCosts[i];
                if (cost == null || string.IsNullOrWhiteSpace(cost.ItemId))
                    continue;

                if (!inventory.TryConsume(cost.ItemId, Mathf.Max(1, cost.Count)))
                    Debug.LogWarning($"[ActionService] '{action.name}': consuming '{cost.ItemId}' failed after CanExecute passed.");
            }
        }

        for (int i = 0; i < StatTypes.All.Length; i++)
        {
            var stat = StatTypes.All[i];
            int reward = action.GetReward(stat);
            if (reward > 0) stats.Add(stat, reward);
        }

        if (action.EnergyRestore > 0)
            stats.RestoreEnergy(action.EnergyRestore);

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

    private static ActionFailReason StatToFailReason(StatType stat) => stat switch
    {
        StatType.Money      => ActionFailReason.NotEnoughMoney,
        StatType.Influence  => ActionFailReason.NotEnoughInfluence,
        StatType.Strategy   => ActionFailReason.NotEnoughStrategy,
        StatType.Networking => ActionFailReason.NotEnoughNetworking,
        StatType.Reputation => ActionFailReason.NotEnoughReputation,
        _ => ActionFailReason.NotAvailableHere
    };
}