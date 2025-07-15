using UnityEngine;
using UnityEngine.SceneManagement;

public class LocationManager : MonoBehaviour
{
    public void TravelTo(string sceneName)
    {
        SceneManager.LoadScene(sceneName);
    }
}

