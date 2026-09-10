using UnityEngine;

// Minimal run-scoped tally for collected pickups: Hearts add to Score, Coins
// add to Coins. Deliberately thin -- no save persistence, no spending -- just
// an accumulation point Pickup can report to and a future HUD/economy system
// can subscribe to via the events below. A static Instance (rather than a
// wired scene reference) because pickups scattered all along the track all
// need to reach it, the same reasoning the input brokers use events for.
public class RunStats : MonoBehaviour
{
    public static RunStats Instance { get; private set; }

    public int Score { get; private set; }
    public int Coins { get; private set; }

    public System.Action<int> OnScoreChanged;
    public System.Action<int> OnCoinsChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public void AddPickup(PickupType type, int value)
    {
        switch (type)
        {
            case PickupType.Heart:
                Score += value;
                OnScoreChanged?.Invoke(Score);
                break;
            case PickupType.Coin:
                Coins += value;
                OnCoinsChanged?.Invoke(Coins);
                break;
        }
    }

    // Separate from AddPickup: this Score bonus comes from a StuntDetector
    // event matching a passenger's CreatureDefinition.heartAction, not from
    // colliding with a placed Pickup, so there's no PickupType for it.
    public void AddStuntHeart(int value)
    {
        if (value <= 0) return;
        Score += value;
        OnScoreChanged?.Invoke(Score);
    }
}
