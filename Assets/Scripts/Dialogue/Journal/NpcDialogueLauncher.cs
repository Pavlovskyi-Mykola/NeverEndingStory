using UnityEngine;

public class NpcDialogueLauncher : MonoBehaviour
{
    private string _npcId;
    private string _locationId;
    private DialogueRouteSet _routes;

    public void Init(string npcId, string locationId, DialogueRouteSet routes)
    {
        _npcId = npcId;
        _locationId = locationId;
        _routes = routes;
    }

    public void TryStartDialogue()
    {
        if (GameManager.Instance != null && GameManager.Instance.IsInDialogue)
            return;

        if (DialogueRunner.Instance == null) return;
        if (_routes == null) return;

        var ctx = DialogueSelectorContext.From(_npcId, _locationId);
        var graph = DialogueSelector.Select(_routes, ctx);

        if (graph == null)
        {
            Debug.LogWarning($"[NpcDialogueLauncher] No dialogue graph selected for npc='{_npcId}' location='{_locationId}'.");
            return;
        }

        DialogueRunner.Instance.StartDialogue(graph);
    }
}

