using System;
using UnityEngine;

[Serializable]
public struct DialogueSelectorContext
{
    public string NpcId;
    public string LocationId;

    public DayOfWeek Day;
    public TimeOfDay Phase;

    public static DialogueSelectorContext From(string npcId, string locationId)
    {
        var ctx = new DialogueSelectorContext
        {
            NpcId = npcId,
            LocationId = locationId,
            Day = TimeManager.Instance != null ? TimeManager.Instance.DayOfWeek : DateTime.Now.DayOfWeek,
            Phase = TimeManager.Instance != null ? TimeManager.Instance.TimeOfDay : TimeOfDay.Morning
        };
        return ctx;
    }
}
