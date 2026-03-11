using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class QuestRewardDefinition
{
    [Header("Stats")]
    public int Money;
    public int Strength;
    public int Intellect;

    [Header("World Flags")]
    public List<FlagChange> Flags = new();

    [Header("Time")]
    public bool AdvanceTimePhase;

    public void Grant()
    {
        if (PlayerStatsManager.Instance != null)
        {
            if (Money != 0) PlayerStatsManager.Instance.AddMoney(Money);
            if (Strength != 0) PlayerStatsManager.Instance.AddStrength(Strength);
            if (Intellect != 0) PlayerStatsManager.Instance.AddIntellect(Intellect);
        }

        WorldFlags.Apply(Flags);

        if (AdvanceTimePhase && TimeManager.Instance != null)
            TimeManager.Instance.AdvancePhase(TimeChangeSource.Quest);
    }
}