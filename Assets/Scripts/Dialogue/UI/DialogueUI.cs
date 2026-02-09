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

    [Header("Choices")]
    [SerializeField] private Transform choicesRoot;
    [SerializeField] private Button choiceButtonPrefab;

    [Header("Behavior")]
    [SerializeField] private bool clearLogOnHide = false;

    private readonly List<Text> _spawnedLines = new();
    private readonly List<Button> _spawnedChoiceButtons = new();

    private bool _subscribed;

    private void Awake()
    {
        // Important: this component should live on an object that stays ACTIVE.
        // Only panelRoot should be toggled on/off.
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

        AppendLine(speaker, text);

        if (continueButton != null)
            continueButton.gameObject.SetActive(true);
    }

    private void HandleShowChoices(List<PresentedChoice> choices)
    {
        if (panelRoot != null) panelRoot.SetActive(true);

        if (continueButton != null)
            continueButton.gameObject.SetActive(false);

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
                if (DialogueRunner.Instance != null)
                    DialogueRunner.Instance.Choose(index);
            });
        }
    }

    private void HandleHide()
    {
        ClearChoices();

        if (clearLogOnHide)
            ClearConversationLog();

        if (panelRoot != null)
            panelRoot.SetActive(false);
    }

    private void OnContinueClicked()
    {
        if (DialogueRunner.Instance != null)
            DialogueRunner.Instance.Continue();
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
