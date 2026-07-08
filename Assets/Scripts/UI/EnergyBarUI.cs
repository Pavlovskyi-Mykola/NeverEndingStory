using UnityEngine;
using UnityEngine.UI;
using Evo.UI;

/// <summary>
/// Binds the HUD energy display to PlayerStatsManager through an Evo ProgressBar.
/// The bar owns the fill, smoothing, and its Value Text; this component feeds it
/// Energy/MaxEnergy (text becomes "7 / 10" via the bar's Text Format) and applies
/// an optional low-energy tint to the fill image.
/// </summary>
public sealed class EnergyBarUI : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Evo progress bar driven 0..MaxEnergy. Assign its Fill Rect and (optionally) Value Text on the bar itself.")]
    [SerializeField] private ProgressBar bar;

    [Header("Low energy tint (optional)")]
    [Tooltip("Fill color switches when energy falls to or below this fraction of max. Needs an Image on the bar's Fill Rect.")]
    [Range(0f, 1f)][SerializeField] private float lowThreshold = 0.25f;
    [SerializeField] private Color normalColor = new Color(0.20f, 0.78f, 0.60f);
    [SerializeField] private Color lowColor = new Color(0.89f, 0.70f, 0.25f);

    private Image _fillImage;
    private bool _initialized;

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
        if (stats == null || bar == null) return;

        int max = Mathf.Max(1, stats.MaxEnergy);

        bar.textFormat = "{0} / " + max;
        bar.MinValue = 0f;
        bar.MaxValue = max;

        // First bind snaps (no sweep from the bar's serialized default); afterwards
        // let the bar's own smoothing animate. WithoutNotify: onValueChanged is for
        // author-wired reactions, not for programmatic data sync.
        if (_initialized)
        {
            bar.SetValueWithoutNotify(stats.Energy);
        }
        else
        {
            bar.SetValueInstant(stats.Energy);
            _initialized = true;
        }

        ApplyTint((float)stats.Energy / max);
    }

    private void ApplyTint(float fraction)
    {
        if (_fillImage == null && bar.fillRect != null)
            _fillImage = bar.fillRect.GetComponent<Image>();

        if (_fillImage != null)
            _fillImage.color = fraction <= lowThreshold ? lowColor : normalColor;
    }
}
