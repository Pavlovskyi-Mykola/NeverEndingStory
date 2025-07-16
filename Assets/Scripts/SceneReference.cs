using UnityEngine;

[System.Serializable]
public struct SceneReference
{
    [SerializeField] private Object sceneAsset;

    public string SceneName => sceneAsset != null ? sceneAsset.name : string.Empty;
}
