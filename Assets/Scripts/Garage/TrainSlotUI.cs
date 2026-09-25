using UnityEngine;
using UnityEngine.UI;
using TMPro;

// One of the 6 fixed train-cart slots in the Garage: shows which creature is
// currently assigned (icon + name) and its fixed role label ("1. Lead",
// "2. Power-Up", "3. Plain" -- see GarageController.SlotRoleNames). The one
// "Power-Up" slot's ability is chosen separately in the power-up picker, not
// here. Clicking a slot assigns whatever creature is currently selected in
// GarageController; while an owned creature is selected the slots show an
// outline ("armed") so it's clear they're valid drop targets.
public class TrainSlotUI : MonoBehaviour
{
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text roleLabel;
    [SerializeField] private Button button;
    [SerializeField] private TMP_Text nameLabel; // optional

    public int SlotIndex { get; private set; }

    private void Awake() => UIButtonFeedback.Ensure(gameObject);

    public void Init(int slotIndex, string roleName, System.Action<int> onClicked)
    {
        SlotIndex = slotIndex;
        roleLabel.text = $"{slotIndex + 1}. {roleName}";
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => onClicked(slotIndex));
    }

    public void SetAssigned(CreatureDefinition def)
    {
        if (def == null)
        {
            icon.enabled = false;
            if (nameLabel != null) nameLabel.text = "Empty";
            return;
        }
        icon.enabled = true;
        icon.sprite = def.passengerSprite;
        icon.color = def.passengerTintColor;
        if (nameLabel != null) nameLabel.text = def.displayName;
    }

    public void SetArmed(bool armed)
    {
        var outline = GetComponent<Outline>();
        if (outline != null) outline.enabled = armed;
    }

    public void Punch()
    {
        var feedback = UIButtonFeedback.Ensure(gameObject);
        if (feedback != null) feedback.Punch();
    }
}
