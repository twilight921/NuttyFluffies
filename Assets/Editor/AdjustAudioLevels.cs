using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

// One-shot editor utility: turns down every SfxOneShot's volume (heart/coin
// pickups, stunt sting) and the background music AudioSource's volume, all
// of which SetupAudio left at the Unity default of 1.0 (max, no headroom).
// Idempotent: just re-stamps the volume fields on whatever already exists,
// same as SetupAudio's own wiring pattern -- safe to re-run.
public static class AdjustAudioLevels
{
    private const float SfxVolume = 0.6f;
    private const float MusicVolume = 0.45f;

    private const string PickupPrefabFolder = "Assets/MyPrefabs";
    private const string MusicHostName = "Main Camera";

    [MenuItem("NuttyFluffies/Adjust Audio Levels")]
    public static void Execute()
    {
        int prefabsAdjusted = AdjustPrefab($"{PickupPrefabFolder}/Heart.prefab")
            + AdjustPrefab($"{PickupPrefabFolder}/Coin.prefab");

        int sceneSfxAdjusted = 0;
        foreach (var sfx in Object.FindObjectsOfType<SfxOneShot>())
        {
            SetVolume(sfx, SfxVolume, useUndo: true);
            sceneSfxAdjusted++;
        }

        bool musicAdjusted = AdjustBackgroundMusic();

        Debug.Log($"[AdjustAudioLevels] SFX -> {SfxVolume}: {prefabsAdjusted} prefab(s), " +
            $"{sceneSfxAdjusted} scene instance(s). Music -> {MusicVolume}: {musicAdjusted}.");
    }

    private static int AdjustPrefab(string path)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null) return 0;

        var contents = PrefabUtility.LoadPrefabContents(path);
        var sfx = contents.GetComponent<SfxOneShot>();
        if (sfx != null) SetVolume(sfx, SfxVolume, useUndo: false);
        PrefabUtility.SaveAsPrefabAsset(contents, path);
        PrefabUtility.UnloadPrefabContents(contents);
        return sfx != null ? 1 : 0;
    }

    private static bool AdjustBackgroundMusic()
    {
        GameObject host = GameObject.Find(MusicHostName);
        if (host == null)
        {
            Debug.LogWarning($"[AdjustAudioLevels] No \"{MusicHostName}\" found in the open scene -- skipped background music.");
            return false;
        }

        var source = host.GetComponent<AudioSource>();
        if (source == null)
        {
            Debug.LogWarning($"[AdjustAudioLevels] \"{MusicHostName}\" has no AudioSource -- skipped background music.");
            return false;
        }

        Undo.RecordObject(source, "Adjust background music volume");
        source.volume = MusicVolume;
        EditorSceneManager.MarkSceneDirty(host.scene);
        return true;
    }

    private static void SetVolume(SfxOneShot sfx, float volume, bool useUndo)
    {
        var so = new SerializedObject(sfx);
        so.FindProperty("volume").floatValue = volume;
        so.ApplyModifiedProperties();
        if (useUndo) EditorUtility.SetDirty(sfx);
    }
}
