# UI Panel System — Scene Setup

The code works without any scene changes (panels without a `UIPanel` component behave
exactly as before). To activate coordination, wire the UI scene as follows.

## 1. Manager

Create an empty, always-active GameObject in `UI.unity` (e.g. `UI_PanelManager`,
sibling of the Canvas) and add **UIPanelManager**.

## 2. Backdrop (optional but recommended)

Under the Canvas, create `IMG_ModalBackdrop`:
- `Image`, stretched full-screen (anchors 0,0 → 1,1), semi-transparent black, **Raycast Target ON**.
- Keep it **disabled** by default; the manager activates it and re-sorts it directly
  behind the topmost panel that has *Blocks UI Behind*.
- Optional: add a `Button` (no visuals) with `OnClick → UIPanelManager.CloseTopmost`
  for click-outside-to-close.
- Assign it to the manager's *Backdrop* field.

It must live under the same Canvas as the panels (the manager re-parents it next to the
blocking panel, so a shared Canvas is enough).

## 3. Panels — add `UIPanel` to each panel root

| Panel root | Layer | Exclusive | Blocks Gameplay | Blocks UI Behind | Close On Escape | Hide Groups |
|---|---|---|---|---|---|---|
| `PANEL_Inventory` | Window | ✓ | ✓ | ✓ | ✓ | `QuestTracker` |
| `JournalPanel` (quest journal) | Window | ✓ | ✓ | ✓ | ✓ | — |
| `PANEL_SaveLoad` | Modal | ✓ | ✓ | ✓ | ✓ | — |
| `PANEL_Dialogue` | Modal | ✓ | ✓ | ✓ | **✗** | `QuestTracker` |

Notes:
- *Exclusive* on the Window layer means opening Inventory auto-closes the Journal and
  vice versa. Same on Modal layer for Dialogue vs SaveLoad.
- `PANEL_Dialogue` must have *Close On Escape* OFF — dialogue must end via
  `DialogueRunner.CloseDialogue()`, not by deactivating the panel.
- *Blocks Gameplay* grays out HUD `ActionButton`s (Advance Time, Sleep, …) and blocks
  NPC interaction while the panel is open. Turn it off per panel if undesired.
- Panel ids default to the GameObject name; set an explicit id if you rename objects.

## 4. Auto-hidden elements — add `UIVisibilityGroup`

On `PANEL_TrackedQuest` (or `TrackedQuestPanel`): add **UIVisibilityGroup** with
`Group Id = QuestTracker`.

Any panel that lists `QuestTracker` in *Hide Groups While Open* now hides the tracker
while open; it reappears when the last such panel closes. If the tracker was already
hidden for another reason, it stays hidden (the component remembers prior visibility).

The same mechanism works for any element: give HUD button rows a group id (e.g. `HudActions`)
and list it on panels that should hide them. Elements must start **active** in the scene
so they can subscribe (use CanvasGroup hide mode if the element needs to keep running
while hidden).

## 5. Code API (for future panels / hotkeys)

```csharp
UIPanelManager.IsGameplayBlocked          // gate movement / interaction / hotkeys
UIPanelManager.GameplayBlockedChanged     // event(bool)
UIPanelManager.HideGroupChanged           // event(groupId, hidden)
UIPanelManager.Instance.Open("PANEL_Inventory");
UIPanelManager.Instance.CloseAll(UIPanelLayer.Window);
UIPanelManager.Instance.IsAnyOpen(UIPanelLayer.Modal);
```

Opening/closing stays plain `SetActive` (or `UIPanel.Open/Close/Toggle`) — the manager
hooks `OnEnable`/`OnDisable`, so all existing scripts work unchanged.
