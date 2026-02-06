using System;
using UnityEngine;

public class ActionService : MonoBehaviour
{
    public static ActionService Instance { get; private set; }

    public event Action OnActionStateChanged; // call when something changes that affects availability

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public bool CanExecute(ActionDefinition action, out ActionFailReason reason)
    {
        reason = ActionFailReason.None;

        var stats = PlayerStatsManager.Instance;
        if (stats == null) { reason = ActionFailReason.NotAvailableHere; return false; }

        // Phase restriction
        if (action.RestrictByPhase && TimeManager.Instance != null)
        {
            var phase = TimeManager.Instance.Phase;
            bool ok = false;
            for (int i = 0; i < action.AllowedPhases.Length; i++)
                if (action.AllowedPhases[i] == phase) { ok = true; break; }

            if (!ok) { reason = ActionFailReason.WrongTimePhase; return false; }
        }

        // Requirements
        if (stats.Money < action.RequiredMoney) { reason = ActionFailReason.NotEnoughMoney; return false; }
        if (stats.Strength < action.RequiredStrength) { reason = ActionFailReason.NotEnoughStrength; return false; }
        if (stats.Intellect < action.RequiredIntellect) { reason = ActionFailReason.NotEnoughIntellect; return false; }

        // Costs
        if (stats.Money < action.MoneyCost) { reason = ActionFailReason.NotEnoughMoney; return false; }

        return true;
    }

    public bool Execute(ActionDefinition action, out ActionFailReason reason)
    {
        if (!CanExecute(action, out reason))
            return false;

        var stats = PlayerStatsManager.Instance;

        // Pay costs
        if (action.MoneyCost > 0)
            stats.TrySpendMoney(action.MoneyCost);

        // Apply rewards
        if (action.MoneyReward > 0)
            stats.AddMoney(action.MoneyReward);
        if (action.StrengthReward > 0)
            stats.AddStrength(action.StrengthReward);
        if (action.IntellectReward > 0)
            stats.AddIntellect(action.IntellectReward);

        // Later: advance time phase, consume energy, etc.

        return true;
    }

    // Call this when time/location changes to force UI refresh
    public void NotifyStateChanged() => OnActionStateChanged?.Invoke();
}
