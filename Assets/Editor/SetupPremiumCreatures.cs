using System.IO;
using UnityEngine;
using UnityEditor;

// One-shot editor utility: creates the five tier-ladder CreatureDefinition
// assets (Squirrel/Owl/Fox/Griffin/Dragon), one per tier, each gated to earn
// Hearts from its own specific stunt. Squirrel is the free starter (unlock
// cost 0, owned from the start -- it fills the whole train by default); Owl
// through Dragon are the coin-gated unlockables. The old base roster
// (Mouse/Cat/Dog/Pig/Elephant) had no real art and was removed.
//
// Real chibi art for every one of these lives in
// Assets/Art/Creatures/Premium/ and is wired onto passengerSprite by
// WirePremiumCreatureSprites.cs. This script only sets passengerSprite on a
// brand-new asset (to null) and leaves it alone on re-runs, so re-running
// never clobbers the wired-in sprite. Idempotent: reuses existing assets in
// place rather than duplicating.
public static class SetupPremiumCreatures
{
    private const string CreaturesFolder = "Assets/ScriptableObjects/Creatures";

    private struct PremiumCreature
    {
        public string name;
        public CreatureTier tier;
        public HeartActionType heartAction;
        public int unlockCost;
        public int stuntHeartBonus;
        public Color tint;

        public PremiumCreature(string name, CreatureTier tier, HeartActionType heartAction, int unlockCost, int stuntHeartBonus, Color tint)
        {
            this.name = name;
            this.tier = tier;
            this.heartAction = heartAction;
            this.unlockCost = unlockCost;
            this.stuntHeartBonus = stuntHeartBonus;
            this.tint = tint;
        }
    }

    // Squirrel is free (cost 0) -- the starter every train begins with. From
    // Owl up the coin costs (and stunt Heart bonuses) climb steeply tier-to-
    // tier so Dragon (the game's signature near-miss/risky-float mechanic,
    // already the hardest to pull off) reads as the premium, aspirational unlock.
    private static readonly PremiumCreature[] Roster =
    {
        new PremiumCreature("Squirrel", CreatureTier.Common,    HeartActionType.TightTurn,  0,    2,  new Color(0.62f, 0.35f, 0.18f)),
        new PremiumCreature("Owl",      CreatureTier.Uncommon,  HeartActionType.Airtime,    300,  3,  new Color(0.55f, 0.5f, 0.42f)),
        new PremiumCreature("Fox",      CreatureTier.Rare,      HeartActionType.SpeedBurst, 800,  4,  new Color(0.85f, 0.42f, 0.15f)),
        new PremiumCreature("Griffin",  CreatureTier.Epic,      HeartActionType.Inversion,  2000, 6,  new Color(0.78f, 0.65f, 0.25f)),
        new PremiumCreature("Dragon",   CreatureTier.Legendary, HeartActionType.NearMiss,   5000, 10, new Color(0.65f, 0.12f, 0.55f)),
    };

    [MenuItem("NuttyFluffies/Setup Premium Creatures")]
    public static void Execute()
    {
        EnsureFolder("Assets/ScriptableObjects");
        EnsureFolder(CreaturesFolder);

        int created = 0, updated = 0;
        foreach (var entry in Roster)
        {
            string path = $"{CreaturesFolder}/{entry.name}.asset";
            bool isNew = AssetDatabase.LoadAssetAtPath<CreatureDefinition>(path) == null;
            GetOrCreateCreature(path, entry);
            if (isNew) created++; else updated++;
        }

        Debug.Log($"[SetupPremiumCreatures] Ready {Roster.Length} creature definition(s) in {CreaturesFolder} ({created} created, {updated} already present/updated). Run WirePremiumCreatureSprites to (re)attach art.");
    }

    private static CreatureDefinition GetOrCreateCreature(string path, PremiumCreature entry)
    {
        var existing = AssetDatabase.LoadAssetAtPath<CreatureDefinition>(path);
        var def = existing != null ? existing : ScriptableObject.CreateInstance<CreatureDefinition>();

        def.displayName = entry.name;
        def.tier = entry.tier;
        def.unlockCost = entry.unlockCost;
        def.heartAction = entry.heartAction;
        def.stuntHeartBonus = entry.stuntHeartBonus;
        def.passengerTintColor = entry.tint;
        // Deliberately leave def.passengerSprite untouched on existing assets
        // so a sprite wired in later (by hand or by the art task) survives
        // re-running this script; a brand-new asset simply starts at null.

        if (existing == null) AssetDatabase.CreateAsset(def, path);
        else EditorUtility.SetDirty(def);

        return def;
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
