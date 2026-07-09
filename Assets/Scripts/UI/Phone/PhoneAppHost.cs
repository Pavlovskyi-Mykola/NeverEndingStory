using UnityEngine;

/// <summary>
/// App switcher for the phone's centre (apps) screen. Shows the app chooser — the
/// grid of square app icons — by default; opening an app hides the chooser and
/// shows that app's screen, closing an app returns to the chooser. Only one app is
/// shown at a time.
///
/// Wiring: each square app icon Button.onClick → OpenApp(&lt;that app's screen
/// GameObject&gt;) (pass the screen as the static GameObject argument); each app's
/// close/back button → CloseApp(). Put this on the apps-screen root so it resets to
/// the chooser every time the phone is opened.
/// </summary>
[DisallowMultipleComponent]
public sealed class PhoneAppHost : MonoBehaviour
{
    [Tooltip("The grid of square app icons. Shown when no app is open.")]
    [SerializeField] private GameObject appChooser;

    [Tooltip("Every app screen this host manages. Each must be listed here so it can be hidden again.")]
    [SerializeField] private GameObject[] apps;

    private void OnEnable() => ShowChooser();

    /// <summary>Hook an app icon's OnClick here, passing the app's screen GameObject.</summary>
    public void OpenApp(GameObject app)
    {
        if (app == null) return;

        bool known = false;
        if (apps != null)
        {
            for (int i = 0; i < apps.Length; i++)
            {
                if (apps[i] == null) continue;
                bool isTarget = apps[i] == app;
                apps[i].SetActive(isTarget);
                if (isTarget) known = true;
            }
        }

        if (!known)
        {
            app.SetActive(true);
            Debug.LogWarning($"[PhoneAppHost] '{app.name}' isn't in the Apps list, so it won't hide when closed. Add it.", this);
        }

        if (appChooser != null) appChooser.SetActive(false);
    }

    /// <summary>Hook an app's close/back button here.</summary>
    public void CloseApp() => ShowChooser();

    private void ShowChooser()
    {
        if (apps != null)
        {
            for (int i = 0; i < apps.Length; i++)
                if (apps[i] != null) apps[i].SetActive(false);
        }

        if (appChooser != null) appChooser.SetActive(true);
    }
}
