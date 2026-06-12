using UnityEngine;

/// <summary>
/// Attach to any UI element (e.g. the tracked-quest panel) that should auto-hide while
/// certain panels are open. A panel hides this element by listing the same group id in
/// its "Hide Groups While Open". When the group is released the element is restored —
/// but only if it was visible before the hide, so manually-hidden elements stay hidden.
/// Note: the GameObject must start active in the scene so Awake can subscribe.
/// </summary>
[DisallowMultipleComponent]
public sealed class UIVisibilityGroup : MonoBehaviour
{
    public enum HideMode
    {
        Deactivate,  // SetActive(false) — also stops Update/coroutines on the element
        CanvasGroup  // alpha 0 + raycasts off — element keeps running invisibly
    }

    [Tooltip("Group id referenced by UIPanel.HideGroupsWhileOpen, e.g. \"QuestTracker\".")]
    [SerializeField] private string groupId;
    [SerializeField] private HideMode hideMode = HideMode.Deactivate;
    [Tooltip("Used in CanvasGroup mode. Auto-added when empty.")]
    [SerializeField] private CanvasGroup canvasGroup;

    private bool _hiddenByGroup;
    private bool _wasVisibleBeforeHide;

    private void Awake()
    {
        if (hideMode == HideMode.CanvasGroup && canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        UIPanelManager.HideGroupChanged += HandleGroupChanged;

        if (UIPanelManager.IsGroupHidden(groupId))
            Hide();
    }

    private void OnDestroy()
    {
        UIPanelManager.HideGroupChanged -= HandleGroupChanged;
    }

    private void OnEnable()
    {
        // Activated externally (e.g. quest tracking turned on) while the group is hidden:
        // hide again, but remember it should reappear when the group is released.
        if (!_hiddenByGroup && UIPanelManager.IsGroupHidden(groupId))
            Hide();
    }

    private void HandleGroupChanged(string group, bool hidden)
    {
        if (!string.Equals(group, groupId, System.StringComparison.Ordinal))
            return;

        if (hidden) Hide();
        else Show();
    }

    private void Hide()
    {
        if (_hiddenByGroup) return;
        _hiddenByGroup = true;

        if (hideMode == HideMode.Deactivate)
        {
            _wasVisibleBeforeHide = gameObject.activeSelf;
            gameObject.SetActive(false);
        }
        else
        {
            _wasVisibleBeforeHide = canvasGroup.alpha > 0f;
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
    }

    private void Show()
    {
        if (!_hiddenByGroup) return;
        _hiddenByGroup = false;

        if (!_wasVisibleBeforeHide)
            return; // was already hidden for another reason — leave it alone

        if (hideMode == HideMode.Deactivate)
        {
            gameObject.SetActive(true);
        }
        else
        {
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
        }
    }
}
