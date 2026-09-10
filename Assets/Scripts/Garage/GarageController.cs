using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Top-level controller for the Garage (shop) scene: builds the scrollable
// creature list, wires the 6 fixed train slots and the power-up picker, and
// handles buy/select/assign clicks plus starting the ride. Manual per-slot
// assignment only -- no auto-fill. Spend-side only: seeds/spends GarageSave's
// Coin balance, never wires ride earnings (RunStats.Coins) back in.
public class GarageController : MonoBehaviour
{
    [SerializeField] private CreatureRoster creatureRoster;
    [SerializeField] private Transform creatureListContent;
    [SerializeField] private CreatureCardUI creatureCardPrefab;
    [SerializeField] private TrainSlotUI[] trainSlots; // size 6, index-ordered
    [SerializeField] private PowerUpCardUI[] powerUpCards; // Rocket / Jump Jet / Magnet
    [SerializeField] private TMP_Text coinsLabel;
    [SerializeField] private Button startRideButton;
    [SerializeField] private string coasterSceneName = "SampleScene";

    // The train carries one special cart (slot 1) plus a lead cart; every other
    // slot is a plain passenger cart.
    private static readonly string[] SlotRoleNames =
        { "Lead", "Power-Up", "Plain", "Plain", "Plain", "Plain" };

    private CreatureDefinition _pendingSelection;
    private readonly System.Collections.Generic.List<CreatureCardUI> _cards = new();

    private void Awake() => startRideButton.onClick.AddListener(OnStartRideClicked);

    private void Start()
    {
        BuildCreatureList();
        for (int i = 0; i < trainSlots.Length; i++)
            trainSlots[i].Init(i, SlotRoleNames[i], OnSlotClicked);
        BuildPowerUpPicker();
        RefreshSlots();
        RefreshCoins();
    }

    private void BuildPowerUpPicker()
    {
        if (powerUpCards == null) return;
        PowerUpType saved = GarageSave.GetPowerUp();
        foreach (var card in powerUpCards)
        {
            if (card == null) continue;
            card.Init(OnPowerUpClicked);
            card.SetSelected(card.PowerUp == saved);
        }
    }

    private void OnPowerUpClicked(PowerUpType type)
    {
        GarageSave.SetPowerUp(type);
        foreach (var card in powerUpCards)
            if (card != null) card.SetSelected(card.PowerUp == type);
    }

    private void BuildCreatureList()
    {
        foreach (var def in creatureRoster.creatures)
        {
            var card = Instantiate(creatureCardPrefab, creatureListContent);
            card.Bind(def, GarageSave.IsUnlocked(def), OnCardClicked);
            _cards.Add(card);
        }
    }

    private void OnCardClicked(CreatureDefinition def)
    {
        if (!GarageSave.IsUnlocked(def))
        {
            if (!GarageSave.TrySpendCoins(def.unlockCost)) return; // can't afford, no-op
            GarageSave.Unlock(def);
            RefreshCoins();
            foreach (var card in _cards) card.Bind(card.Creature, GarageSave.IsUnlocked(card.Creature), OnCardClicked);
        }
        _pendingSelection = def;
        foreach (var card in _cards) card.SetSelected(card.Creature == def);
    }

    private void OnSlotClicked(int slotIndex)
    {
        if (_pendingSelection == null) return;
        GarageSave.SetSlotCreature(slotIndex, _pendingSelection.displayName);
        RefreshSlots();
    }

    private void RefreshSlots()
    {
        for (int i = 0; i < trainSlots.Length; i++)
            trainSlots[i].SetAssigned(creatureRoster.FindByName(GarageSave.GetSlotCreatureName(i)));
    }

    private void RefreshCoins() => coinsLabel.text = $"Coins: {GarageSave.GetCoins()}";

    private void OnStartRideClicked() => UnityEngine.SceneManagement.SceneManager.LoadScene(coasterSceneName);
}
