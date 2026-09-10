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
        Button playButton = EnsurePlayButton(canvas);
        Button quitButton = EnsureQuitButton(canvas);
        EnsureMainMenuController(menuScene, playButton, quitButton);

        EditorSceneManager.MarkSceneDirty(menuScene);
        EditorSceneManager.SaveScene(menuScene, ScenePath);

        if (previousActive.IsValid()) EditorSceneManager.SetActiveScene(previousActive);
        EditorSceneManager.CloseScene(menuScene, true);

        Debug.Log("[SetupMainMenuScene] Main Menu scene built: EventSystem, MainMenuCanvas, background, title, cart decoration, Play/Quit buttons, MainMenuController all wired. Caller's active scene left untouched.");
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

    private static void EnsureTitle(Transform canvas)
    {
        Transform existing = canvas.Find("TitleText");
        GameObject go;
        TMP_Text label;
        if (existing == null)
        {
            go = CreateChildRect(canvas, "TitleText");
            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -80f);
            rect.sizeDelta = new Vector2(1400f, 220f);

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

        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = 96f;
        label.fontStyle = FontStyles.Bold;
        label.color = new Color(1f, 0.85f, 0.15f); // same gold as Garage's Coins/cost labels
        label.text = "Nutty Fluffies\nRollercoaster";

        var bob = go.GetComponent<FloatBob>();
        if (bob == null) bob = go.AddComponent<FloatBob>();

        EnsureSubtitle(canvas);
    }

    private static void EnsureSubtitle(Transform canvas)
    {
        Transform existing = canvas.Find("SubtitleText");
        GameObject go;
        if (existing == null)
        {
            go = CreateChildRect(canvas, "SubtitleText");
            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -300f);
            rect.sizeDelta = new Vector2(900f, 60f);
            go.AddComponent<TextMeshProUGUI>();
        }
        else
        {
            go = existing.gameObject;
        }

        var label = go.GetComponent<TextMeshProUGUI>();
        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = 32f;
        label.fontStyle = FontStyles.Italic;
        label.color = Color.white;
        label.text = "Tap Play to start your ride!";
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
            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, 60f);
            rect.sizeDelta = new Vector2(360f, 360f);
            rect.localRotation = Quaternion.Euler(0f, 0f, -8f);

            icon = go.AddComponent<Image>();
            icon.preserveAspect = true;
            icon.raycastTarget = false;
        }
        else
        {
            go = existing.gameObject;
            icon = go.GetComponent<Image>();
        }

        icon.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(CartPath);

        var bob = go.GetComponent<FloatBob>();
        if (bob == null) bob = go.AddComponent<FloatBob>();
    }

    // --- Play button -------------------------------------------------------

    private static Button EnsurePlayButton(Transform canvas)
    {
        Transform existing = canvas.Find("PlayButton");
        if (existing != null) return existing.GetComponent<Button>();

        GameObject go = CreateChildRect(canvas, "PlayButton");
        var rect = (RectTransform)go.transform;
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(0f, 220f);
        rect.sizeDelta = new Vector2(420f, 110f);

        var bg = go.AddComponent<Image>();
        bg.color = new Color(0.2f, 0.75f, 0.3f); // same green as Garage's start-ride button
        var button = go.AddComponent<Button>();
        button.targetGraphic = bg;

        GameObject labelGO = CreateChildRect(go.transform, "Label");
        var labelRect = (RectTransform)labelGO.transform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
        var label = labelGO.AddComponent<TextMeshProUGUI>();
        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = 44f;
        label.fontStyle = FontStyles.Bold;
        label.color = Color.white;
        label.text = "Play";

        return button;
    }

    // --- Quit button -------------------------------------------------------

    private static Button EnsureQuitButton(Transform canvas)
    {
        Transform existing = canvas.Find("QuitButton");
        if (existing != null) return existing.GetComponent<Button>();

        GameObject go = CreateChildRect(canvas, "QuitButton");
        var rect = (RectTransform)go.transform;
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(0f, 90f);
        rect.sizeDelta = new Vector2(300f, 80f);

        var bg = go.AddComponent<Image>();
        bg.color = new Color(0.85f, 0.25f, 0.2f);
        var button = go.AddComponent<Button>();
        button.targetGraphic = bg;

        GameObject labelGO = CreateChildRect(go.transform, "Label");
        var labelRect = (RectTransform)labelGO.transform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
        var label = labelGO.AddComponent<TextMeshProUGUI>();
        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = 32f;
        label.fontStyle = FontStyles.Bold;
        label.color = Color.white;
        label.text = "Quit";

        return button;
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
