using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TimeUI : MonoBehaviour
{
    [Header("Day of week label (assign TMP or legacy — either works)")]
    [SerializeField] private TMP_Text dayOfWeekTMP;
    [SerializeField] private Text dayOfWeekLegacy;

    [Header("Time of day label (assign TMP or legacy — either works)")]
    [SerializeField] private TMP_Text timeOfDayTMP;
    [SerializeField] private Text timeOfDayLegacy;

    [Header("Phase Image UI")]
    [SerializeField] private Image timeOfDayImage;
    [SerializeField] private Sprite morningSprite;
    [SerializeField] private Sprite afternoonSprite;
    [SerializeField] private Sprite eveningSprite;
    [SerializeField] private Sprite nightSprite;

    private void OnEnable()
    {
        // InstanceReady covers TimeManager waking up after us; the Instance check
        // covers it waking up first — so the display works regardless of load order.
        TimeManager.InstanceReady += HandleTimeManagerReady;
        if (TimeManager.Instance != null)
            HandleTimeManagerReady(TimeManager.Instance);
    }

    private void OnDisable()
    {
        TimeManager.InstanceReady -= HandleTimeManagerReady;
        if (TimeManager.Instance != null)
            TimeManager.Instance.OnTimeChanged -= HandleTimeChanged;
    }

    private void HandleTimeManagerReady(TimeManager tm)
    {
        tm.OnTimeChanged -= HandleTimeChanged;
        tm.OnTimeChanged += HandleTimeChanged;

        // Initial refresh with the current time.
        HandleTimeChanged(tm.DayOfWeek, tm.TimeOfDay);
    }

    private void HandleTimeChanged(System.DayOfWeek day, TimeOfDay phase)
    {
        SetLabel(dayOfWeekTMP, dayOfWeekLegacy, day.ToString());
        SetLabel(timeOfDayTMP, timeOfDayLegacy, phase.ToString());

        if (timeOfDayImage != null)
        {
            var sprite = GetSpriteForPhase(phase);
            timeOfDayImage.enabled = sprite != null;
            timeOfDayImage.sprite = sprite;
        }
    }

    private static void SetLabel(TMP_Text tmp, Text legacy, string value)
    {
        if (tmp != null) tmp.text = value;
        if (legacy != null) legacy.text = value;
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

    public void AdvanceTimeButton()
    {
        if (TimeManager.Instance == null) return;
        TimeManager.Instance.AdvancePhase(TimeChangeSource.PlayerUI);
    }

    public void SleepToMorningButton()
    {
        if (TimeManager.Instance == null) return;
        TimeManager.Instance.SleepToMorning(TimeChangeSource.PlayerUI);
    }
}
