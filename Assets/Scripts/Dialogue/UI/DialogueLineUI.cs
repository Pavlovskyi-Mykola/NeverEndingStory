using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One rendered line in the dialogue history (speaker + body) with basic styling.
/// Uses legacy UI Text (no TMP).
/// </summary>
public class DialogueLineUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Text speakerText;
    [SerializeField] private Text bodyText;

    [Header("Optional Layout")]
    [Tooltip("If assigned, will flip alignment left/right based on isPlayer.")]
    [SerializeField] private HorizontalLayoutGroup rowLayout;

    [Tooltip("Optional bubble background.")]
    [SerializeField] private Image bubbleImage;

    [Header("Colors")]
    [SerializeField] private Color npcTextColor = Color.white;
    [SerializeField] private Color playerTextColor = Color.white;
    [SerializeField] private Color npcBubbleColor = new Color(0f, 0f, 0f, 0.35f);
    [SerializeField] private Color playerBubbleColor = new Color(0f, 0f, 0f, 0.35f);

    [Header("Speaker Formatting")]
    [SerializeField] private bool hideSpeakerWhenEmpty = true;

    public void Setup(string speaker, string body, bool isPlayer)
    {
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
            }
        }

        if (bodyText != null)
            bodyText.text = body;

        var textColor = isPlayer ? playerTextColor : npcTextColor;
        if (speakerText != null) speakerText.color = textColor;
        if (bodyText != null) bodyText.color = textColor;

        if (bubbleImage != null)
            bubbleImage.color = isPlayer ? playerBubbleColor : npcBubbleColor;

        if (rowLayout != null)
            rowLayout.childAlignment = isPlayer ? TextAnchor.UpperRight : TextAnchor.UpperLeft;
    }
}
