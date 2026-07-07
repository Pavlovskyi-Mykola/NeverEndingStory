using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// "The Audit" — spot-the-discrepancy under a return timer. Rows show reported vs
/// actual amounts; flag the padded ones before the target comes back. Wrong flags
/// raise suspicion. Rows are generated procedurally each run.
///
/// Tiers: Gold = all found, no mistakes, fast. Silver = all found, clean-but-slow
/// or one mistake. Bronze = all found, messy. Failed (Caught) = timer out or
/// suspicion maxed. Strategy stat grants bonus time (stats soften difficulty,
/// they don't gate).
/// </summary>
public sealed class AuditMiniGame : MiniGameController
{
    [Header("References")]
    [SerializeField] private RectTransform rowsParent;
    [SerializeField] private AuditRowUI rowPrefab;
    [Tooltip("Image with Image Type = Filled (horizontal). Drains as the target returns.")]
    [SerializeField] private Image timerFill;
    [Tooltip("Image with Image Type = Filled (horizontal). Fills with each wrong flag.")]
    [SerializeField] private Image suspicionFill;
    [SerializeField] private TMP_Text statusLabel;

    [Header("Documents")]
    [SerializeField, Range(4, 24)] private int rowCount = 10;
    [SerializeField, Range(1, 8)] private int discrepancyCount = 3;
    [Tooltip("Discrepancy rows differ from the reported value by at least this fraction.")]
    [SerializeField, Range(0.05f, 0.5f)] private float minDiscrepancyFraction = 0.08f;
    [SerializeField, Range(0.1f, 1f)] private float maxDiscrepancyFraction = 0.45f;

    [Header("Pressure")]
    [Tooltip("Base seconds before the target returns.")]
    [SerializeField] private float baseTimeSeconds = 45f;
    [Tooltip("Bonus seconds per point of Strategy.")]
    [SerializeField] private float secondsPerStrategy = 1.5f;
    [SerializeField] private float maxBonusSeconds = 20f;
    [Tooltip("Wrong flags that max out suspicion -> Caught.")]
    [SerializeField, Range(1, 6)] private int mistakesUntilCaught = 3;

    [Header("Scoring")]
    [Tooltip("Zero mistakes AND at least this fraction of time left = Gold.")]
    [SerializeField, Range(0f, 1f)] private float goldTimeFraction = 0.3f;
    [SerializeField] private float resultDisplaySeconds = 1.4f;

    private static readonly string[] LineItems =
    {
        "Client dinner — Meridian Group", "Team offsite catering", "Software licenses (annual)",
        "Conference travel — Berlin", "Office supplies restock", "Recruiting agency fee",
        "Print & courier services", "Data subscription renewal", "Consulting retainer",
        "Hardware replacement", "Training workshop", "Marketing collateral",
        "Taxi & rideshare", "Hotel — client visit", "Legal review hours", "Networking event tickets",
        "Cloud hosting overage", "Translation services", "Office plants & decor", "Executive coaching"
    };

    private readonly List<AuditRowUI> _rows = new();

    private float _totalTime;
    private float _timeLeft;
    private int _targetCount;
    private int _found;
    private int _mistakes;
    private bool _running;

    protected override void OnStartGame(MiniGameContext context)
    {
        int strategy = PlayerStatsManager.Instance != null ? PlayerStatsManager.Instance.Strategy : 0;
        _totalTime = baseTimeSeconds + Mathf.Min(maxBonusSeconds, strategy * secondsPerStrategy);
        _timeLeft = _totalTime;
        _found = 0;
        _mistakes = 0;

        BuildRows();

        if (suspicionFill != null) suspicionFill.fillAmount = 0f;
        if (timerFill != null) timerFill.fillAmount = 1f;

        _running = true;
        UpdateStatus();
    }

    private void Update()
    {
        if (!_running)
            return;

        _timeLeft -= Time.deltaTime;

        if (timerFill != null)
            timerFill.fillAmount = Mathf.Clamp01(_timeLeft / _totalTime);

        if (_timeLeft <= 0f)
            ResolveCaught("Footsteps — they're back!");
    }

    public override void Abort()
    {
        _running = false;
        base.Abort();
    }

    // -----------------------
    // Row generation
    // -----------------------

    private void BuildRows()
    {
        for (int i = 0; i < _rows.Count; i++)
        {
            if (_rows[i] != null)
                Destroy(_rows[i].gameObject);
        }
        _rows.Clear();

        if (rowsParent == null || rowPrefab == null)
        {
            Debug.LogError("[AuditMiniGame] Rows parent / row prefab not assigned.", this);
            Finish(MiniGameTier.Failed);
            return;
        }

        int count = Mathf.Clamp(rowCount, 4, LineItems.Length);
        _targetCount = Mathf.Clamp(discrepancyCount, 1, count - 1);

        // Shuffled item names and shuffled row indices for the discrepancies.
        int[] itemOrder = ShuffledIndices(LineItems.Length);
        int[] rowOrder = ShuffledIndices(count);

        var discrepancyRows = new HashSet<int>();
        for (int i = 0; i < _targetCount; i++)
            discrepancyRows.Add(rowOrder[i]);

        for (int i = 0; i < count; i++)
        {
            string item = LineItems[itemOrder[i]];
            bool isDiscrepancy = discrepancyRows.Contains(i);

            int reported = Random.Range(12, 480) * 10; // $120 .. $4,800 in tens
            int actual = reported;

            if (isDiscrepancy)
            {
                float fraction = Random.Range(minDiscrepancyFraction, maxDiscrepancyFraction);
                int delta = Mathf.Max(10, Mathf.RoundToInt(reported * fraction / 10f) * 10);
                actual = Random.value < 0.5f ? reported - delta : reported + delta;
                actual = Mathf.Max(10, actual);
            }

            var row = Instantiate(rowPrefab, rowsParent);
            row.Bind(item, reported, actual, isDiscrepancy, HandleRowClicked);
            _rows.Add(row);
        }
    }

    private static int[] ShuffledIndices(int length)
    {
        var indices = new int[length];
        for (int i = 0; i < length; i++) indices[i] = i;

        for (int i = length - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (indices[i], indices[j]) = (indices[j], indices[i]);
        }

        return indices;
    }

    // -----------------------
    // Interaction
    // -----------------------

    private void HandleRowClicked(AuditRowUI row)
    {
        if (!_running || row == null || row.Resolved)
            return;

        row.Resolve();

        if (row.IsDiscrepancy)
        {
            _found++;
            UpdateStatus();

            if (_found >= _targetCount)
                ResolveFinished();
        }
        else
        {
            _mistakes++;

            if (suspicionFill != null)
                suspicionFill.fillAmount = Mathf.Clamp01((float)_mistakes / mistakesUntilCaught);

            if (_mistakes >= mistakesUntilCaught)
                ResolveCaught("Too many disturbed files — you're made.");
            else
                UpdateStatus();
        }
    }

    private void UpdateStatus()
    {
        if (statusLabel != null)
            statusLabel.text = $"Flag the padded entries — {_found}/{_targetCount}";
    }

    // -----------------------
    // Resolution
    // -----------------------

    private void ResolveFinished()
    {
        _running = false;
        LockRows();

        float timeFraction = _totalTime > 0f ? _timeLeft / _totalTime : 0f;

        MiniGameTier tier =
            _mistakes == 0 && timeFraction >= goldTimeFraction ? MiniGameTier.Gold :
            _mistakes <= 1 ? MiniGameTier.Silver :
            MiniGameTier.Bronze;

        if (statusLabel != null)
        {
            statusLabel.text = tier switch
            {
                MiniGameTier.Gold => "Spotless — nobody will ever know.",
                MiniGameTier.Silver => "Got what you came for.",
                _ => "Found it — but you left traces."
            };
        }

        StartCoroutine(FinishAfterDelay(tier));
    }

    private void ResolveCaught(string message)
    {
        if (!_running) return;

        _running = false;
        LockRows();

        if (statusLabel != null)
            statusLabel.text = message;

        StartCoroutine(FinishAfterDelay(MiniGameTier.Failed));
    }

    private void LockRows()
    {
        for (int i = 0; i < _rows.Count; i++)
        {
            if (_rows[i] != null)
                _rows[i].SetInteractable(false);
        }
    }

    private IEnumerator FinishAfterDelay(MiniGameTier tier)
    {
        yield return new WaitForSeconds(resultDisplaySeconds);
        Finish(tier);
    }
}
