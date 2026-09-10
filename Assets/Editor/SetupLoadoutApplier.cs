using UnityEngine;
using UnityEngine.Splines;
using UnityEditor;
using UnityEditor.SceneManagement;

// One-shot editor utility: adds a root "LoadoutApplier" GameObject (sibling
// to InputManager/RunStats/Pickups) to SampleScene.unity carrying both
// TrainLoadoutApplier (wired to CreatureRoster.asset) and
// PowerUpLoadoutApplier (wired to the track SplineContainer), so the Garage's
// saved creature loadout and power-up choice both carry into the ride. Must
// run with SampleScene.unity open/active. Idempotent: reuses the existing
// GameObject/components in place.
public static class SetupLoadoutApplier
{
    private const string RosterAssetPath = "Assets/ScriptableObjects/Creatures/CreatureRoster.asset";

    [MenuItem("NuttyFluffies/Setup Loadout Applier")]
    public static void Execute()
    {
        var roster = AssetDatabase.LoadAssetAtPath<CreatureRoster>(RosterAssetPath);
        if (roster == null)
        {
            Debug.LogError($"[SetupLoadoutApplier] Missing {RosterAssetPath} -- run NuttyFluffies/Setup Creature Roster first.");
            return;
        }

        GameObject go = GameObject.Find("LoadoutApplier");
        if (go == null)
        {
            go = new GameObject("LoadoutApplier");
            Undo.RegisterCreatedObjectUndo(go, "Create LoadoutApplier");
        }

        var applier = go.GetComponent<TrainLoadoutApplier>();
        if (applier == null) applier = Undo.AddComponent<TrainLoadoutApplier>(go);

        var serialized = new SerializedObject(applier);
        serialized.FindProperty("creatureRoster").objectReferenceValue = roster;
        serialized.ApplyModifiedProperties();

        var powerUpApplier = go.GetComponent<PowerUpLoadoutApplier>();
        if (powerUpApplier == null) powerUpApplier = Undo.AddComponent<PowerUpLoadoutApplier>(go);

        var trackSpline = GameObject.Find("CoasterLineRender")?.GetComponent<SplineContainer>();
        if (trackSpline == null)
            Debug.LogWarning("[SetupLoadoutApplier] No CoasterLineRender/SplineContainer in scene -- PowerUpLoadoutApplier will fall back to a runtime lookup.");

        var powerUpSerialized = new SerializedObject(powerUpApplier);
        powerUpSerialized.FindProperty("trackSpline").objectReferenceValue = trackSpline;
        powerUpSerialized.ApplyModifiedProperties();

        Debug.Log("[SetupLoadoutApplier] Wired TrainLoadoutApplier (creatureRoster=CreatureRoster.asset) and PowerUpLoadoutApplier (trackSpline=CoasterLineRender) on LoadoutApplier.");

        EditorSceneManager.MarkSceneDirty(go.scene);
    }
}
