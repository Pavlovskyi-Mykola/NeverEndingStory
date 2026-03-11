using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "FlagDatabase", menuName = "Game/Flag Database")]
public class FlagDatabase : ScriptableObject
{
    [System.Serializable]
    public class FlagEntry
    {
        public string Key;
        [TextArea] public string Description;
    }

    public List<FlagEntry> Flags = new List<FlagEntry>();
}