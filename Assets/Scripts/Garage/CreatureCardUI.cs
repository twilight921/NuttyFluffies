using UnityEngine;
using UnityEngine.UI;
using TMPro;

// One instantiated card per creature in the Garage's scrollable creature
// list: shows icon/name/unlock-state, and reports clicks up to
// GarageController (buy-if-locked, select-if-owned).
public class CreatureCardUI : MonoBehaviour
{
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text nameLabel;
    [SerializeField] private TMP_Text stateLabel;
    [SerializeField] private Button button;
    [SerializeField] private GameObject selectedHighlight;

    public CreatureDefinition Creature { get; private set; }

    public void Bind(CreatureDefinition def, bool unlocked, System.Action<CreatureDefinition> onClicked)
    {
        Creature = def;
        icon.sprite = def.passengerSprite;
        icon.color = def.passengerTintColor;
        nameLabel.text = def.displayName;
        stateLabel.text = unlocked ? "Owned" : $"{def.unlockCost}";
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => onClicked(def));
    }

    public void SetSelected(bool selected) => selectedHighlight.SetActive(selected);
}
