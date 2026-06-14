using System.Collections;
using UnityEngine;
using Evo.UI;

/// <summary>
/// Thin controller for the unified menu panel (Settings / Save-Load / Exit pages
/// driven by an Evo Pages component). Page-to-page switching is handled natively by
/// Pages' page buttons; this just opens the panel and can jump straight to a page
/// (e.g. a HUD "Save" button opening the menu on the Save/Load page).
/// Put this on the panel root alongside its UIPanel.
/// </summary>
[RequireComponent(typeof(UIPanel))]
public sealed class GameMenuController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Pages pages;
    [SerializeField] private UIPanel panel;

    [Header("Page indices (must match the Pages list order)")]
    [SerializeField] private int settingsPage = 0;
    [SerializeField] private int saveLoadPage = 1;
    [SerializeField] private int exitPage = 2;

    private int _pendingPage = -1;

    private void Awake()
    {
        if (panel == null) panel = GetComponent<UIPanel>();
    }

    // OnClick-friendly entry points
    public void Open() => OpenInternal(-1);
    public void OpenSettings() => OpenInternal(settingsPage);
    public void OpenSaveLoad() => OpenInternal(saveLoadPage);
    public void OpenExit() => OpenInternal(exitPage);
    public void OpenToPage(int index) => OpenInternal(index);

    public void Close()
    {
        if (panel != null) panel.Close();
        else gameObject.SetActive(false);
    }

    private void OpenInternal(int page)
    {
        if (panel != null) panel.Open();
        else gameObject.SetActive(true);

        if (page < 0 || pages == null)
            return;

        // The panel is now active, so a coroutine can run. Defer the jump until
        // Pages has finished its own one-frame init, otherwise its initial
        // OpenPage(default) would override our choice on the first open.
        _pendingPage = page;
        StartCoroutine(ApplyPendingPage());
    }

    private IEnumerator ApplyPendingPage()
    {
        while (pages.CurrentPageIndex < 0)
            yield return null;

        if (_pendingPage >= 0)
        {
            pages.OpenPage(_pendingPage, animate: false);
            _pendingPage = -1;
        }
    }
}
