using UnityEngine;

// Per-cart physics watcher that detects the five premium-tier stunts
// (TightTurn/Airtime/SpeedBurst/Inversion/NearMiss) purely from this cart's
// own Rigidbody2D state and its contact with the track -- no scripted
// triggers, matching how movement itself is already physics-only (see
// CoasterCart's opening comment). Lives on every cart (added by
// Assets/Editor/SetupStuntDetectors.cs), not just the lead cart: each
// passenger's stunt is judged by their own seat's physics, since the coupled
// train can differ slightly cart-to-cart on a curve or a crest.
//
// Fires a plain event rather than going through a broker like
// CoasterInputBroker/PowerUpInputBroker -- those exist to decouple a shared
// input source from many listeners across the train; here the only listener
// that ever matters is this same cart's own CreaturePassenger, so a direct
// per-cart event is the simpler fit.
//
// Threshold/cooldown/duration constants below are first-pass starting points,
// not measured from actual play -- expect to retune them once this can be
// played and watched (same as swipeSensitivity/maxSwipeForce were tuned by
// feel on CoasterCart).
[RequireComponent(typeof(Rigidbody2D))]
public class StuntDetector : MonoBehaviour
{
    private const string TrackTag = "Track";

    [Header("Speed Burst (Fox)")]
    [SerializeField] private float speedBurstThreshold = 12f;
    [SerializeField] private float speedBurstCooldown = 1.5f;

    [Header("Tight Turn (Squirrel)")]
    [Tooltip("Sustained yaw rate (deg/sec) while grounded that counts as hugging a tight curve.")]
    [SerializeField] private float tightTurnAngularVelocityThreshold = 220f;
    [SerializeField] private float tightTurnHoldSeconds = 0.25f;
    [SerializeField] private float tightTurnCooldown = 1.5f;

    [Header("Inversion (Griffin)")]
    [Tooltip("How close to exactly upside-down (180 deg) counts as 'inverted', in degrees either way.")]
    [SerializeField] private float invertedZoneDegrees = 45f;
    [Tooltip("Must rotate back to within this many degrees of upright (0 deg) before another inversion can fire.")]
    [SerializeField] private float uprightRearmDegrees = 45f;

    [Header("Airtime (Owl) / Near Miss (Dragon)")]
    [Tooltip("Ungrounded episodes at least this long land as Airtime (big air/drops).")]
    [SerializeField] private float airtimeThresholdSeconds = 0.6f;
    [Tooltip("Ungrounded episodes at least this long but shorter than Airtime land as NearMiss (a brief skim/close call). Shorter than this is just physics jitter and is ignored.")]
    [SerializeField] private float nearMissMinSeconds = 0.1f;

    public event System.Action<HeartActionType> OnStunt;

    private Rigidbody2D _rb;
    private int _trackContacts;
    private float _airborneTimer;
    private float _tightTurnHoldTimer;
    private bool _tightTurnCoolingDown;
    private float _tightTurnCooldownRemaining;
    private float _speedBurstCooldownRemaining;
    private bool _invertedArmed = true;

    private bool Grounded => _trackContacts > 0;

    private void Awake() => _rb = GetComponent<Rigidbody2D>();

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!collision.collider.CompareTag(TrackTag)) return;
        bool wasAirborne = !Grounded;
        _trackContacts++;
        if (wasAirborne) ResolveLanding();
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        if (!collision.collider.CompareTag(TrackTag)) return;
        _trackContacts = Mathf.Max(0, _trackContacts - 1);
    }

    private void ResolveLanding()
    {
        if (_airborneTimer >= airtimeThresholdSeconds) OnStunt?.Invoke(HeartActionType.Airtime);
        else if (_airborneTimer >= nearMissMinSeconds) OnStunt?.Invoke(HeartActionType.NearMiss);
        _airborneTimer = 0f;
    }

    private void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;

        if (!Grounded) _airborneTimer += dt;

        UpdateSpeedBurst(dt);
        UpdateTightTurn(dt);
        UpdateInversion();
    }

    private void UpdateSpeedBurst(float dt)
    {
        if (_speedBurstCooldownRemaining > 0f) _speedBurstCooldownRemaining -= dt;

        if (_speedBurstCooldownRemaining <= 0f && _rb.velocity.magnitude >= speedBurstThreshold)
        {
            _speedBurstCooldownRemaining = speedBurstCooldown;
            OnStunt?.Invoke(HeartActionType.SpeedBurst);
        }
    }

    private void UpdateTightTurn(float dt)
    {
        if (_tightTurnCooldownRemaining > 0f) _tightTurnCooldownRemaining -= dt;

        if (Grounded && Mathf.Abs(_rb.angularVelocity) >= tightTurnAngularVelocityThreshold)
        {
            _tightTurnHoldTimer += dt;
            if (_tightTurnHoldTimer >= tightTurnHoldSeconds && _tightTurnCooldownRemaining <= 0f)
            {
                _tightTurnHoldTimer = 0f;
                _tightTurnCooldownRemaining = tightTurnCooldown;
                OnStunt?.Invoke(HeartActionType.TightTurn);
            }
        }
        else
        {
            _tightTurnHoldTimer = 0f;
        }
    }

    // Edge-triggered so one loop fires one Inversion event: arms as soon as
    // the cart is upright, fires (and disarms) the moment it swings into the
    // inverted zone, then can't fire again until it swings back upright.
    private void UpdateInversion()
    {
        float rotation = _rb.rotation;
        float distFromInverted = Mathf.Abs(Mathf.DeltaAngle(rotation, 180f));
        float distFromUpright = Mathf.Abs(Mathf.DeltaAngle(rotation, 0f));

        if (_invertedArmed && distFromInverted <= invertedZoneDegrees)
        {
            _invertedArmed = false;
            OnStunt?.Invoke(HeartActionType.Inversion);
        }
        else if (!_invertedArmed && distFromUpright <= uprightRearmDegrees)
        {
            _invertedArmed = true;
        }
    }
}
