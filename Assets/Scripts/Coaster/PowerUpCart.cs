using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics;

// A special carriage in the train (rocket / jump-jet / magnet) that the
// player activates with a single button press via PowerUpInputBroker.
// Reusable on a cooldown, not a one-shot pickup -- these are permanent cars
// built into the train, not something collected and consumed.
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

    private void Awake() => _rb = GetComponent<Rigidbody2D>();

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
