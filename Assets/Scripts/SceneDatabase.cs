using System;
using UnityEngine;

[CreateAssetMenu(fileName = "SceneDatabase", menuName = "Game/Scene Database")]
public class SceneDatabase : ScriptableObject
{
    public SceneReference MainMenu;
    public SceneReference PlayersRoom;
    public SceneReference Neighbourhood;
    public SceneReference Cafe;
    public SceneReference Park;

    public SceneReference GetScene(SceneType type)
    {
        return type switch
        {
            SceneType.MainMenu => MainMenu,
            SceneType.PlayersRoom => PlayersRoom,
            SceneType.Neighbourhood => Neighbourhood,
            SceneType.Cafe => Cafe,
            SceneType.Park => Park,
            _ => throw new ArgumentOutOfRangeException(nameof(type), $"Unhandled SceneType: {type}")
        };
    }
}
