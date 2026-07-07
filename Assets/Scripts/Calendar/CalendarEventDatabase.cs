using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "CalendarEventDatabase", menuName = "Game/Calendar/Calendar Event Database")]
public sealed class CalendarEventDatabase : ScriptableObject
{
    public List<CalendarEventDefinition> Events = new();

    private Dictionary<string, CalendarEventDefinition> _byId;

    private void OnEnable() => RebuildCache();

#if UNITY_EDITOR
    private void OnValidate() => RebuildCache();
#endif

    private void RebuildCache()
    {
        _byId = new Dictionary<string, CalendarEventDefinition>(StringComparer.Ordinal);

        if (Events == null) return;

        foreach (var e in Events)
        {
            if (e == null) continue;
            if (string.IsNullOrEmpty(e.EventId)) continue;

            // last one wins if duplicates
            _byId[e.EventId] = e;
        }
    }

    public bool TryGet(string eventId, out CalendarEventDefinition evt)
    {
        evt = null;
        if (string.IsNullOrEmpty(eventId)) return false;

        if (_byId == null) RebuildCache();
        return _byId.TryGetValue(eventId, out evt) && evt != null;
    }
}
