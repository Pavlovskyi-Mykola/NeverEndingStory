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
        GameEvents.LocationEntered += HandleLocationEntered;
        UIPanelManager.GameplayBlockedChanged += HandleGameplayBlockedChanged;

        // Energy lives outside StatsSnapshot, so it needs its own hook for buttons
        // to gray out when it runs low.
        PlayerStatsManager.InstanceReady += HandleStatsManagerReady;
        if (PlayerStatsManager.Instance != null)
            HandleStatsManagerReady(PlayerStatsManager.Instance);

        // Quest-gated actions need refreshing when quest state moves.
        QuestManager.InstanceReady += HandleQuestManagerReady;
        if (QuestManager.Instance != null)
            HandleQuestManagerReady(QuestManager.Instance);
    }

    private void OnDisable()
    {
        GameEvents.InventoryChanged -= HandleInventoryChanged;
        GameEvents.StatsChanged -= HandleStatsChanged;
        GameEvents.TimeChanged -= HandleTimeChanged;
        GameEvents.LocationEntered -= HandleLocationEntered;
        UIPanelManager.GameplayBlockedChanged -= HandleGameplayBlockedChanged;

        PlayerStatsManager.InstanceReady -= HandleStatsManagerReady;
        if (PlayerStatsManager.Instance != null)
            PlayerStatsManager.Instance.OnEnergyChanged -= NotifyStateChanged;

        QuestManager.InstanceReady -= HandleQuestManagerReady;
        if (QuestManager.Instance != null)
            QuestManager.Instance.OnQuestStateChanged -= NotifyStateChanged;
    }

    private void HandleStatsManagerReady(PlayerStatsManager stats)
    {
        stats.OnEnergyChanged -= NotifyStateChanged;
        stats.OnEnergyChanged += NotifyStateChanged;
    }

    private void HandleQuestManagerReady(QuestManager qm)
    {
        qm.OnQuestStateChanged -= NotifyStateChanged;
        qm.OnQuestStateChanged += NotifyStateChanged;
    }

    private void HandleLocationEntered(string locationSceneName)
    {
        NotifyStateChanged();
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

        if (action.AllowedLocations != null && action.AllowedLocations.Length > 0 &&
            !IsAtAllowedLocation(action))
        {
            reason = ActionFailReason.NotAtLocation;
            return false;
        }

        if (!string.IsNullOrWhiteSpace(action.RequiredActiveQuestId) &&
            !IsRequiredQuestActive(action))
        {
            reason = ActionFailReason.RequiredQuestNotActive;
            return false;
        }

        if (action.MiniGame != null &&
            (MiniGameHost.Instance == null || MiniGameHost.Instance.IsRunning))
        {
            reason = ActionFailReason.MiniGameUnavailable;
            return false;
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

        if (action.MiniGame != null)
        {
            var host = MiniGameHost.Instance;
            if (host == null ||
                !host.TryLaunch(action.MiniGame, action, result => HandleMiniGameFinished(action, result)))
            {
                reason = ActionFailReason.MiniGameUnavailable;
                return false;
            }

            // Attempting is the work: costs are paid up front; rewards and the
            // time skip resolve in HandleMiniGameFinished based on the result.
            SpendCosts(action);
            return true;
        }

        SpendCosts(action);
        GrantRewards(action, 1f);
        ApplyTimeSkip(action);
        return true;
    }

    private void HandleMiniGameFinished(ActionDefinition action, MiniGameResult result)
    {
        if (result.Success)
            GrantRewards(action, action.GetRewardMultiplier(result.Tier));

        if (action.MiniGame != null)
            GameEvents.RaiseMiniGameCompleted(action.MiniGame.MiniGameId, result.Tier);

        // The phase passes whether you aced it or flopped.
        ApplyTimeSkip(action);
        NotifyStateChanged();
    }

    private static void SpendCosts(ActionDefinition action)
    {
        var stats = PlayerStatsManager.Instance;
        var inventory = InventoryManager.Instance;

        // CanExecute verified affordability this frame, so these should never
        // fail — if one does, something changed state in between.
        if (action.MoneyCost > 0 && stats != null && !stats.TrySpendMoney(action.MoneyCost))
            Debug.LogWarning($"[ActionService] '{action.name}': money spend failed after CanExecute passed.");

        if (action.EnergyCost > 0 && stats != null && !stats.TrySpendEnergy(action.EnergyCost))
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
    }

    private static void GrantRewards(ActionDefinition action, float statMultiplier)
    {
        var stats = PlayerStatsManager.Instance;
        var inventory = InventoryManager.Instance;

        if (stats != null)
        {
            for (int i = 0; i < StatTypes.All.Length; i++)
            {
                var stat = StatTypes.All[i];
                int reward = action.GetReward(stat);
                if (reward <= 0) continue;

                int scaled = Mathf.RoundToInt(reward * statMultiplier);
                if (scaled > 0) stats.Add(stat, scaled);
            }

            if (action.EnergyRestore > 0)
                stats.RestoreEnergy(action.EnergyRestore);
        }

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

        // Intel/state output — only reached on success (GrantRewards is skipped
        // when a mini-game fails), so failed espionage yields no flags.
        if (action.SuccessFlags != null && action.SuccessFlags.Length > 0)
            WorldFlags.Apply(action.SuccessFlags);
    }

    private static void ApplyTimeSkip(ActionDefinition action)
    {
        if (action.TimeSkip == TimeSkipMode.NextPhase && TimeManager.Instance != null)
            TimeManager.Instance.AdvancePhase(TimeChangeSource.Action);
    }

    private static bool IsAtAllowedLocation(ActionDefinition action)
    {
        var gm = GameManager.Instance;
        if (gm == null || gm.CurrentLocationRef == null || !gm.CurrentLocationRef.IsValid)
            return false;

        string sceneName = gm.CurrentLocationRef.SceneName;

        string locationId = null;
        var db = SceneDatabase.Instance;
        if (db != null && db.TryGetLocation(gm.CurrentLocationRef, out var entry))
            locationId = entry.Id;

        for (int i = 0; i < action.AllowedLocations.Length; i++)
        {
            string allowed = action.AllowedLocations[i];
            if (string.IsNullOrWhiteSpace(allowed))
                continue;

            if (string.Equals(allowed, sceneName, StringComparison.OrdinalIgnoreCase))
                return true;

            if (!string.IsNullOrEmpty(locationId) &&
                string.Equals(allowed, locationId, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static bool IsRequiredQuestActive(ActionDefinition action)
    {
        var journal = QuestJournal.Instance;
        if (journal == null || !journal.IsActive(action.RequiredActiveQuestId))
            return false;

        if (string.IsNullOrWhiteSpace(action.RequiredActiveStepId))
            return true;

        var qm = QuestManager.Instance;
        if (qm == null || !qm.TryGetDefinition(action.RequiredActiveQuestId, out var def) ||
            def == null || def.Steps == null)
            return false;

        var prog = journal.GetOrCreateProgress(action.RequiredActiveQuestId);
        if (prog == null)
            return false;

        int idx = prog.CurrentStepIndex;
        if (idx < 0 || idx >= def.Steps.Count)
            return false;

        var step = def.Steps[idx];
        return step != null && string.Equals(step.StepId, action.RequiredActiveStepId, StringComparison.Ordinal);
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