// Which specific player action earns Hearts for a given passenger. None means
// no per-passenger gating -- Hearts just come from where a Pickup happens to
// be placed (see PickupType/Pickup). Every creature on the current ladder
// (Squirrel/Owl/Fox/Griffin/Dragon) instead earns Hearts from a specific
// stunt, mirroring how the reference game gates scoring by passenger type.
// NearMiss/off-rail float is the game's existing signature Hearts mechanic
// (already expressed generically via Pickup placement) -- Dragon is simply
// the one creature whose *bonus* scoring is explicitly built around doing
// more of that. Standalone file (not nested), matching PickupType/PowerUpType.
public enum HeartActionType
{
    None,
    TightTurn,
    Airtime,
    SpeedBurst,
    Inversion,
    NearMiss
}
