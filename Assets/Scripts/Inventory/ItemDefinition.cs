using UnityEngine;

public enum ItemCategory
{
    Misc,
    KeyItem,
    Consumable,
    Quest,
    Equipment
}

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
    public bool Stackable = true;

    public bool IsValid()
    {
        return !string.IsNullOrWhiteSpace(ItemId);
    }
}