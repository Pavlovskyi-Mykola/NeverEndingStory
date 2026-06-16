using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PlayerStatsUI : MonoBehaviour
{
    [Header("Money (assign TMP or legacy — either works)")]
    [SerializeField] private TMP_Text moneyTMP;
    [SerializeField] private Text moneyLegacy;

    [Header("Influence")]
    [SerializeField] private TMP_Text influenceTMP;
    [SerializeField] private Text influenceLegacy;

    [Header("Strategy")]
    [SerializeField] private TMP_Text strategyTMP;
    [SerializeField] private Text strategyLegacy;

    [Header("Networking")]
    [SerializeField] private TMP_Text networkingTMP;
    [SerializeField] private Text networkingLegacy;

    [Header("Reputation")]
    [SerializeField] private TMP_Text reputationTMP;
    [SerializeField] private Text reputationLegacy;

    private void OnEnable()
    {
        PlayerStatsManager.InstanceReady += HandleManagerReady;
        if (PlayerStatsManager.Instance != null)
            HandleManagerReady(PlayerStatsManager.Instance);
    }

    private void OnDisable()
    {
        PlayerStatsManager.InstanceReady -= HandleManagerReady;
        if (PlayerStatsManager.Instance != null)
            PlayerStatsManager.Instance.OnStatsChanged -= Refresh;
    }

    private void HandleManagerReady(PlayerStatsManager manager)
    {
        manager.OnStatsChanged -= Refresh;
        manager.OnStatsChanged += Refresh;
        Refresh();
    }

    private void Refresh()
    {
        var stats = PlayerStatsManager.Instance;
        if (stats == null) return;

        SetLabel(moneyTMP, moneyLegacy, $"Money: {stats.Money}");
        SetLabel(influenceTMP, influenceLegacy, $"Influence: {stats.Influence}");
        SetLabel(strategyTMP, strategyLegacy, $"Strategy: {stats.Strategy}");
        SetLabel(networkingTMP, networkingLegacy, $"Networking: {stats.Networking}");
        SetLabel(reputationTMP, reputationLegacy, $"Reputation: {stats.Reputation}");
    }

    private static void SetLabel(TMP_Text tmp, Text legacy, string value)
    {
        if (tmp != null) tmp.text = value;
        if (legacy != null) legacy.text = value;
    }
}
