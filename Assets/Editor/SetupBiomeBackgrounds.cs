using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

// One-shot editor utility: gives each themed world its own parallax backdrop so
// Canyon / Peaks levels no longer reuse the Meadow sky. Two jobs:
//
//  * (Re)generates a placeholder gradient sky PNG per biome under
//    Assets/Art/Background (bg_canyon.png, bg_peaks.png). Meadow keeps the
//    existing hand-authored sky_bg.png. Swap these files for real art later --
//    the wiring below keys off the file path, not the pixels.
//
//  * Assigns LevelDefinition.backgroundSprite on every level asset by its
//    worldName, and points LevelLoader.backgroundRenderer at the scene's
//    "Background" SpriteRenderer if the coaster scene is open (LevelLoader also
//    falls back to GameObject.Find("Background") at runtime).
//
// Idempotent: rewrites the same files / re-stamps the same references in place.
public static class SetupBiomeBackgrounds
{
    private const string BackgroundFolder = "Assets/Art/Background";
    private const string LevelsFolder = "Assets/ScriptableObjects/Levels";
    private const int TexWidth = 896;
    private const int TexHeight = 1024;

    private struct Biome
    {
        public string world;
        public string spriteFile;      // null => generate, use existing file as-is
        public Color skyTop, skyHorizon, ground, ridge;
        public float ridgeHeight;      // fraction of height taken by the ground band
        public float ridgeAmplitude;   // silhouette jaggedness, fraction of height
    }

    private static readonly Biome[] Biomes =
    {
        new Biome { world = "Meadow", spriteFile = null },
        new Biome
        {
            world = "Canyon", spriteFile = "bg_canyon.png",
            skyTop     = new Color32(247, 206, 158, 255),
            skyHorizon = new Color32(232, 150,  96, 255),
            ground     = new Color32(150,  78,  52, 255),
            ridge      = new Color32(120,  60,  42, 255),
            ridgeHeight = 0.34f, ridgeAmplitude = 0.05f,
        },
        new Biome
        {
            world = "Peaks", spriteFile = "bg_peaks.png",
            skyTop     = new Color32(120, 168, 224, 255),
            skyHorizon = new Color32(214, 232, 246, 255),
            ground     = new Color32(150, 165, 190, 255),
            ridge      = new Color32(110, 122, 150, 255),
            ridgeHeight = 0.32f, ridgeAmplitude = 0.09f,
        },
    };

    [MenuItem("NuttyFluffies/Setup Biome Backgrounds")]
    public static void Execute()
    {
        var spriteByWorld = new Dictionary<string, Sprite>();

        foreach (var biome in Biomes)
        {
            string file = biome.spriteFile ?? "sky_bg.png";
            string assetPath = $"{BackgroundFolder}/{file}";

            if (biome.spriteFile != null)
                GenerateSkyPng(assetPath, biome);

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            if (sprite == null)
                Debug.LogWarning($"[SetupBiomeBackgrounds] No sprite at {assetPath} for world '{biome.world}'.");
            else
                spriteByWorld[biome.world] = sprite;
        }

        int stamped = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:LevelDefinition", new[] { LevelsFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var def = AssetDatabase.LoadAssetAtPath<LevelDefinition>(path);
            if (def == null || string.IsNullOrEmpty(def.worldName)) continue;
            if (!spriteByWorld.TryGetValue(def.worldName, out var sprite)) continue;

            if (def.backgroundSprite != sprite)
            {
                var so = new SerializedObject(def);
                so.FindProperty("backgroundSprite").objectReferenceValue = sprite;
                so.ApplyModifiedProperties();
                stamped++;
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        WireSceneBackdrop();

        Debug.Log($"[SetupBiomeBackgrounds] {spriteByWorld.Count} biome backdrop(s), "
            + $"{stamped} level asset(s) re-pointed. Re-run 'NuttyFluffies/Setup Level System' is not required "
            + "(LevelLoader falls back to the 'Background' object at runtime).");
    }

    private static void GenerateSkyPng(string assetPath, Biome biome)
    {
        var tex = new Texture2D(TexWidth, TexHeight, TextureFormat.RGBA32, false);
        var pixels = new Color[TexWidth * TexHeight];
        float horizonY = TexHeight * (1f - biome.ridgeHeight);

        for (int y = 0; y < TexHeight; y++)
        {
            // Texture rows run bottom-up; flip so y=0 is the top of the sky.
            float fromTop = TexHeight - 1 - y;
            Color col = fromTop < horizonY
                ? Color.Lerp(biome.skyTop, biome.skyHorizon, fromTop / horizonY)
                : Color.Lerp(biome.skyHorizon, biome.ground,
                    Mathf.Min(1f, (fromTop - horizonY) / (TexHeight - horizonY) * 1.3f));

            for (int x = 0; x < TexWidth; x++)
                pixels[y * TexWidth + x] = col;
        }

        // Deterministic jagged ridgeline silhouette along the horizon.
        for (int x = 0; x < TexWidth; x++)
        {
            float u = (float)x / TexWidth;
            float n = Mathf.Sin(u * 11f) * 0.5f + Mathf.Sin(u * 23f + 1.3f) * 0.3f + Mathf.Sin(u * 47f + 2.1f) * 0.2f;
            float ridgeFromTop = horizonY + (n * biome.ridgeAmplitude - biome.ridgeAmplitude * 0.2f) * TexHeight;
            int ridgeRow = Mathf.Clamp(Mathf.RoundToInt(TexHeight - 1 - ridgeFromTop), 0, TexHeight - 1);

            for (int y = 0; y <= ridgeRow; y++)
            {
                float depth = ridgeRow == 0 ? 0f : (float)(ridgeRow - y) / ridgeRow;
                pixels[y * TexWidth + x] = Color.Lerp(biome.ridge, biome.ground, Mathf.Min(1f, depth * 0.8f));
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        File.WriteAllBytes(Path.GetFullPath(assetPath), tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
        var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Bilinear;
            importer.SaveAndReimport();
        }
    }

    private static void WireSceneBackdrop()
    {
        var loader = Object.FindObjectOfType<LevelLoader>();
        var backdrop = GameObject.Find("Background")?.GetComponent<SpriteRenderer>();
        if (loader == null || backdrop == null) return;

        var so = new SerializedObject(loader);
        var prop = so.FindProperty("backgroundRenderer");
        if (prop.objectReferenceValue != backdrop)
        {
            prop.objectReferenceValue = backdrop;
            so.ApplyModifiedProperties();
            EditorSceneManager.MarkSceneDirty(loader.gameObject.scene);
            Debug.Log("[SetupBiomeBackgrounds] Wired LevelLoader.backgroundRenderer -> 'Background'. Save the scene.");
        }
    }
}
