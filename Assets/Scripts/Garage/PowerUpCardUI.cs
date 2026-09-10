using UnityEngine;
using UnityEngine.UI;
using TMPro;

// One of the three power-up choices in the Garage (Rocket / Jump Jet /
// Magnet). The train carries a single special cart; picking a card here sets
// which ability it runs. Reports clicks up to GarageController, which persists
// the choice via GarageSave.SetPowerUp. Simpler than CreatureCardUI -- power-
// ups aren't bought, so there's no locked/owned state, just selection.
//
// Which ability this card represents is a serialized field set by
// SetupGarageScene.cs, so the card is self-describing before Init runs.
public class PowerUpCardUI : MonoBehaviour
{
    [SerializeField] private PowerUpType powerUp;
    [SerializeField] private Image swatch;
    [SerializeField] private TMP_Text nameLabel;
    [SerializeField] private Button button;
    [SerializeField] private GameObject selectedHighlight;

    public PowerUpType PowerUp => powerUp;

    public void Init(System.Action<PowerUpType> onClicked)
    {
        if (swatch != null && PowerUpCart.TintColors.TryGetValue(powerUp, out var color))
            swatch.color = color;
        if (nameLabel != null) nameLabel.text = DisplayName(powerUp);
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => onClicked(powerUp));
    }

    public void SetSelected(bool selected)
    {
        if (selectedHighlight != null) selectedHighlight.SetActive(selected);
    }

    private static string DisplayName(PowerUpType type) => type switch
    {
        PowerUpType.Rocket => "Rocket",
        PowerUpType.JumpJet => "Jump Jet",
        PowerUpType.Magnet => "Magnet",
        _ => type.ToString(),
    };
}
