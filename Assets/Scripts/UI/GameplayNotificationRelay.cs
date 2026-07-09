using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Translates gameplay events (stat/item deltas, promotions, blocked travel) into
/// player-facing Notifications. Companion to QuestNotificationRelay; drop both on a
/// persistent object.
///
/// Stat and item changes are batched per frame in LateUpdate (same pattern as
/// QuestManager): an action that grants money + stats + items and consumes items in
/// one frame produces a handful of net-delta toasts instead of one per event.
/// </summary>
public sealed class GameplayNotificationRelay : MonoBehaviour
{
    [Header("Stat toasts (per stat)")]
    [Tooltip("Off by default: money deltas are animated at the HUD counter by StatDeltaFlyoutUI. Turn on only if that widget isn't in the scene.")]
    [SerializeField] private bool notifyMoney = false;
    [SerializeField] private bool notifyInfluence = true;
    [SerializeField] private bool notifyStrategy = true;
    [SerializeField] private bool notifyNetworking = true;
    [SerializeField] private bool notifyReputation = true;

    [Header("Toggles")]
    [SerializeField] private bool notifyItemChanges = true;
    [SerializeField] private bool notifyPromotions = true;
    [SerializeField] private bool notifyTravelBlocked = true;
    [SerializeField] private bool notifyRelationships = true;

    // Stats: diff snapshots once per frame (GameEvents.StatsChanged carries no delta).
    private GameEvents.StatsSnapshot _baseline;
    private GameEvents.StatsSnapshot _latest;
    private bool _hasBaseline;
    private bool _statsDirty;

    // Items: accumulate net delta per item within the frame.
    private readonly Dictionary<string, int> _pendingItemDeltas = new();

    private void OnEnable()
    {
        GameEvents.StatsChanged += HandleStatsChanged;
        GameEvents.InventoryChanged += HandleInventoryChanged;

        CareerManager.InstanceReady += HandleCareerReady;
        if (CareerManager.Instance != null)
            HandleCareerReady(CareerManager.Instance);

        GameManager.InstanceReady += HandleGameManagerReady;
        if (GameManager.Instance != null)
            HandleGameManagerReady(GameManager.Instance);

        RelationshipManager.InstanceReady += HandleRelationshipsReady;
        if (RelationshipManager.Instance != null)
            HandleRelationshipsReady(RelationshipManager.Instance);
    }

    private void OnDisable()
    {
        GameEvents.StatsChanged -= HandleStatsChanged;
        GameEvents.InventoryChanged -= HandleInventoryChanged;

        CareerManager.InstanceReady -= HandleCareerReady;
        if (CareerManager.Instance != null)
            CareerManager.Instance.OnTierChanged -= HandleTierChanged;

        GameManager.InstanceReady -= HandleGameManagerReady;
        if (GameManager.Instance != null)
            GameManager.Instance.TravelBlocked -= HandleTravelBlocked;

        RelationshipManager.InstanceReady -= HandleRelationshipsReady;
        if (RelationshipManager.Instance != null)
            RelationshipManager.Instance.OnLevelChanged -= HandleRelationshipLevelChanged;
    }

    private void HandleRelationshipsReady(RelationshipManager relationships)
    {
        relationships.OnLevelChanged -= HandleRelationshipLevelChanged;
        relationships.OnLevelChanged += HandleRelationshipLevelChanged;
    }

    private void HandleCareerReady(CareerManager career)
    {
        career.OnTierChanged -= HandleTierChanged;
        career.OnTierChanged += HandleTierChanged;
    }

    private void HandleGameManagerReady(GameManager gm)
    {
        gm.TravelBlocked -= HandleTravelBlocked;
        gm.TravelBlocked += HandleTravelBlocked;
    }

    // -----------------------
    // Event intake (no posting here — batched in LateUpdate)
    // -----------------------

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

    private void HandleInventoryChanged(GameEvents.InventoryChange change)
    {
        if (string.IsNullOrEmpty(change.ItemId))
            return;

        int delta = change.NewCount - change.OldCount;
        if (delta == 0)
            return;

        _pendingItemDeltas.TryGetValue(change.ItemId, out int accumulated);
        _pendingItemDeltas[change.ItemId] = accumulated + delta;
    }

    private void LateUpdate()
    {
        // While suppressed (loading a save, main menu), swallow the pending changes
        // but still advance the baseline — otherwise the first change after a load
        // would toast a giant delta against pre-load values.
        bool suppressed = IsSuppressed();

        if (_statsDirty)
        {
            _statsDirty = false;

            if (!suppressed)
                PostStatDeltas(_baseline, _latest);

            _baseline = _latest;
        }

        if (_pendingItemDeltas.Count > 0)
        {
            if (!suppressed && notifyItemChanges)
                PostItemDeltas();

            _pendingItemDeltas.Clear();
        }
    }

    private static bool IsSuppressed()
    {
        if (SaveLoadManager.Instance != null && SaveLoadManager.Instance.IsLoading)
            return true;

        return GameManager.Instance == null || !GameManager.Instance.IsInGameplay;
    }

    // -----------------------
    // Posting
    // -----------------------

    private void PostStatDeltas(in GameEvents.StatsSnapshot from, in GameEvents.StatsSnapshot to)
    {
        PostStatDelta(StatType.Money, to.Money - from.Money, notifyMoney);
        PostStatDelta(StatType.Influence, to.Influence - from.Influence, notifyInfluence);
        PostStatDelta(StatType.Strategy, to.Strategy - from.Strategy, notifyStrategy);
        PostStatDelta(StatType.Networking, to.Networking - from.Networking, notifyNetworking);
        PostStatDelta(StatType.Reputation, to.Reputation - from.Reputation, notifyReputation);
    }

    private static void PostStatDelta(StatType stat, int delta, bool enabled)
    {
        if (!enabled || delta == 0)
            return;

        Notifications.Post(StatTypes.FormatDelta(stat, delta), null, NotificationType.StatChange);
    }

    private void PostItemDeltas()
    {
        foreach (var kv in _pendingItemDeltas)
        {
            int delta = kv.Value;
            if (delta == 0)
                continue; // gained and spent in the same frame — net zero, stay quiet

            string name = GetItemName(kv.Key);
            string text = delta > 0 ? $"+{delta} {name}" : $"-{-delta} {name}";
            Notifications.Post(text, null, NotificationType.ItemChange);
        }
    }

    private void HandleTierChanged(CareerTier tier)
    {
        if (!notifyPromotions)
            return;

        Notifications.Post("Promotion", $"You are now {tier}", NotificationType.Promotion);
    }

    private void HandleTravelBlocked(SceneReference target, TravelBlockReason reason)
    {
        if (!notifyTravelBlocked)
            return;

        string message = reason switch
        {
            TravelBlockReason.FloorLocked => "This floor isn't unlocked yet.",
            TravelBlockReason.TimeWindow => "Closed at this time.",
            TravelBlockReason.MissingItems => "You're missing something required to enter.",
            _ => "You can't go there right now."
        };

        Notifications.Post(GetLocationName(target), message, NotificationType.Blocked);
    }

    private void HandleRelationshipLevelChanged(string npcId, int oldLevel, int newLevel)
    {
        if (!notifyRelationships)
            return;

        string name = GetNpcName(npcId);
        string message = newLevel > oldLevel
            ? $"{name} trusts you more."
            : $"{name} trusts you less.";

        Notifications.Post(name, message, NotificationType.Relationship);
    }

    // -----------------------
    // Display-name helpers
    // -----------------------

    private static string GetNpcName(string npcId)
    {
        var nm = NpcManager.Instance;
        if (nm != null && nm.Npcs != null)
        {
            for (int i = 0; i < nm.Npcs.Count; i++)
            {
                var def = nm.Npcs[i];
                if (def != null && string.Equals(def.NpcId, npcId, System.StringComparison.Ordinal))
                    return string.IsNullOrWhiteSpace(def.DisplayName) ? npcId : def.DisplayName;
            }
        }

        return npcId;
    }

    private static string GetItemName(string itemId)
    {
        var inventory = InventoryManager.Instance;
        if (inventory != null &&
            inventory.TryGetDefinition(itemId, out var def) &&
            def != null && !string.IsNullOrWhiteSpace(def.DisplayName))
            return def.DisplayName;

        return itemId;
    }

    private static string GetLocationName(SceneReference target)
    {
        if (target == null || !target.IsValid)
            return "Location";

        var db = SceneDatabase.Instance;
        if (db != null && db.TryGetLocation(target, out var entry) && !string.IsNullOrWhiteSpace(entry.Id))
            return entry.Id;

        return target.SceneName;
    }
}
