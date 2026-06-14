using System;
using UnityEngine;
using UnityEngine.Audio;

public enum DisplayMode
{
    Fullscreen = 0, // borderless fullscreen window
    Windowed = 1
}

/// <summary>
/// Owns global game settings (display + audio), persists them in PlayerPrefs
/// (separate from save slots — settings are per-device, not per-save) and applies
/// them. Lives on a persistent object (e.g. CoreLogic) and survives scene loads.
/// </summary>
public sealed class SettingsManager : MonoBehaviour
{
    public static SettingsManager Instance { get; private set; }
    public static event Action<SettingsManager> InstanceReady;

    [Header("Audio")]
    [Tooltip("Mixer with exposed float params for master and music volume (in dB).")]
    [SerializeField] private AudioMixer audioMixer;
    [SerializeField] private string masterVolumeParam = "MasterVolume";
    [SerializeField] private string musicVolumeParam = "MusicVolume";

    [Header("Defaults")]
    [SerializeField] private DisplayMode defaultDisplayMode = DisplayMode.Fullscreen;
    [Range(0f, 1f)][SerializeField] private float defaultMasterVolume = 1f;
    [Range(0f, 1f)][SerializeField] private float defaultMusicVolume = 1f;

    // Current state
    public DisplayMode DisplayMode { get; private set; }
    public float MasterVolume { get; private set; }
    public float MusicVolume { get; private set; }
    public bool MasterMuted { get; private set; }

    /// <summary>Raised whenever any setting changes, so open UIs can re-sync.</summary>
    public event Action OnSettingsChanged;

    private const string KeyDisplayMode = "settings.display.mode";
    private const string KeyMasterVolume = "settings.audio.master";
    private const string KeyMusicVolume = "settings.audio.music";
    private const string KeyMasterMuted = "settings.audio.master_muted";

    // Below this linear volume we treat the channel as fully silent (-80 dB).
    private const float MinAudibleLinear = 0.0001f;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        Load();
        ApplyDisplayMode();

        InstanceReady?.Invoke(this);
    }

    private void Start()
    {
        // Mixer params can be ignored if set during the first Awake pass before the
        // mixer is fully initialized — apply audio here as well to be safe.
        ApplyAudio();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    // -----------------------
    // Public setters
    // -----------------------

    public void SetDisplayMode(DisplayMode mode)
    {
        DisplayMode = mode;
        ApplyDisplayMode();
        PlayerPrefs.SetInt(KeyDisplayMode, (int)mode);
        Save();
        OnSettingsChanged?.Invoke();
    }

    public void SetMasterVolume(float linear01)
    {
        MasterVolume = Mathf.Clamp01(linear01);
        ApplyAudio();
        PlayerPrefs.SetFloat(KeyMasterVolume, MasterVolume);
        Save();
        OnSettingsChanged?.Invoke();
    }

    public void SetMusicVolume(float linear01)
    {
        MusicVolume = Mathf.Clamp01(linear01);
        ApplyAudio();
        PlayerPrefs.SetFloat(KeyMusicVolume, MusicVolume);
        Save();
        OnSettingsChanged?.Invoke();
    }

    public void SetMasterMuted(bool muted)
    {
        MasterMuted = muted;
        ApplyAudio();
        PlayerPrefs.SetInt(KeyMasterMuted, muted ? 1 : 0);
        Save();
        OnSettingsChanged?.Invoke();
    }

    // -----------------------
    // Apply
    // -----------------------

    private void ApplyDisplayMode()
    {
        Screen.fullScreenMode = DisplayMode == DisplayMode.Fullscreen
            ? FullScreenMode.FullScreenWindow
            : FullScreenMode.Windowed;
    }

    private void ApplyAudio()
    {
        if (audioMixer == null)
        {
            // Fallback when no mixer is assigned: at least honor master mute/volume
            // globally. Music can't be separated without a mixer.
            AudioListener.volume = MasterMuted ? 0f : MasterVolume;
            return;
        }

        // Muting forces the master channel to silence regardless of its slider;
        // music routes under master, so this silences everything.
        float masterLinear = MasterMuted ? 0f : MasterVolume;

        audioMixer.SetFloat(masterVolumeParam, LinearToDecibels(masterLinear));
        audioMixer.SetFloat(musicVolumeParam, LinearToDecibels(MusicVolume));
    }

    // -----------------------
    // Persistence
    // -----------------------

    private void Load()
    {
        DisplayMode = (DisplayMode)PlayerPrefs.GetInt(KeyDisplayMode, (int)defaultDisplayMode);
        MasterVolume = PlayerPrefs.GetFloat(KeyMasterVolume, defaultMasterVolume);
        MusicVolume = PlayerPrefs.GetFloat(KeyMusicVolume, defaultMusicVolume);
        MasterMuted = PlayerPrefs.GetInt(KeyMasterMuted, 0) == 1;
    }

    private void Save()
    {
        PlayerPrefs.Save();
    }

    private static float LinearToDecibels(float linear)
    {
        if (linear <= MinAudibleLinear)
            return -80f; // mixer's practical silence floor

        return Mathf.Log10(linear) * 20f;
    }
}
