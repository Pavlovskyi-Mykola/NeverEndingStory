using UnityEngine;
using Evo.UI;

/// <summary>
/// Quit flow with a confirmation step. Hook RequestQuit() to the Quit button:
/// it opens the assigned Evo ModalWindow. The modal's confirm runs ConfirmQuit()
/// (autosave, then quit); cancel just closes the modal (handled by the modal itself).
/// If no modal is assigned, RequestQuit() quits immediately.
/// In the editor, "quit" stops play mode.
/// </summary>
public sealed class QuitGame : MonoBehaviour
{
    [Header("Confirmation")]
    [Tooltip("Evo ModalWindow shown before quitting. Leave empty to quit without confirmation.")]
    [SerializeField] private ModalWindow confirmModal;

    [Header("Autosave")]
    [SerializeField] private bool autosaveBeforeQuit = true;
    [SerializeField] private string saveLabel = "quit";

    private bool _quitting;

    private void Awake()
    {
        // Auto-wire confirm so you only have to hook the Quit button to RequestQuit().
        // Idempotent, so it's safe even if you also wire onConfirm in the inspector.
        if (confirmModal != null)
        {
            confirmModal.onConfirm.RemoveListener(ConfirmQuit);
            confirmModal.onConfirm.AddListener(ConfirmQuit);
        }
    }

    private void OnEnable()
    {
        // While this object (the menu) is active, the modal gets first claim on
        // Escape — so Esc closes the confirmation instead of the menu behind it.
        if (UIPanelManager.Instance != null)
            UIPanelManager.Instance.PushEscapeInterceptor(HandleEscape);
    }

    private void OnDisable()
    {
        if (UIPanelManager.Instance != null)
            UIPanelManager.Instance.RemoveEscapeInterceptor(HandleEscape);
    }

    private bool HandleEscape()
    {
        if (confirmModal != null && confirmModal.IsOpen)
        {
            confirmModal.Close(); // proper Evo close: resets isOpen, hides cleanly
            return true;          // consume Escape; leave the menu open
        }

        return false; // modal not up — let the manager close the menu as usual
    }

    private void OnDestroy()
    {
        if (confirmModal != null)
            confirmModal.onConfirm.RemoveListener(ConfirmQuit);
    }

    /// <summary>Quit button entry point — opens the confirmation modal (or quits if none).</summary>
    public void RequestQuit()
    {
        if (confirmModal != null)
            confirmModal.Open();
        else
            ConfirmQuit();
    }

    /// <summary>Runs on modal confirm: autosave, then quit. Guarded against double-invoke.</summary>
    public void ConfirmQuit()
    {
        if (_quitting)
            return;
        _quitting = true;

        if (autosaveBeforeQuit && SaveLoadManager.Instance != null)
            SaveLoadManager.Instance.TryAutosave(saveLabel);

        QuitApplication();
    }

    private void QuitApplication()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
