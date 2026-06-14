using UnityEngine;

/// <summary>
/// Shows the HUD only during gameplay; hides it in the main menu (and before any
/// location loads). Lives in the persistent UI scene. Drives visibility off
/// GameManager's location state, not the panel system — "in the menu" is a
/// location state, not an open-panel state (that's what UIVisibilityGroup is for).
/// </summary>
public sealed class HudVisibilityController : MonoBehaviour
{
    [Tooltip("Roots shown only during gameplay (e.g. the HUD container). Hidden in the main menu.")]
    [SerializeField] private GameObject[] gameplayOnlyRoots;

    private void OnEnable()
    {
        GameManager.InstanceReady += HandleGameManagerReady;
        if (GameManager.Instance != null)
            HandleGameManagerReady(GameManager.Instance);

        Apply(); // start hidden until a gameplay location is entered
    }

    private void OnDisable()
    {
        GameManager.InstanceReady -= HandleGameManagerReady;
        if (GameManager.Instance != null)
            GameManager.Instance.LocationReady -= HandleLocationReady;
    }

    private void HandleGameManagerReady(GameManager gm)
    {
        gm.LocationReady -= HandleLocationReady;
        gm.LocationReady += HandleLocationReady;
        Apply();
    }

    private void HandleLocationReady(SceneReference _) => Apply();

    private void Apply()
    {
        bool show = GameManager.Instance != null && GameManager.Instance.IsInGameplay;

        if (gameplayOnlyRoots == null)
            return;

        for (int i = 0; i < gameplayOnlyRoots.Length; i++)
        {
            var root = gameplayOnlyRoots[i];
            if (root != null && root.activeSelf != show)
                root.SetActive(show);
        }
    }
}
