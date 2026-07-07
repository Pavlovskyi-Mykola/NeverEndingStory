using UnityEngine;

[CreateAssetMenu(fileName = "MiniGame", menuName = "Game/Mini Games/Mini Game Definition")]
public sealed class MiniGameDefinition : ScriptableObject
{
    [Tooltip("Stable id referenced by quest steps (CompleteMiniGame). Defaults to the asset name when empty.")]
    [SerializeField] private string miniGameId;

    public string DisplayName;

    [Tooltip("UI prefab with a MiniGameController on its root; instantiated under the MiniGameHost panel.")]
    public MiniGameController ControllerPrefab;

    public string MiniGameId => string.IsNullOrWhiteSpace(miniGameId) ? name : miniGameId;
}
