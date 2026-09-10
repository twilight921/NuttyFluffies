using UnityEngine;
using UnityEngine.Splines;

// Applies the Garage's saved power-up choice to the train's single special
// cart at scene load, the same shape as TrainLoadoutApplier does for the
// per-slot creature loadout. Lives on the root "LoadoutApplier" GameObject,
// added idempotently by Assets/Editor/SetupLoadoutApplier.cs.
//
// The train has exactly one PowerUpCart (built by BuildCoasterTrain on the
// slot-1 cart, "Cart (2)"). BuildCoasterTrain bakes Rocket onto it as a
// working default; this override runs only if the player picked something
// else in the Garage. If the player never visits the Garage, GarageSave
// returns that same default and this is a no-op in effect.
public class PowerUpLoadoutApplier : MonoBehaviour
{
    [Tooltip("The track's SplineContainer (CoasterLineRender). Needed so a Magnet pick can be wired at runtime. Optional -- falls back to a scene lookup.")]
    [SerializeField] private SplineContainer trackSpline;

    private void Awake()
    {
        var cart = FindPowerUpCart();
        if (cart == null)
        {
            Debug.LogWarning("[PowerUpLoadoutApplier] No PowerUpCart in the train -- nothing to apply.");
            return;
        }

        var spline = trackSpline != null
            ? trackSpline
            : GameObject.Find("CoasterLineRender")?.GetComponent<SplineContainer>();

        PowerUpType chosen = GarageSave.GetPowerUp();
        cart.SetPowerUp(chosen, spline);
        ApplyTint(cart.gameObject, chosen);
    }

    private static PowerUpCart FindPowerUpCart()
    {
#if UNITY_2023_1_OR_NEWER
        return Object.FindFirstObjectByType<PowerUpCart>();
#else
        return Object.FindObjectOfType<PowerUpCart>();
#endif
    }

    // Mirrors the placeholder tint BuildCoasterTrain applies to the CartBody
    // sprite, so switching the ability at runtime also switches its color.
    private static void ApplyTint(GameObject cartGO, PowerUpType type)
    {
        var body = cartGO.transform.Find("CartBody");
        var sr = body != null ? body.GetComponent<SpriteRenderer>() : null;
        if (sr != null && PowerUpCart.TintColors.TryGetValue(type, out var color))
            sr.color = color;
    }
}
