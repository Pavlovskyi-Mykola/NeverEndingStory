using UnityEngine;

[System.Serializable]
public struct SceneReference
{
    [SerializeField] private Object sceneAsset;
    [SerializeField] private int scenePriority;

    public string SceneName => sceneAsset != null ? sceneAsset.name : string.Empty;
    public int ScenePriority => scenePriority;
}
