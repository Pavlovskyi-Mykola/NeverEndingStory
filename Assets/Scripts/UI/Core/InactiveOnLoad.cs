using UnityEngine;

/// <summary>
/// Marks a GameObject that must be inactive when its scene starts, regardless of
/// the state it was saved in. Editing panels requires turning them on; forgetting
/// to turn one off before saving then ships a broken UI — this makes the "starts
/// off" rule declarative instead of relying on discipline.
///
/// Two layers of enforcement:
///   1. Editor (InactiveOnLoadEnforcer): marked objects left on are disabled and
///      logged on scene save and on entering play mode, so saved scenes converge
///      to the correct state.
///   2. Runtime (this Awake): last-resort guard for anything that slips through
///      into a build.
///
/// Put it on top-level "starts hidden" roots: panel roots, popups, the phone's
/// apps screen. Don't bother with children whose state a controller already
/// resets when it enables (e.g. screens under PhoneAppHost) — owner-driven reset
/// is the better pattern there, and this marker only acts at scene load anyway.
/// </summary>
[DisallowMultipleComponent]
public sealed class InactiveOnLoad : MonoBehaviour
{
    private void Awake()
    {
        // Only enforce during the scene's initial load. When the object is saved
        // correctly (inactive), Awake instead runs the first time the UI activates
        // it — scene.isLoaded is true by then, and disabling would fight the UI.
        if (gameObject.scene.isLoaded)
            return;

        Debug.LogWarning($"[InactiveOnLoad] '{name}' was saved active — disabled it at load. Save the scene with it off.", this);
        gameObject.SetActive(false);
    }
}
