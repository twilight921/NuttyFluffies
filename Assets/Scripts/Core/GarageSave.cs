using UnityEngine;

// Plain static PlayerPrefs-backed persistence for the Garage (shop) scene:
// Coin balance, which creatures are unlocked, which creature is assigned to
// each of the 6 train slots, and which single special-cart power-up the train
// carries. Deliberately NOT a MonoBehaviour/singleton
// like RunStats -- nothing here needs a live scene callback surface, it's
// just get/set against PlayerPrefs, callable from anywhere (editor or runtime).
//
// Seeds a fixed starting Coin balance and lets the Garage spend it via
// TrySpendCoins. AddCoins is the deposit side, used by ResultsController to
// bank a finished run's RunStats.Coins once it ends -- GarageSave itself
// still never reads RunStats directly, it just exposes the add.
//
// Uses CreatureDefinition.displayName as the persistence key -- already a
// de facto unique ID in this codebase (both SetupCreatures.cs and
// SetupPremiumCreatures.cs key assets by it).
public static class GarageSave
{
    private const string CoinsKey = "NF_Coins";
    private const int StartingCoins = 500;

    private const string UnlockedKeyPrefix = "NF_Unlocked_";
    private const string SlotKeyPrefix = "NF_Slot_";
    private const string PowerUpKey = "NF_PowerUp";

    // The train carries exactly one special (power-up) cart. This is which
    // ability it has -- chosen in the Garage, applied to the built-in special
    // cart at coaster-scene load by PowerUpLoadoutApplier. Rocket is the
    // default so a player who never opens the Garage still gets a working
    // power-up (matching what BuildCoasterTrain.cs bakes onto the cart).
    private const PowerUpType DefaultPowerUp = PowerUpType.Rocket;

    // Every slot starts on Squirrel, the free lowest-tier starter creature, so
    // a brand-new player (or one who plays the coaster scene directly without
    // visiting the Garage) rides a full train of Squirrels -- matching what
    // SetupCreatures.cs bakes onto the carts.
    private static readonly string[] DefaultSlotCreatureNames =
        { "Squirrel", "Squirrel", "Squirrel", "Squirrel", "Squirrel", "Squirrel" };

    public static int GetCoins()
    {
        return PlayerPrefs.GetInt(CoinsKey, StartingCoins);
    }

    public static void AddCoins(int amount)
    {
        if (amount <= 0) return;
        PlayerPrefs.SetInt(CoinsKey, GetCoins() + amount);
        PlayerPrefs.Save();
    }

    public static bool TrySpendCoins(int amount)
    {
        int current = GetCoins();
        if (amount <= 0 || current < amount) return false;
        PlayerPrefs.SetInt(CoinsKey, current - amount);
        PlayerPrefs.Save();
        return true;
    }

    public static bool IsUnlocked(CreatureDefinition def)
    {
        if (def == null) return false;
        if (def.unlockCost <= 0) return true; // free starter (Squirrel) is always owned
        return PlayerPrefs.GetInt(UnlockedKeyPrefix + def.displayName, 0) == 1;
    }

    public static void Unlock(CreatureDefinition def)
    {
        if (def == null) return;
        PlayerPrefs.SetInt(UnlockedKeyPrefix + def.displayName, 1);
        PlayerPrefs.Save();
    }

    public static string GetSlotCreatureName(int slotIndex)
    {
        string defaultName = (slotIndex >= 0 && slotIndex < DefaultSlotCreatureNames.Length)
            ? DefaultSlotCreatureNames[slotIndex]
            : DefaultSlotCreatureNames[0];
        return PlayerPrefs.GetString(SlotKeyPrefix + slotIndex, defaultName);
    }

    public static void SetSlotCreature(int slotIndex, string creatureDisplayName)
    {
        PlayerPrefs.SetString(SlotKeyPrefix + slotIndex, creatureDisplayName);
        PlayerPrefs.Save();
    }

    public static PowerUpType GetPowerUp()
    {
        return (PowerUpType)PlayerPrefs.GetInt(PowerUpKey, (int)DefaultPowerUp);
    }

    public static void SetPowerUp(PowerUpType type)
    {
        PlayerPrefs.SetInt(PowerUpKey, (int)type);
        PlayerPrefs.Save();
    }
}
