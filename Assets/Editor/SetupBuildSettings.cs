using UnityEditor;
using UnityEngine;

// One-shot editor utility: registers MainMenu.unity, Garage.unity and
// SampleScene.unity in Build Settings, in that order, so the game boots to
// the Main Menu, then the Garage (shop/loadout) scene, then the coaster
// scene. Idempotent: always overwrites the full scene list with this exact
// triple/order.
public static class SetupBuildSettings
{
    [MenuItem("NuttyFluffies/Setup Build Settings")]
    public static void Execute()
    {
        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene("Assets/Scenes/MainMenu.unity", true),
            new EditorBuildSettingsScene("Assets/Scenes/Garage.unity", true),
            new EditorBuildSettingsScene("Assets/Scenes/SampleScene.unity", true),
        };

        // EditorBuildSettings.scenes doesn't reliably flush to
        // ProjectSettings/EditorBuildSettings.asset on disk by itself in this
        // environment -- force it so the change survives outside this Editor
        // session (verified via git status, not just this log).
        AssetDatabase.SaveAssets();

        Debug.Log($"[SetupBuildSettings] Build Settings scenes now: {string.Join(", ", System.Array.ConvertAll(EditorBuildSettings.scenes, s => s.path))}.");
    }
}
