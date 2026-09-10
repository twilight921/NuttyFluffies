using UnityEngine;

// A one-shot collectible sitting near the track -- Heart for score, Coin for
// currency. Hand-placed near the spline (risky/off-rail for Hearts, along the
// easy line for Coins), same authoring style as the track/train today.
//
// Detection is trigger-only and asks nothing of the cart: this collider is
// isTrigger, and it's the cart's own (non-trigger) BoxCollider2D + Rigidbody2D
// that makes 2D physics fire OnTriggerEnter2D. Filtered by the "Cart" tag
// (set on every train car) rather than just "any Rigidbody2D", so a pickup
// only ever responds to the train itself.
[RequireComponent(typeof(Collider2D))]
public class Pickup : MonoBehaviour
{
    [SerializeField] private PickupType pickupType;
    [SerializeField] private int value = 1;
    [Tooltip("Optional. Spawned at this pickup's position when collected.")]
    [SerializeField] private GameObject collectedVfx;

    private bool _collected;

    private void Reset()
    {
        var col = GetComponent<Collider2D>();
        if (col != null) col.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (_collected || !other.CompareTag("Cart")) return;

        _collected = true;
        RunStats.Instance?.AddPickup(pickupType, value);
        if (collectedVfx != null) Instantiate(collectedVfx, transform.position, Quaternion.identity);
        GetComponent<SfxOneShot>()?.Play();
        gameObject.SetActive(false);
    }
}
