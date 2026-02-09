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
    [SerializeField] private Text linePrefab;
    [SerializeField] private int maxLines = 200;

    [Header("Controls")]
    [SerializeField] private Button continueButton;
    [SerializeField] private Text continueButtonLabel;

    [Header("Choices")]
    [SerializeField] private Transform choicesRoot;
    [SerializeField] private Button choiceButtonPrefab;

    [Header("Behavior")]
    [SerializeField] private bool clearLogOnHide = false;

    private readonly List<Text> _spawnedLines = new();
    private readonly List<Button> _spawnedChoiceButtons = new();

    private bool _subscribed;

    private bool _hasPendingPlayerReply;
    private string _pendingPlayerSpeaker;
    private string _pendingPlayerText;

    private void Awake()
    {
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

        DialogueRunner.Instance.OnTurn += HandleTurn;
        DialogueRunner.Instance.OnHideDialogue += HandleHide;

        _subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!_subscribed) return;
        if (DialogueRunner.Instance != null)
        {
            DialogueRunner.Instance.OnTurn -= HandleTurn;
            DialogueRunner.Instance.OnHideDialogue -= HandleHide;
        }
        _subscribed = false;
    }

    private void HandleTurn(DialogueTurn turn)
    {
        if (panelRoot != null) panelRoot.SetActive(true);

        ClearChoices();
        _hasPendingPlayerReply = false;

        if (turn.HasNpcLine)
            AppendLine(turn.NpcSpeaker, turn.NpcText);

        switch (turn.Action)
        {
            case DialogueTurnAction.Continue:
                ShowContinue(true);
                SetContinueLabel("Continue");
                break;

            case DialogueTurnAction.PlayerReply:
                _hasPendingPlayerReply = true;
                _pendingPlayerSpeaker = string.IsNullOrWhiteSpace(turn.PlayerSpeaker) ? "Player" : turn.PlayerSpeaker;
                _pendingPlayerText = turn.PlayerText;

                ShowContinue(true);
                SetContinueLabel(turn.PlayerText);
                break;

            case DialogueTurnAction.Choices:
                ShowContinue(false);
                SpawnChoices(turn.Choices);
                break;

            case DialogueTurnAction.End:
            default:
                HandleHide();
                break;
        }
    }

    private void OnContinueClicked()
    {
        if (DialogueRunner.Instance == null) return;
        if (DialogueRunner.Instance.IsAdvancing) return;

        if (_hasPendingPlayerReply)
        {
            AppendLine(_pendingPlayerSpeaker, _pendingPlayerText);
            _hasPendingPlayerReply = false;

            DialogueRunner.Instance.SubmitPlayerReply();
            return;
        }

        DialogueRunner.Instance.Continue();
    }

    private void SpawnChoices(List<PresentedChoice> choices)
    {
        if (choices == null) return;

        for (int i = 0; i < choices.Count; i++)
        {
            int index = i;

            var btn = Instantiate(choiceButtonPrefab, choicesRoot);
            _spawnedChoiceButtons.Add(btn);

            var txt = btn.GetComponentInChildren<Text>();
            if (txt != null) txt.text = choices[index].Text;

            btn.onClick.AddListener(() =>
            {
                if (DialogueRunner.Instance == null) return;
                if (DialogueRunner.Instance.IsAdvancing) return;

                AppendLine("Player", choices[index].Text);

                ClearChoices();
                DialogueRunner.Instance.Choose(index);
            });
        }
    }

    private void HandleHide()
    {
        ClearChoices();
        _hasPendingPlayerReply = false;

        if (clearLogOnHide)
            ClearConversationLog();

        if (panelRoot != null)
            panelRoot.SetActive(false);
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

    private void AppendLine(string speaker, string text)
    {
        if (linePrefab == null || conversationContent == null || conversationScroll == null)
        {
            Debug.LogError("[DialogueUI] Conversation log references are not set (ScrollRect/Content/LinePrefab).");
            return;
        }

        var line = Instantiate(linePrefab, conversationContent);
        line.text = $"{speaker}: {text}";
        _spawnedLines.Add(line);

        if (maxLines > 0 && _spawnedLines.Count > maxLines)
        {
            Destroy(_spawnedLines[0].gameObject);
            _spawnedLines.RemoveAt(0);
        }

        Canvas.ForceUpdateCanvases();
        conversationScroll.verticalNormalizedPosition = 0f;
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
