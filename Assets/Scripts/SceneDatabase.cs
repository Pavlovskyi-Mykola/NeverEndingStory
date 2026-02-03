using UnityEngine;

[CreateAssetMenu(fileName = "SceneDatabase", menuName = "Game/Scene Database")]
public class SceneDatabase : ScriptableObject
{
    public SceneReference Bootstrap; // optional if you always start here
    public SceneReference UI;
    public SceneReference MainMenu;
    public SceneReference Home;
}
