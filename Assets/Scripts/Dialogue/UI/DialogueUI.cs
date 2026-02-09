using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class DialogueUI : MonoBehaviour
{
    [Header("Root")]
    [SerializeField] private GameObject panelRoot;

    [Header("Scrollable Conversation Log")]
    [SerializeField] private ScrollRect conversationScroll;
    [SerializeField] private Transform conversationContent;
    [SerializeField] private DialogueLineUI linePrefab;
    [SerializeField] private int maxLines = 200;

    [Header("Controls")]
    [SerializeField] private Button continueButton;
    [SerializeField] private Text continueButtonLabel; // Text component inside the button

    [Header("Choices")]
    [SerializeField] private Transform choicesRoot;
    [SerializeField] private Button choiceButtonPrefab;

    [Header("Speaker Mapping")]
    [Tooltip("Must match the speaker value used in your dialogue graph for player lines.")]
    [SerializeField] private string playerSpeakerName = "Player";

    [Header("Behavior")]
    [SerializeField] private bool clearLogOnHide = false;

    private readonly List<DialogueLineUI> _spawnedLines = new();
    private readonly List<Button> _spawnedChoiceButtons = new();

    private bool _subscribed;

    // Buffer for "player line shown on button"
    private bool _hasPendingPlayerLine;
    private string _pendingSpeaker;
    private string _pendingText;

    private void Awake()
    {
        // IMPORTANT: keep this component on an always-active object.
        // Toggle panelRoot only (panelRoot can be inactive at start).
        TrySubscribe();
        if (panelRoot != null) panelRoot.SetActive(false);
    }

    private void OnEnable()
    {
        TrySubscribe();

        if (continueButton != null)
            continueButton.onClick.AddListener(OnContinueClicked);
    }

    private void OnDisable()
    {
        if (continueButton != null)
            continueButton.onClick.RemoveListener(OnContinueClicked);

        Unsubscribe();
    }

    private void TrySubscribe()
    {
        if (_subscribed) return;
        if (DialogueRunner.Instance == null) return;

        DialogueRunner.Instance.OnShowLine += HandleShowLine;
        DialogueRunner.Instance.OnShowChoices += HandleShowChoices;
        DialogueRunner.Instance.OnHideDialogue += HandleHide;

        _subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!_subscribed) return;

        if (DialogueRunner.Instance != null)
        {
            DialogueRunner.Instance.OnShowLine -= HandleShowLine;
            DialogueRunner.Instance.OnShowChoices -= HandleShowChoices;
            DialogueRunner.Instance.OnHideDialogue -= HandleHide;
        }

        _subscribed = false;
    }

    private void HandleShowLine(string speaker, string text)
    {
        if (panelRoot != null) panelRoot.SetActive(true);

        ClearChoices();
        _hasPendingPlayerLine = false;

        bool isPlayer = IsPlayerSpeaker(speaker);

        if (isPlayer)
        {
            // Player line: preview on button, only append on click
            _hasPendingPlayerLine = true;
            _pendingSpeaker = speaker;
            _pendingText = text;

            SetContinueLabel(text);
            ShowContinue(true);
        }
        else
        {
            // NPC line: append immediately, button just says "Continue"
            AppendLine(speaker, text, isPlayer: false);

            SetContinueLabel("Continue");
            ShowContinue(true);
        }
    }

    private void HandleShowChoices(List<PresentedChoice> choices)
    {
        if (panelRoot != null) panelRoot.SetActive(true);

        // With choices, we don't want the continue button visible.
        ShowContinue(false);
        _hasPendingPlayerLine = false;

        ClearChoices();

        for (int i = 0; i < choices.Count; i++)
        {
            int index = i;

            var btn = Instantiate(choiceButtonPrefab, choicesRoot);
            _spawnedChoiceButtons.Add(btn);

            var txt = btn.GetComponentInChildren<Text>();
            if (txt != null) txt.text = choices[i].Text;

            btn.onClick.AddListener(() =>
            {
                // Show the player choice in the conversation log
                AppendLine(playerSpeakerName, choices[index].Text, isPlayer: true);

                ClearChoices(); // prevent double-click spam

                if (DialogueRunner.Instance != null)
                    DialogueRunner.Instance.Choose(index);
            });
        }
    }

    private void HandleHide()
    {
        ClearChoices();
        _hasPendingPlayerLine = false;

        if (clearLogOnHide)
            ClearConversationLog();

        if (panelRoot != null)
            panelRoot.SetActive(false);
    }

    private void OnContinueClicked()
    {
        if (DialogueRunner.Instance == null) return;

        // If player line was previewed on the button, commit it to the log now.
        if (_hasPendingPlayerLine)
        {
            AppendLine(_pendingSpeaker, _pendingText, isPlayer: true);
            _hasPendingPlayerLine = false;
        }

        DialogueRunner.Instance.Continue();
    }

    private bool IsPlayerSpeaker(string speaker)
    {
        if (string.IsNullOrEmpty(speaker)) return false;
        return string.Equals(speaker, playerSpeakerName, System.StringComparison.OrdinalIgnoreCase);
    }

    private void SetContinueLabel(string label)
    {
        if (continueButtonLabel != null)
            continueButtonLabel.text = label;
    }

    private void ShowContinue(bool show)
    {
        if (continueButton != null)
            continueButton.gameObject.SetActive(show);
    }

    private void AppendLine(string speaker, string text, bool isPlayer)
    {
        if (linePrefab == null || conversationContent == null || conversationScroll == null)
        {
            Debug.LogError("[DialogueUI] Conversation log references are not set (ScrollRect/Content/LinePrefab).", this);
            return;
        }

        var line = Instantiate(linePrefab, conversationContent);
        line.Setup(speaker, text, isPlayer);
        _spawnedLines.Add(line);

        if (maxLines > 0 && _spawnedLines.Count > maxLines)
        {
            Destroy(_spawnedLines[0].gameObject);
            _spawnedLines.RemoveAt(0);
        }

        Canvas.ForceUpdateCanvases();
        conversationScroll.verticalNormalizedPosition = 0f; // scroll to bottom
    }

    public void ClearConversationLog()
    {
        for (int i = 0; i < _spawnedLines.Count; i++)
        {
            if (_spawnedLines[i] != null)
                Destroy(_spawnedLines[i].gameObject);
        }

        _spawnedLines.Clear();

        if (conversationScroll != null)
        {
            Canvas.ForceUpdateCanvases();
            conversationScroll.verticalNormalizedPosition = 1f;
        }
    }

    private void ClearChoices()
    {
        for (int i = 0; i < _spawnedChoiceButtons.Count; i++)
        {
            if (_spawnedChoiceButtons[i] != null)
                Destroy(_spawnedChoiceButtons[i].gameObject);
        }

        _spawnedChoiceButtons.Clear();
    }
}
