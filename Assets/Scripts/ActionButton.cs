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
        // ActionService funnels every relevant change (stats, time, inventory,
        // UI blocking) into OnActionStateChanged — one subscription covers all.
        ActionService.InstanceReady += HandleServiceReady;
        if (ActionService.Instance != null)
            HandleServiceReady(ActionService.Instance);

        Refresh();
        if (button != null) button.onClick.AddListener(OnClick);
    }

    private void OnDisable()
    {
        ActionService.InstanceReady -= HandleServiceReady;
        if (ActionService.Instance != null)
            ActionService.Instance.OnActionStateChanged -= Refresh;

        if (button != null) button.onClick.RemoveListener(OnClick);
    }

    private void HandleServiceReady(ActionService service)
    {
        service.OnActionStateChanged -= Refresh;
        service.OnActionStateChanged += Refresh;
        Refresh();
    }

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
