using UnityEngine;

// Data-driven description of a track's material style (e.g. wood vs steel).
// One asset = one whole-track property: assign it to the track's
// TrackTypeApplier to drive both the physical feel (friction/bounce) and the
// placeholder visual tint for the rail line and the cart bodies.
[CreateAssetMenu(fileName = "TrackType_", menuName = "NuttyFluffies/Track Type Definition")]
public class TrackTypeDefinition : ScriptableObject
{
    [Header("Identity")]
    public string displayName = "Wood";

    [Header("Physics")]
    [Tooltip("Applied to the track's EdgeCollider2D. Cart-vs-cart friction is unaffected.")]
    public PhysicsMaterial2D trackPhysicsMaterial;

    [Header("Visuals (placeholder tints -- no art yet)")]
    public Color lineColor = Color.white;
    [Tooltip("Optional. If assigned, swaps the LineRenderer's material wholesale; leave null to just tint via color.")]
    public Material lineMaterial;
    [Tooltip("Applied to each listed cart's CartBody SpriteRenderer. Leave white to opt out of tinting.")]
    public Color cartTintColor = Color.white;
}
