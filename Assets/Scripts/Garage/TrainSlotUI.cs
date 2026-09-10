using UnityEngine;
using UnityEngine.UI;
using TMPro;

// One of the 6 fixed train-cart slots in the Garage: shows which creature is
// currently assigned (icon) and its fixed role label ("Lead", "Power-Up",
// "Plain" -- see GarageController.SlotRoleNames). The one "Power-Up" slot's
// ability is chosen separately in the power-up picker, not here. Clicking a
// slot assigns whatever creature is currently pending-selected in
// GarageController.
public class TrainSlotUI : MonoBehaviour
{
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text roleLabel;
    [SerializeField] private Button button;

    public int SlotIndex { get; private set; }

    public void Init(int slotIndex, string roleName, System.Action<int> onClicked)
    {
        SlotIndex = slotIndex;
        roleLabel.text = roleName;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => onClicked(slotIndex));
    }

    public void SetAssigned(CreatureDefinition def)
    {
        if (def == null) { icon.sprite = null; return; }
        icon.sprite = def.passengerSprite;
        icon.color = def.passengerTintColor;
    }
}
