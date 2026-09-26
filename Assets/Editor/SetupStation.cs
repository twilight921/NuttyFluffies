using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

// One-shot editor utility: adds the end-of-level Station to the open coaster
// scene (SampleScene) and wires it into LevelLoader and RunEndTrigger. The
// Station GameObject only needs the component -- its platform visual and
// buffer stop are built at runtime by LevelLoader -> Station.Place -- so the
// generated station art is dropped into the Station's "Station Sprite" slot.
//
// Run 'NuttyFluffies/Setup Level System' and 'NuttyFluffies/Setup Results UI'
// first (this needs the LevelLoader and RunEndTrigger they create).
// Idempotent: reuses the Station GameObject in place and re-stamps the wiring.
public static class SetupStation
{
    private const string StationName = "Station";

    [MenuItem("NuttyFluffies/Setup Station")]
    public static void Execute()
    {
        var loader = Object.FindFirstObjectByType<LevelLoader>();
        if (loader == null)
        {
            Debug.LogError("[SetupStation] No LevelLoader in the open scene -- open the coaster scene (SampleScene) and run 'NuttyFluffies/Setup Level System' first.");
            return;
        }

        GameObject stationGO = GameObject.Find(StationName);
        if (stationGO == null)
        {
            stationGO = new GameObject(StationName);
            Undo.RegisterCreatedObjectUndo(stationGO, "Create Station");
        }
        var station = stationGO.GetComponent<Station>();
        if (station == null) station = Undo.AddComponent<Station>(stationGO);

        var loaderSo = new SerializedObject(loader);
        loaderSo.FindProperty("station").objectReferenceValue = station;
        loaderSo.ApplyModifiedProperties();

        var runEnd = Object.FindFirstObjectByType<RunEndTrigger>();
        if (runEnd != null)
        {
            var runEndSo = new SerializedObject(runEnd);
            runEndSo.FindProperty("station").objectReferenceValue = station;
            runEndSo.ApplyModifiedProperties();
        }
        else
        {
            Debug.LogWarning("[SetupStation] No RunEndTrigger in scene -- run 'NuttyFluffies/Setup Results UI' so the Results screen appears when the train stops.");
        }

        EditorSceneManager.MarkSceneDirty(stationGO.scene);
        Debug.Log("[SetupStation] Station added and wired to LevelLoader" + (runEnd != null ? " + RunEndTrigger" : "")
            + ". Save the scene. Assign the station art to Station > Station Sprite (tune Visual Offset / Scale) when it's ready.");
    }
}
