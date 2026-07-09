using UnityEngine;

public class NpcDialogueLauncher : MonoBehaviour
{
    private string _npcId;
    private string _locationId;
    private NpcDefinition _npc;

    public void Init(string npcId, string locationId, NpcDefinition npc)
    {
        _npcId = npcId;
        _locationId = locationId;
        _npc = npc;
    }

    public void TryStartDialogue()
    {
        if (GameManager.Instance != null && GameManager.Instance.IsInDialogue)
            return;

        // NPC prefabs invoke this directly from their Button OnClick, so the
        // gameplay-block check in NpcInteractable.Talk is bypassed — guard here too.
        if (UIPanelManager.IsGameplayBlocked)
            return;

        if (DialogueRunner.Instance == null) return;
        if (_npc == null) return;

        var ctx = DialogueSelectorContext.From(_npcId, _locationId);
        var graph = DialogueSelector.Select(_npc, ctx);

        if (graph == null)
        {
            Debug.LogWarning($"[NpcDialogueLauncher] No dialogue graph selected for npc='{_npcId}' location='{_locationId}'.");
            return;
        }

        //Pass npcId/locationId to DialogueRunner
        var context = DialogueContext.From(_npcId, _locationId);
        DialogueRunner.Instance.StartDialogue(graph, context);
    }
}

