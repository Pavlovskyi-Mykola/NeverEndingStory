using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Binds the HUD energy display to PlayerStatsManager. Supports a fill bar
/// (Image with Image Type = Filled), a "7 / 10" label, and an optional low-energy
/// tint — assign whichever you use, the rest can stay empty.
/// </summary>
public sealed class EnergyBarUI : MonoBehaviour
{
    [Header("References (assign any)")]
    [Tooltip("Image with Image Type = Filled; fillAmount is driven 0..1.")]
    [SerializeField] private Image fillImage;
    [SerializeField] private TMP_Text label;

    [Header("Low energy tint (optional, needs Fill Image)")]
    [Tooltip("Fill color switches when energy falls to or below this fraction of max.")]
    [Range(0f, 1f)][SerializeField] private float lowThreshold = 0.25f;
    [SerializeField] private Color normalColor = new Color(0.20f, 0.78f, 0.60f);
    [SerializeField] private Color lowColor = new Color(0.89f, 0.70f, 0.25f);

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
            PlayerStatsManager.Instance.OnEnergyChanged -= Refresh;
    }

    private void HandleManagerReady(PlayerStatsManager manager)
    {
        manager.OnEnergyChanged -= Refresh;
        manager.OnEnergyChanged += Refresh;
        Refresh();
    }

    private void Refresh()
    {
        var stats = PlayerStatsManager.Instance;
        if (stats == null) return;

        int max = Mathf.Max(1, stats.MaxEnergy);
        float fraction = (float)stats.Energy / max;

        if (fillImage != null)
        {
            fillImage.fillAmount = fraction;
            fillImage.color = fraction <= lowThreshold ? lowColor : normalColor;
        }

        if (label != null)
            label.text = $"{stats.Energy} / {stats.MaxEnergy}";
    }
}
