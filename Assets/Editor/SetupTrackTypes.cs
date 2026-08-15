using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Collections.Generic;
using System.Linq;

// One-shot editor utility: creates the Wood/Steel PhysicsMaterial2D + example
// TrackTypeDefinition assets, and wires TrackTypeApplier onto CoasterLineRender
// with every plain cart's CartTintReceiver assigned (power-up carts are
// deliberately excluded so their identity color isn't overwritten).
// Idempotent: reuses existing assets/components in place rather than duplicating.
public static class SetupTrackTypes
{
    private const string PhysicsFolder = "Assets/Physics";
    private const string TrackTypesFolder = "Assets/ScriptableObjects/TrackTypes";

    [MenuItem("NuttyFluffies/Setup Track Types")]
    public static void Execute()
    {
        EnsureFolder("Assets/ScriptableObjects");
        EnsureFolder(TrackTypesFolder);

        var woodMaterial = GetOrCreatePhysicsMaterial($"{PhysicsFolder}/WoodTrack.physicsMaterial2D", friction: 0.45f, bounciness: 0f);
        var steelMaterial = GetOrCreatePhysicsMaterial($"{PhysicsFolder}/SteelTrack.physicsMaterial2D", friction: 0.2f, bounciness: 0.05f);

        var wood = GetOrCreateTrackType($"{TrackTypesFolder}/Wood.asset", "Wood", woodMaterial,
            lineColor: new Color(0.55f, 0.35f, 0.17f), cartTint: Color.white);
        GetOrCreateTrackType($"{TrackTypesFolder}/Steel.asset", "Steel", steelMaterial,
            lineColor: new Color(0.68f, 0.7f, 0.72f), cartTint: Color.white);

        GameObject trackGO = GameObject.Find("CoasterLineRender");
        if (trackGO == null)
        {
            Debug.LogError("[SetupTrackTypes] Missing CoasterLineRender in scene.");
            return;
        }

        var applier = trackGO.GetComponent<TrackTypeApplier>();
        if (applier == null) applier = Undo.AddComponent<TrackTypeApplier>(trackGO);

        // Plain (non-power-up) carts get tinted by the track type; power-up
        // carts keep their own identity color, set directly by BuildCoasterTrain.
        // Checked by component presence rather than BuildCoasterTrain's static
        // LastBuiltPowerUpCarts list -- execute_script may run each file as its
        // own fresh compile, so static state isn't reliably shared across calls.
        var cartsToTint = new List<CartTintReceiver>();
        int excludedCount = 0;
        for (int i = 1; i <= 6; i++)
        {
            string name = i == 1 ? "Cart" : $"Cart ({i})";
            GameObject cartGO = GameObject.Find(name);
            if (cartGO == null) continue;
            if (cartGO.GetComponent<PowerUpCart>() != null) { excludedCount++; continue; } // skip power-up carts

            var body = cartGO.transform.Find("CartBody");
            var receiver = body != null ? body.GetComponent<CartTintReceiver>() : null;
            if (receiver != null) cartsToTint.Add(receiver);
        }

        var applierSerialized = new SerializedObject(applier);
        applierSerialized.FindProperty("trackType").objectReferenceValue = wood; // default to Wood; swap in Inspector for Steel
        var cartsProp = applierSerialized.FindProperty("cartsToTint");
        cartsProp.arraySize = cartsToTint.Count;
        for (int i = 0; i < cartsToTint.Count; i++)
            cartsProp.GetArrayElementAtIndex(i).objectReferenceValue = cartsToTint[i];
        applierSerialized.ApplyModifiedProperties();

        Debug.Log($"[SetupTrackTypes] Wired TrackTypeApplier on CoasterLineRender, default=Wood, tinting {cartsToTint.Count} plain cart(s), excluded {excludedCount} power-up cart(s).");

        EditorSceneManager.MarkSceneDirty(trackGO.scene);
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
        string leaf = System.IO.Path.GetFileName(path);
        AssetDatabase.CreateFolder(parent, leaf);
    }

    private static PhysicsMaterial2D GetOrCreatePhysicsMaterial(string path, float friction, float bounciness)
    {
        var existing = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(path);
        if (existing != null)
        {
            existing.friction = friction;
            existing.bounciness = bounciness;
            EditorUtility.SetDirty(existing);
            return existing;
        }
        var mat = new PhysicsMaterial2D { friction = friction, bounciness = bounciness };
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    private static TrackTypeDefinition GetOrCreateTrackType(string path, string displayName, PhysicsMaterial2D physicsMaterial, Color lineColor, Color cartTint)
    {
        var existing = AssetDatabase.LoadAssetAtPath<TrackTypeDefinition>(path);
        var def = existing != null ? existing : ScriptableObject.CreateInstance<TrackTypeDefinition>();
        def.displayName = displayName;
        def.trackPhysicsMaterial = physicsMaterial;
        def.lineColor = lineColor;
        def.cartTintColor = cartTint;
        if (existing == null) AssetDatabase.CreateAsset(def, path);
        else EditorUtility.SetDirty(def);
        return def;
    }
}
