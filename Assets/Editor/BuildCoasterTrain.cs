using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Splines;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Collections.Generic;
using System.Linq;

// One-shot editor utility: turns the single "Cart" into a 6-cart train,
// coupled with HingeJoint2Ds, placed by walking arc-length along the actual
// track spline (not a straight-line guess) so it matches the curve.
// Idempotent: cleans up any previous run's duplicates/joints before rebuilding.
//
// Also configures the trailing carts' CartTintReceiver (so TrackTypeApplier
// can tint them) and turns ONE designated trailing cart into a PowerUpCart,
// wiring it to the shared PowerUpInputBroker on InputManager. The train
// carries a single special cart; which ability it actually runs is chosen in
// the Garage and applied at scene load by PowerUpLoadoutApplier -- the type
// baked here (Rocket) is just a working default for a Garage-skipped run.
// Promoted here from an ephemeral scratch script now that it's load-bearing
// for the power-up cart configuration too.
public static class BuildCoasterTrain
{
    private const int TrainSize = 6;
    private const float ColliderLength = 0.8f; // BoxCollider2D size.x on the cart
    private const float CouplerGap = 0.4f; // visible gap between cart bodies, so it reads as a train, not a block
    private const float CartSpacing = ColliderLength + CouplerGap; // center-to-center distance
    private const float AnchorOffset = CartSpacing / 2f; // joint anchors sit at the gap's midpoint so they coincide with no snap
    private const float StartMargin = 0.5f;
    private const int SampleCount = 800;

    // How far adjacent carts are allowed to rotate relative to each other beyond
    // their resting (track-following) alignment, in degrees each way. Keeps the
    // train coupler-rigid instead of hinging freely; loosen if it fights curves,
    // tighten if it still looks floppy.
    private const float CouplerAngleLimitDegrees = 15f;

    // The lead cart's swipe-push tuning as authored for a single, solo cart
    // (read from the scene before this script ever touched it). Kept as a fixed
    // base -- rather than an incremental multiply -- so reruns stay idempotent.
    private const float BaseSwipeSensitivity = 30f;
    private const float BaseMaxSwipeForce = 50000f;

    // Which trailing cart slot (0-based, so index 1 == "Cart (2)") becomes the
    // single PowerUpCart, and the ability baked onto it as a default. The
    // player's real choice is applied at runtime by PowerUpLoadoutApplier from
    // GarageSave; every other slot is a plain passenger cart. The placeholder
    // identity tint (PowerUpCart.TintColors) is applied directly here,
    // bypassing TrackTypeApplier's tint pass so track color doesn't overwrite it.
    private static readonly Dictionary<int, PowerUpType> PowerUpSlots = new Dictionary<int, PowerUpType>
    {
        { 1, PowerUpType.Rocket },  // Cart (2)
    };

    // Set of cart root GameObjects that are power-up carts once built, so
    // callers (e.g. SetupTrackTypes) can exclude them from track-color tinting.
    public static readonly List<GameObject> LastBuiltPowerUpCarts = new List<GameObject>();
    public static readonly List<GameObject> LastBuiltPlainCarts = new List<GameObject>();

    [MenuItem("NuttyFluffies/Build Coaster Train")]
    public static void Execute()
    {
        LastBuiltPowerUpCarts.Clear();
        LastBuiltPlainCarts.Clear();

        // --- Clean up any previous run first ---
        for (int i = 2; i <= TrainSize; i++)
        {
            GameObject stale = GameObject.Find($"Cart ({i})");
            if (stale != null) Object.DestroyImmediate(stale);
        }
        GameObject leadGO = GameObject.Find("Cart");
        if (leadGO == null)
        {
            Debug.LogError("[BuildCoasterTrain] Missing Cart in scene.");
            return;
        }
        var staleJoint = leadGO.GetComponent<HingeJoint2D>();
        if (staleJoint != null) Object.DestroyImmediate(staleJoint);

        GameObject trackGO = GameObject.Find("CoasterLineRender");
        if (trackGO == null)
        {
            Debug.LogError("[BuildCoasterTrain] Missing CoasterLineRender in scene.");
            return;
        }
        var container = trackGO.GetComponent<SplineContainer>();
        if (container == null)
        {
            Debug.LogError("[BuildCoasterTrain] CoasterLineRender has no SplineContainer.");
            return;
        }

        // Reset the lead cart onto the spline's start point every run so
        // placement is idempotent and adapts to whatever track shape is loaded
        // (this was a hand-tuned constant back when there was only one track).
        // Small t offset avoids a degenerate tangent exactly at t=0.
        {
            Vector3 startWorld = container.EvaluatePosition(0f);
            Vector3 startTangent = ((Vector3)container.EvaluateTangent(0.001f)).normalized;
            if (startTangent.sqrMagnitude < 0.0001f) startTangent = Vector3.right;
            Vector3 startNormal = new Vector3(-startTangent.y, startTangent.x, 0f);
            if (startNormal.y < 0f) startNormal = -startNormal;
            float startAngle = Mathf.Atan2(startTangent.y, startTangent.x) * Mathf.Rad2Deg;
            leadGO.transform.SetPositionAndRotation(startWorld + startNormal * 0.5f, Quaternion.Euler(0f, 0f, startAngle));
        }
        // NOTE: SplineContainer.EvaluatePosition/EvaluateTangent already return
        // world-space values in this project's package version -- do NOT also
        // run them through transform.TransformPoint/TransformDirection, or every
        // sample gets the track's position/rotation applied twice.

        // Arc-length lookup so carts can be spaced by real distance along the
        // curve, not the spline's non-uniform t. Shared with the runtime
        // LevelLoader via SplineArcLengthTable.
        var arc = new SplineArcLengthTable(container, SampleCount);
        float totalLength = arc.TotalLength;
        float TAtDistance(float d) => arc.TAtDistance(d);

        // Brute-force nearest sample to the (freshly reset) lead cart position.
        int bestIdx = arc.NearestSampleIndex(leadGO.transform.position, out float bestDist);
        float currentT = arc.TSamples[bestIdx];
        float currentArcDistance = arc.CumulativeDistance[bestIdx];
        Vector3 currentSplineWorld = arc.WorldPositions[bestIdx];

        // Preserve how far the cart currently rests off the rail (perpendicular
        // offset), so repositioned/duplicated carts sit the same way.
        Vector3 currentTangentWorld = container.EvaluateTangent(currentT);
        currentTangentWorld = currentTangentWorld.normalized;
        Vector3 currentNormalWorld = new Vector3(-currentTangentWorld.y, currentTangentWorld.x, 0f);
        float restOffset = Vector3.Dot(leadGO.transform.position - currentSplineWorld, currentNormalWorld);

        // Sanity check: the cart should be resting close to the rail (within a
        // couple of collider-widths), not units away. If the nearest-sample search
        // still landed far off, bail rather than fling carts into space.
        if (bestDist > 2f)
        {
            Debug.LogError($"[BuildCoasterTrain] Nearest spline sample is {bestDist:F2} units from the cart -- aborting, something is off (track/spline mismatch?).");
            return;
        }

        // There must be enough track behind the lead cart to fit the whole train's
        // tail; if not, slide the whole train forward along the spline just enough.
        float requiredArcDistance = CartSpacing * (TrainSize - 1) + StartMargin;
        float leadArcDistance = Mathf.Max(currentArcDistance, requiredArcDistance);

        var carts = new List<GameObject>();

        for (int i = 0; i < TrainSize; i++)
        {
            float arcDistance = leadArcDistance - i * CartSpacing;
            float t = TAtDistance(arcDistance);
            Vector3 splineWorld = container.EvaluatePosition(t);
            Vector3 tangentWorld = container.EvaluateTangent(t);
            tangentWorld = tangentWorld.normalized;
            Vector3 normalWorld = new Vector3(-tangentWorld.y, tangentWorld.x, 0f);
            Vector3 pos = splineWorld + normalWorld * restOffset;
            float angle = Mathf.Atan2(tangentWorld.y, tangentWorld.x) * Mathf.Rad2Deg;

            GameObject cartGO;
            if (i == 0)
            {
                cartGO = leadGO;
                Undo.RecordObject(cartGO.transform, "Reposition lead cart");
            }
            else
            {
                cartGO = Object.Instantiate(leadGO, leadGO.transform.parent);
                cartGO.name = $"Cart ({i + 1})";
                Undo.RegisterCreatedObjectUndo(cartGO, "Create trailing cart");

                // Trailing carts are dumb followers: no player input, they're
                // dragged along purely by the HingeJoint2D coupling to the cart
                // ahead of them.
                var followerInput = cartGO.GetComponent<CoasterCart>();
                if (followerInput != null) Object.DestroyImmediate(followerInput);
            }

            cartGO.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, 0f, angle));
            carts.Add(cartGO);
        }

        // Couple each cart to the one behind it: anchor at this cart's rear edge,
        // connected anchor at the next cart's front edge. autoConfigureConnectedAnchor
        // is off so the two anchors are pinned exactly where we intend, and
        // enableCollision stays false (Unity's default) so the couplers -- not the
        // box colliders touching -- are what holds the train together.
        for (int i = 0; i < carts.Count - 1; i++)
        {
            var joint = Undo.AddComponent<HingeJoint2D>(carts[i]);
            joint.autoConfigureConnectedAnchor = false;
            joint.anchor = new Vector2(-AnchorOffset, 0f);
            joint.connectedBody = carts[i + 1].GetComponent<Rigidbody2D>();
            joint.connectedAnchor = new Vector2(AnchorOffset, 0f);
            joint.enableCollision = false;
            // Zero reference is each pair's own resting (track-following) relative
            // angle at the moment the joint is added -- i.e. the limit is *extra*
            // flex beyond however aligned they already are, not an absolute angle.
            joint.useLimits = true;
            joint.limits = new JointAngleLimits2D { min = -CouplerAngleLimitDegrees, max = CouplerAngleLimitDegrees };
        }

        // The lead cart's swipe push has to move the whole coupled train now, not
        // just itself -- scale it up by how much heavier the train got, so a swipe
        // still produces roughly the same acceleration/feel as it did solo.
        float totalMass = 0f;
        foreach (var cart in carts) totalMass += cart.GetComponent<Rigidbody2D>().mass;
        float massScale = totalMass / carts[0].GetComponent<Rigidbody2D>().mass;

        var inputSerialized = new SerializedObject(leadGO.GetComponent<CoasterCart>());
        inputSerialized.FindProperty("swipeSensitivity").floatValue = BaseSwipeSensitivity * massScale;
        inputSerialized.FindProperty("maxSwipeForce").floatValue = BaseMaxSwipeForce * massScale;
        inputSerialized.ApplyModifiedProperties();

        // --- Every cart gets a CartTintReceiver on its CartBody, so TrackTypeApplier can tint it. ---
        foreach (var cart in carts)
        {
            var body = cart.transform.Find("CartBody");
            if (body == null) continue;
            if (body.GetComponent<CartTintReceiver>() == null)
                Undo.AddComponent<CartTintReceiver>(body.gameObject);
        }

        // --- Wire up the shared PowerUpInputBroker on InputManager (add it if missing). ---
        GameObject inputManagerGO = GameObject.Find("InputManager");
        PowerUpInputBroker powerUpBroker = null;
        if (inputManagerGO == null)
        {
            Debug.LogWarning("[BuildCoasterTrain] No InputManager in scene -- power-up carts won't have an activation broker wired.");
        }
        else
        {
            powerUpBroker = inputManagerGO.GetComponent<PowerUpInputBroker>();
            if (powerUpBroker == null) powerUpBroker = Undo.AddComponent<PowerUpInputBroker>(inputManagerGO);

            var activateRef = AssetDatabase.LoadAllAssetsAtPath("Assets/CoasterActions.inputactions")
                .OfType<InputActionReference>()
                .FirstOrDefault(r => r.action != null && r.action.name == "Activate");
            if (activateRef == null)
            {
                Debug.LogWarning("[BuildCoasterTrain] Could not find an 'Activate' InputActionReference sub-asset in CoasterActions.inputactions.");
            }
            else
            {
                var brokerSerialized = new SerializedObject(powerUpBroker);
                var activateActionProp = brokerSerialized.FindProperty("activateAction");
                activateActionProp.FindPropertyRelative("m_UseReference").boolValue = true;
                activateActionProp.FindPropertyRelative("m_Reference").objectReferenceValue = activateRef;
                brokerSerialized.ApplyModifiedProperties();
            }
        }

        // --- Turn the designated trailing carts into PowerUpCarts. ---
        for (int i = 0; i < carts.Count; i++)
        {
            if (!PowerUpSlots.TryGetValue(i, out PowerUpType powerUpType))
            {
                LastBuiltPlainCarts.Add(carts[i]);
                continue;
            }

            GameObject cartGO = carts[i];
            var existingStale = cartGO.GetComponent<PowerUpCart>();
            if (existingStale != null) Object.DestroyImmediate(existingStale);
            var powerUpCart = Undo.AddComponent<PowerUpCart>(cartGO);

            var puSerialized = new SerializedObject(powerUpCart);
            puSerialized.FindProperty("powerUpType").enumValueIndex = (int)powerUpType;
            if (powerUpBroker != null)
                puSerialized.FindProperty("activationBroker").objectReferenceValue = powerUpBroker;
            if (powerUpType == PowerUpType.Magnet)
                puSerialized.FindProperty("trackSpline").objectReferenceValue = container;
            puSerialized.ApplyModifiedProperties();

            // Placeholder tint applied directly (not via TrackTypeApplier), so
            // track-color tinting doesn't overwrite the power-up's identity color.
            var body = cartGO.transform.Find("CartBody");
            if (body != null)
            {
                var sr = body.GetComponent<SpriteRenderer>();
                if (sr != null && PowerUpCart.TintColors.TryGetValue(powerUpType, out var tint)) sr.color = tint;
            }

            LastBuiltPowerUpCarts.Add(cartGO);
        }

        Debug.Log($"[BuildCoasterTrain] massScale={massScale:F1} newSwipeSensitivity={BaseSwipeSensitivity * massScale:F0} newMaxSwipeForce={BaseMaxSwipeForce * massScale:F0}");
        Debug.Log($"[BuildCoasterTrain] bestSampleDist={bestDist:F3} currentArcDistance={currentArcDistance:F2} leadArcDistance={leadArcDistance:F2} totalTrackLength={totalLength:F2} restOffset={restOffset:F3}");
        Debug.Log($"[BuildCoasterTrain] Built {carts.Count}-cart train: {string.Join(", ", carts.ConvertAll(c => c.name))}");
        Debug.Log($"[BuildCoasterTrain] Power-up carts: {string.Join(", ", PowerUpSlots.Select(kv => $"{carts[kv.Key].name}={kv.Value}"))}");

        EditorSceneManager.MarkSceneDirty(leadGO.scene);
    }
}
