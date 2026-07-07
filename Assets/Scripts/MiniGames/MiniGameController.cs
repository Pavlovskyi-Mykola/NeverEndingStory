using System;
using UnityEngine;

/// <summary>
/// Base class every mini-game prefab implements. The host instantiates the prefab,
/// calls StartGame, and waits for exactly one Finish — the concrete game decides
/// when and with what tier. Keep games self-contained: all UI lives in the prefab.
/// </summary>
public abstract class MiniGameController : MonoBehaviour
{
    private Action<MiniGameResult> _onComplete;
    private bool _finished;

    public void StartGame(MiniGameContext context, Action<MiniGameResult> onComplete)
    {
        _onComplete = onComplete;
        _finished = false;
        OnStartGame(context);
    }

    /// <summary>Called by the host on forfeit (Escape). Default: fail out immediately.</summary>
    public virtual void Abort() => Finish(MiniGameTier.Failed);

    protected abstract void OnStartGame(MiniGameContext context);

    /// <summary>Report the result. Idempotent — only the first call counts.</summary>
    protected void Finish(MiniGameTier tier)
    {
        if (_finished) return;
        _finished = true;

        var callback = _onComplete;
        _onComplete = null;
        callback?.Invoke(new MiniGameResult(tier));
    }
}
