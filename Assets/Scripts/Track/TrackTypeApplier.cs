using UnityEngine;

// Pushes a TrackTypeDefinition's physics/visual settings onto the track this
// sits on. Separate from SplineLineSampler on purpose: that script re-samples
// geometry every frame (ExecuteAlways, per-frame job); this is a one-shot
// config push that only needs to re-run when the assigned definition changes,
// so it stays out of the hot per-frame path.
[ExecuteAlways]
[RequireComponent(typeof(LineRenderer))]
[RequireComponent(typeof(EdgeCollider2D))]
public class TrackTypeApplier : MonoBehaviour
{
    [SerializeField] private TrackTypeDefinition trackType;
    [Tooltip("Cart bodies to tint with this track type's cartTintColor. Power-up carts should be left out so their own placeholder color isn't overwritten.")]
    [SerializeField] private CartTintReceiver[] cartsToTint;

    private LineRenderer _lineRenderer;
    private EdgeCollider2D _edgeCollider;

    private void Awake()
    {
        _lineRenderer = GetComponent<LineRenderer>();
        _edgeCollider = GetComponent<EdgeCollider2D>();
        Apply();
    }

    // Lets a designer see the swap live in edit mode when reassigning the SO.
    private void OnValidate() => Apply();

    private void Apply()
    {
        if (trackType == null) return;
        if (_lineRenderer == null) _lineRenderer = GetComponent<LineRenderer>();
        if (_edgeCollider == null) _edgeCollider = GetComponent<EdgeCollider2D>();

        if (_edgeCollider != null) _edgeCollider.sharedMaterial = trackType.trackPhysicsMaterial;

        if (_lineRenderer != null)
        {
            if (trackType.lineMaterial != null) _lineRenderer.material = trackType.lineMaterial;
            _lineRenderer.startColor = trackType.lineColor;
            _lineRenderer.endColor = trackType.lineColor;
        }

        if (cartsToTint != null)
        {
            foreach (var cart in cartsToTint)
            {
                if (cart != null) cart.ApplyTint(trackType.cartTintColor);
            }
        }
    }
}
