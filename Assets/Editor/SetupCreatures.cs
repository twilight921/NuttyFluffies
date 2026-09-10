using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;

// One-shot editor utility: gives every cart in the scene a "Passenger" child
// riding the starter creature (Squirrel, the lowest tier).
//
// The original base roster (Mouse/Cat/Dog/Pig/Elephant) had no real per-
// character art -- every one of them was just a generated placeholder circle
// distinguished by tint -- so it was removed. Squirrel, which does have real
// chibi art (Assets/Art/Creatures/Premium/squirrel.png), is now the free
// starter every cart rides until the player swaps in an unlocked creature via
// the Garage.
//
// Squirrel's CreatureDefinition asset itself is created by
// SetupPremiumCreatures.cs -- this script only wires it onto the carts.
// Idempotent: reuses existing child objects/components in place rather than
// duplicating.
public static class SetupCreatures
{
    private const string StarterCreaturePath = "Assets/ScriptableObjects/Creatures/Squirrel.asset";
    private const int CartCount = 6;

    // Local offset from the cart's own CartBody, and scale relative to it, so
    // the rider reads as a small head/torso poking up out of the seat. Assumes
    // the creature sprite is ~1 world unit tall (WirePremiumCreatureSprites
    // normalizes every premium PNG's import PPU to guarantee that).
    private static readonly Vector3 PassengerLocalPosition = new Vector3(0f, 0.35f, -0.05f);
    private static readonly Vector3 PassengerLocalScale = Vector3.one * 0.5f;

    [MenuItem("NuttyFluffies/Setup Creatures")]
    public static void Execute()
    {
        var starter = AssetDatabase.LoadAssetAtPath<CreatureDefinition>(StarterCreaturePath);
        if (starter == null)
        {
            Debug.LogError($"[SetupCreatures] Missing {StarterCreaturePath} -- run NuttyFluffies/Setup Premium Creatures first.");
            return;
        }

        int wired = 0;
        Scene activeScene = default;
        for (int i = 1; i <= CartCount; i++)
        {
            string name = i == 1 ? "Cart" : $"Cart ({i})";
            GameObject cartGO = GameObject.Find(name);
            if (cartGO == null) continue;

            WirePassenger(cartGO, starter);
            wired++;
            activeScene = cartGO.scene;
        }

        Debug.Log($"[SetupCreatures] Wired starter passenger ({starter.displayName}) on {wired} cart(s).");

        if (activeScene.IsValid()) EditorSceneManager.MarkSceneDirty(activeScene);
    }

    private static void WirePassenger(GameObject cartGO, CreatureDefinition creature)
    {
        Transform passenger = cartGO.transform.Find("Passenger");
        GameObject passengerGO;
        if (passenger == null)
        {
            passengerGO = new GameObject("Passenger", typeof(SpriteRenderer));
            Undo.RegisterCreatedObjectUndo(passengerGO, "Create passenger");
            passengerGO.transform.SetParent(cartGO.transform, false);
            passengerGO.transform.localPosition = PassengerLocalPosition;
            passengerGO.transform.localScale = PassengerLocalScale;
        }
        else
        {
            passengerGO = passenger.gameObject;
        }

        // Set directly rather than relying on CreaturePassenger.OnValidate to
        // pick up the change -- OnValidate fires on the component's own data
        // changing, not on a referenced ScriptableObject's fields changing.
        var sr = passengerGO.GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            sr.sortingOrder = 1; // draw above CartBody
            if (creature.passengerSprite != null) sr.sprite = creature.passengerSprite;
            sr.color = creature.passengerTintColor;
        }

        var passengerComponent = passengerGO.GetComponent<CreaturePassenger>();
        if (passengerComponent == null) passengerComponent = Undo.AddComponent<CreaturePassenger>(passengerGO);

        var serialized = new SerializedObject(passengerComponent);
        serialized.FindProperty("creature").objectReferenceValue = creature;
        serialized.ApplyModifiedProperties();
    }
}
