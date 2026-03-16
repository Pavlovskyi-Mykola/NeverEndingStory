using UnityEngine;

public sealed class CareerNpcGate : MonoBehaviour
{
    [Header("Career Requirements")]
    [SerializeField] private CareerTier minimumTier = CareerTier.Intern;
    [SerializeField] private string requiredUnlockedFloorSceneName;

    private void OnEnable()
    {
        CareerManager.InstanceReady += HandleCareerReady;

        if (CareerManager.Instance != null)
            Bind(CareerManager.Instance);

        Refresh();
    }

    private void OnDisable()
    {
        CareerManager.InstanceReady -= HandleCareerReady;

        if (CareerManager.Instance != null)
            CareerManager.Instance.OnCareerStateChanged -= Refresh;
    }

    private void HandleCareerReady(CareerManager cm)
    {
        Bind(cm);
        Refresh();
    }

    private void Bind(CareerManager cm)
    {
        if (cm == null) return;

        cm.OnCareerStateChanged -= Refresh;
        cm.OnCareerStateChanged += Refresh;
    }

    public void Refresh()
    {
        var cm = CareerManager.Instance;
        if (cm == null)
        {
            gameObject.SetActive(true);
            return;
        }

        bool allowed = cm.CurrentTier >= minimumTier;

        if (allowed && !string.IsNullOrWhiteSpace(requiredUnlockedFloorSceneName))
            allowed = cm.IsFloorUnlocked(requiredUnlockedFloorSceneName);

        gameObject.SetActive(allowed);
    }
}