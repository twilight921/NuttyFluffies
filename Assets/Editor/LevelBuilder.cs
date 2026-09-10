using UnityEngine;
using UnityEngine.Splines;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Collections.Generic;

// Editor authoring tool for levels. Two jobs:
//
//  * "Build Level In Scene" -- takes the LevelDefinition selected in the
//    Project window and builds it into the open coaster scene (spline knots,
//    baked rail collider, track type, train, pickups) so you can look at it.
//    Idempotent, and it leans on the existing NuttyFluffies/* builders
//    (BuildCoasterTrain, SetupPickups) for the train/pickup passes.
//
//  * "Bake Generated Knots Into Level" -- freezes the current generated shape
//    into the asset's bakedKnots list and flips useBakedKnots, so you can then
//    hand-tune the track with Unity's spline tools.
//
// At runtime LevelLoader rebuilds whatever level LevelSelection points at, so
// the track saved into the scene here is just a preview / default.
public static class LevelBuilder
{
    [MenuItem("NuttyFluffies/Build Level In Scene")]
    public static void BuildFromSelection()
    {
        var def = Selection.activeObject as LevelDefinition;
        if (def == null)
        {
            Debug.LogError("[LevelBuilder] Select a LevelDefinition asset in the Project window first.");
            return;
        }
        Build(def);
    }

    public static void Build(LevelDefinition def)
    {
        GameObject trackGO = GameObject.Find("CoasterLineRender");
        if (trackGO == null)
        {
            Debug.LogError("[LevelBuilder] No CoasterLineRender in the open scene -- open the coaster scene first.");
            return;
        }

        var container = trackGO.GetComponent<SplineContainer>();
        var edge = trackGO.GetComponent<EdgeCollider2D>();
        var line = trackGO.GetComponent<LineRenderer>();
        if (container == null || edge == null)
        {
            Debug.LogError("[LevelBuilder] CoasterLineRender needs a SplineContainer and an EdgeCollider2D.");
            return;
        }

        List<Vector3> knots = def.useBakedKnots && def.bakedKnots != null && def.bakedKnots.Count >= 2
            ? new List<Vector3>(def.bakedKnots)
            : TrackGenerator.Generate(def);

        Undo.RecordObjects(new Object[] { edge, line }, "Build Level");
        SplineTrackBuilder.Build(container, edge, line, knots);
        EditorUtility.SetDirty(container);
        if (line != null) EditorUtility.SetDirty(line);

        var applier = trackGO.GetComponent<TrackTypeApplier>();
        if (applier != null && def.trackType != null)
        {
            var so = new SerializedObject(applier);
            so.FindProperty("trackType").objectReferenceValue = def.trackType;
            so.ApplyModifiedProperties();
        }

        // Reuse the existing idempotent builders for the train + pickups so
        // this stays consistent with a hand-run of those menu items.
        BuildCoasterTrain.Execute();
        SetupPickups.Run(def.heartCount, def.coinCount, def.heartMinOffset, def.heartMaxOffset, def.coinOffset);

        Camera cam = Camera.main;
        if (cam != null) cam.backgroundColor = def.backgroundTint;

        if (def.backgroundSprite != null)
        {
            var backdrop = GameObject.Find("Background")?.GetComponent<SpriteRenderer>();
            if (backdrop != null)
            {
                Undo.RecordObject(backdrop, "Build Level");
                backdrop.sprite = def.backgroundSprite;
                EditorUtility.SetDirty(backdrop);
            }
        }

        EditorSceneManager.MarkSceneDirty(trackGO.scene);
        Debug.Log($"[LevelBuilder] Built '{def.displayName}' -- {knots.Count} knots, track type "
            + $"{(def.trackType != null ? def.trackType.displayName : "none")}"
            + $"{(def.useBakedKnots ? " (baked knots)" : "")}.");
    }

    [MenuItem("NuttyFluffies/Bake Generated Knots Into Level")]
    public static void BakeKnots()
    {
        var def = Selection.activeObject as LevelDefinition;
        if (def == null)
        {
            Debug.LogError("[LevelBuilder] Select a LevelDefinition asset in the Project window first.");
            return;
        }

        var knots = TrackGenerator.Generate(def);
        Undo.RecordObject(def, "Bake Generated Knots");
        def.bakedKnots = knots;
        def.useBakedKnots = true;
        EditorUtility.SetDirty(def);
        AssetDatabase.SaveAssets();
        Debug.Log($"[LevelBuilder] Baked {knots.Count} knots into '{def.displayName}'. useBakedKnots is now ON -- "
            + "shape params are ignored for this level until you turn it back off.");
    }
}
