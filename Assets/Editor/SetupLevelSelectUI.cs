using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;

// One-shot editor utility: adds the level picker (◀  World N  ▶) to the Garage
// scene UI and wires a LevelSelectUI to it, pointed at LevelCatalog.asset.
// Sits in the free top-left strip above the creature list so it doesn't
// collide with the existing coins label / power-up picker / train slots.
//
// Must run with Garage.unity open/active (needs the GarageCanvas that
// SetupGarageScene builds). Idempotent: reuses the LevelSelectPanel in place.
public static class SetupLevelSelectUI
{
    private const string CatalogPath = "Assets/ScriptableObjects/Levels/LevelCatalog.asset";

    [MenuItem("NuttyFluffies/Setup Level Select UI")]
    public static void Execute()
    {
        GameObject canvasGO = GameObject.Find("GarageCanvas");
        if (canvasGO == null)
        {
            Debug.LogError("[SetupLevelSelectUI] No GarageCanvas -- open Garage.unity and run 'NuttyFluffies/Setup Garage Scene' first.");
            return;
        }
        Transform canvas = canvasGO.transform;

        var catalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>(CatalogPath);
        if (catalog == null)
            Debug.LogWarning($"[SetupLevelSelectUI] {CatalogPath} not found -- run 'NuttyFluffies/Generate Level Batch'. Wiring the picker with a null catalog for now.");

        Transform existing = canvas.Find("LevelSelectPanel");
        GameObject panel = existing != null ? existing.gameObject : CreateChildRect(canvas, "LevelSelectPanel");

        // Centered bar in the empty strip between the creature list (top 0.62)
        // and the train slots (bottom 0.68) -- clears the coins label and
        // power-up picker in the top bar.
        var panelRect = (RectTransform)panel.transform;
        panelRect.anchorMin = new Vector2(0.5f, 0.65f);
        panelRect.anchorMax = new Vector2(0.5f, 0.65f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(720f, 56f);
        panelRect.anchoredPosition = Vector2.zero;

        var panelBg = panel.GetComponent<Image>();
        if (panelBg == null) panelBg = panel.AddComponent<Image>();
        panelBg.color = new Color(0f, 0f, 0f, 0.15f);

        Button prev = EnsureButton(panel.transform, "PrevButton", "<", pinLeft: true);
        Button next = EnsureButton(panel.transform, "NextButton", ">", pinLeft: false);
        TMP_Text label = EnsureLabel(panel.transform, "LevelLabel");

        var ui = panel.GetComponent<LevelSelectUI>();
        if (ui == null) ui = Undo.AddComponent<LevelSelectUI>(panel);

        var so = new SerializedObject(ui);
        so.FindProperty("catalog").objectReferenceValue = catalog;
        so.FindProperty("prevButton").objectReferenceValue = prev;
        so.FindProperty("nextButton").objectReferenceValue = next;
        so.FindProperty("label").objectReferenceValue = label;
        so.ApplyModifiedProperties();

        EditorSceneManager.MarkSceneDirty(canvasGO.scene);
        Debug.Log("[SetupLevelSelectUI] Level picker added to the Garage. Save the scene.");
    }

    private static Button EnsureButton(Transform parent, string name, string glyph, bool pinLeft)
    {
        Transform existing = parent.Find(name);
        GameObject go = existing != null ? existing.gameObject : CreateChildRect(parent, name);

        const float width = 80f;
        var rect = (RectTransform)go.transform;
        rect.anchorMin = new Vector2(pinLeft ? 0f : 1f, 0f);
        rect.anchorMax = new Vector2(pinLeft ? 0f : 1f, 1f);
        rect.offsetMin = pinLeft ? new Vector2(8f, 6f) : new Vector2(-width - 8f, 6f);
        rect.offsetMax = pinLeft ? new Vector2(width + 8f, -6f) : new Vector2(-8f, -6f);

        var bg = go.GetComponent<Image>();
        if (bg == null) bg = go.AddComponent<Image>();
        bg.color = new Color(0.7f, 0.8f, 0.9f, 1f);

        var button = go.GetComponent<Button>();
        if (button == null) button = go.AddComponent<Button>();
        button.targetGraphic = bg;

        Transform labelT = go.transform.Find("Label");
        GameObject labelGO = labelT != null ? labelT.gameObject : CreateChildRect(go.transform, "Label");
        var labelRect = (RectTransform)labelGO.transform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
        var text = labelGO.GetComponent<TextMeshProUGUI>();
        if (text == null) text = labelGO.AddComponent<TextMeshProUGUI>();
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = 28f;
        text.color = Color.black;
        text.text = glyph;

        return button;
    }

    private static TMP_Text EnsureLabel(Transform parent, string name)
    {
        Transform existing = parent.Find(name);
        GameObject go = existing != null ? existing.gameObject : CreateChildRect(parent, name);

        var rect = (RectTransform)go.transform;
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.offsetMin = new Vector2(100f, 0f);
        rect.offsetMax = new Vector2(-100f, 0f);

        var text = go.GetComponent<TextMeshProUGUI>();
        if (text == null) text = go.AddComponent<TextMeshProUGUI>();
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = 20f;
        text.enableAutoSizing = true;
        text.fontSizeMin = 12f;
        text.fontSizeMax = 22f;
        text.color = Color.white;
        text.text = "Level";

        return text;
    }

    private static GameObject CreateChildRect(Transform parent, string name)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        return go;
    }
}
