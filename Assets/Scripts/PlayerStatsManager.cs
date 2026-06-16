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

    public int Money => money;
    public int Influence => influence;
    public int Strategy => strategy;
    public int Networking => networking;
    public int Reputation => reputation;

    public event Action OnStatsChanged;

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
        RaiseChanged();
    }

    public PlayerStatsSave CaptureState()
    {
        return new PlayerStatsSave
        {
            money = money,
            influence = influence,
            strategy = strategy,
            networking = networking,
            reputation = reputation
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

        RaiseChanged();
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
