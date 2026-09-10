using UnityEngine;

// Movement is fully physics-driven now: gravity, track contact, friction,
// detaching on a hill/curve, and landing all come from the real Rigidbody2D
// + the track's EdgeCollider2D (baked from the spline) instead of being
// scripted. This class's only job is turning swipe input into a force.
[RequireComponent(typeof(Rigidbody2D))]
public class CoasterCart : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private CoasterInputBroker inputBroker;

    [Header("Input Push Configuration")]
    // Tuned so a genuinely fast flick (~3000px/s) approaches maxSwipeForce
    // while normal, slower drags stay well below it and scale proportionally.
    [SerializeField] private float swipeSensitivity = 15f;
    [SerializeField] private float maxSwipeForce = 150000f;

    private Rigidbody2D _rb;
    private float _playerInputForce;
    private bool _isSubscribed;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
    }

    private void Start()
    {
        if (inputBroker == null)
        {
            Debug.LogError($"[CoasterCart] Input Broker slot is empty on {gameObject.name}!");
            return;
        }

        if (!_isSubscribed)
        {
            inputBroker.OnSwipePerformed += AccumulatePlayerForce;
            _isSubscribed = true;
        }
    }

    private void OnDestroy()
    {
        if (_isSubscribed && inputBroker != null)
        {
            inputBroker.OnSwipePerformed -= AccumulatePlayerForce;
        }
    }

    private void AccumulatePlayerForce(float swipeVelocity)
    {
        _playerInputForce = swipeVelocity * swipeSensitivity;
    }

    private void FixedUpdate()
    {
        float appliedForce = Mathf.Clamp(_playerInputForce, -maxSwipeForce, maxSwipeForce);
        if (Mathf.Abs(appliedForce) > 0.01f)
        {
            // Push along the cart's current facing rather than a fixed world
            // direction. The cart's rotation tracks the slope it's resting
            // on via real contact, so this keeps a swipe close to fully
            // "along the rail" instead of losing more and more of its push
            // to the incline (and into the track as extra normal force/
            // friction) the steeper a hill gets.
            _rb.AddForce((Vector2)transform.right * appliedForce);
        }

        // Decay immediately so it functions as a single-tick touch impulse.
        _playerInputForce = 0f;
    }
}
