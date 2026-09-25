using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;

// One-shot editor utility: builds the Main Menu scene -- EventSystem, Canvas,
// sky/circus background, title, a bobbing cart decoration, Play/Quit buttons,
// and the MainMenuController tying it together. Mirrors SetupGarageScene.cs's
// shape (built directly with UnityEngine.UI/TMPro APIs in one execute_script
// pass rather than individual Coplay MCP UI-tool calls).
//
// Built to run ADDITIVELY alongside whatever scene is currently open and
// active in the Editor (e.g. another agent's SampleScene work-in-progress):
// it opens/creates MainMenu.unity as an additive scene, temporarily makes
// IT the active scene only so new GameObjects land there instead of the
// caller's scene, then restores the original active scene and closes
// MainMenu.unity again before returning. The caller's open/active scene is
// never touched, never marked dirty, and never saved. Idempotent: reuses
// existing GameObjects/assets in place rather than duplicating.
public static class SetupMainMenuScene
{
    private const string ScenePath = "Assets/Scenes/MainMenu.unity";
    private const string SkyBgPath = "Assets/Art/Background/sky_bg.png";
    private const string CartPath = "Assets/Art/Carts/cart_cute.png";

    [MenuItem("NuttyFluffies/Setup Main Menu Scene")]
    public static void Execute()
    {
        Scene previousActive = EditorSceneManager.GetActiveScene();

        Scene menuScene = OpenOrCreateMenuScene();
        EditorSceneManager.SetActiveScene(menuScene);

        EnsureEventSystem(menuScene);
        Transform canvas = EnsureCanvas(menuScene);
        EnsureBackground(canvas);
        EnsureTitle(canvas);
        EnsureCartDecoration(canvas);
        BuildButtonColumn(canvas, out Button playButton, out Button quitButton);
        EnsureMainMenuController(menuScene, playButton, quitButton);

        EditorSceneManager.MarkSceneDirty(menuScene);
        EditorSceneManager.SaveScene(menuScene, ScenePath);

        if (previousActive.IsValid()) EditorSceneManager.SetActiveScene(previousActive);
        EditorSceneManager.CloseScene(menuScene, true);

        Debug.Log("[SetupMainMenuScene] Main Menu scene built: EventSystem, MainMenuCanvas, background, title, cart decoration, aligned Play/Quit button column, MainMenuController all wired. Caller's active scene left untouched.");
    }

    // --- Scene open/create -------------------------------------------------

    private static Scene OpenOrCreateMenuScene()
    {
        if (System.IO.File.Exists(ScenePath))
            return EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);

        // DefaultGameObjects (Main Camera + Directional Light) matches how
        // Garage.unity was set up before SetupGarageScene.cs ran on it.
        return EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Additive);
    }

    // --- Scene-scoped lookup helper -----------------------------------------
    // Deliberately not GameObject.Find/FindObjectOfType: those search across
    // ALL loaded scenes, which would find another open scene's EventSystem
    // (e.g. SampleScene's) and skip creating one here -- leaving this scene
    // without its own EventSystem when it's loaded standalone at runtime.

    private static GameObject FindInScene(Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
            if (root.name == name) return root;
        return null;
    }

    // --- EventSystem ------------------------------------------------------

    private static void EnsureEventSystem(Scene scene)
    {
        GameObject go = FindInScene(scene, "EventSystem");
        if (go == null)
        {
            go = new GameObject("EventSystem");
            Undo.RegisterCreatedObjectUndo(go, "Create EventSystem");
        }

        if (go.GetComponent<EventSystem>() == null) Undo.AddComponent<EventSystem>(go);

        var inputModule = go.GetComponent<InputSystemUIInputModule>();
        if (inputModule == null) inputModule = Undo.AddComponent<InputSystemUIInputModule>(go);
        inputModule.AssignDefaultActions();
    }

    // --- Canvas -------------------------------------------------------

    private static Transform EnsureCanvas(Scene scene)
    {
        GameObject go = FindInScene(scene, "MainMenuCanvas");
        if (go == null)
        {
            go = new GameObject("MainMenuCanvas");
            Undo.RegisterCreatedObjectUndo(go, "Create MainMenuCanvas");
        }

        var canvas = go.GetComponent<Canvas>();
        if (canvas == null) canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        var scaler = go.GetComponent<CanvasScaler>();
        if (scaler == null) scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        if (go.GetComponent<GraphicRaycaster>() == null) go.AddComponent<GraphicRaycaster>();

        return go.transform;
    }

    // --- Background -----------------------------------------------------

    private static void EnsureBackground(Transform canvas)
    {
        Transform existing = canvas.Find("Background");
        Image bg;
        if (existing == null)
        {
            GameObject go = CreateChildRect(canvas, "Background");
            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            bg = go.AddComponent<Image>();
            bg.raycastTarget = false;
            go.transform.SetAsFirstSibling();
        }
        else
        {
            bg = existing.GetComponent<Image>();
        }

        bg.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SkyBgPath);
        bg.type = Image.Type.Simple;
        bg.preserveAspect = false;
    }

    // --- Title -------------------------------------------------------------
    // Layout (1920x1080 reference): title top-centre, subtitle under it, cart
    // in the middle, Play/Quit column pinned to the bottom. Applied every run
    // (not just on creation) so re-running upgrades an existing scene.

    private static void EnsureTitle(Transform canvas)
    {
        Transform existing = canvas.Find("TitleText");
        GameObject go;
        TMP_Text label;
        if (existing == null)
        {
            go = CreateChildRect(canvas, "TitleText");
            label = go.AddComponent<TextMeshProUGUI>();
            var outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(0.35f, 0.15f, 0.05f, 1f);
            outline.effectDistance = new Vector2(4f, -4f);
        }
        else
        {
            go = existing.gameObject;
            label = go.GetComponent<TMP_Text>();
        }

        MenuUiKit.Place(go, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -60f), new Vector2(1500f, 260f));

        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = 100f;
        label.fontStyle = FontStyles.Bold;
        MenuUiKit.AutoSize(label, 60f, 100f); // two lines must fit the 260px box
        label.color = MenuUiKit.Gold; // same gold as Garage's Coins/cost labels
        label.text = "Nutty Fluffies\nRollercoaster";

        var bob = go.GetComponent<FloatBob>();
        if (bob == null) bob = go.AddComponent<FloatBob>();

        EnsureSubtitle(canvas);
    }

    private static void EnsureSubtitle(Transform canvas)
    {
        Transform existing = canvas.Find("SubtitleText");
        GameObject go = existing != null ? existing.gameObject : CreateChildRect(canvas, "SubtitleText");

        MenuUiKit.Place(go, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -340f), new Vector2(1100f, 60f));

        var label = go.GetComponent<TextMeshProUGUI>();
        if (label == null) label = go.AddComponent<TextMeshProUGUI>();
        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = 36f;
        label.fontStyle = FontStyles.Italic;
        label.color = Color.white;
        label.raycastTarget = false;
        label.text = "Build your fluffy train and ride!";
    }

    // --- Cart decoration -------------------------------------------------

    private static void EnsureCartDecoration(Transform canvas)
    {
        Transform existing = canvas.Find("CartDecoration");
        GameObject go;
        Image icon;
        if (existing == null)
        {
            go = CreateChildRect(canvas, "CartDecoration");
            icon = go.AddComponent<Image>();
            icon.preserveAspect = true;
            icon.raycastTarget = false;
        }
        else
        {
            go = existing.gameObject;
            icon = go.GetComponent<Image>();
        }

        // Centred in the gap between the subtitle (ends ~y=400 from top) and the button column.
        MenuUiKit.Place(go, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -60f), new Vector2(340f, 340f));
        go.transform.localRotation = Quaternion.Euler(0f, 0f, -8f);

        icon.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(CartPath);

        // Draw order follows sibling order: keep the cart right above the
        // Background so the title/subtitle/buttons always render on top of it.
        go.transform.SetSiblingIndex(1);

        var bob = go.GetComponent<FloatBob>();
        if (bob == null) bob = go.AddComponent<FloatBob>();

        // Half a cycle out of phase with the title so they don't move in lockstep.
        var bobSO = new SerializedObject(bob);
        bobSO.FindProperty("phase").floatValue = Mathf.PI;
        bobSO.ApplyModifiedProperties();
    }

    // --- Play / Quit column ------------------------------------------------
    // One VerticalLayoutGroup so both buttons share the same centre line and
    // width (they used to be different widths at hand-placed offsets). Rebuilt
    // every run; buttons left over at the canvas root by older builds are removed.

    private static void BuildButtonColumn(Transform canvas, out Button playButton, out Button quitButton)
    {
        MenuUiKit.Remove(canvas, "ButtonColumn");
        MenuUiKit.Remove(canvas, "PlayButton");
        MenuUiKit.Remove(canvas, "QuitButton");

        GameObject column = CreateChildRect(canvas, "ButtonColumn");
        MenuUiKit.Place(column, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 70f), new Vector2(460f, 240f));
        var layout = column.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 24f;
        layout.childAlignment = TextAnchor.LowerCenter;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        playButton = MenuUiKit.MakeButton(column.transform, "PlayButton", "Play", MenuUiKit.Green, 52f, out GameObject playGO);
        ((RectTransform)playGO.transform).sizeDelta = new Vector2(460f, 116f);

        quitButton = MenuUiKit.MakeButton(column.transform, "QuitButton", "Quit", MenuUiKit.Red, 34f, out GameObject quitGO);
        ((RectTransform)quitGO.transform).sizeDelta = new Vector2(460f, 84f);
    }

    // --- MainMenuController -------------------------------------------------

    private static void EnsureMainMenuController(Scene scene, Button playButton, Button quitButton)
    {
        GameObject go = FindInScene(scene, "MainMenuController");
        if (go == null)
        {
            go = new GameObject("MainMenuController");
            Undo.RegisterCreatedObjectUndo(go, "Create MainMenuController");
        }

        var controller = go.GetComponent<MainMenuController>();
        if (controller == null) controller = Undo.AddComponent<MainMenuController>(go);

        var serialized = new SerializedObject(controller);
        serialized.FindProperty("playButton").objectReferenceValue = playButton;
        serialized.FindProperty("quitButton").objectReferenceValue = quitButton;
        serialized.FindProperty("playSceneName").stringValue = "Garage";
        serialized.ApplyModifiedProperties();
    }

    // --- Helpers -----------------------------------------------------------

    private static GameObject CreateChildRect(Transform parent, string name)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }
}
