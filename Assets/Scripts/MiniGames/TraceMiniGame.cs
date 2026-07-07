using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// "The Trace" — hack a device before security locates you. The TRACE bar fills
/// constantly; clear nodes to fill your own progress bar first. Nodes alternate
/// between two micro-puzzles:
///   Code match — pick the option that exactly matches the target checksum
///                (decoys differ by one character).
///   Scanner    — a sweep ping-pongs; lock it in the center window.
/// Mistakes trip countermeasures (+trace surge). Strategy stat slows the trace.
///
/// Tiers by trace level at completion: Gold = plenty of headroom, Silver = tight,
/// Bronze = by a hair. Trace full = Caught (alarm).
/// </summary>
public sealed class TraceMiniGame : MiniGameController
{
    private enum NodeType { CodeMatch, Scanner }

    [Header("Shared References")]
    [Tooltip("Image with Image Type = Filled — security locating you.")]
    [SerializeField] private Image traceFill;
    [Tooltip("Image with Image Type = Filled — nodes cleared.")]
    [SerializeField] private Image progressFill;
    [SerializeField] private TMP_Text statusLabel;
    [SerializeField] private TMP_Text feedbackLabel;

    [Header("Code Match Round")]
    [SerializeField] private GameObject codeGroup;
    [SerializeField] private TMP_Text targetCodeLabel;
    [Tooltip("One correct option is placed among these; decoys differ by one character.")]
    [SerializeField] private Button[] optionButtons = new Button[4];
    [SerializeField] private TMP_Text[] optionLabels = new TMP_Text[4];

    [Header("Scanner Round")]
    [SerializeField] private GameObject scannerGroup;
    [Tooltip("Image with Image Type = Filled — the sweeping value.")]
    [SerializeField] private Image scannerFill;
    [SerializeField] private Button lockButton;
    [Tooltip("Half-width of the valid window around center (0.5).")]
    [SerializeField, Range(0.03f, 0.3f)] private float scannerWindow = 0.12f;
    [Tooltip("Sweeps per second.")]
    [SerializeField] private float scannerSpeed = 0.9f;

    [Header("Difficulty")]
    [SerializeField, Range(2, 10)] private int totalNodes = 5;
    [SerializeField] private int codeLength = 4;
    [Tooltip("Seconds for the trace to complete, before the Strategy bonus.")]
    [SerializeField] private float baseTraceSeconds = 40f;
    [SerializeField] private float secondsPerStrategy = 1.5f;
    [SerializeField] private float maxBonusSeconds = 20f;
    [Tooltip("Trace fraction added instantly per mistake.")]
    [SerializeField, Range(0f, 0.5f)] private float mistakePenaltyFraction = 0.08f;

    [Header("Scoring")]
    [Tooltip("Win with trace at or below this = Gold.")]
    [SerializeField, Range(0f, 1f)] private float goldMaxTrace = 0.5f;
    [Tooltip("Win with trace at or below this = Silver (above = Bronze).")]
    [SerializeField, Range(0f, 1f)] private float silverMaxTrace = 0.8f;
    [SerializeField] private float resultDisplaySeconds = 1.4f;

    private const string CodeChars = "0123456789ABCDEF";

    private float _trace;          // 0..1
    private float _traceSeconds;   // full duration after Strategy bonus
    private int _cleared;
    private NodeType _node;
    private int _correctOption;
    private float _scanTime;
    private bool _running;

    protected override void OnStartGame(MiniGameContext context)
    {
        int strategy = PlayerStatsManager.Instance != null ? PlayerStatsManager.Instance.Strategy : 0;
        _traceSeconds = baseTraceSeconds + Mathf.Min(maxBonusSeconds, strategy * secondsPerStrategy);

        _trace = 0f;
        _cleared = 0;
        _node = Random.value < 0.5f ? NodeType.CodeMatch : NodeType.Scanner;

        WireButtons();
        RefreshBars();
        SetFeedback("Connection open. Don't get traced.");
        StartNode(advanceType: false);

        _running = true;
    }

    public override void Abort()
    {
        _running = false;
        base.Abort();
    }

    private void Update()
    {
        if (!_running)
            return;

        _trace += Time.deltaTime / Mathf.Max(1f, _traceSeconds);

        if (_node == NodeType.Scanner && scannerFill != null)
        {
            _scanTime += Time.deltaTime * scannerSpeed;
            scannerFill.fillAmount = Mathf.PingPong(_scanTime, 1f);
        }

        RefreshBars();

        if (_trace >= 1f)
            ResolveCaught();
    }

    // -----------------------
    // Node lifecycle
    // -----------------------

    private void StartNode(bool advanceType = true)
    {
        if (advanceType)
            _node = _node == NodeType.CodeMatch ? NodeType.Scanner : NodeType.CodeMatch;

        if (codeGroup != null) codeGroup.SetActive(_node == NodeType.CodeMatch);
        if (scannerGroup != null) scannerGroup.SetActive(_node == NodeType.Scanner);

        if (statusLabel != null)
        {
            string task = _node == NodeType.CodeMatch ? "match the checksum" : "lock the scanner in the center";
            statusLabel.text = $"NODE {_cleared + 1}/{totalNodes} — {task}";
        }

        if (_node == NodeType.CodeMatch)
            BuildCodeRound();
        else
            _scanTime = Random.value; // start the sweep at a random phase
    }

    private void BuildCodeRound()
    {
        string target = GenerateCode();

        if (targetCodeLabel != null)
            targetCodeLabel.text = target;

        int count = Mathf.Min(optionButtons.Length, optionLabels.Length);
        _correctOption = Random.Range(0, count);

        for (int i = 0; i < count; i++)
        {
            if (optionLabels[i] != null)
                optionLabels[i].text = i == _correctOption ? target : MutateCode(target);

            if (optionButtons[i] != null)
                optionButtons[i].interactable = true;
        }
    }

    private string GenerateCode()
    {
        var sb = new StringBuilder(codeLength);
        for (int i = 0; i < codeLength; i++)
            sb.Append(CodeChars[Random.Range(0, CodeChars.Length)]);

        return sb.ToString();
    }

    private string MutateCode(string target)
    {
        var chars = target.ToCharArray();
        int index = Random.Range(0, chars.Length);

        char replacement;
        do { replacement = CodeChars[Random.Range(0, CodeChars.Length)]; }
        while (replacement == chars[index]);

        chars[index] = replacement;
        return new string(chars);
    }

    // -----------------------
    // Input
    // -----------------------

    private void WireButtons()
    {
        for (int i = 0; i < optionButtons.Length; i++)
        {
            if (optionButtons[i] == null) continue;

            int index = i;
            optionButtons[i].onClick.RemoveAllListeners();
            optionButtons[i].onClick.AddListener(() => HandleOption(index));
        }

        if (lockButton != null)
        {
            lockButton.onClick.RemoveAllListeners();
            lockButton.onClick.AddListener(HandleLock);
        }
    }

    private void HandleOption(int index)
    {
        if (!_running || _node != NodeType.CodeMatch)
            return;

        if (index == _correctOption)
        {
            NodeCleared();
        }
        else
        {
            if (index < optionButtons.Length && optionButtons[index] != null)
                optionButtons[index].interactable = false; // burn the decoy

            Mistake();
        }
    }

    private void HandleLock()
    {
        if (!_running || _node != NodeType.Scanner)
            return;

        float value = Mathf.PingPong(_scanTime, 1f);

        if (Mathf.Abs(value - 0.5f) <= scannerWindow)
            NodeCleared();
        else
            Mistake();
    }

    private void NodeCleared()
    {
        _cleared++;
        SetFeedback("ACCESS GRANTED");
        RefreshBars();

        if (_cleared >= totalNodes)
        {
            ResolveWin();
            return;
        }

        StartNode();
    }

    private void Mistake()
    {
        _trace = Mathf.Min(1f, _trace + mistakePenaltyFraction);
        SetFeedback("COUNTERMEASURE TRIPPED — trace accelerating");
        RefreshBars();

        if (_trace >= 1f)
            ResolveCaught();
    }

    // -----------------------
    // Presentation
    // -----------------------

    private void RefreshBars()
    {
        if (traceFill != null)
            traceFill.fillAmount = Mathf.Clamp01(_trace);

        if (progressFill != null)
            progressFill.fillAmount = (float)_cleared / Mathf.Max(1, totalNodes);
    }

    private void SetFeedback(string text)
    {
        if (feedbackLabel != null)
            feedbackLabel.text = text;
    }

    // -----------------------
    // Resolution
    // -----------------------

    private void ResolveWin()
    {
        _running = false;
        LockInputs();

        MiniGameTier tier =
            _trace <= goldMaxTrace ? MiniGameTier.Gold :
            _trace <= silverMaxTrace ? MiniGameTier.Silver :
            MiniGameTier.Bronze;

        SetFeedback(tier switch
        {
            MiniGameTier.Gold => "Ghost. No trace left behind.",
            MiniGameTier.Silver => "In and out — they'll never prove it.",
            _ => "Barely out — the access log is messy."
        });

        StartCoroutine(FinishAfterDelay(tier));
    }

    private void ResolveCaught()
    {
        if (!_running) return;

        _running = false;
        LockInputs();

        SetFeedback("TRACE COMPLETE — connection burned. They know someone was here.");
        StartCoroutine(FinishAfterDelay(MiniGameTier.Failed));
    }

    private void LockInputs()
    {
        for (int i = 0; i < optionButtons.Length; i++)
        {
            if (optionButtons[i] != null)
                optionButtons[i].interactable = false;
        }

        if (lockButton != null)
            lockButton.interactable = false;
    }

    private IEnumerator FinishAfterDelay(MiniGameTier tier)
    {
        yield return new WaitForSeconds(resultDisplaySeconds);
        Finish(tier);
    }
}
