using UnityEngine;
using UnityEngine.UI;

public class PlayerStatsUI : MonoBehaviour
{
    [SerializeField] private Text moneyText;
    [SerializeField] private Text influenceText;
    [SerializeField] private Text strategyText;
    [SerializeField] private Text networkingText;
    [SerializeField] private Text reputationText;

    private void OnEnable()
    {
        if (PlayerStatsManager.Instance != null)
        {
            PlayerStatsManager.Instance.OnStatsChanged += Refresh;
            Refresh();
        }
    }

    private void OnDisable()
    {
        if (PlayerStatsManager.Instance != null)
            PlayerStatsManager.Instance.OnStatsChanged -= Refresh;
    }

    private void Refresh()
    {
        var stats = PlayerStatsManager.Instance;
        if (stats == null) return;

        if (moneyText != null) moneyText.text = $"Money: {stats.Money}";
        if (influenceText != null) influenceText.text = $"Influence: {stats.Influence}";
        if (strategyText != null) strategyText.text = $"Strategy: {stats.Strategy}";
        if (networkingText != null) networkingText.text = $"Networking: {stats.Networking}";
        if (reputationText != null) reputationText.text = $"Reputation: {stats.Reputation}";
    }
}