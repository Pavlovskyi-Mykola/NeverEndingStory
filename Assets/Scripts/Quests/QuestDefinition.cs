using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Quest", menuName = "Game/Quests/Quest Definition")]
public sealed class QuestDefinition : ScriptableObject
{
    [Header("Identity")]
    public string QuestId;

    [Header("Display")]
    public string Title;
    [TextArea(2, 6)] public string Description;

    [Header("Steps (linear)")]
    public List<QuestStepDefinition> Steps = new();

    public bool IsValid()
    {
        if (string.IsNullOrEmpty(QuestId)) return false;
        if (Steps == null || Steps.Count == 0) return false;
        return true;
    }
}

[Serializable]
public class QuestStepDefinition
{
    [Header("Meta")]
    public string StepId;
    [TextArea(1, 4)] public string Text;
    public QuestStepType Type = QuestStepType.Manual;

    [Header("Location (ReachLocation)")]
    public string TargetLocationSceneName; // set by editor dropdown (no manual typing)

    [Header("Time Restrictions (optional; gates ANY step)")]
    public bool RestrictByDay = false;
    public DayOfWeekMask AllowedDays = DayOfWeekMask.All;

    public bool RestrictByPhase = false;
    public DayPhaseMask AllowedPhases = DayPhaseMask.All;

    [Header("Stats requirements (MinStats)")]
    public int RequiredStrength = 0;
    public int RequiredIntellect = 0;

    [Header("Money (HaveMoney / PayMoney)")]
    public int RequiredMoney = 0;

    [Header("Dialogue (TalkToNpc)")]
    public string TargetNpcId;       // required for TalkToNpc
    public string TargetDialogueId;  // optional; if empty => any dialogue with that NPC
}