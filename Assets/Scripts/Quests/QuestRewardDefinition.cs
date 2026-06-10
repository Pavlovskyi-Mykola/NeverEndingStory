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
            foreach (StatType stat in System.Enum.GetValues(typeof(StatType)))
            {
                int amount = GetReward(stat);
                if (amount != 0) stats.Add(stat, amount);
            }
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