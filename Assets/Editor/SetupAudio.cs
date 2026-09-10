using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

// One-shot editor utility: wires the SFX/music clips under Assets/Audio onto
// whatever GameObjects already host Pickup/StuntDetector, and adds a looping
// AudioSource for background music on Main Camera. Idempotent: reuses
// existing SfxOneShot/AudioSource components in place and just (re)points
// their clip reference rather than duplicating anything.
//
// Doesn't generate the clips itself -- generation happens once up front via
// the generate_sfx/generate_music MCP tools (this script has no way to call
// those). If a clip is missing this just logs an error and bails, so it's
// safe to re-run once the clips exist without regenerating them.
public static class SetupAudio
{
    private const string AudioFolder = "Assets/Audio";
    private const string HeartClipPath = AudioFolder + "/heart_collect.wav";
    private const string CoinClipPath = AudioFolder + "/coin_collect.wav";
    private const string StuntClipPath = AudioFolder + "/stunt_sting.wav";
    private const string MusicClipPath = AudioFolder + "/background_music.mp3";

    private const string PickupPrefabFolder = "Assets/MyPrefabs";
    private const string MusicHostName = "Main Camera";

    [MenuItem("NuttyFluffies/Setup Audio")]
    public static void Execute()
    {
        var heartClip = AssetDatabase.LoadAssetAtPath<AudioClip>(HeartClipPath);
        var coinClip = AssetDatabase.LoadAssetAtPath<AudioClip>(CoinClipPath);
        var stuntClip = AssetDatabase.LoadAssetAtPath<AudioClip>(StuntClipPath);
        var musicClip = AssetDatabase.LoadAssetAtPath<AudioClip>(MusicClipPath);

        if (heartClip == null || coinClip == null || stuntClip == null || musicClip == null)
        {
            Debug.LogError($"[SetupAudio] Missing one or more clips under {AudioFolder} " +
                "(heart_collect.wav / coin_collect.wav / stunt_sting.wav / background_music.mp3) -- " +
                "generate them first (generate_sfx/generate_music), then re-run.");
            return;
        }

        int pickupsWired = WirePickups(heartClip, coinClip);
        int stuntsWired = WireStuntDetectors(stuntClip);
        bool musicWired = WireBackgroundMusic(musicClip);

        Debug.Log($"[SetupAudio] Wired {pickupsWired} pickup(s), {stuntsWired} stunt detector(s), background music: {musicWired}.");
    }

    // Wires SfxOneShot + the right clip onto the Heart/Coin prefab assets
    // (so every instance scattered by SetupPickups inherits it for free),
    // and defensively onto any Pickup already sitting directly in an open
    // scene in case it isn't a prefab instance.
    private static int WirePickups(AudioClip heartClip, AudioClip coinClip)
    {
        int wired = 0;
        wired += WirePickupPrefab($"{PickupPrefabFolder}/Heart.prefab", heartClip);
        wired += WirePickupPrefab($"{PickupPrefabFolder}/Coin.prefab", coinClip);

        foreach (var pickup in Object.FindObjectsOfType<Pickup>())
        {
            var typeProp = new SerializedObject(pickup).FindProperty("pickupType");
            var clip = (PickupType)typeProp.enumValueIndex == PickupType.Heart ? heartClip : coinClip;
            WireSfxOneShot(pickup.gameObject, clip, useUndo: true);
            wired++;
        }
        return wired;
    }

    private static int WirePickupPrefab(string path, AudioClip clip)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null) return 0;

        var contents = PrefabUtility.LoadPrefabContents(path);
        WireSfxOneShot(contents, clip, useUndo: false);
        PrefabUtility.SaveAsPrefabAsset(contents, path);
        PrefabUtility.UnloadPrefabContents(contents);
        return 1;
    }

    // Adds StuntSfx (which pulls in SfxOneShot via RequireComponent) to
    // every cart that already has a StuntDetector, so the sting plays no
    // matter which creature (if any) is riding that cart.
    private static int WireStuntDetectors(AudioClip stuntClip)
    {
        int wired = 0;
        foreach (var detector in Object.FindObjectsOfType<StuntDetector>())
        {
            if (detector.GetComponent<StuntSfx>() == null)
                Undo.AddComponent<StuntSfx>(detector.gameObject);

            WireSfxOneShot(detector.gameObject, stuntClip, useUndo: true);
            wired++;
        }
        return wired;
    }

    // One persistent AudioSource for background music, added to Main Camera
    // (already always-present in SampleScene) rather than a new dedicated
    // GameObject, since nothing else needs it.
    private static bool WireBackgroundMusic(AudioClip musicClip)
    {
        GameObject host = GameObject.Find(MusicHostName);
        if (host == null)
        {
            Debug.LogWarning($"[SetupAudio] No \"{MusicHostName}\" found in the open scene -- skipped background music.");
            return false;
        }

        var source = host.GetComponent<AudioSource>();
        if (source == null) source = Undo.AddComponent<AudioSource>(host);

        source.clip = musicClip;
        source.loop = true;
        source.playOnAwake = true;
        source.spatialBlend = 0f; // 2D: music shouldn't attenuate with camera position

        EditorSceneManager.MarkSceneDirty(host.scene);
        return true;
    }

    // Ensures `go` has an SfxOneShot pointed at `clip`, adding the component
    // if needed. useUndo is false while editing detached prefab contents,
    // where there's no scene undo stack to record against.
    private static void WireSfxOneShot(GameObject go, AudioClip clip, bool useUndo)
    {
        var sfx = go.GetComponent<SfxOneShot>();
        if (sfx == null) sfx = useUndo ? Undo.AddComponent<SfxOneShot>(go) : go.AddComponent<SfxOneShot>();

        var so = new SerializedObject(sfx);
        so.FindProperty("clip").objectReferenceValue = clip;
        so.ApplyModifiedProperties();
    }
}
