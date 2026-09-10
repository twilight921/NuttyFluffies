using UnityEngine;

// Purely decorative RectTransform oscillator -- sine-wave bob on local Y,
// used to give Main Menu elements (the cart, the title) some life. This is a
// menu-only cosmetic flourish; it has nothing to do with the project's
// Rigidbody2D-only rule for actual coaster movement.
[RequireComponent(typeof(RectTransform))]
public class FloatBob : MonoBehaviour
{
    [SerializeField] private float amplitude = 12f;
    [SerializeField] private float speed = 1.5f;

    private RectTransform _rect;
    private Vector2 _basePos;

    private void Awake()
    {
        _rect = (RectTransform)transform;
        _basePos = _rect.anchoredPosition;
    }

    private void Update()
    {
        float offset = Mathf.Sin(Time.unscaledTime * speed) * amplitude;
        _rect.anchoredPosition = _basePos + new Vector2(0f, offset);
    }
}
