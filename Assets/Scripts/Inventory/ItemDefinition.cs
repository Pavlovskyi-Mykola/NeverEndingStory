using UnityEngine;

/// <summary>
/// Broad grouping used for inventory filtering/sorting and gameplay rules.
/// </summary>
public enum ItemCategory
{
    /// <summary>Anything that doesn't fit the other buckets: junk, trinkets, collectibles, crafting bits.</summary>
    Misc,

    /// <summary>Unique progression items (keycards, keys, passes). Almost always non-stackable,
    /// and usually can't be dropped or sold so the player can't lock themselves out of progress.</summary>
    KeyItem,

    /// <summary>Items consumed on use: food, drinks, medicine, buff items. Stackable as a rule.</summary>
    Consumable,

    /// <summary>Items granted or required by quests. Typically hidden from shops/dropping and
    /// removed automatically when the owning quest completes or fails.</summary>
    Quest,

    /// <summary>Wearable or holdable gear that changes the player's stats or appearance.</summary>
    Equipment
}

/// <summary>
/// Static data for one item type. Runtime state (who owns how many) lives in <see cref="InventoryManager"/>.
///
/// Common fields games in this genre add here when needed (deliberately left out until a system uses them):
/// - MaxStackSize (int)      — cap per stack instead of the boolean Stackable, e.g. potions stack to 99.
/// - Value / Price (int)     — base buy/sell price once shops or trading exist.
/// - Rarity (enum)           — common/rare/etc., usually just drives UI color and drop tables.
/// - Weight (float)          — for encumbrance/carry-limit systems.
/// - Droppable / Sellable    — safety flags so key/quest items can't be thrown away.
/// - UseEffect(s)            — a ScriptableObject list describing what happens on "Use" (heal, buff, unlock).
/// - EquipSlot + stat mods   — for Equipment: which slot it occupies and what it changes.
/// - WorldPrefab             — prefab spawned when the item is dropped into the scene.
/// - Tags (string[])         — free-form filtering for crafting recipes, shop inventories, quest checks.
/// - SortOrder (int)         — stable ordering in inventory UI beyond category grouping.
/// </summary>
[CreateAssetMenu(fileName = "Item", menuName = "Game/Inventory/Item Definition")]
public class ItemDefinition : ScriptableObject
{
    [Header("Identity")]
    public string ItemId;

    [Header("Display")]
    public string DisplayName;
    [TextArea(2, 5)] public string Description;
    public Sprite Icon;

    [Header("Rules")]
    public ItemCategory Category = ItemCategory.Misc;

    [Tooltip("If false the player can hold at most one of this item; further adds are ignored. Use for key items and other uniques.")]
    public bool Stackable = true;

    public bool IsValid()
    {
        return !string.IsNullOrWhiteSpace(ItemId);
    }
}