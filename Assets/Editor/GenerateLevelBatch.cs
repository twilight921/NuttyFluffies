using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

// One-shot editor utility: generates a spread of LevelDefinition assets across
// a few themed "worlds" with a rising difficulty curve, then (re)builds
// LevelCatalog.asset from them in order. This is the "many unique levels"
// button -- every level pairs a different seed + base terrain feel with its own
// TrackArchetype and a hand-picked mix of signature features (hairpins, launch
// crests, plunges, washboards). No two tracks play the same, and because each
// feature type scores for a different premium creature, each level has a clear
// "bring this passenger" hook.
//
// Idempotent: each (world, slot) maps to a fixed asset path, reused and
// re-stamped in place rather than duplicated. Safe to re-run after tweaking
// the tables below; hand-tuned levels (useBakedKnots) keep their baked knots
// but their metadata/params are refreshed -- duplicate + rename a level you
// want to protect from re-runs.
public static class GenerateLevelBatch
{
    private const string LevelsFolder = "Assets/ScriptableObjects/Levels";
    private const string CatalogPath = "Assets/ScriptableObjects/Levels/LevelCatalog.asset";
    private const string TrackTypesFolder = "Assets/ScriptableObjects/TrackTypes";

    private const int LevelsPerWorld = 5;

    // Per-level personality. `world` supplies the base terrain bands (size,
    // hill height/frequency, tint); this supplies everything that makes the
    // slot feel distinct from its neighbours.
    private struct Slot
    {
        public TrackArchetype archetype;
        public bool steel;               // false = Wood
        public int hairpins;   public float hairpinSharpness;
        public int launches;   public float launchStrength;
        public int plunges;    public float plungeSteepness;
        public int washboards; public float washboardStrength;
    }

    private struct World
    {
        public string name;
        public Color tint;
        public Vector2 hillHeight;      // easy -> hard
        public Vector2 hillFrequency;
        public Vector2 roughness;
        public Vector2 length;
        public float knotSpacing;
        public float downhillBias;
        public Slot[] slots;             // length LevelsPerWorld
    }

    private static readonly World[] Worlds =
    {
        // Meadow -- calm intro. Each level teaches ONE signature feature so the
        // player meets the whole creature roster one at a time.
        new World
        {
            name = "Meadow",
            tint = new Color(0.56f, 0.78f, 0.55f),
            hillHeight = new Vector2(2.3f, 4.2f),
            hillFrequency = new Vector2(0.16f, 0.24f),
            roughness = new Vector2(0.06f, 0.16f),
            length = new Vector2(85f, 115f),
            knotSpacing = 8f,
            downhillBias = 0.020f,
            slots = new[]
            {
                new Slot { archetype = TrackArchetype.RollingHills, steel = false },
                new Slot { archetype = TrackArchetype.Whoops,       steel = false, washboards = 2, washboardStrength = 0.32f },
                new Slot { archetype = TrackArchetype.Switchback,   steel = false, hairpins = 2, hairpinSharpness = 0.42f },
                new Slot { archetype = TrackArchetype.Airborne,     steel = false, launches = 2, launchStrength = 0.40f },
                new Slot { archetype = TrackArchetype.Plunges,      steel = false, plunges = 1, plungeSteepness = 0.48f },
            },
        },
        // Canyon -- levels now combine two mechanics and the Steel/Wood split
        // starts to matter (Steel is faster -> bigger air, longer bursts).
        new World
        {
            name = "Canyon",
            tint = new Color(0.85f, 0.6f, 0.4f),
            hillHeight = new Vector2(4.2f, 6.8f),
            hillFrequency = new Vector2(0.24f, 0.38f),
            roughness = new Vector2(0.18f, 0.34f),
            length = new Vector2(115f, 145f),
            knotSpacing = 7f,
            downhillBias = 0.030f,
            slots = new[]
            {
                new Slot { archetype = TrackArchetype.Switchback, steel = false, hairpins = 3, hairpinSharpness = 0.55f, washboards = 1, washboardStrength = 0.30f },
                new Slot { archetype = TrackArchetype.Airborne,   steel = true,  launches = 3, launchStrength = 0.55f },
                new Slot { archetype = TrackArchetype.Plunges,    steel = false, plunges = 2, plungeSteepness = 0.60f, launches = 1, launchStrength = 0.45f },
                new Slot { archetype = TrackArchetype.Whoops,     steel = true,  washboards = 4, washboardStrength = 0.50f, launches = 1, launchStrength = 0.45f },
                new Slot { archetype = TrackArchetype.Mixed,      steel = true,  hairpins = 2, hairpinSharpness = 0.55f, launches = 2, launchStrength = 0.55f, plunges = 1, plungeSteepness = 0.55f, washboards = 1, washboardStrength = 0.40f },
            },
        },
        // Peaks -- wild finale. Steel throughout, features cranked, the last
        // level throws the entire toolkit at the player at once.
        new World
        {
            name = "Peaks",
            tint = new Color(0.62f, 0.74f, 0.9f),
            hillHeight = new Vector2(6.5f, 10f),
            hillFrequency = new Vector2(0.34f, 0.52f),
            roughness = new Vector2(0.32f, 0.55f),
            length = new Vector2(135f, 175f),
            knotSpacing = 6.5f,
            downhillBias = 0.044f,
            slots = new[]
            {
                new Slot { archetype = TrackArchetype.Airborne,   steel = true, launches = 4, launchStrength = 0.72f, washboards = 1, washboardStrength = 0.40f },
                new Slot { archetype = TrackArchetype.Plunges,    steel = true, plunges = 3, plungeSteepness = 0.75f, launches = 1, launchStrength = 0.50f },
                new Slot { archetype = TrackArchetype.Switchback, steel = true, hairpins = 5, hairpinSharpness = 0.80f, washboards = 1, washboardStrength = 0.40f },
                new Slot { archetype = TrackArchetype.Whoops,     steel = true, washboards = 6, washboardStrength = 0.65f, launches = 2, launchStrength = 0.60f, hairpins = 1, hairpinSharpness = 0.50f },
                new Slot { archetype = TrackArchetype.Mixed,      steel = true, hairpins = 3, hairpinSharpness = 0.72f, launches = 3, launchStrength = 0.72f, plunges = 2, plungeSteepness = 0.72f, washboards = 2, washboardStrength = 0.60f },
            },
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
                Slot profile = world.slots[Mathf.Clamp(slot, 0, world.slots.Length - 1)];

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
                def.knotSpacing = world.knotSpacing;
                def.leadInLength = 10f;
                def.runOutLength = 8f;
                def.maxHillHeight = Mathf.Lerp(world.hillHeight.x, world.hillHeight.y, d);
                def.hillFrequency = Mathf.Lerp(world.hillFrequency.x, world.hillFrequency.y, d);
                def.roughness = Mathf.Lerp(world.roughness.x, world.roughness.y, d);
                def.downhillBias = world.downhillBias;
                def.amplitudeEnvelope = BuildEnvelope(d);

                def.archetype = profile.archetype;
                def.hairpinCount = profile.hairpins;
                def.hairpinSharpness = profile.hairpinSharpness > 0f ? profile.hairpinSharpness : 0.5f;
                def.launchCount = profile.launches;
                def.launchStrength = profile.launchStrength > 0f ? profile.launchStrength : 0.5f;
                def.plungeCount = profile.plunges;
                def.plungeSteepness = profile.plungeSteepness > 0f ? profile.plungeSteepness : 0.5f;
                def.washboardCount = profile.washboards;
                def.washboardStrength = profile.washboardStrength > 0f ? profile.washboardStrength : 0.5f;

                // Only stamp shape params over a hand-tuned level -- don't wipe its baked knots.
                if (!def.useBakedKnots)
                    def.bakedKnots = new List<Vector3>();

                def.trackType = profile.steel ? steel : wood;

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
