using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

// One-shot editor utility: generates a spread of LevelDefinition assets across
// a few themed "worlds" with a rising difficulty curve, then (re)builds
// LevelCatalog.asset from them in order. This is the "many unique levels"
// button -- each level is a different seed + shape params, so no two tracks
// are the same, and the catalog is what LevelLoader / the Garage picker read
// at runtime.
//
// Idempotent: each (world, slot) maps to a fixed asset path, reused and
// re-stamped in place rather than duplicated. Safe to re-run after tweaking
// the bands below; hand-tuned levels (useBakedKnots) keep their baked knots
// but their metadata/params are refreshed -- duplicate + rename a level you
// want to protect from re-runs.
public static class GenerateLevelBatch
{
    private const string LevelsFolder = "Assets/ScriptableObjects/Levels";
    private const string CatalogPath = "Assets/ScriptableObjects/Levels/LevelCatalog.asset";
    private const string TrackTypesFolder = "Assets/ScriptableObjects/TrackTypes";

    private const int LevelsPerWorld = 5;

    private struct World
    {
        public string name;
        public Color tint;
        public Vector2 hillHeight;     // easy -> hard
        public Vector2 hillFrequency;
        public Vector2 roughness;
        public Vector2 length;
        public float downhillBias;
        public string[] trackTypeBySlot; // per-slot "Wood"/"Steel", length LevelsPerWorld
    }

    private static readonly World[] Worlds =
    {
        new World
        {
            name = "Meadow",
            tint = new Color(0.56f, 0.78f, 0.55f),
            hillHeight = new Vector2(2.5f, 4.5f),
            hillFrequency = new Vector2(0.20f, 0.30f),
            roughness = new Vector2(0.08f, 0.20f),
            length = new Vector2(85f, 110f),
            downhillBias = 0.022f,
            trackTypeBySlot = new[] { "Wood", "Wood", "Wood", "Wood", "Wood" },
        },
        new World
        {
            name = "Canyon",
            tint = new Color(0.85f, 0.6f, 0.4f),
            hillHeight = new Vector2(4.5f, 7.0f),
            hillFrequency = new Vector2(0.28f, 0.40f),
            roughness = new Vector2(0.20f, 0.38f),
            length = new Vector2(110f, 140f),
            downhillBias = 0.032f,
            trackTypeBySlot = new[] { "Wood", "Steel", "Wood", "Steel", "Steel" },
        },
        new World
        {
            name = "Peaks",
            tint = new Color(0.62f, 0.74f, 0.9f),
            hillHeight = new Vector2(6.5f, 10f),
            hillFrequency = new Vector2(0.36f, 0.52f),
            roughness = new Vector2(0.35f, 0.55f),
            length = new Vector2(135f, 170f),
            downhillBias = 0.045f,
            trackTypeBySlot = new[] { "Steel", "Steel", "Steel", "Steel", "Steel" },
        },
    };

    [MenuItem("NuttyFluffies/Generate Level Batch")]
    public static void Execute()
    {
        EnsureFolder(LevelsFolder);

        var wood = AssetDatabase.LoadAssetAtPath<TrackTypeDefinition>($"{TrackTypesFolder}/Wood.asset");
        var steel = AssetDatabase.LoadAssetAtPath<TrackTypeDefinition>($"{TrackTypesFolder}/Steel.asset");
        if (wood == null || steel == null)
            Debug.LogWarning("[GenerateLevelBatch] Wood/Steel TrackTypeDefinition assets not found -- run 'NuttyFluffies/Setup Track Types' first so levels get a track type.");

        var ordered = new List<LevelDefinition>();
        int globalIndex = 0;

        foreach (var world in Worlds)
        {
            for (int slot = 0; slot < LevelsPerWorld; slot++, globalIndex++)
            {
                float d = LevelsPerWorld == 1 ? 0f : (float)slot / (LevelsPerWorld - 1); // difficulty 0..1

                string safeName = $"{world.name}{slot + 1}";
                string path = $"{LevelsFolder}/Level_{globalIndex:00}_{safeName}.asset";

                var def = AssetDatabase.LoadAssetAtPath<LevelDefinition>(path);
                bool isNew = def == null;
                if (isNew) def = ScriptableObject.CreateInstance<LevelDefinition>();

                def.displayName = $"{world.name} {slot + 1}";
                def.worldName = world.name;
                def.order = globalIndex;
                def.seed = 1000 + globalIndex * 97;

                def.length = Mathf.Lerp(world.length.x, world.length.y, d);
                def.knotSpacing = 8f;
                def.leadInLength = 10f;
                def.runOutLength = 8f;
                def.maxHillHeight = Mathf.Lerp(world.hillHeight.x, world.hillHeight.y, d);
                def.hillFrequency = Mathf.Lerp(world.hillFrequency.x, world.hillFrequency.y, d);
                def.roughness = Mathf.Lerp(world.roughness.x, world.roughness.y, d);
                def.downhillBias = world.downhillBias;
                def.amplitudeEnvelope = BuildEnvelope(d);

                // Only stamp shape params over a hand-tuned level -- don't wipe its baked knots.
                if (!def.useBakedKnots)
                    def.bakedKnots = new List<Vector3>();

                string wanted = world.trackTypeBySlot[Mathf.Clamp(slot, 0, world.trackTypeBySlot.Length - 1)];
                def.trackType = wanted == "Steel" ? steel : wood;

                def.heartCount = Mathf.RoundToInt(Mathf.Lerp(6f, 12f, d));
                def.coinCount = Mathf.RoundToInt(Mathf.Lerp(14f, 24f, d));
                def.heartMinOffset = 1.2f;
                def.heartMaxOffset = Mathf.Lerp(2.2f, 3.4f, d);
                def.coinOffset = 0.55f;

                def.backgroundTint = world.tint;

                if (isNew) AssetDatabase.CreateAsset(def, path);
                else EditorUtility.SetDirty(def);

                ordered.Add(def);
            }
        }

        var catalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>(CatalogPath);
        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<LevelCatalog>();
            catalog.levels = ordered.ToArray();
            AssetDatabase.CreateAsset(catalog, CatalogPath);
        }
        else
        {
            catalog.levels = ordered.ToArray();
            EditorUtility.SetDirty(catalog);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[GenerateLevelBatch] {ordered.Count} levels across {Worlds.Length} worlds -> {CatalogPath}. "
            + "Wire the catalog with 'NuttyFluffies/Setup Level System'.");
    }

    // Rises to a peak then eases back for a gentler finish; harder levels start
    // less gentle and dip less at the end.
    private static AnimationCurve BuildEnvelope(float difficulty)
    {
        float start = Mathf.Lerp(0.30f, 0.55f, difficulty);
        float end = Mathf.Lerp(0.45f, 0.7f, difficulty);
        var curve = new AnimationCurve(
            new Keyframe(0f, start),
            new Keyframe(0.82f, 1f),
            new Keyframe(1f, end));
        for (int i = 0; i < curve.length; i++)
            curve.SmoothTangents(i, 0f);
        return curve;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
        string leaf = System.IO.Path.GetFileName(path);
        if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, leaf);
    }
}
