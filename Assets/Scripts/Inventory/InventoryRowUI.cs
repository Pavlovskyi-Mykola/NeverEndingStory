using UnityEngine;
using UnityEngine.UI;

public class InventoryRowUI : MonoBehaviour
{
    [SerializeField] private Image iconImage;
    [SerializeField] private Text nameText;
    [SerializeField] private Text countText;

    public void Bind(ItemDefinition item, int count)
    {
        if (nameText != null)
            nameText.text = item != null ? item.DisplayName : "(Unknown Item)";

        if (countText != null)
            countText.text = count.ToString();

        if (iconImage != null)
        {
            if (item != null && item.Icon != null)
            {
                iconImage.sprite = item.Icon;
                iconImage.enabled = true;
            }
            else
            {
                iconImage.sprite = null;
                iconImage.enabled = false;
            }
        }
    }
}