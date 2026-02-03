using UnityEngine;

public class LocationButton : MonoBehaviour
{
    [SerializeField] private SceneReference targetLocation;

    public void SwitchScene()
    {
        _ = GameManager.Instance.SwitchLocation(targetLocation);
    }
}
