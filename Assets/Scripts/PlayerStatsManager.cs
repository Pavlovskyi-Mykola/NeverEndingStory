using System;
using UnityEngine;

public class PlayerStatsManager : MonoBehaviour
{
    public static PlayerStatsManager Instance { get; private set; }

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

    public void AddMoney(int amount)
    {
        if (amount <= 0) return;
        money += amount;
        RaiseChanged();
    }

    public bool CanAfford(int cost) => cost <= money;

    public bool TrySpendMoney(int cost)
    {
        if (cost <= 0) return true;
        if (money < cost) return false;

        money -= cost;
        RaiseChanged();
        return true;
    }

    public void AddInfluence(int amount)
    {
        if (amount <= 0) return;
        influence += amount;
        RaiseChanged();
    }

    public void AddStrategy(int amount)
    {
        if (amount <= 0) return;
        strategy += amount;
        RaiseChanged();
    }

    public void AddNetworking(int amount)
    {
        if (amount <= 0) return;
        networking += amount;
        RaiseChanged();
    }

    public void AddReputation(int amount)
    {
        if (amount <= 0) return;
        reputation += amount;
        RaiseChanged();
    }

    public bool MeetsRequirements(
        int requiredMoney,
        int requiredInfluence,
        int requiredStrategy,
        int requiredNetworking,
        int requiredReputation)
    {
        if (money < requiredMoney) return false;
        if (influence < requiredInfluence) return false;
        if (strategy < requiredStrategy) return false;
        if (networking < requiredNetworking) return false;
        if (reputation < requiredReputation) return false;
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