using System;
using UnityEngine;

public class PlayerStatsManager : MonoBehaviour
{
    public static PlayerStatsManager Instance { get; private set; }

    [Header("Stats")]
    [SerializeField] private int money = 0;
    [SerializeField] private int strength = 1;
    [SerializeField] private int intellect = 1;

    public int Money => money;
    public int Strength => strength;
    public int Intellect => intellect;

    /// <summary>
    /// Fired whenever any stat changes.
    /// </summary>
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

        // Push initial state to listeners (UI can safely subscribe later too)
        RaiseChanged();
    }

    // -------------------------
    // Money
    // -------------------------
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

    // -------------------------
    // Strength / Intellect
    // -------------------------
    public void AddStrength(int amount)
    {
        if (amount <= 0) return;
        strength += amount;
        RaiseChanged();
    }

    public void AddIntellect(int amount)
    {
        if (amount <= 0) return;
        intellect += amount;
        RaiseChanged();
    }

    // -------------------------
    // Requirements (for gating actions)
    // -------------------------
    public bool MeetsRequirements(int requiredMoney, int requiredStrength, int requiredIntellect)
    {
        if (money < requiredMoney) return false;
        if (strength < requiredStrength) return false;
        if (intellect < requiredIntellect) return false;
        return true;
    }

    private void RaiseChanged()
    {
        OnStatsChanged?.Invoke();
        GameEvents.RaiseStatsChanged(money, strength, intellect);
    }
}
