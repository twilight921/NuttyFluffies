using System.Collections.Generic;
using UnityEngine;

// Data-driven description of one coaster level. Same Definition-asset idea as
// TrackTypeDefinition / CreatureDefinition: one asset = one whole level, and
// the asset alone is enough to (re)build that level -- the track shape is
// regenerated from `seed` + the shape params below, so we don't have to store
// every spline knot.
//
// A designer who wants to hand-tune a specific track can bake the generated
// knots into `bakedKnots` (NuttyFluffies/Bake Generated Knots Into Level) and
// flip `useBakedKnots`; from then on the build uses that exact list and the
// shape params are ignored for this level.
[CreateAssetMenu(fileName = "Level_", menuName = "NuttyFluffies/Level Definition")]
public class LevelDefinition : ScriptableObject
{
    [Header("Identity")]
    public string displayName = "New Level";
    [Tooltip("Optional grouping label for a themed set (e.g. 'Meadow', 'Canyon'). Organizational only today.")]
    public string worldName = "";
    [Tooltip("Sort order inside LevelCatalog. Lower shows first.")]
    public int order = 0;

    [Header("Track Shape (ignored when Use Baked Knots is on)")]
    [Tooltip("Seed for the procedural hill layout. Same seed + same params => identical track, every build.")]
    public int seed = 12345;
    [Tooltip("Total track length in world units along X (lead-in and run-out included).")]
    public float length = 110f;
    [Tooltip("X distance between spline knots. Smaller = more control points = tighter hills possible.")]
    public float knotSpacing = 8f;
    [Tooltip("Flat, level stretch at the very start so the train can spawn and settle.")]
    public float leadInLength = 10f;
    [Tooltip("Flat stretch at the end so the run finishes cleanly before RunEndTrigger fires.")]
    public float runOutLength = 8f;
    [Tooltip("Peak possible hill height in world units. Actual per-knot height is scaled by the envelope below.")]
    public float maxHillHeight = 6f;
    [Tooltip("Hill height across the track: X = 0..1 progress, Y = 0..1 multiplier. Ramp up for a track that gets wilder; keep the end low for a gentle finish.")]
    public AnimationCurve amplitudeEnvelope = AnimationCurve.Linear(0f, 0.35f, 1f, 1f);
    [Range(0.05f, 1f)]
    [Tooltip("Higher = more, shorter hills. Lower = long rolling swells.")]
    public float hillFrequency = 0.32f;
    [Range(0f, 1f)]
    [Tooltip("A finer second layer of bumps on top of the main hills. 0 = perfectly smooth swells.")]
    public float roughness = 0.22f;
    [Tooltip("Overall downhill drift so momentum trends forward. Units of drop per unit of length.")]
    public float downhillBias = 0.03f;

    [Header("Track Character")]
    [Tooltip("Personality of this track. Warps the base hill waveform AND presets the feature counts below when the level is (re)generated. See TrackArchetype.")]
    public TrackArchetype archetype = TrackArchetype.RollingHills;
    [Tooltip("Sharp back-to-back turn pairs scattered along the run. Rewards the Squirrel (Tight Turn).")]
    [Range(0, 8)] public int hairpinCount = 0;
    [Range(0f, 1f)]
    [Tooltip("How vicious each hairpin is. Higher = snappier direction change (still slope-clamped so it stays rideable).")]
    public float hairpinSharpness = 0.5f;
    [Tooltip("Abrupt convex launch crests that fling the train off the rail. Rewards the Owl (Airtime) and Dragon (Near Miss).")]
    [Range(0, 8)] public int launchCount = 0;
    [Range(0f, 1f)]
    [Tooltip("Launch crest height / drop on the far side. Higher = more hang time.")]
    public float launchStrength = 0.5f;
    [Tooltip("Long, steep sustained descents that build top speed. Rewards the Fox (Speed Burst).")]
    [Range(0, 6)] public int plungeCount = 0;
    [Range(0f, 1f)]
    [Tooltip("Depth and steepness of each plunge.")]
    public float plungeSteepness = 0.5f;
    [Tooltip("Runs of rapid-fire little bumps (washboard) that keep the wheels skipping. Rewards the Dragon (Near Miss).")]
    [Range(0, 8)] public int washboardCount = 0;
    [Range(0f, 1f)]
    [Tooltip("Bump amplitude of each washboard run.")]
    public float washboardStrength = 0.5f;

    [Header("Hand-tuning")]
    [Tooltip("When on, the build uses the exact knot list below instead of regenerating from the seed.")]
    public bool useBakedKnots = false;
    [Tooltip("Frozen knot positions (local space, X increasing). Populated by 'Bake Generated Knots Into Level'.")]
    public List<Vector3> bakedKnots = new List<Vector3>();

    [Header("Track Type")]
    [Tooltip("Wood (grippy, slower) vs Steel (fast, slight bounce). Drives feel + placeholder tint.")]
    public TrackTypeDefinition trackType;

    [Header("Pickups")]
    public int heartCount = 8;
    public int coinCount = 18;
    [Tooltip("How far Hearts float off the rail (risk for score). Wider on harder levels.")]
    public float heartMinOffset = 1.2f;
    public float heartMaxOffset = 2.6f;
    [Tooltip("How far Coins sit off the easy line.")]
    public float coinOffset = 0.55f;

    [Header("Theme (placeholder -- no art yet)")]
    [Tooltip("Flat camera background tint for this level / world.")]
    public Color backgroundTint = new Color(0.55f, 0.74f, 0.92f);
}
