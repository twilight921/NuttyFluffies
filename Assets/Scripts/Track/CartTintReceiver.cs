using UnityEngine;

// Marker/apply component for a cart's visible body, so TrackTypeApplier can
// tint carts without knowing anything about cart internals. Placed on the
// CartBody child (the one holding the SpriteRenderer), not the cart root.
[RequireComponent(typeof(SpriteRenderer))]
public class CartTintReceiver : MonoBehaviour
{
    private SpriteRenderer _spriteRenderer;

    private void Awake()
    {
        _spriteRenderer = GetComponent<SpriteRenderer>();
    }

    public void ApplyTint(Color color)
    {
        if (_spriteRenderer == null) _spriteRenderer = GetComponent<SpriteRenderer>();
        _spriteRenderer.color = color;
    }
}
