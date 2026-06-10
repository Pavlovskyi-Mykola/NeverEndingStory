using UnityEngine;
using UnityEngine.UI;

public class ActionButton : MonoBehaviour
{
    [SerializeField] private ActionDefinition action;
    [SerializeField] private Button button;

    private void Awake()
    {
        if (button == null) button = GetComponent<Button>();
    }

    private void OnEnable()
    {
        Subscribe();
        Refresh();
        if (button != null) button.onClick.AddListener(OnClick);
    }

    private void OnDisable()
    {
        Unsubscribe();
        if (button != null) button.onClick.RemoveListener(OnClick);
    }

    private void Subscribe()
    {
        if (PlayerStatsManager.Instance != null)
            PlayerStatsManager.Instance.OnStatsChanged += Refresh;

        if (TimeManager.Instance != null)
            TimeManager.Instance.OnTimeChanged += HandleTimeChanged;

        if (ActionService.Instance != null)
            ActionService.Instance.OnActionStateChanged += Refresh;
    }

    private void Unsubscribe()
    {
        if (PlayerStatsManager.Instance != null)
            PlayerStatsManager.Instance.OnStatsChanged -= Refresh;

        if (TimeManager.Instance != null)
            TimeManager.Instance.OnTimeChanged -= HandleTimeChanged;

        if (ActionService.Instance != null)
            ActionService.Instance.OnActionStateChanged -= Refresh;
    }

    private void HandleTimeChanged(System.DayOfWeek d, TimeOfDay p) => Refresh();

    private void Refresh()
    {
        if (button == null || action == null || ActionService.Instance == null)
            return;

        button.interactable = ActionService.Instance.CanExecute(action, out _);
        // Later: show reason text/tooltip if not interactable.
    }

    private void OnClick()
    {
        if (ActionService.Instance == null || action == null) return;

        if (!ActionService.Instance.Execute(action, out var reason))
        {
            // Later: show message based on reason
            // Debug.Log($"Action failed: {reason}");
        }
    }
}
