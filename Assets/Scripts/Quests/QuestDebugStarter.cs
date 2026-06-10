using UnityEngine;

public sealed class QuestDebugStarter : MonoBehaviour
{
    [SerializeField] private string questId = "q_test_ui";
    [SerializeField] private bool startOnPlay = true;

    private void Start()
    {
        if (!startOnPlay) return;

        if (QuestManager.Instance == null)
        {
            Debug.LogWarning("[QuestDebugStarter] QuestManager.Instance is null.");
            return;
        }

        bool ok = QuestManager.Instance.StartQuest(questId);
        //Debug.Log($"[QuestDebugStarter] StartQuest('{questId}') => {ok}");
    }
}