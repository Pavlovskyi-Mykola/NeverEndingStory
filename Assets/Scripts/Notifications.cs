using System;

public enum NotificationType
{
    Info,
    QuestStarted,
    QuestUpdated,
    QuestCompleted
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
        Posted?.Invoke(new NotificationRequest(title, message, type));
    }
}
