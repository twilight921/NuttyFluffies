using UnityEngine;
using UnityEditor;

// One-shot editor utility: assigns the real 2D art in
// Assets/Art/Creatures/Premium/ to each CreatureDefinition's passengerSprite
// field (SetupPremiumCreatures.cs deliberately leaves that field null so it
// survives re-runs -- this script is the wiring step now that the art
// exists).
//
// It also normalizes each source PNG's import PixelsPerUnit to the texture's
// own pixel height, so every creature sprite is exactly 1 world unit tall
// regardless of the resolution it was authored at. Without this the raw
// ~900-1000px art at the default 100 PPU renders ~9-10 world units tall --
// several times the size of the ~2-unit cart it rides on. The "small head
// poking out of the seat" look then comes purely from the 0.45 local scale
// SetupCreatures gives the Passenger child.
//
// Idempotent: safe to re-run, just re-applies the same mapping/PPU.
public static class WirePremiumCreatureSprites
{
    private const string CreaturesFolder = "Assets/ScriptableObjects/Creatures";
    private const string ArtFolder = "Assets/Art/Creatures/Premium";

    private static readonly string[] Roster = { "Squirrel", "Owl", "Fox", "Griffin", "Dragon" };

    [MenuItem("NuttyFluffies/Wire Premium Creature Sprites")]
    public static void Execute()
    {
        int wired = 0;
        foreach (var name in Roster)
        {
            string defPath = $"{CreaturesFolder}/{name}.asset";
            string spritePath = $"{ArtFolder}/{name.ToLowerInvariant()}.png";

            var def = AssetDatabase.LoadAssetAtPath<CreatureDefinition>(defPath);
            if (def == null)
            {
                Debug.LogWarning($"[WirePremiumCreatureSprites] Missing CreatureDefinition at {defPath} -- skipped.");
                continue;
            }

            NormalizeSpritePixelsPerUnit(spritePath);

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
            if (sprite == null)
            {
                Debug.LogWarning($"[WirePremiumCreatureSprites] Missing Sprite at {spritePath} -- skipped.");
                continue;
            }

            def.passengerSprite = sprite;
            EditorUtility.SetDirty(def);
            wired++;
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[WirePremiumCreatureSprites] Wired {wired}/{Roster.Length} creature sprite(s), each normalized to 1 world unit tall.");
    }

    // Sets the texture's sprite PixelsPerUnit to its own pixel height so the
    // resulting sprite is 1 world unit tall. Only reimports when the value
    // actually changes, so re-runs are cheap no-ops.
    private static void NormalizeSpritePixelsPerUnit(string spritePath)
    {
        var importer = AssetImporter.GetAtPath(spritePath) as TextureImporter;
        if (importer == null)
        {
            Debug.LogWarning($"[WirePremiumCreatureSprites] No TextureImporter at {spritePath} -- PPU not normalized.");
            return;
        }

        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(spritePath);
        if (tex == null) return;

        float targetPpu = tex.height;
        if (Mathf.Approximately(importer.spritePixelsPerUnit, targetPpu)) return;

        importer.spritePixelsPerUnit = targetPpu;
        importer.SaveAndReimport();
    }
}
