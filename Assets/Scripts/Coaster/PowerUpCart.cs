using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics;

// The train's single special carriage (rocket / jump-jet / magnet) that the
// player activates with a single button press via PowerUpInputBroker.
// Reusable on a cooldown, not a one-shot pickup -- this is a permanent car
// built into the train, not something collected and consumed.
//
// Which ability it carries is chosen in the Garage and applied at coaster-
// scene load by PowerUpLoadoutApplier via SetPowerUp(); BuildCoasterTrain
// only bakes a default (Rocket) so the cart works if the Garage is skipped.
//
// All effects are real Rigidbody2D forces/impulses applied to this cart's own
// body; the existing HingeJoint2D chain already propagates a push from any
// one cart to the whole train (that's how the lead cart's swipe already
// moves all 6 cars), so no train-wide broadcast is needed here.
[RequireComponent(typeof(Rigidbody2D))]
public class PowerUpCart : MonoBehaviour
{
    [Header("Identity")]
    [SerializeField] private PowerUpType powerUpType;

    [Header("Activation")]
    [SerializeField] private PowerUpInputBroker activationBroker;
    [SerializeField] private float cooldownSeconds = 8f;

    [Header("Rocket")]
    [SerializeField] private float rocketForce = 400000f;
    [SerializeField] private float rocketDuration = 1.5f;

    [Header("Jump Jet")]
    [SerializeField] private float jumpJetImpulse = 250000f;

    [Header("Magnet")]
    [Tooltip("The track's SplineContainer (CoasterLineRender). Only needed for the Magnet type.")]
    [SerializeField] private SplineContainer trackSpline;
    [SerializeField] private float magnetDuration = 2.5f;
    [SerializeField] private float magnetCorrectiveForce = 150000f;

    private Rigidbody2D _rb;
    private bool _isSubscribed;
    private float _cooldownRemaining;
    private float _rocketTimeRemaining;
    private float _magnetTimeRemaining;

    // Placeholder identity tint per ability (no real art yet). Shared source of
    // truth: BuildCoasterTrain bakes it at build time, PowerUpLoadoutApplier
    // re-applies it whenever the Garage choice changes the type at runtime.
    public static readonly IReadOnlyDictionary<PowerUpType, Color> TintColors = new Dictionary<PowerUpType, Color>
    {
        { PowerUpType.Rocket, new Color(0.85f, 0.15f, 0.1f) },
        { PowerUpType.JumpJet, new Color(0.15f, 0.4f, 0.9f) },
        { PowerUpType.Magnet, new Color(0.55f, 0.15f, 0.75f) },
    };

    private void Awake() => _rb = GetComponent<Rigidbody2D>();

    // Runtime loadout swap: PowerUpLoadoutApplier calls this at scene load with
    // the ability chosen in the Garage. The track spline is only needed for the
    // Magnet type -- pass it whenever it's known, ignored otherwise. Any
    // in-flight effect and the cooldown are cleared so the new ability starts
    // clean.
    public void SetPowerUp(PowerUpType type, SplineContainer track = null)
    {
        powerUpType = type;
        if (track != null) trackSpline = track;
        _rocketTimeRemaining = 0f;
        _magnetTimeRemaining = 0f;
        _cooldownRemaining = 0f;
    }

    private void OnEnable()
    {
        if (activationBroker != null && !_isSubscribed)
        {
            activationBroker.OnActivatePressed += TryActivate;
            _isSubscribed = true;
        }
    }

    private void OnDisable()
    {
        if (_isSubscribed && activationBroker != null)
        {
            activationBroker.OnActivatePressed -= TryActivate;
            _isSubscribed = false;
        }
    }

    private void TryActivate()
    {
        if (_cooldownRemaining > 0f) return;
        _cooldownRemaining = cooldownSeconds;

        switch (powerUpType)
        {
            case PowerUpType.Rocket:
                _rocketTimeRemaining = rocketDuration;
                break;
            case PowerUpType.JumpJet:
                _rb.AddForce(Vector2.up * jumpJetImpulse, ForceMode2D.Impulse);
                break;
            case PowerUpType.Magnet:
                _magnetTimeRemaining = magnetDuration;
                break;
        }
    }

    private void FixedUpdate()
    {
        if (_cooldownRemaining > 0f) _cooldownRemaining -= Time.fixedDeltaTime;

        if (_rocketTimeRemaining > 0f)
        {
            _rb.AddForce((Vector2)transform.right * rocketForce);
            _rocketTimeRemaining -= Time.fixedDeltaTime;
        }

        if (_magnetTimeRemaining > 0f)
        {
            ApplyMagnetCorrection();
            _magnetTimeRemaining -= Time.fixedDeltaTime;
        }
    }

    private void ApplyMagnetCorrection()
    {
        if (trackSpline == null) return;

        // SplineContainer.EvaluatePosition/EvaluateTangent (and by extension
        // GetNearestPoint here) return world-space values directly in this
        // project's package version -- confirmed while building the train.
        SplineUtility.GetNearestPoint(trackSpline.Spline, (float3)transform.position, out float3 nearest, out float t);
        Vector2 toTrack = (Vector2)(Vector3)nearest - (Vector2)transform.position;
        if (toTrack.sqrMagnitude > 0.0001f)
            _rb.AddForce(toTrack.normalized * magnetCorrectiveForce);
    }
}
