using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// "The Shot" — secret photo through a phone viewfinder. The target wanders the
/// frame; drag the focus reticle to keep them framed while their awareness cone
/// shows where they're looking (you are the bottom-center of the frame). Wait for
/// the incriminating act, then snap while they're not looking your way.
///
/// Two-axis timing: aim (reticle over target) + safe moment (mid-act, unseen).
/// Tiers by frame quality at the snap: Gold = dead center, Silver = framed,
/// Bronze = partial/blurry. Snapping while they look your way = spotted = Caught.
/// Limited film and limited act windows; run out of either = Failed.
///
/// Networking stat extends the act window (stats soften difficulty, they don't
/// gate). The photo evidence item is granted by the launching ActionDefinition's
/// Item Rewards (full on any success) — pair with a SuccessFlag for intel.
/// </summary>
public sealed class ShotMiniGame : MiniGameController, IPointerDownHandler, IDragHandler
{
    private enum ActPhase { Idle, Telegraph, Active }

    [Header("References")]
    [Tooltip("Bounds the target and reticle move in. The prefab root's Image (Raycast Target ON) doubles as the drag surface.")]
    [SerializeField] private RectTransform viewfinderArea;
    [Tooltip("The target — child of the viewfinder area, anchors/pivot centered.")]
    [SerializeField] private RectTransform target;
    [Tooltip("Awareness cone — child of the target, its art pointing RIGHT (+X) at 0° rotation.")]
    [SerializeField] private RectTransform awarenessCone;
    [SerializeField] private Image coneImage;
    [Tooltip("Focus reticle — child of the viewfinder area, anchors/pivot centered.")]
    [SerializeField] private RectTransform reticle;
    [SerializeField] private Image reticleImage;
    [SerializeField] private Button snapButton;
    [SerializeField] private TMP_Text statusLabel;
    [SerializeField] private TMP_Text feedbackLabel;
    [SerializeField] private TMP_Text filmLabel;
    [Tooltip("Optional Image with Image Type = Filled — remaining act-window time.")]
    [SerializeField] private Image actWindowFill;

    [Header("Target Movement")]
    [Tooltip("Wander speed in viewfinder pixels per second.")]
    [SerializeField] private float moveSpeed = 130f;
    [Tooltip("Inset from the viewfinder edges the target stays within.")]
    [SerializeField] private float movementMargin = 70f;
    [Tooltip("Movement speed multiplier while mid-act (they're occupied).")]
    [SerializeField, Range(0f, 1f)] private float actMoveMultiplier = 0.35f;

    [Header("Reticle")]
    [Tooltip("How fast the reticle chases the pointer, pixels per second (focus lag).")]
    [SerializeField] private float reticleFollowSpeed = 1400f;

    [Header("Awareness")]
    [Tooltip("Cone turn rate, degrees per second — the sweep toward you IS the telegraph.")]
    [SerializeField] private float lookTurnSpeed = 110f;
    [SerializeField] private float lookHoldMinSeconds = 0.8f;
    [SerializeField] private float lookHoldMaxSeconds = 2.2f;
    [Tooltip("Chance each look change is a glance straight at the camera.")]
    [SerializeField, Range(0f, 1f)] private float cameraCheckChance = 0.3f;
    [Tooltip("How long a camera check lingers on you.")]
    [SerializeField] private float cameraCheckHoldSeconds = 1.1f;
    [Tooltip("Cone within this many degrees of facing you = they see the shot.")]
    [SerializeField, Range(5f, 90f)] private float lookingHalfAngle = 35f;
    [Tooltip("Cone color starts warning inside this angle.")]
    [SerializeField, Range(10f, 170f)] private float warnHalfAngle = 80f;
    [SerializeField] private Color coneSafeColor = new Color(0.4f, 0.9f, 0.5f, 0.45f);
    [SerializeField] private Color coneDangerColor = new Color(1f, 0.3f, 0.25f, 0.6f);

    [Header("Incriminating Act")]
    [Tooltip("Act windows before the target wraps up and leaves (Failed).")]
    [SerializeField, Range(1, 8)] private int opportunities = 3;
    [SerializeField] private float actIntervalMinSeconds = 3f;
    [SerializeField] private float actIntervalMaxSeconds = 6f;
    [Tooltip("Warning beat before the act ('they reach into their coat...').")]
    [SerializeField] private float telegraphSeconds = 1.2f;
    [Tooltip("Base act duration, before the Networking bonus.")]
    [SerializeField] private float baseActSeconds = 2.5f;
    [Tooltip("Bonus act seconds per point of Networking.")]
    [SerializeField] private float secondsPerNetworking = 0.15f;
    [SerializeField] private float maxNetworkingBonusSeconds = 2f;

    [Header("Film")]
    [Tooltip("Bad snaps allowed before the run fails (a snap while seen fails instantly).")]
    [SerializeField, Range(1, 6)] private int filmShots = 2;

    [Header("Scoring")]
    [Tooltip("Reticle within this many pixels of the target = perfect frame (Gold).")]
    [SerializeField] private float goldRadius = 32f;
    [Tooltip("Within this = cleanly framed (Silver).")]
    [SerializeField] private float silverRadius = 75f;
    [Tooltip("Within this = partial/blurry but usable (Bronze). Beyond = wasted film.")]
    [SerializeField] private float bronzeRadius = 120f;
    [SerializeField] private float resultDisplaySeconds = 1.4f;

    private static readonly string[] IdleLines =
    {
        "They're just talking. Wait for it...",
        "Small talk. Nothing usable yet.",
        "They keep glancing around. Stay patient."
    };

    private static readonly string[] TelegraphLines =
    {
        "They reach into their coat — get ready.",
        "An envelope slides across the table...",
        "They lean in close. Something's changing hands."
    };

    private const string ActLine = "THE HANDOFF — take the shot!";

    private Vector2 _waypoint;
    private Vector2 _reticleGoal;

    private float _lookAngle;        // current cone facing, degrees
    private float _targetLookAngle;
    private float _lookHoldTimer;
    private bool _cameraChecking;

    private ActPhase _actPhase;
    private float _actTimer;
    private float _actSeconds;       // full window after the Networking bonus
    private int _opportunitiesLeft;
    private int _filmLeft;
    private bool _running;

    protected override void OnStartGame(MiniGameContext context)
    {
        int networking = PlayerStatsManager.Instance != null ? PlayerStatsManager.Instance.Networking : 0;
        _actSeconds = baseActSeconds + Mathf.Min(maxNetworkingBonusSeconds, networking * secondsPerNetworking);

        _opportunitiesLeft = opportunities;
        _filmLeft = filmShots;
        _actPhase = ActPhase.Idle;
        _actTimer = Random.Range(actIntervalMinSeconds, actIntervalMaxSeconds);

        if (target != null) target.anchoredPosition = Vector2.zero;
        if (reticle != null) _reticleGoal = reticle.anchoredPosition;
        PickWaypoint();

        _lookAngle = _targetLookAngle = Random.Range(0f, 360f);
        _lookHoldTimer = Random.Range(lookHoldMinSeconds, lookHoldMaxSeconds);
        _cameraChecking = false;

        // The root image is the drag surface — children must not swallow the raycast.
        if (reticleImage != null) reticleImage.raycastTarget = false;
        if (coneImage != null) coneImage.raycastTarget = false;
        var targetImage = target != null ? target.GetComponent<Image>() : null;
        if (targetImage != null) targetImage.raycastTarget = false;

        if (snapButton != null)
        {
            snapButton.onClick.RemoveListener(HandleSnap);
            snapButton.onClick.AddListener(HandleSnap);
            snapButton.interactable = true;
        }

        SetStatus(IdleLines[Random.Range(0, IdleLines.Length)]);
        SetFeedback("Frame them. Wait for the moment. Don't get seen.");
        RefreshFilmLabel();

        _running = true;
    }

    public override void Abort()
    {
        _running = false;
        base.Abort();
    }

    private void Update()
    {
        if (!_running)
            return;

        TickTarget();
        TickReticle();
        TickAwareness();
        TickAct();
        RefreshVisuals();

        if (Input.GetKeyDown(KeyCode.Space))
            HandleSnap();
    }

    // -----------------------
    // Simulation
    // -----------------------

    private void TickTarget()
    {
        if (target == null)
            return;

        float speed = moveSpeed * (_actPhase == ActPhase.Active ? actMoveMultiplier : 1f);
        target.anchoredPosition = Vector2.MoveTowards(target.anchoredPosition, _waypoint, speed * Time.deltaTime);

        if (Vector2.Distance(target.anchoredPosition, _waypoint) < 8f)
            PickWaypoint();
    }

    private void PickWaypoint()
    {
        Vector2 half = MovementHalfExtents();
        _waypoint = new Vector2(Random.Range(-half.x, half.x), Random.Range(-half.y, half.y));
    }

    private Vector2 MovementHalfExtents()
    {
        if (viewfinderArea == null)
            return new Vector2(200f, 120f);

        var rect = viewfinderArea.rect;
        return new Vector2(
            Mathf.Max(10f, rect.width * 0.5f - movementMargin),
            Mathf.Max(10f, rect.height * 0.5f - movementMargin));
    }

    private void TickReticle()
    {
        if (reticle == null)
            return;

        reticle.anchoredPosition = Vector2.MoveTowards(
            reticle.anchoredPosition, _reticleGoal, reticleFollowSpeed * Time.deltaTime);
    }

    private void TickAwareness()
    {
        _lookAngle = Mathf.MoveTowardsAngle(_lookAngle, _targetLookAngle, lookTurnSpeed * Time.deltaTime);

        if (Mathf.Abs(Mathf.DeltaAngle(_lookAngle, _targetLookAngle)) > 0.5f)
            return; // still turning

        _lookHoldTimer -= Time.deltaTime;
        if (_lookHoldTimer > 0f)
            return;

        // Pick the next look. Camera checks don't start mid-act — they're occupied —
        // which is exactly why the act is the safe window.
        _cameraChecking = _actPhase != ActPhase.Active && Random.value < cameraCheckChance;

        if (_cameraChecking)
        {
            _targetLookAngle = AngleToCamera();
            _lookHoldTimer = cameraCheckHoldSeconds;
        }
        else
        {
            // Anywhere except straight at you — incidental sweeps still graze the warn zone.
            float offset = Random.Range(warnHalfAngle, 360f - warnHalfAngle);
            _targetLookAngle = AngleToCamera() + offset;
            _lookHoldTimer = Random.Range(lookHoldMinSeconds, lookHoldMaxSeconds);
        }
    }

    private void TickAct()
    {
        _actTimer -= Time.deltaTime;
        if (_actTimer > 0f)
            return;

        switch (_actPhase)
        {
            case ActPhase.Idle:
                _actPhase = ActPhase.Telegraph;
                _actTimer = telegraphSeconds;
                SetStatus(TelegraphLines[Random.Range(0, TelegraphLines.Length)]);
                break;

            case ActPhase.Telegraph:
                _actPhase = ActPhase.Active;
                _actTimer = _actSeconds;
                SetStatus(ActLine);
                break;

            case ActPhase.Active:
                _opportunitiesLeft--;

                if (_opportunitiesLeft <= 0)
                {
                    ResolveMomentGone();
                    return;
                }

                _actPhase = ActPhase.Idle;
                _actTimer = Random.Range(actIntervalMinSeconds, actIntervalMaxSeconds);
                SetStatus(IdleLines[Random.Range(0, IdleLines.Length)]);
                SetFeedback($"Window missed — {_opportunitiesLeft} left.");
                break;
        }
    }

    /// <summary>Degrees the target must face to look at you (bottom-center of the frame).</summary>
    private float AngleToCamera()
    {
        Vector2 targetPos = target != null ? target.anchoredPosition : Vector2.zero;
        float halfHeight = viewfinderArea != null ? viewfinderArea.rect.height * 0.5f : 200f;
        Vector2 toCamera = new Vector2(0f, -halfHeight) - targetPos;
        return Mathf.Atan2(toCamera.y, toCamera.x) * Mathf.Rad2Deg;
    }

    private bool IsLookingAtYou()
    {
        return Mathf.Abs(Mathf.DeltaAngle(_lookAngle, AngleToCamera())) <= lookingHalfAngle;
    }

    // -----------------------
    // Input
    // -----------------------

    public void OnPointerDown(PointerEventData eventData) => MoveReticleGoal(eventData);

    public void OnDrag(PointerEventData eventData) => MoveReticleGoal(eventData);

    private void MoveReticleGoal(PointerEventData eventData)
    {
        if (!_running || viewfinderArea == null)
            return;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                viewfinderArea, eventData.position, eventData.pressEventCamera, out Vector2 local))
            return;

        var rect = viewfinderArea.rect;
        _reticleGoal = new Vector2(
            Mathf.Clamp(local.x, -rect.width * 0.5f, rect.width * 0.5f),
            Mathf.Clamp(local.y, -rect.height * 0.5f, rect.height * 0.5f));
    }

    private void HandleSnap()
    {
        if (!_running)
            return;

        // Axis 1: the safe moment. Shutter while they're looking = spotted.
        if (IsLookingAtYou())
        {
            ResolveCaught();
            return;
        }

        // Axis 2: the aim.
        float distance = target != null && reticle != null
            ? Vector2.Distance(target.anchoredPosition, reticle.anchoredPosition)
            : float.MaxValue;

        if (_actPhase == ActPhase.Active && distance <= bronzeRadius)
        {
            ResolveWin(distance <= goldRadius ? MiniGameTier.Gold :
                       distance <= silverRadius ? MiniGameTier.Silver :
                       MiniGameTier.Bronze);
            return;
        }

        // Wasted film — wrong moment or empty frame.
        _filmLeft--;
        RefreshFilmLabel();

        SetFeedback(_actPhase != ActPhase.Active
            ? $"Nothing usable — just two people talking. {_filmLeft} shot(s) left."
            : $"Out of frame — a blurry wall. {_filmLeft} shot(s) left.");

        if (_filmLeft <= 0)
            ResolveOutOfFilm();
    }

    // -----------------------
    // Presentation
    // -----------------------

    private void RefreshVisuals()
    {
        if (awarenessCone != null)
            awarenessCone.localRotation = Quaternion.Euler(0f, 0f, _lookAngle);

        if (coneImage != null)
        {
            float diff = Mathf.Abs(Mathf.DeltaAngle(_lookAngle, AngleToCamera()));
            float danger = 1f - Mathf.InverseLerp(lookingHalfAngle, warnHalfAngle, diff);
            coneImage.color = Color.Lerp(coneSafeColor, coneDangerColor, danger);
        }

        if (reticleImage != null && target != null && reticle != null)
        {
            // Live focus feedback: white = empty frame, yellow = framed, green = perfect.
            float distance = Vector2.Distance(target.anchoredPosition, reticle.anchoredPosition);
            reticleImage.color =
                distance <= goldRadius ? new Color(0.45f, 1f, 0.5f) :
                distance <= silverRadius ? new Color(1f, 0.9f, 0.4f) :
                Color.white;
        }

        if (actWindowFill != null)
        {
            actWindowFill.fillAmount = _actPhase == ActPhase.Active
                ? Mathf.Clamp01(_actTimer / Mathf.Max(0.1f, _actSeconds))
                : 0f;
        }
    }

    private void RefreshFilmLabel()
    {
        if (filmLabel != null)
            filmLabel.text = $"FILM {_filmLeft}/{filmShots}";
    }

    private void SetStatus(string text)
    {
        if (statusLabel != null)
            statusLabel.text = text;
    }

    private void SetFeedback(string text)
    {
        if (feedbackLabel != null)
            feedbackLabel.text = text;
    }

    // -----------------------
    // Resolution
    // -----------------------

    private void ResolveWin(MiniGameTier tier)
    {
        EndRun(tier switch
        {
            MiniGameTier.Gold => "Dead center, mid-handoff. Front-page material.",
            MiniGameTier.Silver => "Clear enough — faces and the envelope in frame.",
            _ => "Blurry, half out of frame... but it's them. It'll do."
        }, tier);
    }

    private void ResolveCaught()
    {
        EndRun("They look straight into your lens. Time to disappear.", MiniGameTier.Failed);
    }

    private void ResolveOutOfFilm()
    {
        EndRun("Out of film, nothing usable. The moment slips away.", MiniGameTier.Failed);
    }

    private void ResolveMomentGone()
    {
        EndRun("Deal done, evidence pocketed. They shake hands and part ways.", MiniGameTier.Failed);
    }

    private void EndRun(string message, MiniGameTier tier)
    {
        _running = false;

        if (snapButton != null)
            snapButton.interactable = false;

        SetFeedback(message);
        StartCoroutine(FinishAfterDelay(tier));
    }

    private IEnumerator FinishAfterDelay(MiniGameTier tier)
    {
        yield return new WaitForSeconds(resultDisplaySeconds);
        Finish(tier);
    }
}
