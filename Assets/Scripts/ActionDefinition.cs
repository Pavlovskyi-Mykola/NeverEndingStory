using UnityEngine;

public enum ActionFailReason
{
    None,
    NotEnoughMoney,
    NotEnoughStrength,
    NotEnoughIntellect,
    WrongTimePhase,
    NotAvailableHere
}

[CreateAssetMenu(fileName = "ActionDefinition", menuName = "Game/Action Definition")]
public class ActionDefinition : ScriptableObject
{
    [Header("UI")]
    public string Title;
    [TextArea] public string Description;

    [Header("Requirements")]
    public int RequiredMoney;      // if you want "must have at least"
    public int RequiredStrength;
    public int RequiredIntellect;

    [Header("Costs / Rewards")]
    public int MoneyCost;          // spend
    public int MoneyReward;        // earn
    public int StrengthReward;
    public int IntellectReward;

    [Header("Time Constraints")]
    public bool RestrictByPhase = false;
    public TimeOfDay[] AllowedPhases; // reuse your DayPhase enum

    // Later you can add: AllowedLocations, Cooldown, EnergyCost, etc.
}
