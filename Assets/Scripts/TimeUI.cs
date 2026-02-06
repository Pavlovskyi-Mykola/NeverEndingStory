using UnityEngine;
using UnityEngine.UI;

public class TimeUI : MonoBehaviour
{
    [Header("Text UI")]
    [SerializeField] private Text DayOfWeek;
    [SerializeField] private Text TimeOfDay;

    [Header("Phase Image UI")]
    [SerializeField] private Image TimeOfDayImage;
    [SerializeField] private Sprite morningSprite;
    [SerializeField] private Sprite afternoonSprite;
    [SerializeField] private Sprite eveningSprite;
    [SerializeField] private Sprite nightSprite;

    private void OnEnable()
    {
        if (TimeManager.Instance != null)
        {
            TimeManager.Instance.OnTimeChanged += HandleTimeChanged;

            // Initial refresh
            HandleTimeChanged(TimeManager.Instance.DayOfWeek, TimeManager.Instance.TimeOfDay);
        }
    }

    private void OnDisable()
    {
        if (TimeManager.Instance != null)
            TimeManager.Instance.OnTimeChanged -= HandleTimeChanged;
    }

    private void HandleTimeChanged(System.DayOfWeek day, TimeOfDay phase)
    {
        if (DayOfWeek != null) DayOfWeek.text = day.ToString();
        if (TimeOfDay != null) TimeOfDay.text = phase.ToString();

        if (TimeOfDayImage != null)
            TimeOfDayImage.sprite = GetSpriteForPhase(phase);
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
        TimeManager.Instance.AdvancePhase();
    }

    public void SleepToMorningButton()
    {
        if (TimeManager.Instance == null) return;
        TimeManager.Instance.SleepToMorning();
    }

}
