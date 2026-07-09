using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Anchored stat-delta presenter: floats "+$50" / "-2 Reputation" text next to the
/// HUD counter of each stat that has an anchor assigned. Second consumer of
/// GameEvents.StatsChanged alongside GameplayNotificationRelay — stats animated here
/// should have their toast toggle turned off on the relay to avoid double-reporting.
///
/// Same batching contract as the relay: snapshots are diffed once per frame in
/// LateUpdate, so an action that touches money several times in one frame produces
/// a single net flyout. While suppressed (loading a save, main menu) pending changes
/// are swallowed but the baseline still advances, so the first change after a load
/// doesn't animate a giant delta.
/// </summary>
public sealed class StatDeltaFlyoutUI : MonoBehaviour
{
    [Serializable]
    public struct StatAnchor
    {
        public StatType stat;
        [Tooltip("Empty RectTransform placed where this stat's flyouts should spawn (e.g. just right of the counter). Stats without an anchor are ignored.")]
        public RectTransform anchor;
    }

    [Header("Anchors")]
    [SerializeField] private List<StatAnchor> anchors = new();

    [Header("Flyout text")]
    [Tooltip("Disabled TMP_Text child used as the template. Instances are pooled, parented under the stat's anchor, and animated by this component.")]
    [SerializeField] private TMP_Text flyoutTemplate;
    [SerializeField] private Color gainColor = new Color(0.20f, 0.78f, 0.60f);
    [SerializeField] private Color lossColor = new Color(0.90f, 0.35f, 0.30f);

    [Header("Animation")]
    [Tooltip("Lifetime of one flyout, seconds. Rises for the whole duration, fades over the second half.")]
    [SerializeField] private float duration = 1.2f;
    [SerializeField] private float riseDistance = 40f;
    [Tooltip("Vertical spacing when several flyouts are alive on the same anchor (e.g. deltas on consecutive frames).")]
    [SerializeField] private float stackOffset = 22f;

    // Snapshot diffing (same pattern as GameplayNotificationRelay).
    private GameEvents.StatsSnapshot _baseline;
    private GameEvents.StatsSnapshot _latest;
    private bool _hasBaseline;
    private bool _statsDirty;

    private sealed class Flyout
    {
        public TMP_Text text;
        public RectTransform rect;
        public RectTransform anchor;
        public Vector2 startPosition;
        public float age;
    }

    private readonly List<Flyout> _active = new();
    private readonly Stack<Flyout> _pool = new();
    private bool _warnedNoTemplate;

    private void OnEnable()
    {
        GameEvents.StatsChanged += HandleStatsChanged;
    }

    private void OnDisable()
    {
        GameEvents.StatsChanged -= HandleStatsChanged;

        // Don't leave frozen text on screen while disabled.
        for (int i = _active.Count - 1; i >= 0; i--)
            Recycle(_active[i]);
        _active.Clear();
    }

    private void HandleStatsChanged(GameEvents.StatsSnapshot snapshot)
    {
        _latest = snapshot;

        if (!_hasBaseline)
        {
            _baseline = snapshot;
            _hasBaseline = true;
            return;
        }

        _statsDirty = true;
    }

    private void LateUpdate()
    {
        if (_statsDirty)
        {
            _statsDirty = false;

            if (!IsSuppressed())
                SpawnDeltas(_baseline, _latest);

            _baseline = _latest;
        }

        Animate();
    }

    private static bool IsSuppressed()
    {
        if (SaveLoadManager.Instance != null && SaveLoadManager.Instance.IsLoading)
            return true;

        return GameManager.Instance == null || !GameManager.Instance.IsInGameplay;
    }

    private void SpawnDeltas(in GameEvents.StatsSnapshot from, in GameEvents.StatsSnapshot to)
    {
        Spawn(StatType.Money, to.Money - from.Money);
        Spawn(StatType.Influence, to.Influence - from.Influence);
        Spawn(StatType.Strategy, to.Strategy - from.Strategy);
        Spawn(StatType.Networking, to.Networking - from.Networking);
        Spawn(StatType.Reputation, to.Reputation - from.Reputation);
    }

    private void Spawn(StatType stat, int delta)
    {
        if (delta == 0)
            return;

        RectTransform anchor = GetAnchor(stat);
        if (anchor == null)
            return;

        if (flyoutTemplate == null)
        {
            if (!_warnedNoTemplate)
            {
                _warnedNoTemplate = true;
                Debug.LogWarning("[StatDeltaFlyoutUI] No flyout template assigned — stat flyouts dropped.", this);
            }
            return;
        }

        Flyout flyout = _pool.Count > 0 ? _pool.Pop() : Create();
        flyout.anchor = anchor;
        flyout.age = 0f;
        flyout.startPosition = new Vector2(0f, stackOffset * CountAliveOn(anchor));

        flyout.rect.SetParent(anchor, false);
        flyout.rect.anchoredPosition = flyout.startPosition;
        flyout.text.text = StatTypes.FormatDelta(stat, delta);
        flyout.text.color = delta > 0 ? gainColor : lossColor;
        flyout.text.alpha = 1f;
        flyout.text.gameObject.SetActive(true);

        _active.Add(flyout);
    }

    private Flyout Create()
    {
        TMP_Text text = Instantiate(flyoutTemplate, transform);
        text.raycastTarget = false; // never block clicks on the HUD underneath
        return new Flyout { text = text, rect = text.rectTransform };
    }

    private int CountAliveOn(RectTransform anchor)
    {
        int count = 0;
        for (int i = 0; i < _active.Count; i++)
        {
            if (_active[i].anchor == anchor)
                count++;
        }
        return count;
    }

    private void Animate()
    {
        if (_active.Count == 0)
            return;

        // Unscaled so flyouts finish even if gameplay pauses the timescale.
        float dt = Time.unscaledDeltaTime;

        for (int i = _active.Count - 1; i >= 0; i--)
        {
            Flyout flyout = _active[i];
            flyout.age += dt;

            float t = Mathf.Clamp01(flyout.age / Mathf.Max(0.01f, duration));
            float easedRise = 1f - (1f - t) * (1f - t); // ease-out: fast start, gentle settle

            flyout.rect.anchoredPosition = flyout.startPosition + new Vector2(0f, riseDistance * easedRise);
            flyout.text.alpha = t < 0.5f ? 1f : 1f - (t - 0.5f) * 2f;

            if (t >= 1f)
            {
                Recycle(flyout);
                _active.RemoveAt(i);
            }
        }
    }

    private void Recycle(Flyout flyout)
    {
        flyout.text.gameObject.SetActive(false);
        flyout.rect.SetParent(transform, false);
        flyout.anchor = null;
        _pool.Push(flyout);
    }

    private RectTransform GetAnchor(StatType stat)
    {
        for (int i = 0; i < anchors.Count; i++)
        {
            if (anchors[i].stat == stat)
                return anchors[i].anchor;
        }

        return null;
    }
}
