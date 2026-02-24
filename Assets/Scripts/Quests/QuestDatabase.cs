using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "QuestDatabase", menuName = "Game/Quests/Quest Database")]
public sealed class QuestDatabase : ScriptableObject
{
    public List<QuestDefinition> Quests = new();

    private Dictionary<string, QuestDefinition> _byId;

    private void OnEnable() => RebuildCache();

#if UNITY_EDITOR
    private void OnValidate() => RebuildCache();
#endif

    private void RebuildCache()
    {
        _byId = new Dictionary<string, QuestDefinition>(StringComparer.Ordinal);

        if (Quests == null) return;

        foreach (var q in Quests)
        {
            if (q == null) continue;
            if (string.IsNullOrEmpty(q.QuestId)) continue;

            // last one wins if duplicates
            _byId[q.QuestId] = q;
        }
    }

    public bool TryGet(string questId, out QuestDefinition quest)
    {
        quest = null;
        if (string.IsNullOrEmpty(questId)) return false;

        if (_byId == null) RebuildCache();
        return _byId.TryGetValue(questId, out quest) && quest != null;
    }
}