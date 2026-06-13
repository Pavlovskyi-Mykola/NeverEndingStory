using System;
using UnityEngine;

/// <summary>
/// Turns QuestManager's granular events into player-facing notifications.
/// Drop on any persistent object (e.g. alongside QuestManager). The actual
/// display is whatever listens to Notifications.Posted.
/// </summary>
public sealed class QuestNotificationRelay : MonoBehaviour
{
    [Header("Toggles")]
    [SerializeField] private bool notifyStarted = true;
    [SerializeField] private bool notifyStepAdvanced = true;
    [SerializeField] private bool notifyCompleted = true;

    [Header("Titles")]
    [SerializeField] private string startedTitle = "New Quest";
    [SerializeField] private string updatedTitle = "Objective Updated";
    [SerializeField] private string completedTitle = "Quest Completed";

    private const string AutoPrefix = "[AUTO] ";

    private void OnEnable()
    {
        QuestManager.InstanceReady += HandleManagerReady;
        if (QuestManager.Instance != null)
            HandleManagerReady(QuestManager.Instance);
    }

    private void OnDisable()
    {
        QuestManager.InstanceReady -= HandleManagerReady;
        Unsubscribe(QuestManager.Instance);
    }

    private void HandleManagerReady(QuestManager qm)
    {
        Unsubscribe(qm);

        qm.OnQuestStarted += HandleStarted;
        qm.OnQuestStepAdvanced += HandleStepAdvanced;
        qm.OnQuestCompleted += HandleCompleted;
    }

    private void Unsubscribe(QuestManager qm)
    {
        if (qm == null) return;

        qm.OnQuestStarted -= HandleStarted;
        qm.OnQuestStepAdvanced -= HandleStepAdvanced;
        qm.OnQuestCompleted -= HandleCompleted;
    }

    private void HandleStarted(string questId)
    {
        if (!notifyStarted) return;
        Notifications.Post(startedTitle, GetTitle(questId), NotificationType.QuestStarted);
    }

    private void HandleStepAdvanced(string questId, int stepIndex)
    {
        if (!notifyStepAdvanced) return;

        string objective = GetStepText(questId, stepIndex);
        if (string.IsNullOrEmpty(objective))
            objective = GetTitle(questId);

        Notifications.Post(updatedTitle, objective, NotificationType.QuestUpdated);
    }

    private void HandleCompleted(string questId)
    {
        if (!notifyCompleted) return;
        Notifications.Post(completedTitle, GetTitle(questId), NotificationType.QuestCompleted);
    }

    private static string GetTitle(string questId)
    {
        if (QuestManager.Instance != null &&
            QuestManager.Instance.TryGetDefinition(questId, out var def) &&
            def != null && !string.IsNullOrEmpty(def.Title))
            return def.Title;

        return questId;
    }

    private static string GetStepText(string questId, int stepIndex)
    {
        if (QuestManager.Instance == null) return null;
        if (!QuestManager.Instance.TryGetDefinition(questId, out var def) || def == null) return null;
        if (def.Steps == null || stepIndex < 0 || stepIndex >= def.Steps.Count) return null;

        var step = def.Steps[stepIndex];
        return step != null ? StripAuto(step.Text) : null;
    }

    private static string StripAuto(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        return s.StartsWith(AutoPrefix, StringComparison.Ordinal) ? s.Substring(AutoPrefix.Length) : s;
    }
}
