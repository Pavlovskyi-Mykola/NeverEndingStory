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
            HandleTimeChanged(TimeManager.Instance.DayOfWeek, TimeManager.Instance.Phase);
        }
    }

    private void OnDisable()
    {
        if (TimeManager.Instance != null)
            TimeManager.Instance.OnTimeChanged -= HandleTimeChanged;
    }

    private void HandleTimeChanged(System.DayOfWeek day, DayPhase phase)
    {
        if (DayOfWeek != null) DayOfWeek.text = day.ToString();
        if (TimeOfDay != null) TimeOfDay.text = phase.ToString();

        if (TimeOfDayImage != null)
            TimeOfDayImage.sprite = GetSpriteForPhase(phase);
    }

    private Sprite GetSpriteForPhase(DayPhase phase)
    {
        switch (phase)
        {
            case DayPhase.Morning: return morningSprite;
            case DayPhase.Afternoon: return afternoonSprite;
            case DayPhase.Evening: return eveningSprite;
            case DayPhase.Night: return nightSprite;
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
