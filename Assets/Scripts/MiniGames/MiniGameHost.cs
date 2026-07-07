using System;
using UnityEngine;
using Evo.UI;

/// <summary>
/// Launches mini-game prefabs inside a Modal UIPanel in the UI scene. Lives on an
/// always-active object (like UIPanelManager) with references to the panel and a
/// content root inside it. The panel's Close On Escape must be OFF — the host owns
/// Escape while a game runs (forfeit, optionally confirmed via an Evo modal).
/// Guarantees exactly one result callback per launch, even if the panel is
/// force-closed externally.
/// </summary>
public sealed class MiniGameHost : MonoBehaviour
{
    public static MiniGameHost Instance { get; private set; }

    [Header("References")]
    [Tooltip("Modal UIPanel framing mini-games. Close On Escape OFF.")]
    [SerializeField] private UIPanel hostPanel;
    [Tooltip("RectTransform inside the panel where the mini-game prefab is instantiated.")]
    [SerializeField] private RectTransform contentRoot;

    [Header("Forfeit (optional)")]
    [Tooltip("Evo confirmation shown on Escape mid-game ('Give up?'). Leave empty to forfeit immediately.")]
    [SerializeField] private ModalWindow forfeitConfirmModal;

    public bool IsRunning { get; private set; }

    private MiniGameController _current;
    private Action<MiniGameResult> _onComplete;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (forfeitConfirmModal != null)
        {
            forfeitConfirmModal.onConfirm.RemoveListener(Forfeit);
            forfeitConfirmModal.onConfirm.AddListener(Forfeit);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        if (forfeitConfirmModal != null)
            forfeitConfirmModal.onConfirm.RemoveListener(Forfeit);
    }

    private void OnEnable()
    {
        if (hostPanel != null)
            hostPanel.Closed += HandlePanelClosed;
    }

    private void OnDisable()
    {
        if (hostPanel != null)
            hostPanel.Closed -= HandlePanelClosed;
    }

    public bool TryLaunch(MiniGameDefinition definition, ActionDefinition sourceAction, Action<MiniGameResult> onComplete)
    {
        if (IsRunning)
            return false;

        if (definition == null || definition.ControllerPrefab == null)
            return false;

        if (hostPanel == null || contentRoot == null)
        {
            Debug.LogWarning("[MiniGameHost] Host panel / content root not wired in the UI scene.", this);
            return false;
        }

        IsRunning = true;
        _onComplete = onComplete;

        hostPanel.Open();

        _current = Instantiate(definition.ControllerPrefab, contentRoot);
        _current.StartGame(new MiniGameContext(definition, sourceAction), HandleGameFinished);

        if (UIPanelManager.Instance != null)
            UIPanelManager.Instance.PushEscapeInterceptor(HandleEscape);

        return true;
    }

    /// <summary>Forfeits the running game (Escape confirm, or a Give Up button in the panel).</summary>
    public void Forfeit()
    {
        if (!IsRunning || _current == null)
            return;

        _current.Abort(); // resolves through HandleGameFinished with Failed
    }

    private void HandleGameFinished(MiniGameResult result)
    {
        if (!IsRunning) return;
        FinishInternal(result);
    }

    private void HandlePanelClosed(UIPanel panel)
    {
        // Force-closed externally (another exclusive modal, scene teardown) while a
        // game runs — count it as a forfeit so the launcher always gets its callback.
        if (IsRunning)
            FinishInternal(new MiniGameResult(MiniGameTier.Failed));
    }

    private void FinishInternal(MiniGameResult result)
    {
        IsRunning = false;

        if (UIPanelManager.Instance != null)
            UIPanelManager.Instance.RemoveEscapeInterceptor(HandleEscape);

        if (forfeitConfirmModal != null && forfeitConfirmModal.IsOpen)
            forfeitConfirmModal.Close();

        if (_current != null)
        {
            Destroy(_current.gameObject);
            _current = null;
        }

        if (hostPanel != null && hostPanel.IsOpen)
            hostPanel.Close();

        var callback = _onComplete;
        _onComplete = null;
        callback?.Invoke(result);
    }

    private bool HandleEscape()
    {
        if (!IsRunning)
            return false;

        if (forfeitConfirmModal != null)
        {
            // First Esc asks; Esc again while asking = changed my mind, keep playing.
            if (forfeitConfirmModal.IsOpen) forfeitConfirmModal.Close();
            else forfeitConfirmModal.Open();
            return true;
        }

        Forfeit();
        return true;
    }
}
