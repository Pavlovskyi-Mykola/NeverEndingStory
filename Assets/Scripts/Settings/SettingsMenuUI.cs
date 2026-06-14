using UnityEngine;
using Evo.UI;

/// <summary>
/// Binds the settings panel to SettingsManager using Evo UI controls
/// (Dropdown, Slider, Switch). Reads current values into the controls on enable,
/// pushes user changes back, and re-syncs when settings change elsewhere.
/// </summary>
public sealed class SettingsMenuUI : MonoBehaviour
{
    [Header("Visuals")]
    [Tooltip("Evo Dropdown. Items must be: index 0 = Fullscreen, index 1 = Windowed (auto-filled if empty).")]
    [SerializeField] private Dropdown displayModeDropdown;

    [Header("Audio")]
    [Tooltip("Evo Slider (Horizontal Input variant).")]
    [SerializeField] private Slider masterVolumeSlider;
    [Tooltip("Evo Switch (Label variant).")]
    [SerializeField] private Switch masterMuteSwitch;
    [Tooltip("Evo Slider (Horizontal Input variant).")]
    [SerializeField] private Slider musicVolumeSlider;

    [Header("Behaviour")]
    [Tooltip("Grey out the master volume slider while muted.")]
    [SerializeField] private bool disableMasterSliderWhenMuted = true;

    private void OnEnable()
    {
        SettingsManager.InstanceReady += HandleManagerReady;
        if (SettingsManager.Instance != null)
            HandleManagerReady(SettingsManager.Instance);

        WireControls();
        SyncFromManager();
    }

    private void OnDisable()
    {
        SettingsManager.InstanceReady -= HandleManagerReady;
        if (SettingsManager.Instance != null)
            SettingsManager.Instance.OnSettingsChanged -= SyncFromManager;

        UnwireControls();
    }

    private void HandleManagerReady(SettingsManager manager)
    {
        manager.OnSettingsChanged -= SyncFromManager;
        manager.OnSettingsChanged += SyncFromManager;
        SyncFromManager();
    }

    // -----------------------
    // Control wiring
    // -----------------------

    private void WireControls()
    {
        if (displayModeDropdown != null)
        {
            displayModeDropdown.onItemSelected.RemoveListener(HandleDisplayModeChanged);
            displayModeDropdown.onItemSelected.AddListener(HandleDisplayModeChanged);
        }

        if (masterVolumeSlider != null)
        {
            masterVolumeSlider.minValue = 0f;
            masterVolumeSlider.maxValue = 1f;
            masterVolumeSlider.onValueChanged.RemoveListener(HandleMasterVolumeChanged);
            masterVolumeSlider.onValueChanged.AddListener(HandleMasterVolumeChanged);
        }

        if (masterMuteSwitch != null)
        {
            masterMuteSwitch.onValueChanged.RemoveListener(HandleMasterMuteChanged);
            masterMuteSwitch.onValueChanged.AddListener(HandleMasterMuteChanged);
        }

        if (musicVolumeSlider != null)
        {
            musicVolumeSlider.minValue = 0f;
            musicVolumeSlider.maxValue = 1f;
            musicVolumeSlider.onValueChanged.RemoveListener(HandleMusicVolumeChanged);
            musicVolumeSlider.onValueChanged.AddListener(HandleMusicVolumeChanged);
        }
    }

    private void UnwireControls()
    {
        if (displayModeDropdown != null)
            displayModeDropdown.onItemSelected.RemoveListener(HandleDisplayModeChanged);
        if (masterVolumeSlider != null)
            masterVolumeSlider.onValueChanged.RemoveListener(HandleMasterVolumeChanged);
        if (masterMuteSwitch != null)
            masterMuteSwitch.onValueChanged.RemoveListener(HandleMasterMuteChanged);
        if (musicVolumeSlider != null)
            musicVolumeSlider.onValueChanged.RemoveListener(HandleMusicVolumeChanged);
    }

    // -----------------------
    // UI -> manager
    // -----------------------

    private void HandleDisplayModeChanged(int index)
    {
        if (SettingsManager.Instance == null) return;
        SettingsManager.Instance.SetDisplayMode((DisplayMode)index);
    }

    private void HandleMasterVolumeChanged(float value)
    {
        if (SettingsManager.Instance == null) return;
        SettingsManager.Instance.SetMasterVolume(value);
    }

    private void HandleMasterMuteChanged(bool muted)
    {
        if (SettingsManager.Instance == null) return;
        SettingsManager.Instance.SetMasterMuted(muted);
    }

    private void HandleMusicVolumeChanged(float value)
    {
        if (SettingsManager.Instance == null) return;
        SettingsManager.Instance.SetMusicVolume(value);
    }

    // -----------------------
    // Manager -> UI (no-notify setters, so this never re-triggers the handlers)
    // -----------------------

    private void SyncFromManager()
    {
        var m = SettingsManager.Instance;
        if (m == null) return;

        if (displayModeDropdown != null)
        {
            EnsureDisplayModeOptions();
            displayModeDropdown.SelectItem((int)m.DisplayMode, triggerEvents: false);
        }

        if (masterVolumeSlider != null)
            masterVolumeSlider.SetValueWithoutNotify(m.MasterVolume);

        if (masterMuteSwitch != null)
            masterMuteSwitch.SetValue(m.MasterMuted, sendCallback: false);

        if (musicVolumeSlider != null)
            musicVolumeSlider.SetValueWithoutNotify(m.MusicVolume);

        if (disableMasterSliderWhenMuted && masterVolumeSlider != null)
            masterVolumeSlider.interactable = !m.MasterMuted;
    }

    private void EnsureDisplayModeOptions()
    {
        if (displayModeDropdown == null) return;
        if (displayModeDropdown.items.Count >= 2) return; // assume the scene set them up

        displayModeDropdown.ClearAllItems();
        displayModeDropdown.AddItems("Fullscreen", "Windowed"); // index 0 / 1 match DisplayMode
    }
}
