using UnityEngine;
using UnityEngine.UI;

public class PlayerStatsUI : MonoBehaviour
{
    [SerializeField] private Text moneyText;
    [SerializeField] private Text strengthText;
    [SerializeField] private Text intellectText;

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
        if (strengthText != null) strengthText.text = $"Strength: {stats.Strength}";
        if (intellectText != null) intellectText.text = $"Intellect: {stats.Intellect}";
    }
}
