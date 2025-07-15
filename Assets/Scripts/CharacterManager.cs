using UnityEngine;

public class CharacterManager : MonoBehaviour
{
    public string CharacterName;
    public float Hunger;
    public float Energy;

    private void Update()
    {
        Hunger += Time.deltaTime * 0.01f;
        // Update UI etc.
    }
}

