using System;

public enum NotificationType
{
    Info,
    QuestStarted,
    QuestUpdated,
    QuestCompleted,
    StatChange,
    ItemChange,
    Promotion,
    Blocked,
    Relationship,
    CalendarEvent,
    RandomEvent
}

public readonly struct NotificationRequest
{
    public readonly string Title;
    public readonly string Message;
    public readonly NotificationType Type;

    public NotificationRequest(string title, string message, NotificationType type)
    {
        Title = title;
        Message = message;
        Type = type;
    }
}

/// <summary>
/// Player-facing toast/notification hub. Gameplay systems Post here; a UI listener
/// (e.g. an Evo Notification widget wired to Posted) decides how to display them.
/// Kept separate from GameEvents, which carries simulation signals, not presentation.
/// </summary>
public static class Notifications
{
    public static event Action<NotificationRequest> Posted;

    public static void Post(string title, string message, NotificationType type = NotificationType.Info)
    {
        // Centralized suppression, checked once for every producer:
        // - never toast while a save is restoring (restore re-fires StatsChanged /
        //   InventoryChanged for the entire loaded state), and
        // - never toast outside gameplay (main menu).
        if (SaveLoadManager.Instance != null && SaveLoadManager.Instance.IsLoading)
            return;

        if (GameManager.Instance == null || !GameManager.Instance.IsInGameplay)
            return;

        Posted?.Invoke(new NotificationRequest(title, message, type));
    }
}
