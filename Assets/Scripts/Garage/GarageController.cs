using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Top-level controller for the Garage (shop) scene: builds the scrollable
// creature list, wires the 6 fixed train slots and the power-up picker, and
// handles buy/select/assign clicks plus starting the ride. Manual per-slot
// assignment only -- no auto-fill. Spend-side only: seeds/spends GarageSave's
// Coin balance, never wires ride earnings (RunStats.Coins) back in.
//
// Interaction model: tap a creature card to select it (owned) or to inspect
// it (locked); tap a locked card a second time to buy it; then tap train
// slots to place the selected creature. Every step reports back through the
// optional status strip so the player always knows what just happened.
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
    [SerializeField] private UIStatusMessage statusMessage; // optional
    [SerializeField] private Button backButton;             // optional
    [SerializeField] private string menuSceneName = "MainMenu";

    // The train carries one special cart (slot 1) plus a lead cart; every other
    // slot is a plain passenger cart.
    private static readonly string[] SlotRoleNames =
        { "Lead", "Power-Up", "Plain", "Plain", "Plain", "Plain" };

    private CreatureDefinition _selected;
    private readonly System.Collections.Generic.List<CreatureCardUI> _cards = new();

    private void Awake()
    {
        startRideButton.onClick.AddListener(OnStartRideClicked);
        UIButtonFeedback.Ensure(startRideButton.gameObject);

        if (backButton != null)
        {
            backButton.onClick.AddListener(OnBackClicked);
            UIButtonFeedback.Ensure(backButton.gameObject);
        }
    }

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
        Say($"Power-Up cart set to {PowerUpName(type)}.", UIStatusMessage.Good);
    }

    private void BuildCreatureList()
    {
        int coins = GarageSave.GetCoins();
        foreach (var def in creatureRoster.creatures)
        {
            var card = Instantiate(creatureCardPrefab, creatureListContent);
            card.Bind(def, GarageSave.IsUnlocked(def), coins, OnCardClicked);
            _cards.Add(card);
        }
    }

    private void RefreshCards()
    {
        int coins = GarageSave.GetCoins();
        foreach (var card in _cards)
        {
            card.Bind(card.Creature, GarageSave.IsUnlocked(card.Creature), coins, OnCardClicked);
            card.SetSelected(card.Creature == _selected);
        }
    }

    private void OnCardClicked(CreatureDefinition def)
    {
        if (GarageSave.IsUnlocked(def))
        {
            Select(def);
            Say($"{def.displayName} selected - tap a train cart to place it.", UIStatusMessage.Neutral);
            return;
        }

        // Locked: first tap inspects, second tap on the same card buys.
        int coins = GarageSave.GetCoins();
        if (_selected != def)
        {
            Select(def);
            if (coins >= def.unlockCost)
                Say($"{def.displayName} costs {def.unlockCost} coins - tap again to buy.", UIStatusMessage.Warn);
            else
                Say($"{def.displayName} costs {def.unlockCost} coins - you need {def.unlockCost - coins} more.", UIStatusMessage.Bad);
            return;
        }

        if (!GarageSave.TrySpendCoins(def.unlockCost))
        {
            Say($"Not enough coins for {def.displayName}: need {def.unlockCost - coins} more.", UIStatusMessage.Bad);
            return;
        }

        GarageSave.Unlock(def);
        RefreshCoins();
        RefreshCards();
        RefreshArmedSlots();
        Say($"Unlocked {def.displayName}! Tap a train cart to place it.", UIStatusMessage.Good);
    }

    private void Select(CreatureDefinition def)
    {
        _selected = def;
        foreach (var card in _cards) card.SetSelected(card.Creature == def);
        RefreshArmedSlots();
    }

    private void OnSlotClicked(int slotIndex)
    {
        if (_selected == null)
        {
            Say("Pick a creature from the list first.", UIStatusMessage.Warn);
            return;
        }
        if (!GarageSave.IsUnlocked(_selected))
        {
            Say($"{_selected.displayName} is locked - tap its card again to buy it first.", UIStatusMessage.Warn);
            return;
        }
        if (GarageSave.GetSlotCreatureName(slotIndex) == _selected.displayName)
        {
            Say($"{SlotDescription(slotIndex)} already has {_selected.displayName}.", UIStatusMessage.Neutral);
            return;
        }

        GarageSave.SetSlotCreature(slotIndex, _selected.displayName);
        RefreshSlots();
        trainSlots[slotIndex].Punch();
        Say($"{_selected.displayName} placed in {SlotDescription(slotIndex).ToLowerInvariant()}.", UIStatusMessage.Good);
    }

    private void RefreshSlots()
    {
        for (int i = 0; i < trainSlots.Length; i++)
            trainSlots[i].SetAssigned(creatureRoster.FindByName(GarageSave.GetSlotCreatureName(i)));
        RefreshArmedSlots();
    }

    private void RefreshArmedSlots()
    {
        bool armed = _selected != null && GarageSave.IsUnlocked(_selected);
        foreach (var slot in trainSlots) slot.SetArmed(armed);
    }

    private void RefreshCoins() => coinsLabel.text = $"Coins: {GarageSave.GetCoins()}";

    private void Say(string message, Color color)
    {
        if (statusMessage != null) statusMessage.Show(message, color);
    }

    private static string SlotDescription(int slotIndex) => slotIndex switch
    {
        0 => "The lead cart",
        1 => "The power-up cart",
        _ => $"Cart {slotIndex + 1}",
    };

    private static string PowerUpName(PowerUpType type) => type switch
    {
        PowerUpType.JumpJet => "Jump Jet",
        _ => type.ToString(),
    };

    private void OnStartRideClicked() => UnityEngine.SceneManagement.SceneManager.LoadScene(coasterSceneName);

    private void OnBackClicked() => UnityEngine.SceneManagement.SceneManager.LoadScene(menuSceneName);
}
