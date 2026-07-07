using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// "The Room" — the persuasion pitch. Fill the NPC's conviction meter before their
/// patience runs out. Each round the NPC shows a tell hinting which argument
/// category lands; the right card raises conviction, the wrong one drains patience.
/// Intel cards (unlocked by world flags gathered through espionage) always land for
/// a massive swing but cost reputation and relationship standing.
///
/// Reading + deduction, no timing. Influence stat grants bonus patience.
/// Tiers by patience remaining on the win: Gold clean, Silver tight, Bronze scraped.
/// Per-NPC encounters = prefab variants with tuned weights/patience + their own
/// MiniGameDefinition.
/// </summary>
public sealed class PersuasionMiniGame : MiniGameController
{
    public enum ArgumentCategory { Logic = 0, Ambition = 1, Flattery = 2, Pressure = 3 }

    [System.Serializable]
    public sealed class IntelCard
    {
        [Tooltip("Card appears only when this flag is set (e.g. intel.bob.expense_fraud).")]
        [FlagId] public string RequiredFlag;
        public string Label = "Leverage";
        public int ConvictionGain = 40;
        [Tooltip("Reputation lost when played (ruthless moves have a price).")]
        public int ReputationCost = 2;
        [Tooltip("Npc whose relationship suffers when played (usually the one in the room). Empty = no relationship cost.")]
        public string RelationshipTargetNpcId;
        public int RelationshipCost = 5;
    }

    [Header("References")]
    [Tooltip("Image with Image Type = Filled — fills toward the win.")]
    [SerializeField] private Image convictionFill;
    [Tooltip("Image with Image Type = Filled — drains on misses.")]
    [SerializeField] private Image patienceFill;
    [SerializeField] private TMP_Text tellLabel;
    [SerializeField] private TMP_Text feedbackLabel;
    [SerializeField] private RectTransform cardsParent;
    [SerializeField] private PersuasionCardUI cardPrefab;

    [Header("Meters")]
    [SerializeField] private int targetConviction = 100;
    [SerializeField] private int basePatience = 100;
    [Tooltip("Bonus patience per point of Influence (stats soften difficulty).")]
    [SerializeField] private float patiencePerInfluence = 2f;
    [SerializeField] private float maxPatienceBonus = 40f;

    [Header("Plays")]
    [SerializeField] private int hitConviction = 22;
    [SerializeField] private int missPatience = 18;

    [Tooltip("How receptive this NPC is to each category (Logic, Ambition, Flattery, Pressure). Higher = tells favor it more. Tune per NPC via prefab variants.")]
    [SerializeField] private int[] categoryWeights = { 2, 2, 2, 2 };

    [Header("Intel Cards")]
    [SerializeField] private List<IntelCard> intelCards = new();

    [Header("Scoring")]
    [SerializeField, Range(0f, 1f)] private float goldPatienceFraction = 0.6f;
    [SerializeField, Range(0f, 1f)] private float silverPatienceFraction = 0.3f;
    [SerializeField] private float resultDisplaySeconds = 1.4f;

    private static readonly string[] CardTitles = { "Cite the numbers", "Appeal to ambition", "Flatter", "Pressure" };

    private static readonly string[][] Tells =
    {
        new[] { "They keep tapping the revenue table.", "\"Walk me through the actual figures.\"", "They re-read your summary, twice." },
        new[] { "\"And where does this leave me?\" they murmur.", "Their eyes drift to the corner office.", "They straighten up at the word 'promotion'." },
        new[] { "They angle their industry award toward you.", "\"You've heard about my last project, surely.\"", "They smooth their lapel, waiting." },
        new[] { "They glance nervously at the door.", "\"The board meets Friday...\" they trail off.", "They check their phone for the third time." }
    };

    private static readonly string[] HitLines = { "They nod along.", "You've got their attention.", "That landed." };
    private static readonly string[] MissLines = { "A cold stare. That missed.", "They sigh and check the time.", "Wrong angle — they stiffen." };

    private readonly List<PersuasionCardUI> _cards = new();

    private int _conviction;
    private float _patience;
    private float _maxPatience;
    private ArgumentCategory _receptive;
    private bool _running;

    protected override void OnStartGame(MiniGameContext context)
    {
        int influence = PlayerStatsManager.Instance != null ? PlayerStatsManager.Instance.Influence : 0;
        _maxPatience = basePatience + Mathf.Min(maxPatienceBonus, influence * patiencePerInfluence);
        _patience = _maxPatience;
        _conviction = 0;

        BuildCards();
        NextRound(first: true);
        RefreshMeters();

        if (feedbackLabel != null)
            feedbackLabel.text = "Make your case.";

        _running = true;
    }

    public override void Abort()
    {
        _running = false;
        base.Abort();
    }

    // -----------------------
    // Setup
    // -----------------------

    private void BuildCards()
    {
        for (int i = 0; i < _cards.Count; i++)
        {
            if (_cards[i] != null)
                Destroy(_cards[i].gameObject);
        }
        _cards.Clear();

        if (cardsParent == null || cardPrefab == null)
        {
            Debug.LogError("[PersuasionMiniGame] Cards parent / card prefab not assigned.", this);
            Finish(MiniGameTier.Failed);
            return;
        }

        for (int i = 0; i < CardTitles.Length; i++)
        {
            var category = (ArgumentCategory)i;
            var card = Instantiate(cardPrefab, cardsParent);
            card.Bind(CardTitles[i], null, isIntel: false, () => PlayArgument(category, card));
            _cards.Add(card);
        }

        // Intel cards appear only when their flag was earned (espionage output).
        for (int i = 0; i < intelCards.Count; i++)
        {
            var intel = intelCards[i];
            if (intel == null || string.IsNullOrWhiteSpace(intel.RequiredFlag))
                continue;

            if (!WorldFlags.Get(intel.RequiredFlag))
                continue;

            var captured = intel;
            var card = Instantiate(cardPrefab, cardsParent);
            card.Bind(captured.Label, BuildIntelSubtitle(captured), isIntel: true, () => PlayIntel(captured, card));
            _cards.Add(card);
        }
    }

    private static string BuildIntelSubtitle(IntelCard intel)
    {
        var parts = new List<string>(2);
        if (intel.ReputationCost > 0) parts.Add($"-{intel.ReputationCost} Reputation");
        if (intel.RelationshipCost > 0 && !string.IsNullOrWhiteSpace(intel.RelationshipTargetNpcId))
            parts.Add("burns trust");

        return parts.Count > 0 ? string.Join(" · ", parts) : "no cost";
    }

    // -----------------------
    // Rounds
    // -----------------------

    private void NextRound(bool first = false)
    {
        var previous = _receptive;
        _receptive = PickCategory();

        // Avoid the same tell twice in a row so rounds feel distinct.
        if (!first && _receptive == previous)
            _receptive = PickCategory();

        if (tellLabel != null)
        {
            var pool = Tells[(int)_receptive];
            tellLabel.text = pool[Random.Range(0, pool.Length)];
        }
    }

    private ArgumentCategory PickCategory()
    {
        int total = 0;
        for (int i = 0; i < 4; i++)
            total += CategoryWeight(i);

        if (total <= 0)
            return (ArgumentCategory)Random.Range(0, 4);

        int roll = Random.Range(0, total);
        for (int i = 0; i < 4; i++)
        {
            roll -= CategoryWeight(i);
            if (roll < 0)
                return (ArgumentCategory)i;
        }

        return ArgumentCategory.Logic;
    }

    private int CategoryWeight(int index)
    {
        if (categoryWeights == null || index >= categoryWeights.Length)
            return 1;

        return Mathf.Max(0, categoryWeights[index]);
    }

    // -----------------------
    // Plays
    // -----------------------

    private void PlayArgument(ArgumentCategory category, PersuasionCardUI card)
    {
        if (!_running)
            return;

        if (category == _receptive)
        {
            _conviction += hitConviction;
            SetFeedback(HitLines[Random.Range(0, HitLines.Length)]);
        }
        else
        {
            _patience -= missPatience;
            SetFeedback(MissLines[Random.Range(0, MissLines.Length)]);
        }

        AfterPlay();
    }

    private void PlayIntel(IntelCard intel, PersuasionCardUI card)
    {
        if (!_running)
            return;

        card.SetUsed();

        _conviction += intel.ConvictionGain;

        if (intel.ReputationCost > 0 && PlayerStatsManager.Instance != null)
            PlayerStatsManager.Instance.Add(StatType.Reputation, -intel.ReputationCost);

        if (intel.RelationshipCost > 0 && !string.IsNullOrWhiteSpace(intel.RelationshipTargetNpcId) &&
            RelationshipManager.Instance != null)
            RelationshipManager.Instance.AddPoints(intel.RelationshipTargetNpcId, -intel.RelationshipCost);

        SetFeedback("The color drains from their face. They fold.");
        AfterPlay();
    }

    private void AfterPlay()
    {
        RefreshMeters();

        if (_conviction >= targetConviction)
        {
            ResolveWin();
            return;
        }

        if (_patience <= 0f)
        {
            ResolveLoss();
            return;
        }

        NextRound();
    }

    private void SetFeedback(string text)
    {
        if (feedbackLabel != null)
            feedbackLabel.text = text;
    }

    private void RefreshMeters()
    {
        if (convictionFill != null)
            convictionFill.fillAmount = Mathf.Clamp01((float)_conviction / Mathf.Max(1, targetConviction));

        if (patienceFill != null)
            patienceFill.fillAmount = Mathf.Clamp01(_patience / Mathf.Max(1f, _maxPatience));
    }

    // -----------------------
    // Resolution
    // -----------------------

    private void ResolveWin()
    {
        _running = false;
        LockCards();

        float fraction = _patience / Mathf.Max(1f, _maxPatience);

        MiniGameTier tier =
            fraction >= goldPatienceFraction ? MiniGameTier.Gold :
            fraction >= silverPatienceFraction ? MiniGameTier.Silver :
            MiniGameTier.Bronze;

        if (tellLabel != null)
        {
            tellLabel.text = tier switch
            {
                MiniGameTier.Gold => "\"You had me halfway through. Consider it done.\"",
                MiniGameTier.Silver => "\"...Fine. You've made your point.\"",
                _ => "\"Against my better judgment — alright.\""
            };
        }

        StartCoroutine(FinishAfterDelay(tier));
    }

    private void ResolveLoss()
    {
        _running = false;
        LockCards();

        if (tellLabel != null)
            tellLabel.text = "\"We're done here.\" They gather their things.";

        StartCoroutine(FinishAfterDelay(MiniGameTier.Failed));
    }

    private void LockCards()
    {
        for (int i = 0; i < _cards.Count; i++)
        {
            if (_cards[i] != null)
                _cards[i].SetInteractable(false);
        }
    }

    private IEnumerator FinishAfterDelay(MiniGameTier tier)
    {
        yield return new WaitForSeconds(resultDisplaySeconds);
        Finish(tier);
    }
}
