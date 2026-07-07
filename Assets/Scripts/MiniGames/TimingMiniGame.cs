using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Placeholder mini-game: a bar ping-pongs and the player stops it as close to the
/// middle as possible. Distance from center decides the tier. Exists to prove the
/// pipeline (action -> host -> result -> rewards/quest) end to end.
/// </summary>
public sealed class TimingMiniGame : MiniGameController
{
    [Header("References")]
    [Tooltip("Image with Image Type = Filled (horizontal). fillAmount ping-pongs 0..1.")]
    [SerializeField] private Image markerFill;
    [SerializeField] private Button stopButton;
    [SerializeField] private TMP_Text promptLabel;

    [Header("Tuning")]
    [Tooltip("Bar sweeps per second (higher = harder).")]
    [SerializeField] private float speed = 0.9f;
    [SerializeField] private float goldWindow = 0.05f;
    [SerializeField] private float silverWindow = 0.14f;
    [SerializeField] private float bronzeWindow = 0.26f;
    [SerializeField] private float resultDisplaySeconds = 1.1f;

    private float _time;
    private bool _stopped;

    protected override void OnStartGame(MiniGameContext context)
    {
        _time = 0f;
        _stopped = false;

        if (promptLabel != null)
            promptLabel.text = "Stop the bar in the middle";

        if (stopButton != null)
        {
            stopButton.onClick.RemoveListener(HandleStop);
            stopButton.onClick.AddListener(HandleStop);
            stopButton.interactable = true;
        }
    }

    private void Update()
    {
        if (_stopped)
            return;

        _time += Time.deltaTime * speed;

        if (markerFill != null)
            markerFill.fillAmount = Mathf.PingPong(_time, 1f);
    }

    private void HandleStop()
    {
        if (_stopped)
            return;

        _stopped = true;

        if (stopButton != null)
            stopButton.interactable = false;

        float value = Mathf.PingPong(_time, 1f);
        float distance = Mathf.Abs(value - 0.5f);

        MiniGameTier tier =
            distance <= goldWindow ? MiniGameTier.Gold :
            distance <= silverWindow ? MiniGameTier.Silver :
            distance <= bronzeWindow ? MiniGameTier.Bronze :
            MiniGameTier.Failed;

        if (promptLabel != null)
            promptLabel.text = tier == MiniGameTier.Failed ? "Missed!" : $"{tier}!";

        StartCoroutine(FinishAfterDelay(tier));
    }

    private IEnumerator FinishAfterDelay(MiniGameTier tier)
    {
        yield return new WaitForSeconds(resultDisplaySeconds);
        Finish(tier);
    }

    public override void Abort()
    {
        _stopped = true; // freeze the bar; base reports Failed (Finish is idempotent)
        base.Abort();
    }
}
