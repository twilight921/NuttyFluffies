using UnityEngine;

// Data-driven description of a stuffed-animal passenger (Squirrel, Owl, Fox,
// Griffin, Dragon), matching the reference game's cast. One asset = one
// character: assign it to a cart's CreaturePassenger to decide which fluffy
// is riding that car. Mirrors TrackTypeDefinition's shape on purpose (same
// "data asset drives an Applier component" pattern already used for tracks).
[CreateAssetMenu(fileName = "Creature_", menuName = "NuttyFluffies/Creature Definition")]
public class CreatureDefinition : ScriptableObject
{
    [Header("Identity")]
    public string displayName = "Squirrel";

    [Header("Unlocking")]
    [Tooltip("Rarity/unlock tier. Squirrel (the free starter) stays Common; the Owl..Dragon unlock ladder uses the full range.")]
    public CreatureTier tier = CreatureTier.Common;
    [Tooltip("Coin cost to unlock this passenger. 0 for the Squirrel starter, which is owned from the start.")]
    public int unlockCost;

    [Header("Scoring")]
    [Tooltip("Which specific player action earns this passenger Hearts. None means no per-passenger gating -- Hearts come from generic Pickup placement instead.")]
    public HeartActionType heartAction = HeartActionType.None;
    [Tooltip("Bonus Hearts awarded (via RunStats.AddStuntHeart) when heartAction fires for this passenger's own cart. Unused while heartAction is None.")]
    public int stuntHeartBonus = 3;

    [Header("Visuals")]
    [Tooltip("Sprite shown for this passenger. Real chibi art lives in Assets/Art/Creatures/Premium/, wired by WirePremiumCreatureSprites; passengerTintColor still tints it.")]
    public Sprite passengerSprite;
    public Color passengerTintColor = Color.white;
}
