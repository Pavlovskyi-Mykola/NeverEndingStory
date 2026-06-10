using UnityEngine;

[System.Serializable]
public class SceneReference
{
    [SerializeField] private Object sceneAsset;

    public string SceneName => sceneAsset != null ? sceneAsset.name : string.Empty;
    public bool IsValid => sceneAsset != null && !string.IsNullOrEmpty(SceneName);
}