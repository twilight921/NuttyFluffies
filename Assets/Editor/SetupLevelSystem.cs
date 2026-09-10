using UnityEngine;
using UnityEngine.Splines;
using UnityEditor;
using UnityEditor.SceneManagement;

// One-shot editor utility: wires the runtime level system into the open
// coaster scene. Creates a LevelLoader GameObject and fills in every scene
// reference it needs (track spline + rail collider, track-type applier,
// run-end trigger, Heart/Coin prefabs, the 6 train cars). Ensures a
// LevelCatalog exists first (runs Generate Level Batch if it's missing).
//
// Must run with the coaster scene (SampleScene) open/active. Idempotent:
// reuses the LevelLoader GameObject in place and just re-stamps its wiring.
public static class SetupLevelSystem
{
    private const string CatalogPath = "Assets/ScriptableObjects/Levels/LevelCatalog.asset";
    private const string HeartPrefabPath = "Assets/MyPrefabs/Heart.prefab";
    private const string CoinPrefabPath = "Assets/MyPrefabs/Coin.prefab";

    [MenuItem("NuttyFluffies/Setup Level System")]
    public static void Execute()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>(CatalogPath);
        if (catalog == null)
        {
            Debug.Log("[SetupLevelSystem] No LevelCatalog -- running Generate Level Batch first.");
            GenerateLevelBatch.Execute();
            catalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>(CatalogPath);
        }
        if (catalog == null)
        {
            Debug.LogError("[SetupLevelSystem] Still no LevelCatalog after Generate Level Batch -- aborting.");
            return;
        }

        GameObject trackGO = GameObject.Find("CoasterLineRender");
        if (trackGO == null)
        {
            Debug.LogError("[SetupLevelSystem] No CoasterLineRender in the open scene -- open the coaster scene (SampleScene) first.");
            return;
        }

        var container = trackGO.GetComponent<SplineContainer>();
        var edge = trackGO.GetComponent<EdgeCollider2D>();
        var line = trackGO.GetComponent<LineRenderer>();
        var applier = trackGO.GetComponent<TrackTypeApplier>();
        if (container == null || edge == null)
        {
            Debug.LogError("[SetupLevelSystem] CoasterLineRender needs a SplineContainer and an EdgeCollider2D.");
            return;
        }
        if (line == null)
            Debug.LogWarning("[SetupLevelSystem] CoasterLineRender has no LineRenderer -- the visible rail won't be rebaked per level.");

        var runEnd = Object.FindObjectOfType<RunEndTrigger>();
        if (runEnd == null)
            Debug.LogWarning("[SetupLevelSystem] No RunEndTrigger in scene -- LevelLoader won't re-point it (run 'NuttyFluffies/Setup Results UI' to add one).");

        var heartPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(HeartPrefabPath);
        var coinPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CoinPrefabPath);
        var heartPickup = heartPrefab != null ? heartPrefab.GetComponent<Pickup>() : null;
        var coinPickup = coinPrefab != null ? coinPrefab.GetComponent<Pickup>() : null;
        if (heartPickup == null || coinPickup == null)
            Debug.LogWarning("[SetupLevelSystem] Heart/Coin prefabs not found -- run 'NuttyFluffies/Setup Pickups' first so LevelLoader can scatter pickups.");

        var carts = CollectTrainCarts();
        if (carts.Length == 0)
            Debug.LogWarning("[SetupLevelSystem] No 'Cart' GameObjects found -- run 'NuttyFluffies/Build Coaster Train' first so LevelLoader can place the train.");

        GameObject loaderGO = GameObject.Find("LevelLoader");
        if (loaderGO == null)
        {
            loaderGO = new GameObject("LevelLoader");
            Undo.RegisterCreatedObjectUndo(loaderGO, "Create LevelLoader");
        }
        var loader = loaderGO.GetComponent<LevelLoader>();
        if (loader == null) loader = Undo.AddComponent<LevelLoader>(loaderGO);

        var so = new SerializedObject(loader);
        so.FindProperty("catalog").objectReferenceValue = catalog;
        so.FindProperty("fallbackLevel").objectReferenceValue = catalog.Count > 0 ? catalog.levels[0] : null;
        so.FindProperty("track").objectReferenceValue = container;
        so.FindProperty("trackCollider").objectReferenceValue = edge;
        so.FindProperty("trackLine").objectReferenceValue = line;
        so.FindProperty("trackTypeApplier").objectReferenceValue = applier;
        so.FindProperty("runEndTrigger").objectReferenceValue = runEnd;
        so.FindProperty("heartPrefab").objectReferenceValue = heartPickup;
        so.FindProperty("coinPrefab").objectReferenceValue = coinPickup;

        var cartsProp = so.FindProperty("trainCarts");
        cartsProp.arraySize = carts.Length;
        for (int i = 0; i < carts.Length; i++)
            cartsProp.GetArrayElementAtIndex(i).objectReferenceValue = carts[i];

        so.ApplyModifiedProperties();

        EditorSceneManager.MarkSceneDirty(loaderGO.scene);
        Debug.Log($"[SetupLevelSystem] LevelLoader wired: catalog={catalog.Count} levels, {carts.Length} train car(s). "
            + "Save the scene, then use 'NuttyFluffies/Setup Level Select UI' on the Garage scene to add the picker.");
    }

    private static Transform[] CollectTrainCarts()
    {
        var list = new System.Collections.Generic.List<Transform>();
        for (int i = 1; i <= 6; i++)
        {
            string name = i == 1 ? "Cart" : $"Cart ({i})";
            GameObject go = GameObject.Find(name);
            if (go != null) list.Add(go.transform);
        }
        return list.ToArray();
    }
}
