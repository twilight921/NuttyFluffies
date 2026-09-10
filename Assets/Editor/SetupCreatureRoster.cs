using UnityEngine;
using UnityEditor;

// One-shot editor utility: builds/populates the single CreatureRoster.asset
// runtime-loadable list of every CreatureDefinition asset (Squirrel starter +
// the Owl..Dragon unlock ladder), so the Garage scene can enumerate creatures at runtime
// without AssetDatabase (which doesn't work in builds). Mutate-in-place style
// like SetupTrackTypes.cs: load or create, then repopulate. Idempotent.
public static class SetupCreatureRoster
{
    private const string CreaturesFolder = "Assets/ScriptableObjects/Creatures";
    private const string RosterAssetPath = "Assets/ScriptableObjects/Creatures/CreatureRoster.asset";

    // Starter first, then the unlock ladder, in that fixed order.
    private static readonly string[] CreatureNames =
        { "Squirrel", "Owl", "Fox", "Griffin", "Dragon" };

    [MenuItem("NuttyFluffies/Setup Creature Roster")]
    public static void Execute()
    {
        var roster = AssetDatabase.LoadAssetAtPath<CreatureRoster>(RosterAssetPath);
        bool isNew = roster == null;
        if (isNew) roster = ScriptableObject.CreateInstance<CreatureRoster>();

        var creatures = new CreatureDefinition[CreatureNames.Length];
        int missing = 0;
        for (int i = 0; i < CreatureNames.Length; i++)
        {
            string path = $"{CreaturesFolder}/{CreatureNames[i]}.asset";
            var def = AssetDatabase.LoadAssetAtPath<CreatureDefinition>(path);
            if (def == null)
            {
                Debug.LogError($"[SetupCreatureRoster] Missing CreatureDefinition asset at {path}.");
                missing++;
            }
            creatures[i] = def;
        }

        roster.creatures = creatures;
        EditorUtility.SetDirty(roster);

        if (isNew) AssetDatabase.CreateAsset(roster, RosterAssetPath);
        AssetDatabase.SaveAssets();

        Debug.Log($"[SetupCreatureRoster] Populated {RosterAssetPath} with {CreatureNames.Length - missing}/{CreatureNames.Length} creature definition(s) ({(missing > 0 ? $"{missing} missing!" : "all found")}).");
    }
}
