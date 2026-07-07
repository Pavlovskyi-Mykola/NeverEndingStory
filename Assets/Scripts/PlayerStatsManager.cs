using System;
using UnityEngine;

public class PlayerStatsManager : MonoBehaviour
{
    public static PlayerStatsManager Instance { get; private set; }
    public static event Action<PlayerStatsManager> InstanceReady;

    [Header("Stats")]
    [SerializeField] private int money = 0;
    [SerializeField] private int influence = 1;
    [SerializeField] private int strategy = 1;
    [SerializeField] private int networking = 1;
    [SerializeField] private int reputation = 1;

    [Header("Energy")]
    [Tooltip("Design default for a fresh game. Runtime max can grow via AddMaxEnergy (promotions).")]
    [SerializeField] private int defaultMaxEnergy = 10;

    // Energy is a spend-and-refill resource (like Money), not a StatType progression
    // stat — it deliberately stays out of StatsSnapshot/quest requirements/toast deltas.
    private int maxEnergy;
    private int energy;

    public int Money => money;
    public int Influence => influence;
    public int Strategy => strategy;
    public int Networking => networking;
    public int Reputation => reputation;
    public int Energy => energy;
    public int MaxEnergy => maxEnergy;

    public event Action OnStatsChanged;
    public event Action OnEnergyChanged;

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        maxEnergy = Mathf.Max(1, defaultMaxEnergy);
        energy = maxEnergy;

        InstanceReady?.Invoke(this);
        RaiseChanged();
        RaiseEnergyChanged();
    }

    public PlayerStatsSave CaptureState()
    {
        return new PlayerStatsSave
        {
            money = money,
            influence = influence,
            strategy = strategy,
            networking = networking,
            reputation = reputation,
            energy = energy,
            maxEnergy = maxEnergy
        };
    }

    public void RestoreState(PlayerStatsSave data)
    {
        if (data == null) return;

        money = Mathf.Max(0, data.money);
        influence = Mathf.Max(0, data.influence);
        strategy = Mathf.Max(0, data.strategy);
        networking = Mathf.Max(0, data.networking);
        reputation = Mathf.Max(0, data.reputation);

        // -1 sentinels: fresh game (new PlayerStatsSave) and pre-energy saves both
        // fall back to the design default at full charge.
        maxEnergy = data.maxEnergy > 0 ? data.maxEnergy : Mathf.Max(1, defaultMaxEnergy);
        energy = data.energy >= 0 ? Mathf.Min(data.energy, maxEnergy) : maxEnergy;

        RaiseChanged();
        RaiseEnergyChanged();
    }

    public int Get(StatType stat) => stat switch
    {
        StatType.Money => money,
        StatType.Influence => influence,
        StatType.Strategy => strategy,
        StatType.Networking => networking,
        StatType.Reputation => reputation,
        _ => 0
    };

    /// <summary>
    /// Applies a signed delta to a stat, clamped at 0. Negative amounts work
    /// (e.g. quest/dialogue penalties); amount 0 is a no-op.
    /// </summary>
    public void Add(StatType stat, int amount)
    {
        if (amount == 0) return;

        switch (stat)
        {
            case StatType.Money:      money      = Mathf.Max(0, money + amount);      break;
            case StatType.Influence:  influence  = Mathf.Max(0, influence + amount);  break;
            case StatType.Strategy:   strategy   = Mathf.Max(0, strategy + amount);   break;
            case StatType.Networking: networking = Mathf.Max(0, networking + amount); break;
            case StatType.Reputation: reputation = Mathf.Max(0, reputation + amount); break;
            default: return;
        }

        RaiseChanged();
    }

    public void AddMoney(int amount) => Add(StatType.Money, amount);
    public void AddInfluence(int amount) => Add(StatType.Influence, amount);
    public void AddStrategy(int amount) => Add(StatType.Strategy, amount);
    public void AddNetworking(int amount) => Add(StatType.Networking, amount);
    public void AddReputation(int amount) => Add(StatType.Reputation, amount);

    public bool CanAfford(int cost) => cost <= money;

    /// <summary>Spends money only if affordable. Use for purchases; use Add for rewards/penalties.</summary>
    public bool TrySpendMoney(int cost)
    {
        if (cost <= 0) return true;
        if (money < cost) return false;

        money -= cost;
        RaiseChanged();
        return true;
    }

    // -----------------------
    // Energy
    // -----------------------

    public bool HasEnergy(int cost) => cost <= energy;

    public bool TrySpendEnergy(int cost)
    {
        if (cost <= 0) return true;
        if (energy < cost) return false;

        energy -= cost;
        RaiseEnergyChanged();
        return true;
    }

    public void RestoreEnergy(int amount)
    {
        if (amount <= 0) return;

        int next = Mathf.Min(maxEnergy, energy + amount);
        if (next == energy) return;

        energy = next;
        RaiseEnergyChanged();
    }

    public void RestoreEnergyFull()
    {
        if (energy == maxEnergy) return;

        energy = maxEnergy;
        RaiseEnergyChanged();
    }

    /// <summary>Grows (or shrinks) the max — e.g. +1 per promotion. Current energy is clamped, never refilled.</summary>
    public void AddMaxEnergy(int amount)
    {
        if (amount == 0) return;

        maxEnergy = Mathf.Max(1, maxEnergy + amount);
        energy = Mathf.Min(energy, maxEnergy);
        RaiseEnergyChanged();
    }

    private void RaiseEnergyChanged()
    {
        OnEnergyChanged?.Invoke();
    }

    private void RaiseChanged()
    {
        OnStatsChanged?.Invoke();

        GameEvents.RaiseStatsChanged(
            money,
            influence,
            strategy,
            networking,
            reputation);
    }
}
