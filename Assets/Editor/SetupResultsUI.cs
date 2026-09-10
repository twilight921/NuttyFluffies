using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Splines;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;

// One-shot editor utility: builds the post-ride results overlay -- a
// "Results Canvas" with a dimmed panel (Score/Coins labels + Continue
// button), wired to a ResultsController -- and adds RunEndTrigger to the
// lead cart so the panel has something to listen to. Must run with
// SampleScene open/active. Idempotent: reuses the existing "Results Canvas"/
// panel/labels/button/components in place rather than duplicating.
//
// Named "Results Canvas" deliberately (not "Canvas"/"GarageCanvas") to stay
// distinct from the "HUD Canvas" a parallel agent is adding to the same
// scene. Likewise only creates an EventSystem if none exists yet, since that
// other agent's script may add one first.
public static class SetupResultsUI
{
    private const string CanvasName = "Results Canvas";
    private const string LeadCartName = "Cart";
    private const string TrackName = "CoasterLineRender";

    [MenuItem("NuttyFluffies/Setup Results UI")]
    public static void Execute()
    {
        EnsureEventSystem();

        Transform canvas = EnsureCanvas();
        GameObject panel = EnsurePanel(canvas);
        TMP_Text scoreLabel = EnsureLabel(panel.transform, "ScoreLabel", yPos: 40f, initialText: "Score: 0");
        TMP_Text coinsLabel = EnsureLabel(panel.transform, "CoinsLabel", yPos: -20f, initialText: "Coins: 0");
        Button continueButton = EnsureContinueButton(panel.transform);

        RunEndTrigger runEndTrigger = EnsureRunEndTrigger();

        EnsureResultsController(canvas, panel, runEndTrigger, scoreLabel, coinsLabel, continueButton);

        EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
        Debug.Log("[SetupResultsUI] Results overlay built: \"Results Canvas\" (hidden panel with Score/Coins labels + Continue button), RunEndTrigger on the lead cart, ResultsController wired.");
    }

    // --- EventSystem --------------------------------------------------

    private static void EnsureEventSystem()
    {
        if (Object.FindObjectOfType<EventSystem>() != null) return; // a parallel setup script may already own one

        GameObject go = new GameObject("EventSystem");
        Undo.RegisterCreatedObjectUndo(go, "Create EventSystem");
        go.AddComponent<EventSystem>();
        var inputModule = go.AddComponent<InputSystemUIInputModule>();
        inputModule.AssignDefaultActions();
    }

    // --- Canvas ---------------------------------------------------------

    private static Transform EnsureCanvas()
    {
        GameObject go = GameObject.Find(CanvasName);
        if (go == null)
        {
            go = new GameObject(CanvasName);
            Undo.RegisterCreatedObjectUndo(go, $"Create {CanvasName}");
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

    // --- Dimmed results panel -------------------------------------------

    private static GameObject EnsurePanel(Transform canvas)
    {
        Transform existing = canvas.Find("ResultsPanel");
        if (existing != null) return existing.gameObject;

        GameObject panel = new GameObject("ResultsPanel", typeof(RectTransform));
        panel.transform.SetParent(canvas, false);
        var rect = (RectTransform)panel.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        var dim = panel.AddComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.6f);

        // ResultsController explicitly SetActive(false)s this at runtime
        // (Awake), so leaving it active here in the authored scene is fine --
        // it also means the panel is visible/editable in the Scene view.

        return panel;
    }

    // --- Score / Coins labels -------------------------------------------

    private static TMP_Text EnsureLabel(Transform panel, string name, float yPos, string initialText)
    {
        Transform existing = panel.Find(name);
        if (existing != null) return existing.GetComponent<TMP_Text>();

        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(panel, false);

        var rect = (RectTransform)go.transform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, yPos);
        rect.sizeDelta = new Vector2(600f, 60f);

        var label = go.AddComponent<TextMeshProUGUI>();
        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = 42f;
        label.color = Color.white;
        label.text = initialText;

        return label;
    }

    // --- Continue button --------------------------------------------------

    private static Button EnsureContinueButton(Transform panel)
    {
        Transform existing = panel.Find("ContinueButton");
        if (existing != null) return existing.GetComponent<Button>();

        GameObject go = new GameObject("ContinueButton", typeof(RectTransform));
        go.transform.SetParent(panel, false);

        var rect = (RectTransform)go.transform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, -140f);
        rect.sizeDelta = new Vector2(300f, 80f);

        var bg = go.AddComponent<Image>();
        bg.color = new Color(0.2f, 0.75f, 0.3f);
        var button = go.AddComponent<Button>();
        button.targetGraphic = bg;

        GameObject labelGO = new GameObject("Label", typeof(RectTransform));
        labelGO.transform.SetParent(go.transform, false);
        var labelRect = (RectTransform)labelGO.transform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
        var label = labelGO.AddComponent<TextMeshProUGUI>();
        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = 28f;
        label.color = Color.white;
        label.text = "Continue";

        return button;
    }

    // --- RunEndTrigger on the lead cart -----------------------------------

    private static RunEndTrigger EnsureRunEndTrigger()
    {
        GameObject leadCart = GameObject.Find(LeadCartName);
        if (leadCart == null)
        {
            Debug.LogError($"[SetupResultsUI] Missing {LeadCartName} in scene -- can't add RunEndTrigger.");
            return null;
        }

        GameObject trackGO = GameObject.Find(TrackName);
        SplineContainer trackSpline = trackGO != null ? trackGO.GetComponent<SplineContainer>() : null;
        if (trackSpline == null)
            Debug.LogError($"[SetupResultsUI] Missing {TrackName} SplineContainer -- RunEndTrigger won't be able to detect the end of the track.");

        var trigger = leadCart.GetComponent<RunEndTrigger>();
        if (trigger == null) trigger = Undo.AddComponent<RunEndTrigger>(leadCart);

        var serialized = new SerializedObject(trigger);
        serialized.FindProperty("trackSpline").objectReferenceValue = trackSpline;
        serialized.ApplyModifiedProperties();

        return trigger;
    }

    // --- ResultsController ------------------------------------------------

    private static void EnsureResultsController(Transform canvas, GameObject panel, RunEndTrigger runEndTrigger,
        TMP_Text scoreLabel, TMP_Text coinsLabel, Button continueButton)
    {
        var controller = canvas.GetComponent<ResultsController>();
        if (controller == null) controller = Undo.AddComponent<ResultsController>(canvas.gameObject);

        var serialized = new SerializedObject(controller);
        serialized.FindProperty("runEndTrigger").objectReferenceValue = runEndTrigger;
        serialized.FindProperty("resultsPanel").objectReferenceValue = panel;
        serialized.FindProperty("scoreLabel").objectReferenceValue = scoreLabel;
        serialized.FindProperty("coinsLabel").objectReferenceValue = coinsLabel;
        serialized.FindProperty("continueButton").objectReferenceValue = continueButton;
        serialized.FindProperty("garageSceneName").stringValue = "Garage";
        serialized.ApplyModifiedProperties();
    }
}
