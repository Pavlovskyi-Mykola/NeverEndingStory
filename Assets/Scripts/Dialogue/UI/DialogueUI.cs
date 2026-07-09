using System.Collections.Generic;
using TMPro;
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

    [Header("Choices")]
    [SerializeField] private Transform choicesRoot;
    [SerializeField] private Button choiceButtonPrefab;

    [Header("Speaker Mapping")]
    [SerializeField] private string playerSpeakerName = NpcManager.PlayerSpeakerId;

    private readonly List<DialogueLineUI> _spawnedLines = new();
    private readonly List<Button> _spawnedChoiceButtons = new();

    private void Awake()
    {
        if (panelRoot != null) panelRoot.SetActive(false);
    }

    private void OnEnable()
    {
        // InstanceReady covers the runner waking up after us; the Instance check
        // covers it waking up first — so the UI never misses turn events.
        DialogueRunner.InstanceReady += HandleRunnerReady;
        if (DialogueRunner.Instance != null)
            HandleRunnerReady(DialogueRunner.Instance);
    }

    private void OnDisable()
    {
        DialogueRunner.InstanceReady -= HandleRunnerReady;
        if (DialogueRunner.Instance != null)
        {
            DialogueRunner.Instance.OnTurn -= HandleTurn;
            DialogueRunner.Instance.OnHideDialogue -= HandleHide;
        }
    }

    private void HandleRunnerReady(DialogueRunner runner)
    {
        runner.OnTurn -= HandleTurn;
        runner.OnTurn += HandleTurn;
        runner.OnHideDialogue -= HandleHide;
        runner.OnHideDialogue += HandleHide;
    }

    private void HandleTurn(DialogueTurn turn)
    {
        if (panelRoot != null) panelRoot.SetActive(true);

        ClearChoices();

        // Append NPC line if provided
        if (turn.HasNpcLine)
            AppendLine(turn.NpcSpeaker, turn.NpcText, isPlayer: false);

        switch (turn.Action)
        {
            case DialogueTurnAction.Continue:
                SpawnActionButton("Continue", () => DialogueRunner.Instance.Continue());
                break;

            case DialogueTurnAction.PlayerReply:
            {
                string speaker = string.IsNullOrWhiteSpace(turn.PlayerSpeaker) ? playerSpeakerName : turn.PlayerSpeaker;
                string text = turn.PlayerText;
                SpawnActionButton(text, () =>
                {
                    AppendLine(speaker, text, isPlayer: true);
                    DialogueRunner.Instance.SubmitPlayerReply();
                });
                break;
            }

            case DialogueTurnAction.Choices:
                SpawnChoices(turn.Choices);
                break;

            case DialogueTurnAction.Close:
                SpawnActionButton("Continue", () => DialogueRunner.Instance.CloseDialogue());
                break;
        }
    }

    // Continue / Close / forced player reply all present as a single button,
    // spawned from the same prefab as choices.
    private void SpawnActionButton(string label, System.Action onClick)
    {
        if (choiceButtonPrefab == null || choicesRoot == null)
        {
            Debug.LogError("[DialogueUI] Choice button prefab / root are not set.", this);
            return;
        }

        var btn = Instantiate(choiceButtonPrefab, choicesRoot);
        _spawnedChoiceButtons.Add(btn);

        SetButtonLabel(btn, label);

        btn.onClick.AddListener(() =>
        {
            if (DialogueRunner.Instance == null) return;
            if (DialogueRunner.Instance.IsAdvancing) return;
            onClick();
        });
    }

    private void SpawnChoices(List<PresentedChoice> choices)
    {
        if (choices == null) return;

        for (int i = 0; i < choices.Count; i++)
        {
            int index = i;

            var btn = Instantiate(choiceButtonPrefab, choicesRoot);
            _spawnedChoiceButtons.Add(btn);

            // Prefix each choice with its option number: "1. ", "2. ", ...
            SetButtonLabel(btn, $"{index + 1}. {choices[index].Text}");

            btn.onClick.AddListener(() =>
            {
                if (DialogueRunner.Instance == null) return;
                if (DialogueRunner.Instance.IsAdvancing) return;

                AppendLine(playerSpeakerName, choices[index].Text, isPlayer: true);

                ClearChoices();
                DialogueRunner.Instance.Choose(index);
            });
        }
    }

    private static void SetButtonLabel(Button btn, string label)
    {
        var txt = btn.GetComponentInChildren<TMP_Text>();
        if (txt != null) txt.text = label;
    }

    private void HandleHide()
    {
        ClearChoices();
        ClearConversationLog();

        if (panelRoot != null)
            panelRoot.SetActive(false);
    }

    private void AppendLine(string speaker, string text, bool isPlayer)
    {
        if (linePrefab == null || conversationContent == null || conversationScroll == null)
        {
            Debug.LogError("[DialogueUI] Conversation log references are not set (ScrollRect/Content/LinePrefab).", this);
            return;
        }

        Color npcTextColor = Color.white;
        Color npcNameColor = Color.white;
        if (!isPlayer)
            ResolveNpcColors(out npcTextColor, out npcNameColor);

        // Fade the previous latest line into "history" so the new one stands out.
        if (_spawnedLines.Count > 0)
            _spawnedLines[_spawnedLines.Count - 1].SetDimmed(true);

        var line = Instantiate(linePrefab, conversationContent);
        line.Setup(speaker, text, isPlayer, npcTextColor, npcNameColor);
        line.SetDimmed(false);
        _spawnedLines.Add(line);

        if (maxLines > 0 && _spawnedLines.Count > maxLines)
        {
            Destroy(_spawnedLines[0].gameObject);
            _spawnedLines.RemoveAt(0);
        }

        // Layout refresh (helps avoid overlap)
        LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)conversationContent);

        Canvas.ForceUpdateCanvases();
        conversationScroll.verticalNormalizedPosition = 0f;
    }

    // NPC dialogue colors live on the speaker's NpcDefinition — resolve them for
    // the current conversation partner. Falls back to white if unavailable.
    private void ResolveNpcColors(out Color textColor, out Color nameColor)
    {
        textColor = Color.white;
        nameColor = Color.white;

        if (NpcManager.Instance == null || DialogueRunner.Instance == null)
            return;

        var def = NpcManager.Instance.GetById(DialogueRunner.Instance.CurrentNpcId);
        if (def == null)
            return;

        textColor = def.DialogueTextColor;
        nameColor = def.DialogueNameColor;
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
