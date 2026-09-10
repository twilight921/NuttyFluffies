using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;

// One-shot editor utility: builds the in-ride HUD -- a "HUD Canvas" with a
// top-left Hearts label and a top-right Coins label, wired up via a RideHud
// component. Must run with SampleScene open/active. Idempotent: reuses the
// existing "HUD Canvas"/labels/component in place rather than duplicating.
//
// Named "HUD Canvas" deliberately (not "Canvas"/"GarageCanvas") to stay
// distinct from the "Results Canvas" a parallel agent is adding to the same
// scene. Likewise only creates an EventSystem if none exists yet, since that
// other agent's script may add one first.
public static class SetupRideHud
{
    private const string CanvasName = "HUD Canvas";

    [MenuItem("NuttyFluffies/Setup Ride HUD")]
    public static void Execute()
    {
        EnsureEventSystem();

        Transform canvas = EnsureCanvas();
        TMP_Text heartsLabel = EnsureLabel(canvas, "HeartsLabel", topLeft: true, initialText: "Hearts: 0");
        TMP_Text coinsLabel = EnsureLabel(canvas, "CoinsLabel", topLeft: false, initialText: "Coins: 0");

        EnsureRideHud(canvas, heartsLabel, coinsLabel);

        EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
        Debug.Log($"[SetupRideHud] Ride HUD built: \"{CanvasName}\" with Hearts/Coins labels and a RideHud component wired up.");
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

    // --- Hearts / Coins labels -------------------------------------------

    private static TMP_Text EnsureLabel(Transform canvas, string name, bool topLeft, string initialText)
    {
        Transform existing = canvas.Find(name);
        if (existing != null) return existing.GetComponent<TMP_Text>();

        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(canvas, false);

        var rect = (RectTransform)go.transform;
        float x = topLeft ? 0f : 1f;
        rect.anchorMin = new Vector2(x, 1f);
        rect.anchorMax = new Vector2(x, 1f);
        rect.pivot = new Vector2(x, 1f);
        rect.anchoredPosition = new Vector2(topLeft ? 40f : -40f, -30f);
        rect.sizeDelta = new Vector2(400f, 60f);

        var label = go.AddComponent<TextMeshProUGUI>();
        label.alignment = topLeft ? TextAlignmentOptions.Left : TextAlignmentOptions.Right;
        label.fontSize = 36f;
        label.color = topLeft ? new Color(0.9f, 0.2f, 0.3f) : new Color(0.95f, 0.8f, 0.15f);
        label.text = initialText;

        return label;
    }

    // --- RideHud component ------------------------------------------------

    private static void EnsureRideHud(Transform canvas, TMP_Text heartsLabel, TMP_Text coinsLabel)
    {
        var hud = canvas.GetComponent<RideHud>();
        if (hud == null) hud = Undo.AddComponent<RideHud>(canvas.gameObject);

        var serialized = new SerializedObject(hud);
        serialized.FindProperty("heartsLabel").objectReferenceValue = heartsLabel;
        serialized.FindProperty("coinsLabel").objectReferenceValue = coinsLabel;
        serialized.ApplyModifiedProperties();
    }
}
