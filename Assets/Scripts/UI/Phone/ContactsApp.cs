using UnityEngine;

/// <summary>
/// The phone's Contacts app: one row per NPC in NpcManager's database, showing
/// portrait, name and current location — or the "busy" fallback when the NPC is
/// placed nowhere right now (Hidden / off-screen). Rebuilds every time the app is
/// shown, so the locations are current for the moment the player opens it.
///
/// Put this on the Contacts app screen root (a screen managed by PhoneAppHost).
/// </summary>
[DisallowMultipleComponent]
public sealed class ContactsApp : MonoBehaviour
{
    [Header("List")]
    [Tooltip("The ScrollRect's Content transform that rows are added under.")]
    [SerializeField] private Transform contentRoot;

    [Tooltip("Prefab with a ContactRow component, instantiated once per NPC.")]
    [SerializeField] private ContactRow rowPrefab;

    [Header("Fallback")]
    [Tooltip("Location text shown when the NPC isn't present anywhere right now.")]
    [SerializeField] private string busyLabel = "Busy";

    private void OnEnable() => Rebuild();

    public void Rebuild()
    {
        if (contentRoot == null || rowPrefab == null)
            return;

        // Clear any rows from a previous open.
        for (int i = contentRoot.childCount - 1; i >= 0; i--)
            Destroy(contentRoot.GetChild(i).gameObject);

        var manager = NpcManager.Instance;
        if (manager == null)
            return;

        var npcs = manager.Npcs;
        for (int i = 0; i < npcs.Count; i++)
        {
            var def = npcs[i];
            if (def == null) continue;

            string displayName = string.IsNullOrWhiteSpace(def.DisplayName) ? def.NpcId : def.DisplayName;
            string location = manager.TryGetCurrentLocation(def, out var sceneName) && !string.IsNullOrEmpty(sceneName)
                ? sceneName
                : busyLabel;

            var row = Instantiate(rowPrefab, contentRoot);
            row.Bind(def.Portrait, displayName, location);
        }
    }
}
