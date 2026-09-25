using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;

// One-shot editor utility: adds the level picker (<  World N  >) to the Garage
// scene UI and wires a LevelSelectUI to it, pointed at LevelCatalog.asset.
// Sits at the right end of the Garage's top bar (TopBar, built by
// SetupGarageScene) so it lines up with the Back button / Coins pill; falls
// back to the canvas root if no top bar exists yet.
//
// Must run with Garage.unity open/active (needs the GarageCanvas that
// SetupGarageScene builds). SetupGarageScene calls this at the end of its own
// run. Idempotent: the panel is rebuilt in place each run.
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

        // Rebuild from scratch so restyling/relayout always applies.
        MenuUiKit.Remove(canvas, "LevelSelectPanel");

        Transform topBar = MenuUiKit.FindDeep(canvas, "TopBar");
        Transform parent = topBar != null ? topBar : canvas;

        GameObject panel = MenuUiKit.Child(parent, "LevelSelectPanel");
        Undo.RegisterCreatedObjectUndo(panel, "Create LevelSelectPanel");

        // Right-aligned inside the top bar; vertically centred on it.
        if (topBar != null)
            MenuUiKit.Place(panel, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-30f, 0f), new Vector2(640f, 64f));
        else
            MenuUiKit.Place(panel, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-30f, -18f), new Vector2(640f, 64f));
        MenuUiKit.AddImage(panel, MenuUiKit.PanelDark);

        Button prev = MakeArrowButton(panel.transform, "PrevButton", "<", pinLeft: true);
        Button next = MakeArrowButton(panel.transform, "NextButton", ">", pinLeft: false);

        GameObject labelGO = MenuUiKit.Child(panel.transform, "LevelLabel");
        MenuUiKit.Fill(labelGO, 84f, 0f, 84f, 0f);
        TMP_Text label = MenuUiKit.AddLabel(labelGO, "Level", 26f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
        MenuUiKit.AutoSize(label, 14f, 26f);

        var ui = panel.AddComponent<LevelSelectUI>();
        var so = new SerializedObject(ui);
        so.FindProperty("catalog").objectReferenceValue = catalog;
        so.FindProperty("prevButton").objectReferenceValue = prev;
        so.FindProperty("nextButton").objectReferenceValue = next;
        so.FindProperty("label").objectReferenceValue = label;
        so.ApplyModifiedProperties();

        EditorSceneManager.MarkSceneDirty(canvasGO.scene);
        Debug.Log("[SetupLevelSelectUI] Level picker added to the Garage. Save the scene.");
    }

    private static Button MakeArrowButton(Transform parent, string name, string glyph, bool pinLeft)
    {
        Button button = MenuUiKit.MakeButton(parent, name, glyph, MenuUiKit.Slate, 34f, out GameObject go);

        // Square-ish arrow pinned to one end, inset 6px from the panel edge.
        const float width = 72f;
        var rect = (RectTransform)go.transform;
        rect.anchorMin = new Vector2(pinLeft ? 0f : 1f, 0f);
        rect.anchorMax = new Vector2(pinLeft ? 0f : 1f, 1f);
        rect.pivot = new Vector2(pinLeft ? 0f : 1f, 0.5f);
        rect.sizeDelta = new Vector2(width, -12f);
        rect.anchoredPosition = new Vector2(pinLeft ? 6f : -6f, 0f);
        return button;
    }
}
