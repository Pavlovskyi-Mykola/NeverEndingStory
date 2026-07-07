using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class QuestRewardDefinition
{
    [Header("Stats")]
    public int Money;
    public int Influence;
    public int Strategy;
    public int Networking;
    public int Reputation;

    [Header("World Flags")]
    public List<FlagChange> Flags = new();

    [Header("Time")]
    public bool AdvanceTimePhase;

    [Header("Energy")]
    [Tooltip("Permanently raises max energy (e.g. +1 on promotion). Current energy is not refilled.")]
    public int MaxEnergyBonus = 0;
    [Tooltip("One-off energy refill, clamped at max.")]
    public int RestoreEnergy = 0;

    [Header("Career / Promotion")]
    public bool ApplyPromotion;
    public CareerTier PromoteTo = CareerTier.Intern;
    public List<SceneReference> UnlockFloorScenes = new();

    public int GetReward(StatType stat) => stat switch
    {
        StatType.Money      => Money,
        StatType.Influence  => Influence,
        StatType.Strategy   => Strategy,
        StatType.Networking => Networking,
        StatType.Reputation => Reputation,
        _ => 0
    };

    public void Grant()
    {
        var stats = PlayerStatsManager.Instance;
        if (stats != null)
        {
            for (int i = 0; i < StatTypes.All.Length; i++)
            {
                int amount = GetReward(StatTypes.All[i]);
                if (amount != 0) stats.Add(StatTypes.All[i], amount);
            }
        }

        if (stats != null)
        {
            if (MaxEnergyBonus != 0)
                stats.AddMaxEnergy(MaxEnergyBonus);

            if (RestoreEnergy > 0)
                stats.RestoreEnergy(RestoreEnergy);
        }

        WorldFlags.Apply(Flags);

        if (AdvanceTimePhase && TimeManager.Instance != null)
            TimeManager.Instance.AdvancePhase(TimeChangeSource.Quest);

        if (ApplyPromotion && CareerManager.Instance != null)
        {
            CareerManager.Instance.PromoteTo(PromoteTo);

            if (UnlockFloorScenes != null)
            {
                for (int i = 0; i < UnlockFloorScenes.Count; i++)
                {
                    var sceneRef = UnlockFloorScenes[i];
                    if (sceneRef == null || !sceneRef.IsValid)
                        continue;

                    CareerManager.Instance.UnlockFloor(sceneRef.SceneName);
                }
            }
        }
    }
}