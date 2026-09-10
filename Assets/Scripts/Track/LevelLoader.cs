using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;

// Builds the selected level's track when the coaster scene loads. One coaster
// scene, N levels: reads LevelSelection -> LevelCatalog -> writes the spline,
// bakes the rail collider, scatters pickups, then repositions the authored
// 6-cart train onto the new track (re-coupling it) and re-points the spline
// references other components hold.
//
// Runs from Awake at a very low execution order so physics never sees the old
// track. Falls back to `fallbackLevel` when there's no catalog/selection, so
// opening the coaster scene directly still gives a playable ride -- the same
// "playable without visiting the Garage" guarantee GarageSave and
// BuildCoasterTrain already keep.
[DefaultExecutionOrder(-100)]
public class LevelLoader : MonoBehaviour
{
    [Header("Content")]
    [SerializeField] private LevelCatalog catalog;
    [Tooltip("Used when there is no catalog or no valid selection (e.g. playing the coaster scene directly).")]
    [SerializeField] private LevelDefinition fallbackLevel;

    [Header("Scene references")]
    [SerializeField] private SplineContainer track;            // CoasterLineRender
    [SerializeField] private EdgeCollider2D trackCollider;     // CoasterLineRender
    [SerializeField] private LineRenderer trackLine;           // CoasterLineRender (the visible rail)
    [SerializeField] private TrackTypeApplier trackTypeApplier;
    [SerializeField] private RunEndTrigger runEndTrigger;
    [SerializeField] private Pickup heartPrefab;
    [SerializeField] private Pickup coinPrefab;
    [Tooltip("Every train car, lead first, in order.")]
    [SerializeField] private Transform[] trainCarts;

    [Header("Tuning")]
    [SerializeField] private int colliderSamples = 600;
    [SerializeField] private int arcSamples = 800;
    [Tooltip("How far above the rail a car rests (matches how BuildCoasterTrain seats them).")]
    [SerializeField] private float railStandoff = 0.5f;
    [Tooltip("Center-to-center distance between cars. Matches BuildCoasterTrain (0.8 body + 0.4 gap).")]
    [SerializeField] private float cartSpacing = 1.2f;
    [SerializeField] private float startMargin = 0.5f;
    [SerializeField] private float couplerAngleLimitDegrees = 15f;

    public LevelDefinition ActiveLevel { get; private set; }

    private void Awake()
    {
        LevelDefinition level = ResolveLevel();
        if (level == null)
        {
            Debug.LogWarning("[LevelLoader] No level to build (no catalog, selection, or fallback) -- leaving the authored track as-is.");
            return;
        }
        if (track == null || trackCollider == null)
        {
            Debug.LogError("[LevelLoader] Track SplineContainer / EdgeCollider2D not wired.");
            return;
        }

        ActiveLevel = level;
        BuildTrack(level);
        PlaceTrain();
        ScatterPickups(level);
        ApplyTheme(level);
        Debug.Log($"[LevelLoader] Built level '{level.displayName}' (index {LevelSelection.Index}).");
    }

    private LevelDefinition ResolveLevel()
    {
        if (catalog != null && catalog.Count > 0)
            return catalog.Get(LevelSelection.Index);
        return fallbackLevel;
    }

    private void BuildTrack(LevelDefinition level)
    {
        List<Vector3> knots = level.useBakedKnots && level.bakedKnots != null && level.bakedKnots.Count >= 2
            ? level.bakedKnots
            : TrackGenerator.Generate(level);

        SplineTrackBuilder.Build(track, trackCollider, trackLine, knots, colliderSamples);

        if (trackTypeApplier != null && level.trackType != null)
            trackTypeApplier.SetTrackType(level.trackType);

        if (runEndTrigger != null)
            runEndTrigger.SetSpline(track);
    }

    // Reposition every car along the freshly built spline (arc-length spaced,
    // matching BuildCoasterTrain) and rebuild the HingeJoint2D couplers so the
    // limit zero-reference matches this track's resting angles, not the shape
    // the joints were originally baked against.
    private void PlaceTrain()
    {
        if (trainCarts == null || trainCarts.Length == 0) return;

        var arc = new SplineArcLengthTable(track, arcSamples);
        float tailLength = cartSpacing * (trainCarts.Length - 1) + startMargin;
        float leadDistance = Mathf.Min(Mathf.Max(startMargin, tailLength), arc.TotalLength);

        var bodies = new Rigidbody2D[trainCarts.Length];

        for (int i = 0; i < trainCarts.Length; i++)
        {
            Transform cart = trainCarts[i];
            if (cart == null) continue;

            float d = Mathf.Clamp(leadDistance - i * cartSpacing, 0f, arc.TotalLength);
            float t = arc.TAtDistance(d);
            Vector3 pos = track.EvaluatePosition(t);
            Vector3 tangent = ((Vector3)track.EvaluateTangent(t)).normalized;
            if (tangent.sqrMagnitude < 0.0001f) tangent = Vector3.right;
            Vector3 normal = new Vector3(-tangent.y, tangent.x, 0f);
            if (normal.y < 0f) normal = -normal;
            float angle = Mathf.Atan2(tangent.y, tangent.x) * Mathf.Rad2Deg;

            cart.SetPositionAndRotation(pos + normal * railStandoff, Quaternion.Euler(0f, 0f, angle));

            var rb = cart.GetComponent<Rigidbody2D>();
            bodies[i] = rb;
            if (rb != null)
            {
                rb.velocity = Vector2.zero;
                rb.angularVelocity = 0f;
            }
        }

        // Re-couple: anchor at this car's rear edge, connected anchor at the
        // next car's front edge (same math as BuildCoasterTrain).
        float anchorOffset = cartSpacing / 2f;
        for (int i = 0; i < trainCarts.Length - 1; i++)
        {
            if (trainCarts[i] == null || bodies[i + 1] == null) continue;

            foreach (var stale in trainCarts[i].GetComponents<HingeJoint2D>())
                Destroy(stale);

            var joint = trainCarts[i].gameObject.AddComponent<HingeJoint2D>();
            joint.autoConfigureConnectedAnchor = false;
            joint.anchor = new Vector2(-anchorOffset, 0f);
            joint.connectedBody = bodies[i + 1];
            joint.connectedAnchor = new Vector2(anchorOffset, 0f);
            joint.enableCollision = false;
            joint.useLimits = true;
            joint.limits = new JointAngleLimits2D { min = -couplerAngleLimitDegrees, max = couplerAngleLimitDegrees };
        }
    }

    private void ScatterPickups(LevelDefinition level)
    {
        GameObject old = GameObject.Find("Pickups");
        if (old != null)
        {
            old.SetActive(false); // hide this frame; Destroy lands at end of frame
            Destroy(old);
        }

        var root = new GameObject("Pickups").transform;

        Scatter(coinPrefab, "Coin", level.coinCount, root,
            _ => level.coinOffset);
        Scatter(heartPrefab, "Heart", level.heartCount, root,
            i => Mathf.Lerp(level.heartMinOffset, level.heartMaxOffset, (i % 3) / 2f));
    }

    private void Scatter(Pickup prefab, string label, int count, Transform parent, System.Func<int, float> offsetForIndex)
    {
        if (prefab == null || count <= 0) return;
        const float margin = 0.04f;

        for (int i = 0; i < count; i++)
        {
            float t = Mathf.Lerp(margin, 1f - margin, count == 1 ? 0f : (float)i / (count - 1));
            Vector3 pos = track.EvaluatePosition(t);
            Vector3 tangent = ((Vector3)track.EvaluateTangent(t)).normalized;
            Vector3 normal = new Vector3(-tangent.y, tangent.x, 0f);
            if (normal.y < 0f) normal = -normal; // stay on the reachable, above-rail side
            float along = (i % 2 == 0) ? 0.3f : -0.3f;

            var instance = Instantiate(prefab, pos + normal * offsetForIndex(i) + tangent * along, Quaternion.identity, parent);
            instance.name = $"{label} ({i + 1})";
        }
    }

    private void ApplyTheme(LevelDefinition level)
    {
        Camera cam = Camera.main;
        if (cam != null) cam.backgroundColor = level.backgroundTint;
    }
}
