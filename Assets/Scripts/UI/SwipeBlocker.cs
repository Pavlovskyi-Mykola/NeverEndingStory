using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Swallows pointer-drag events so they don't bubble up to a parent Evo Pages
/// component — disabling swipe-to-change-page while leaving the tab buttons working.
///
/// Unity sends a drag to the first ancestor (from the hit object up) that implements
/// a drag handler; putting this on a page's root makes it that handler, so Pages
/// (the parent) never receives the swipe.
///
/// Place on each page's root. The object must be a raycast target (e.g. have a
/// background Image with Raycast Target on) so empty areas of the page catch the drag.
/// Controls that handle their own drag (sliders, scroll lists) already block swipe
/// on themselves; this covers the empty space around them.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public sealed class SwipeBlocker : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public void OnBeginDrag(PointerEventData eventData) { /* consume — do nothing */ }
    public void OnDrag(PointerEventData eventData) { /* consume — do nothing */ }
    public void OnEndDrag(PointerEventData eventData) { /* consume — do nothing */ }
}
