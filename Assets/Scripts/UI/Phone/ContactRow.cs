using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// One row in the phone's Contacts app: an NPC's portrait, name and current
/// location. Put this on the row prefab and wire the three fields; ContactsApp
/// instantiates it per NPC and calls <see cref="Bind"/>.
/// </summary>
[DisallowMultipleComponent]
public sealed class ContactRow : MonoBehaviour
{
    [SerializeField] private Image portrait;
    [SerializeField] private TMP_Text nameLabel;
    [SerializeField] private TMP_Text locationLabel;

    public void Bind(Sprite portraitSprite, string displayName, string location)
    {
        if (portrait != null)
        {
            portrait.sprite = portraitSprite;
            portrait.preserveAspect = true;
            // Hide the image entirely when there's no portrait, so the row doesn't
            // show a blank white box.
            portrait.enabled = portraitSprite != null;
        }

        if (nameLabel != null) nameLabel.text = displayName;
        if (locationLabel != null) locationLabel.text = location;
    }
}
