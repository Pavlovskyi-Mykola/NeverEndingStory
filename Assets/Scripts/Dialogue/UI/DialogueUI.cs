using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class DialogueUI : MonoBehaviour
{
    [Header("Root")]
    [SerializeField] private GameObject panelRoot;

    [Header("Line UI (legacy Text)")]
    [SerializeField] private Text speakerText;
    [SerializeField] private Text bodyText;

    [Header("Controls")]
    [SerializeField] private Button continueButton;

    [Header("Choices")]
    [SerializeField] private Transform choicesRoot;
    [SerializeField] private Button choiceButtonPrefab;

    private readonly List<Button> _spawned = new();

    private void OnEnable()
    {
        if (DialogueRunner.Instance == null) return;

        DialogueRunner.Instance.OnShowLine += HandleShowLine;
        DialogueRunner.Instance.OnShowChoices += HandleShowChoices;
        DialogueRunner.Instance.OnHideDialogue += HandleHide;

        if (continueButton != null)
            continueButton.onClick.AddListener(OnContinueClicked);
    }

    private void OnDisable()
    {
        if (DialogueRunner.Instance != null)
        {
            DialogueRunner.Instance.OnShowLine -= HandleShowLine;
            DialogueRunner.Instance.OnShowChoices -= HandleShowChoices;
            DialogueRunner.Instance.OnHideDialogue -= HandleHide;
        }

        if (continueButton != null)
            continueButton.onClick.RemoveListener(OnContinueClicked);
    }

    private void HandleShowLine(string speaker, string text)
    {
        if (panelRoot != null) panelRoot.SetActive(true);

        ClearChoices();

        if (speakerText != null) speakerText.text = speaker;
        if (bodyText != null) bodyText.text = text;

        if (continueButton != null) continueButton.gameObject.SetActive(true);
    }

    private void HandleShowChoices(List<PresentedChoice> choices)
    {
        if (panelRoot != null) panelRoot.SetActive(true);

        if (continueButton != null) continueButton.gameObject.SetActive(false);

        ClearChoices();

        for (int i = 0; i < choices.Count; i++)
        {
            int index = i;
            var btn = Instantiate(choiceButtonPrefab, choicesRoot);
            _spawned.Add(btn);

            var txt = btn.GetComponentInChildren<Text>();
            if (txt != null) txt.text = choices[i].Text;

            btn.onClick.AddListener(() => DialogueRunner.Instance.Choose(index));
        }
    }

    private void HandleHide()
    {
        ClearChoices();
        if (panelRoot != null) panelRoot.SetActive(false);
    }

    private void OnContinueClicked()
    {
        if (DialogueRunner.Instance != null)
            DialogueRunner.Instance.Continue();
    }

    private void ClearChoices()
    {
        for (int i = 0; i < _spawned.Count; i++)
        {
            if (_spawned[i] != null)
                Destroy(_spawned[i].gameObject);
        }
        _spawned.Clear();
    }
}
