using UnityEngine;

public class LocationButton : MonoBehaviour
{
    [SerializeField] private SceneReference targetLocation;

    public void SwitchScene()
    {
        if (GameManager.Instance == null) return;
        if (GameManager.Instance.IsInDialogue) return;

        _ = GameManager.Instance.SwitchLocation(targetLocation);
    }
}
