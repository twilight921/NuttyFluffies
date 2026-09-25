using UnityEngine;
using UnityEngine.UI;
using TMPro;

// One instantiated card per creature in the Garage's scrollable creature
// list: shows icon/name/tier/unlock-state, and reports clicks up to
// GarageController (select-if-owned, two-tap buy-if-locked). Locked creatures
// render as a darkened silhouette; the price turns red when unaffordable.
public class CreatureCardUI : MonoBehaviour
{
    private static readonly Color OwnedColor = new Color(0.55f, 1f, 0.6f);
    private static readonly Color AffordableColor = new Color(1f, 0.85f, 0.15f);
    private static readonly Color UnaffordableColor = new Color(1f, 0.5f, 0.45f);

    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text nameLabel;
    [SerializeField] private TMP_Text stateLabel;
    [SerializeField] private Button button;
    [SerializeField] private GameObject selectedHighlight;
    [SerializeField] private TMP_Text tierLabel; // optional
    [SerializeField] private Image tierBar;      // optional

    public CreatureDefinition Creature { get; private set; }

    private void Awake() => UIButtonFeedback.Ensure(gameObject);

    public static Color TierColor(CreatureTier tier) => tier switch
    {
        CreatureTier.Uncommon => new Color(0.4f, 0.8f, 0.4f),
        CreatureTier.Rare => new Color(0.35f, 0.6f, 1f),
        CreatureTier.Epic => new Color(0.75f, 0.45f, 1f),
        CreatureTier.Legendary => new Color(1f, 0.65f, 0.15f),
        _ => new Color(0.7f, 0.72f, 0.78f),
    };

    public void Bind(CreatureDefinition def, bool unlocked, int coins, System.Action<CreatureDefinition> onClicked)
    {
        Creature = def;
        icon.sprite = def.passengerSprite;
        icon.color = unlocked ? def.passengerTintColor : Color.Lerp(def.passengerTintColor, Color.black, 0.65f);
        nameLabel.text = def.displayName;

        Color tierColor = TierColor(def.tier);
        if (tierLabel != null) { tierLabel.text = def.tier.ToString(); tierLabel.color = tierColor; }
        if (tierBar != null) tierBar.color = tierColor;

        if (unlocked)
        {
            stateLabel.text = "OWNED";
            stateLabel.color = OwnedColor;
        }
        else
        {
            stateLabel.text = $"Buy: {def.unlockCost}";
            stateLabel.color = coins >= def.unlockCost ? AffordableColor : UnaffordableColor;
        }

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => onClicked(def));
    }

    public void SetSelected(bool selected)
    {
        if (selectedHighlight != null) selectedHighlight.SetActive(selected);
        var outline = GetComponent<Outline>();
        if (outline != null) outline.enabled = selected;
    }
}
