using UnityEngine;

public class TimeSystem : MonoBehaviour
{
    public int Hour;
    public int Minute;
    public float TimeMultiplier = 60f;

    void Update()
    {
        Minute += (int)(Time.deltaTime * TimeMultiplier);
        if (Minute >= 60) { Hour++; Minute = 0; }
        if (Hour >= 24) { Hour = 0; /* new day */ }
    }
}

