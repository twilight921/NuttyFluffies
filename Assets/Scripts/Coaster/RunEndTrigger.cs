using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics;

// Detects the lead cart reaching the end of the track. Neither CoasterCart
// nor SplineLineSampler tracks a spline-progress value today -- CoasterCart
// only ever turns swipes into forces, and SplineLineSampler only draws the
// fixed line -- so there's nothing existing to read. This adds the minimal
// accessor needed: every physics tick, project this cart's live Rigidbody2D
// position onto the track spline (purely observing existing physics state,
// not scripting any movement) and watch the resulting normalized position
// (t, 0-1 along the spline) approach 1.0. This is the same
// SplineUtility.GetNearestPoint approach PowerUpCart's Magnet correction
// already uses to read a cart's position back into spline-space, so it's a
// proven fit for this project's SplineContainer/package version (world-space
// results, no extra transform needed).
//
// Fires OnRunEnd exactly once (guarded by _hasFired) rather than every frame
// past the threshold, matching StuntDetector/PowerUpInputBroker's plain
// per-listener event style -- ResultsController is the only intended
// listener, so there's no need for a shared broker.
[RequireComponent(typeof(Rigidbody2D))]
public class RunEndTrigger : MonoBehaviour
{
    [Tooltip("The track's SplineContainer (CoasterLineRender).")]
    [SerializeField] private SplineContainer trackSpline;

    [Tooltip("Normalized spline progress (0-1) that counts as 'reached the end'. Just under 1.0 so sampling/resolution noise near the very last stretch of track can't strand a run just short of firing.")]
    [SerializeField] private float endThreshold = 0.98f;

    [Tooltip("Optional end-of-level Station. When set, the run ends when the train has stopped at the station instead of when progress crosses End Threshold.")]
    [SerializeField] private Station station;

    public event System.Action OnRunEnd;

    // Exposed read-only in case a HUD or debug view wants to show progress;
    // not required by ResultsController itself.
    public float Progress { get; private set; }

    private bool _hasFired;

    // Runtime re-point, used by LevelLoader after it rebuilds the track spline.
    public void SetSpline(SplineContainer spline) => trackSpline = spline;

    // Runtime re-point, used by LevelLoader (and the Setup Station editor tool).
    public void SetStation(Station newStation)
    {
        if (_isSubscribed && station != null) station.OnTrainStopped -= HandleTrainStopped;
        _isSubscribed = false;
        station = newStation;
        if (isActiveAndEnabled) Subscribe();
    }

    private bool _isSubscribed;

    private void OnEnable() => Subscribe();

    private void OnDisable()
    {
        if (_isSubscribed && station != null) station.OnTrainStopped -= HandleTrainStopped;
        _isSubscribed = false;
    }

    private void Subscribe()
    {
        if (station == null || _isSubscribed) return;
        station.OnTrainStopped += HandleTrainStopped;
        _isSubscribed = true;
    }

    private void HandleTrainStopped() => Fire();

    private void Fire()
    {
        if (_hasFired) return;
        _hasFired = true;
        OnRunEnd?.Invoke();
    }

    private void FixedUpdate()
    {
        if (_hasFired || trackSpline == null) return;

        SplineUtility.GetNearestPoint(trackSpline.Spline, (float3)transform.position, out float3 _, out float t);
        Progress = t;

        // With a placed station, the station decides when the run is over.
        if ((station == null || !station.IsPlaced) && t >= endThreshold)
            Fire();
    }
}
