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

    public void Grant()
    {
        if (PlayerStatsManager.Instance != null)
        {
            if (Money != 0) PlayerStatsManager.Instance.AddMoney(Money);
            if (Influence != 0) PlayerStatsManager.Instance.AddInfluence(Influence);
            if (Strategy != 0) PlayerStatsManager.Instance.AddStrategy(Strategy);
            if (Networking != 0) PlayerStatsManager.Instance.AddNetworking(Networking);
            if (Reputation != 0) PlayerStatsManager.Instance.AddReputation(Reputation);
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