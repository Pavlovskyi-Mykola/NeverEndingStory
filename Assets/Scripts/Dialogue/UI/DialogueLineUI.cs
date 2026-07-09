using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One rendered line in the dialogue history (speaker + body) with basic styling.
/// NPC text/name colors come from the speaker's NpcDefinition and are passed in;
/// player colors have no per-speaker asset, so they stay serialized here.
/// </summary>
public class DialogueLineUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TMP_Text speakerText;
    [SerializeField] private TMP_Text bodyText;

    [Header("Optional Layout")]
    [Tooltip("If assigned, will flip alignment left/right based on isPlayer.")]
    [SerializeField] private HorizontalLayoutGroup rowLayout;

    [Header("Player Colors")]
    [SerializeField] private Color playerTextColor = Color.white;
    [SerializeField] private Color playerNameColor = Color.white;

    [Header("Speaker Formatting")]
    [SerializeField] private bool hideSpeakerWhenEmpty = true;

    public void Setup(string speaker, string body, bool isPlayer, Color npcTextColor, Color npcNameColor)
    {
        Color textColor = isPlayer ? playerTextColor : npcTextColor;
        Color nameColor = isPlayer ? playerNameColor : npcNameColor;

        if (speakerText != null)
        {
            bool hasSpeaker = !string.IsNullOrWhiteSpace(speaker);
            if (!hasSpeaker && hideSpeakerWhenEmpty)
            {
                speakerText.gameObject.SetActive(false);
            }
            else
            {
                speakerText.gameObject.SetActive(true);
                speakerText.text = speaker;
                speakerText.color = nameColor;
            }
        }

        if (bodyText != null)
        {
            bodyText.text = body;
            bodyText.color = textColor;
        }

        if (rowLayout != null)
            rowLayout.childAlignment = isPlayer ? TextAnchor.UpperRight : TextAnchor.UpperLeft;
    }
}
