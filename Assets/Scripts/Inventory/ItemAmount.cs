using System;

[Serializable]
public class ItemAmount
{
    [ItemId] public string ItemId;
    public int Count = 1;
}