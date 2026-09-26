using UnityEngine;
using UnityEngine.Splines;

// The station the train rolls into at the end of a level. Scaffolding for the
// end-of-level stop: LevelLoader calls Place() after it (re)builds the track,
// which snaps this object to the spline's far end, lays out a brake zone over
// the final `platformLength` units of rail, and a solid buffer stop just past
// the last knot so the train can never roll off the end of the rail.
//
// Nothing scripts the cart's path -- the train is still fully physics-driven.
// Inside the brake zone every car simply has its speed bled off at a constant
// deceleration (a station "brake"). Once the lead car has been effectively
// still for `settleSeconds`, OnTrainStopped fires exactly once; RunEndTrigger
// forwards that as OnRunEnd (when a station is wired), so the Results screen
// appears when the train has actually pulled in and stopped.
//
// Art: drop the generated station sprite into `stationSprite` and nudge
// `visualOffset` / `visualScale` until it lines up. Until then a flat
// placeholder slab (tinted `placeholderColor`) marks the platform.
//
// The child renderer / buffer collider are created on demand, so the only
// scene setup needed is a GameObject with this component (see
// 'NuttyFluffies/Setup Station').
public class Station : MonoBehaviour
{
    [Header("Layout")]
    [Tooltip("Length of the braking platform, measured back from the end of the rail.")]
    [SerializeField] private float platformLength = 6f;
    [Tooltip("Height of the brake zone / buffer stop above the rail, so raised or bouncing cars are still caught.")]
    [SerializeField] private float zoneHeight = 4f;

    [Header("Stopping")]
    [Tooltip("Constant deceleration (units/s^2) applied to cars while they are on the platform.")]
    [SerializeField] private float brakeDeceleration = 8f;
    [Tooltip("Lead car speed (units/s) below which the train counts as stopped.")]
    [SerializeField] private float stopSpeed = 0.15f;
    [Tooltip("How long the lead car must stay below Stop Speed before the train counts as stopped.")]
    [SerializeField] private float settleSeconds = 0.5f;

    [Header("Visual")]
    [Tooltip("Station art. Leave empty to show a flat placeholder slab.")]
    [SerializeField] private Sprite stationSprite;
    [Tooltip("Where the sprite's pivot sits relative to the end of the rail (in the rail's local frame: X along the track, Y up).")]
    [SerializeField] private Vector2 visualOffset = Vector2.zero;
    [SerializeField] private float visualScale = 1f;
    [SerializeField] private string sortingLayerName = "Default";
    [SerializeField] private int sortingOrder = -1;
    [SerializeField] private Color placeholderColor = new Color(0.45f, 0.35f, 0.28f, 0.9f);

    // Fires once, when the lead car has come to rest on the platform.
    public event System.Action OnTrainStopped;

    public bool HasStopped { get; private set; }

    // True once LevelLoader has laid the station out on a track.
    public bool IsPlaced => _placed;

    private Rigidbody2D[] _bodies = new Rigidbody2D[0];
    private Vector2 _platformStart;
    private Vector2 _forward = Vector2.right;
    private float _settleTimer;
    private bool _placed;

    private SpriteRenderer _renderer;
    private BoxCollider2D _buffer;

    private static Sprite _placeholderSprite;

    // Called by LevelLoader after the track is rebuilt. `carts` is the train,
    // lead car first.
    public void Place(SplineContainer track, Transform[] carts)
    {
        _bodies = CollectBodies(carts);
        HasStopped = false;
        _settleTimer = 0f;
        _placed = false;
        if (track == null) return;

        // Spline evaluation is already world space in this project's package
        // version (see RunEndTrigger / LevelLoader) -- no extra transform.
        Vector3 end = track.EvaluatePosition(1f);
        Vector3 tangent = ((Vector3)track.EvaluateTangent(1f)).normalized;
        if (tangent.sqrMagnitude < 0.0001f) tangent = Vector3.right;

        _forward = tangent;
        _platformStart = (Vector2)end - _forward * platformLength;

        float angle = Mathf.Atan2(tangent.y, tangent.x) * Mathf.Rad2Deg;
        transform.SetPositionAndRotation(end, Quaternion.Euler(0f, 0f, angle));

        EnsureParts();
        ApplyLayout();
        _placed = true;
    }

    private static Rigidbody2D[] CollectBodies(Transform[] carts)
    {
        if (carts == null) return new Rigidbody2D[0];
        var list = new System.Collections.Generic.List<Rigidbody2D>(carts.Length);
        foreach (Transform cart in carts)
        {
            if (cart == null) continue;
            var rb = cart.GetComponent<Rigidbody2D>();
            if (rb != null) list.Add(rb);
        }
        return list.ToArray();
    }

    private void FixedUpdate()
    {
        if (!_placed || HasStopped || _bodies.Length == 0) return;

        float dt = Time.fixedDeltaTime;
        foreach (Rigidbody2D rb in _bodies)
        {
            if (rb == null) continue;
            if (Vector2.Dot(rb.position - _platformStart, _forward) < 0f) continue; // not on the platform yet
            rb.linearVelocity = Vector2.MoveTowards(rb.linearVelocity, Vector2.zero, brakeDeceleration * dt);
        }

        // "Stopped" is judged on the lead car, and only once it is on the platform.
        Rigidbody2D lead = _bodies[0];
        bool leadOnPlatform = lead != null && Vector2.Dot(lead.position - _platformStart, _forward) >= 0f;
        if (leadOnPlatform && lead.linearVelocity.magnitude <= stopSpeed)
            _settleTimer += dt;
        else
            _settleTimer = 0f;

        if (_settleTimer >= settleSeconds)
        {
            HasStopped = true;
            OnTrainStopped?.Invoke();
        }
    }

    // --- Scaffolding: child visual + buffer stop -----------------------------

    private void EnsureParts()
    {
        if (_renderer == null)
        {
            Transform visual = transform.Find("StationVisual");
            if (visual == null)
            {
                visual = new GameObject("StationVisual").transform;
                visual.SetParent(transform, false);
            }
            _renderer = visual.GetComponent<SpriteRenderer>();
            if (_renderer == null) _renderer = visual.gameObject.AddComponent<SpriteRenderer>();
        }

        if (_buffer == null)
        {
            Transform stop = transform.Find("BufferStop");
            if (stop == null)
            {
                stop = new GameObject("BufferStop").transform;
                stop.SetParent(transform, false);
            }
            _buffer = stop.GetComponent<BoxCollider2D>();
            if (_buffer == null) _buffer = stop.gameObject.AddComponent<BoxCollider2D>();
        }
    }

    private void ApplyLayout()
    {
        // Solid wall just past the last knot, tall enough to catch a bouncing car.
        const float bufferThickness = 0.5f;
        _buffer.size = new Vector2(bufferThickness, zoneHeight);
        _buffer.transform.localPosition = new Vector3(bufferThickness / 2f, zoneHeight / 2f, 0f);
        _buffer.transform.localRotation = Quaternion.identity;

        _renderer.sortingLayerName = sortingLayerName;
        _renderer.sortingOrder = sortingOrder;

        Transform visual = _renderer.transform;
        if (stationSprite != null)
        {
            _renderer.sprite = stationSprite;
            _renderer.color = Color.white;
            visual.localPosition = new Vector3(visualOffset.x, visualOffset.y, 0f);
            visual.localScale = Vector3.one * visualScale;
        }
        else
        {
            // Placeholder: a slab spanning the platform, just under the rail.
            const float slabHeight = 0.4f;
            _renderer.sprite = GetPlaceholderSprite();
            _renderer.color = placeholderColor;
            visual.localPosition = new Vector3(-platformLength / 2f, -slabHeight / 2f, 0f);
            visual.localScale = new Vector3(platformLength, slabHeight, 1f);
        }
    }

    private static Sprite GetPlaceholderSprite()
    {
        if (_placeholderSprite == null)
        {
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            _placeholderSprite = Sprite.Create(tex, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
            _placeholderSprite.name = "StationPlaceholder";
        }
        return _placeholderSprite;
    }

    private void OnDrawGizmosSelected()
    {
        // Brake zone preview in the editor (only meaningful once Place() has run).
        if (!_placed) return;
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(_platformStart, transform.position);
        Gizmos.DrawWireSphere(_platformStart, 0.3f);
    }
}
