using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Main menu actions: New Game, Continue (latest save), and keeping the Continue
/// button enabled only when a save exists. Settings and Quit reuse the shared
/// components (open the settings panel via UIPanelButton; quit via QuitGame with
/// its autosave toggle off).
/// </summary>
public sealed class MainMenuController : MonoBehaviour
{
    [Header("Continue")]
    [Tooltip("Greyed out when no save exists. Any button works — Evo Button derives from Selectable.")]
    [SerializeField] private Selectable continueButton;

    private void OnEnable()
    {
        RefreshContinueButton();
    }

    /// <summary>Enables/disables Continue based on whether any save exists. Call when returning to the menu.</summary>
    public void RefreshContinueButton()
    {
        if (continueButton == null)
            return;

        continueButton.interactable =
            SaveLoadManager.Instance != null && SaveLoadManager.Instance.HasAnySave();
    }

    /// <summary>Hook to the New Game button. Resets runtime state (keeps existing save files) and enters Home.</summary>
    public async void StartNewGame()
    {
        var gm = GameManager.Instance;
        if (gm == null)
        {
            Debug.LogWarning("[MainMenuController] GameManager missing.");
            return;
        }

        var home = gm.Scenes != null ? gm.Scenes.Home : null;
        if (home == null || !home.IsValid)
        {
            Debug.LogError("[MainMenuController] No valid Home scene configured in SceneDatabase.");
            return;
        }

        // Fresh state, then travel into the starting location (force past entry gates).
        SaveLoadManager.Instance?.ResetRuntimeState();
        await gm.SwitchLocation(home, force: true);
        // MainMenu scene unloads as part of the switch; nothing to touch afterward.
    }

    /// <summary>Hook to the Continue button. Loads the most recent save.</summary>
    public async void Continue()
    {
        var slm = SaveLoadManager.Instance;
        if (slm == null)
        {
            Debug.LogWarning("[MainMenuController] SaveLoadManager missing.");
            return;
        }

        bool loaded = await slm.ContinueLatest();

        // Only reachable if the load failed (otherwise MainMenu has unloaded).
        if (!loaded)
            RefreshContinueButton();
    }
}
