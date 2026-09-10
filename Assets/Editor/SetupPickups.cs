using System.IO;
using UnityEngine;
using UnityEngine.Splines;
using UnityEditor;
using UnityEditor.SceneManagement;

// One-shot editor utility: generates the placeholder pickup sprite, builds
// the Heart/Coin prefabs, scatters them along the track spline (Hearts
// floated off the rail in "risky" spots for score, Coins along the easy line
// for currency), and makes sure a RunStats singleton exists in the scene to
// tally them. Positions come from walking the spline by t like
// BuildCoasterTrain's arc-length sampling, so this counts as the "hand-placed
// near the spline" authoring style, just placed by tool instead of by hand.
//
// No pickup art exists yet, so both types reuse one generated placeholder
// circle sprite, distinguished by tint/scale -- same "placeholder tints" idea
// TrackTypeDefinition and SetupCreatures already use.
// Idempotent: previous run's generated Pickups container is torn down and
// rebuilt fresh; the placeholder sprite and prefabs are reused in place.
public static class SetupPickups
{
    private const string PlaceholderSpritePath = "Assets/Art/Pickups/pickup_placeholder.png";
    private const string PrefabFolder = "Assets/MyPrefabs";
    private const int SampleCount = 800;

    private const int HeartCount = 8;
    private const int CoinCount = 18;
    private const float HeartMinOffset = 1.2f;
    private const float HeartMaxOffset = 2.6f;
    private const float CoinOffset = 0.55f;
    // Keep clear of the very start/end of the track (train spawn / no more rail).
    private const float TMargin = 0.04f;

    [MenuItem("NuttyFluffies/Setup Pickups")]
    public static void Execute()
    {
        Run(HeartCount, CoinCount, HeartMinOffset, HeartMaxOffset, CoinOffset);
    }

    // Parameterized entry point so LevelBuilder can scatter per-level pickup
    // counts/offsets from a LevelDefinition. Execute() calls this with the
    // module defaults for the standalone menu item. Also (re)creates the
    // Heart/Coin prefabs and placeholder sprite if they're missing.
    public static void Run(int heartCount, int coinCount, float heartMinOffset, float heartMaxOffset, float coinOffset)
    {
        EnsurePlaceholderSprite();
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(PlaceholderSpritePath);

        var heartPrefab = GetOrCreatePickupPrefab("Heart.prefab", PickupType.Heart, value: 1, sprite,
            tint: new Color(0.95f, 0.25f, 0.4f), scale: 0.4f);
        var coinPrefab = GetOrCreatePickupPrefab("Coin.prefab", PickupType.Coin, value: 1, sprite,
            tint: new Color(1f, 0.82f, 0.15f), scale: 0.28f);

        GameObject trackGO = GameObject.Find("CoasterLineRender");
        if (trackGO == null)
        {
            Debug.LogError("[SetupPickups] Missing CoasterLineRender in scene.");
            return;
        }
        var container = trackGO.GetComponent<SplineContainer>();
        if (container == null)
        {
            Debug.LogError("[SetupPickups] CoasterLineRender has no SplineContainer.");
            return;
        }

        // --- Clean up any previous run first. ---
        GameObject existingRoot = GameObject.Find("Pickups");
        if (existingRoot != null) Object.DestroyImmediate(existingRoot);
        GameObject pickupsRoot = new GameObject("Pickups");
        Undo.RegisterCreatedObjectUndo(pickupsRoot, "Create pickups root");

        int heartsPlaced = ScatterAlongSpline(container, heartPrefab, "Heart", heartCount, pickupsRoot.transform,
            i => heartMinOffset + (i % 3) * ((heartMaxOffset - heartMinOffset) / 2f));
        int coinsPlaced = ScatterAlongSpline(container, coinPrefab, "Coin", coinCount, pickupsRoot.transform,
            i => coinOffset);

        // --- Make sure a RunStats singleton exists to tally what gets collected. ---
        GameObject statsGO = GameObject.Find("RunStats");
        if (statsGO == null)
        {
            statsGO = new GameObject("RunStats", typeof(RunStats));
            Undo.RegisterCreatedObjectUndo(statsGO, "Create RunStats");
        }
        else if (statsGO.GetComponent<RunStats>() == null)
        {
            Undo.AddComponent<RunStats>(statsGO);
        }

        Debug.Log($"[SetupPickups] Placed {heartsPlaced} Heart(s) and {coinsPlaced} Coin(s) along the track; RunStats present.");

        EditorSceneManager.MarkSceneDirty(trackGO.scene);
    }

    // Evenly spaces `count` instances across the spline's t range (skipping a
    // small margin at each end) and offsets each perpendicular to the rail by
    // however far `offsetForIndex` says. The offset always leans toward
    // whichever perpendicular direction is closer to world-up: the cart rides
    // an EdgeCollider2D baked from this same spline, so the "below the rail"
    // side is solid track/ground the cart can never reach. Variety against a
    // dead-straight line comes from jittering fore/aft along the tangent
    // instead of ever flipping to the unreachable side.
    private static int ScatterAlongSpline(SplineContainer container, GameObject prefab, string label, int count, Transform parent, System.Func<int, float> offsetForIndex)
    {
        if (prefab == null || count <= 0) return 0;

        int placed = 0;
        for (int i = 0; i < count; i++)
        {
            float t = Mathf.Lerp(TMargin, 1f - TMargin, count == 1 ? 0f : (float)i / (count - 1));
            Vector3 splineWorld = container.EvaluatePosition(t);
            Vector3 tangent = ((Vector3)container.EvaluateTangent(t)).normalized;
            Vector3 normal = new Vector3(-tangent.y, tangent.x, 0f);
            if (normal.y < 0f) normal = -normal; // stay on the reachable, above-rail side
            float along = (i % 2 == 0) ? 1f : -1f;
            Vector3 pos = splineWorld + normal * offsetForIndex(i) + tangent * (along * 0.3f);

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            instance.name = $"{label} ({i + 1})";
            instance.transform.position = pos;
            Undo.RegisterCreatedObjectUndo(instance, $"Place {label}");
            placed++;
        }
        return placed;
    }

    private static GameObject GetOrCreatePickupPrefab(string fileName, PickupType type, int value, Sprite sprite, Color tint, float scale)
    {
        string path = $"{PrefabFolder}/{fileName}";
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null) return existing;

        EnsureFolder(PrefabFolder);

        var go = new GameObject(Path.GetFileNameWithoutExtension(fileName), typeof(SpriteRenderer), typeof(CircleCollider2D), typeof(Pickup));
        var sr = go.GetComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = tint;

        var col = go.GetComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = 0.5f;

        go.transform.localScale = Vector3.one * scale;

        var pickupSerialized = new SerializedObject(go.GetComponent<Pickup>());
        pickupSerialized.FindProperty("pickupType").enumValueIndex = (int)type;
        pickupSerialized.FindProperty("value").intValue = value;
        pickupSerialized.ApplyModifiedProperties();

        var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);
        return prefab;
    }

    // Bakes a simple soft-edged white circle into a PNG and imports it as a
    // Sprite. Stands in for real heart/coin art -- Pickup instances tint and
    // scale this one shape to tell Hearts and Coins apart until real art exists.
    private static void EnsurePlaceholderSprite()
    {
        if (AssetDatabase.LoadAssetAtPath<Sprite>(PlaceholderSpritePath) != null) return;

        const int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Vector2 center = new Vector2(size / 2f, size / 2f);
        float radius = size / 2f - 2f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                float alpha = Mathf.Clamp01(radius - dist + 1f); // ~1px soft edge
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }
        tex.Apply();

        EnsureFolder(Path.GetDirectoryName(PlaceholderSpritePath).Replace('\\', '/'));
        File.WriteAllBytes(PlaceholderSpritePath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(PlaceholderSpritePath);

        var importer = (TextureImporter)AssetImporter.GetAtPath(PlaceholderSpritePath);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 64f;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Bilinear;
        importer.SaveAndReimport();
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        string leaf = Path.GetFileName(path);
        if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, leaf);
    }
}
