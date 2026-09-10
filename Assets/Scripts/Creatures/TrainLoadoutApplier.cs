using UnityEngine;

// Applies the Garage's saved slot->creature assignments to the actual train
// at scene load, so the manual per-slot loadout chosen in the Garage carries
// over into SampleScene. Lives on a root "LoadoutApplier" GameObject, added
// idempotently by Assets/Editor/SetupLoadoutApplier.cs.
//
// If the player never visits the Garage, GarageSave.GetSlotCreatureName's
// defaults (a full train of the Squirrel starter) match what SetupCreatures.cs
// bakes onto the carts, so this is a no-op in effect (every slot re-assigns
// the same creature it already has).
public class TrainLoadoutApplier : MonoBehaviour
{
    [SerializeField] private CreatureRoster creatureRoster;

    private static readonly string[] CartNames =
        { "Cart", "Cart (2)", "Cart (3)", "Cart (4)", "Cart (5)", "Cart (6)" };

    private void Awake()
    {
        for (int i = 0; i < CartNames.Length; i++)
        {
            var cartGO = GameObject.Find(CartNames[i]);
            var passenger = cartGO?.transform.Find("Passenger")?.GetComponent<CreaturePassenger>();
            if (passenger == null) continue;
            var def = creatureRoster.FindByName(GarageSave.GetSlotCreatureName(i));
            if (def != null) passenger.SetCreature(def);
        }
    }
}
