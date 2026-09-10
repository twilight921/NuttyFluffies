using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;

// One-shot editor utility: tags the track's collider "Track" (creating that
// tag in TagManager if needed, the same way "Cart" already exists for
// Pickup.cs's OnTriggerEnter2D filter) and adds a StuntDetector to every cart
// in the scene, so each cart's own passenger can score off that cart's own
// stunts. Idempotent: safe to re-run, just reuses the existing tag/components.
public static class SetupStuntDetectors
{
    private const string TrackTag = "Track";
    private const string TrackObjectName = "CoasterLineRender";
    private const int CartCount = 6;

    [MenuItem("NuttyFluffies/Setup Stunt Detectors")]
    public static void Execute()
    {
        EnsureTag(TrackTag);

        GameObject trackGO = GameObject.Find(TrackObjectName);
        if (trackGO == null)
        {
            Debug.LogError($"[SetupStuntDetectors] Missing {TrackObjectName} in scene.");
            return;
        }
        Undo.RecordObject(trackGO, "Tag track for stunt detection");
        trackGO.tag = TrackTag;

        int wired = 0;
        Scene activeScene = default;
        for (int i = 1; i <= CartCount; i++)
        {
            string name = i == 1 ? "Cart" : $"Cart ({i})";
            GameObject cartGO = GameObject.Find(name);
            if (cartGO == null) continue;

            if (cartGO.GetComponent<StuntDetector>() == null)
                Undo.AddComponent<StuntDetector>(cartGO);

            wired++;
            activeScene = cartGO.scene;
        }

        Debug.Log($"[SetupStuntDetectors] Tagged {TrackObjectName} \"{TrackTag}\", ensured StuntDetector on {wired} cart(s).");

        if (activeScene.IsValid()) EditorSceneManager.MarkSceneDirty(activeScene);
    }

    private static void EnsureTag(string tag)
    {
        var tagManagerAsset = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0];
        var tagManager = new SerializedObject(tagManagerAsset);
        var tagsProp = tagManager.FindProperty("tags");

        for (int i = 0; i < tagsProp.arraySize; i++)
        {
            if (tagsProp.GetArrayElementAtIndex(i).stringValue == tag) return;
        }

        tagsProp.InsertArrayElementAtIndex(tagsProp.arraySize);
        tagsProp.GetArrayElementAtIndex(tagsProp.arraySize - 1).stringValue = tag;
        tagManager.ApplyModifiedProperties();
    }
}
