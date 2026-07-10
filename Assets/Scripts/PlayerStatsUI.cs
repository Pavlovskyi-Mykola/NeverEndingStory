using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PlayerStatsUI : MonoBehaviour
{
    [Header("Money")]
    [SerializeField] private TMP_Text moneyTMP;

    [Header("Influence")]
    [SerializeField] private TMP_Text influenceTMP;

    [Header("Strategy")]
    [SerializeField] private TMP_Text strategyTMP;

    [Header("Networking")]
    [SerializeField] private TMP_Text networkingTMP;

    [Header("Reputation")]
    [SerializeField] private TMP_Text reputationTMP;

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

        SetLabel(moneyTMP, $"{stats.Money}");
        SetLabel(influenceTMP, $"{stats.Influence}");
        SetLabel(strategyTMP, $"{stats.Strategy}");
        SetLabel(networkingTMP, $"{stats.Networking}");
        SetLabel(reputationTMP, $"{stats.Reputation}");
    }

    private static void SetLabel(TMP_Text tmp, string value)
    {
        if (tmp != null) tmp.text = value;
    }
}
