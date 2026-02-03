using System;
using UnityEngine;

[CreateAssetMenu(fileName = "SceneDatabase", menuName = "Game/Scene Database")]
public class SceneDatabase : ScriptableObject
{
    public SceneReference MainMenu;
    public SceneReference TutorialArea;
    public SceneReference Neighbourhood;
    public SceneReference Cafe;
    public SceneReference Park;

}
