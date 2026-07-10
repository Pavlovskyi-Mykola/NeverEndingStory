using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TimeUI : MonoBehaviour
{
    [Header("Day of week label")]
    [SerializeField] private TMP_Text dayOfWeekTMP;

    [Header("Time of day label")]
    [SerializeField] private TMP_Text timeOfDayTMP;

    [Header("Phase Image UI")]
    [SerializeField] private Image timeOfDayImage;
    [SerializeField] private Sprite morningSprite;
    [SerializeField] private Sprite afternoonSprite;
    [SerializeField] private Sprite eveningSprite;
    [SerializeField] private Sprite nightSprite;

    [Header("Advance / Sleep button")]
    [Tooltip("Single button that skips a phase during the day and sleeps to morning at night. " +
             "Wire its OnClick to OnAdvanceOrSleepPressed().")]
    [SerializeField] private Button actionButton;
    [Tooltip("Optional label on the button; swaps between the skip and sleep captions.")]
    [SerializeField] private TMP_Text actionButtonLabel;
    [SerializeField] private string skipLabel = "Skip";
    [SerializeField] private string sleepLabel = "Sleep";

    private void OnEnable()
    {
        // InstanceReady covers TimeManager waking up after us; the Instance check
        // covers it waking up first — so the display works regardless of load order.
        TimeManager.InstanceReady += HandleTimeManagerReady;
        if (TimeManager.Instance != null)
            HandleTimeManagerReady(TimeManager.Instance);

        // The button is home-gated at night, so re-evaluate it whenever the player
        // changes location.
        GameEvents.LocationEntered += HandleLocationEntered;
    }

    private void OnDisable()
    {
        TimeManager.InstanceReady -= HandleTimeManagerReady;
        if (TimeManager.Instance != null)
            TimeManager.Instance.OnTimeChanged -= HandleTimeChanged;

        GameEvents.LocationEntered -= HandleLocationEntered;
    }

    private void HandleTimeManagerReady(TimeManager tm)
    {
        tm.OnTimeChanged -= HandleTimeChanged;
        tm.OnTimeChanged += HandleTimeChanged;

        // Initial refresh with the current time.
        HandleTimeChanged(tm.DayOfWeek, tm.TimeOfDay);
    }

    private void HandleLocationEntered(string sceneName) => RefreshActionButton();

    private void HandleTimeChanged(System.DayOfWeek day, TimeOfDay phase)
    {
        SetLabel(dayOfWeekTMP, day.ToString());
        SetLabel(timeOfDayTMP, phase.ToString());

        if (timeOfDayImage != null)
        {
            var sprite = GetSpriteForPhase(phase);
            timeOfDayImage.enabled = sprite != null;
            timeOfDayImage.sprite = sprite;
        }

        RefreshActionButton();
    }

    /// <summary>
    /// Night turns the button into a Sleep action that only works at Home;
    /// otherwise it's a plain phase skip that's always available.
    /// </summary>
    private void RefreshActionButton()
    {
        if (actionButton == null) return;

        bool isNight = TimeManager.Instance != null &&
                       TimeManager.Instance.TimeOfDay == TimeOfDay.Night;

        actionButton.interactable = isNight ? IsAtHome() : true;
        SetLabel(actionButtonLabel, isNight ? sleepLabel : skipLabel);
    }

    private static bool IsAtHome()
    {
        var gm = GameManager.Instance;
        if (gm == null || gm.Scenes == null) return false;

        var home = gm.Scenes.Home;
        var current = gm.CurrentLocationRef;
        if (home == null || !home.IsValid || current == null || !current.IsValid)
            return false;

        return string.Equals(current.SceneName, home.SceneName, StringComparison.Ordinal);
    }

    private static void SetLabel(TMP_Text tmp, string value)
    {
        if (tmp != null) tmp.text = value;
    }

    private Sprite GetSpriteForPhase(TimeOfDay phase)
    {
        switch (phase)
        {
            case global::TimeOfDay.Morning: return morningSprite;
            case global::TimeOfDay.Afternoon: return afternoonSprite;
            case global::TimeOfDay.Evening: return eveningSprite;
            case global::TimeOfDay.Night: return nightSprite;
            default: return null;
        }
    }

    /// <summary>
    /// Single OnClick target for the action button: skips a phase during the day,
    /// sleeps to morning at night (Home only). Wire the button's OnClick here.
    /// </summary>
    public void OnAdvanceOrSleepPressed()
    {
        var tm = TimeManager.Instance;
        if (tm == null) return;

        if (tm.TimeOfDay == TimeOfDay.Night)
        {
            // Button is non-interactable away from Home, but guard the direct call too.
            if (!IsAtHome()) return;
            tm.SleepToMorning(TimeChangeSource.PlayerUI);
        }
        else
        {
            tm.AdvancePhase(TimeChangeSource.PlayerUI);
        }
    }
}
