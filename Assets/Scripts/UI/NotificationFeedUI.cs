using System;
using System.Collections.Generic;
using UnityEngine;
using Evo.UI;

/// <summary>
/// The single UI consumer of Notifications.Posted: spawns an Evo Notification per
/// toast. Lives in the persistent UI scene (System canvas), with a spawn anchor
/// where toasts appear (e.g. top-right). Evo's stacking queue shows them one by one;
/// spawned instances destroy themselves after closing.
/// </summary>
public sealed class NotificationFeedUI : MonoBehaviour
{
    [Header("Evo Notification")]
    [Tooltip("Prefab with an Evo Notification component (Enable Stacking on, Play On Enable off). Instances self-destroy after closing.")]
    [SerializeField] private GameObject notificationPreset;

    [Tooltip("Anchored container toasts spawn under. Defaults to this transform.")]
    [SerializeField] private Transform spawnParent;

    [Header("Limits")]
    [Tooltip("Max toasts alive at once (visible + queued). When saturated, juice toasts (stats/items/info) are dropped; quest/promotion/blocked always get through.")]
    [SerializeField] private int maxAlive = 6;

    [Header("Icons per type (optional)")]
    [SerializeField] private List<TypeIcon> icons = new();

    [Serializable]
    public struct TypeIcon
    {
        public NotificationType type;
        public Sprite icon;
    }

    private void Awake()
    {
        if (spawnParent == null)
            spawnParent = transform;
    }

    private void OnEnable() => Notifications.Posted += HandlePosted;
    private void OnDisable() => Notifications.Posted -= HandlePosted;

    private void HandlePosted(NotificationRequest request)
    {
        if (notificationPreset == null)
        {
            Debug.LogWarning("[NotificationFeedUI] No Evo Notification preset assigned — toast dropped.", this);
            return;
        }

        if (spawnParent.childCount >= maxAlive && IsDroppable(request.Type))
            return;

        Notification.Create(
            notificationPreset,
            GetIcon(request.Type),
            request.Title ?? string.Empty,
            request.Message ?? string.Empty,
            spawnParent);
    }

    private static bool IsDroppable(NotificationType type)
    {
        switch (type)
        {
            case NotificationType.StatChange:
            case NotificationType.ItemChange:
            case NotificationType.Info:
                return true;
            default:
                return false; // story beats always queue
        }
    }

    private Sprite GetIcon(NotificationType type)
    {
        for (int i = 0; i < icons.Count; i++)
        {
            if (icons[i].type == type)
                return icons[i].icon;
        }

        return null;
    }
}
