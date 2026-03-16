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

    [Header("Career / Promotion")]
    public bool ApplyPromotion;
    public CareerTier PromoteTo = CareerTier.Intern;
    public string UnlockFloorSceneName;
    public bool SetUnlockedFloorAsCurrent = true;

    [Tooltip("If true, player is moved to the unlocked floor immediately after reward is granted.")]
    public bool MovePlayerToUnlockedFloor;

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

        if (ApplyPromotion && CareerManager.Instance != null)
        {
            CareerManager.Instance.ApplyPromotion(
                PromoteTo,
                UnlockFloorSceneName,
                SetUnlockedFloorAsCurrent
            );

            if (MovePlayerToUnlockedFloor &&
                !string.IsNullOrWhiteSpace(UnlockFloorSceneName) &&
                GameManager.Instance != null)
            {
                _ = GameManager.Instance.SwitchLocationBySceneName(UnlockFloorSceneName);
            }
        }
    }
}