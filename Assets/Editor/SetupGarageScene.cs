using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;

// One-shot editor utility: builds the entire Garage (shop/loadout) scene UI --
// TMP essentials import, EventSystem (new Input System UI module), Canvas,
// creature card prefab, scrollable creature list, 6 fixed train slots, the
// power-up picker (Rocket / Jump Jet / Magnet -- the train's one special
// cart), coins label, start-ride button, and the GarageController tying it
// all together. Must run with Garage.unity open/active. Idempotent: reuses
// existing GameObjects/assets in place rather than duplicating.
//
// Built directly with UnityEngine.UI/TMPro APIs in one execute_script pass
// (rather than a sequence of individual Coplay MCP UI-tool calls) because the
// whole thing already has to run as a single editor script for the
// prefab-saving/SerializedObject-wiring/TMP-import/EventSystem steps the MCP
// UI tools don't cover, and the Coplay `create_scene` tool was unreliable
// (timed out repeatedly) earlier in this same build -- keeping scene
// construction in plain, inspectable C# avoids depending on it further.
public static class SetupGarageScene
{
    private const string CreatureRosterPath = "Assets/ScriptableObjects/Creatures/CreatureRoster.asset";
    private const string CardPrefabPath = "Assets/MyPrefabs/CreatureCard.prefab";

    private static readonly string[] SlotRoleNames =
        { "Lead", "Power-Up", "Plain", "Plain", "Plain", "Plain" };

    private static readonly PowerUpType[] PowerUpChoices =
        { PowerUpType.Rocket, PowerUpType.JumpJet, PowerUpType.Magnet };

    [MenuItem("NuttyFluffies/Setup Garage Scene")]
    public static void Execute()
    {
        EnsureTmpEssentials();
        EnsureEventSystem();

        Transform canvas = EnsureCanvas();
        CreatureCardUI cardPrefab = EnsureCreatureCardPrefab();
        Transform listContent = EnsureCreatureListPanel(canvas);
        TrainSlotUI[] slots = EnsureTrainSlots(canvas);
        PowerUpCardUI[] powerUpCards = EnsurePowerUpPicker(canvas);
        TMP_Text coinsLabel = EnsureCoinsLabel(canvas);
        Button startButton = EnsureStartRideButton(canvas);

        var roster = AssetDatabase.LoadAssetAtPath<CreatureRoster>(CreatureRosterPath);
        if (roster == null)
            Debug.LogError($"[SetupGarageScene] Missing {CreatureRosterPath} -- run NuttyFluffies/Setup Creature Roster first.");

        EnsureGarageController(roster, listContent, cardPrefab, slots, powerUpCards, coinsLabel, startButton);

        EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
        Debug.Log("[SetupGarageScene] Garage scene built: EventSystem, GarageCanvas, CreatureCard prefab, creature list, 6 train slots, power-up picker (Rocket/Jump Jet/Magnet), coins label, start-ride button, GarageController all wired.");
    }

    // --- TMP essentials -------------------------------------------------

    private static void EnsureTmpEssentials()
    {
        if (AssetDatabase.IsValidFolder("Assets/TextMesh Pro"))
        {
            Debug.Log("[SetupGarageScene] TMP Essentials already imported.");
            return;
        }
        AssetDatabase.ImportPackage("Packages/com.unity.textmeshpro/Package Resources/TMP Essential Resources.unitypackage", false);
        AssetDatabase.Refresh();
        Debug.Log("[SetupGarageScene] Imported TMP Essential Resources.");
    }

    // --- EventSystem ------------------------------------------------------

    private static void EnsureEventSystem()
    {
        GameObject go = GameObject.Find("EventSystem");
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

    private static Transform EnsureCanvas()
    {
        GameObject go = GameObject.Find("GarageCanvas");
        if (go == null)
        {
            go = new GameObject("GarageCanvas");
            Undo.RegisterCreatedObjectUndo(go, "Create GarageCanvas");
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

    // --- Creature card prefab -------------------------------------------

    private static CreatureCardUI EnsureCreatureCardPrefab()
    {
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(CardPrefabPath);
        if (existing != null)
        {
            var existingComp = existing.GetComponent<CreatureCardUI>();
            if (existingComp != null) return existingComp;
        }

        EnsureFolder("Assets/MyPrefabs");

        GameObject root = new GameObject("CreatureCard", typeof(RectTransform));
        var rootRect = (RectTransform)root.transform;
        rootRect.sizeDelta = new Vector2(200f, 260f);

        var bg = root.AddComponent<Image>();
        bg.color = new Color(0.85f, 0.85f, 0.85f, 1f);
        var button = root.AddComponent<Button>();
        button.targetGraphic = bg;

        // Icon
        GameObject iconGO = CreateChildRect(root.transform, "Icon");
        var iconRect = (RectTransform)iconGO.transform;
        iconRect.anchorMin = new Vector2(0.5f, 1f);
        iconRect.anchorMax = new Vector2(0.5f, 1f);
        iconRect.pivot = new Vector2(0.5f, 1f);
        iconRect.anchoredPosition = new Vector2(0f, -16f);
        iconRect.sizeDelta = new Vector2(120f, 120f);
        var icon = iconGO.AddComponent<Image>();
        icon.preserveAspect = true;

        // Name label
        GameObject nameGO = CreateChildRect(root.transform, "NameLabel");
        var nameRect = (RectTransform)nameGO.transform;
        nameRect.anchorMin = new Vector2(0.5f, 1f);
        nameRect.anchorMax = new Vector2(0.5f, 1f);
        nameRect.pivot = new Vector2(0.5f, 1f);
        nameRect.anchoredPosition = new Vector2(0f, -150f);
        nameRect.sizeDelta = new Vector2(190f, 36f);
        var nameLabel = nameGO.AddComponent<TextMeshProUGUI>();
        nameLabel.alignment = TextAlignmentOptions.Center;
        nameLabel.fontSize = 24f;
        nameLabel.text = "Name";

        // State label ("Owned" or cost)
        GameObject stateGO = CreateChildRect(root.transform, "StateLabel");
        var stateRect = (RectTransform)stateGO.transform;
        stateRect.anchorMin = new Vector2(0.5f, 1f);
        stateRect.anchorMax = new Vector2(0.5f, 1f);
        stateRect.pivot = new Vector2(0.5f, 1f);
        stateRect.anchoredPosition = new Vector2(0f, -190f);
        stateRect.sizeDelta = new Vector2(190f, 30f);
        var stateLabel = stateGO.AddComponent<TextMeshProUGUI>();
        stateLabel.alignment = TextAlignmentOptions.Center;
        stateLabel.fontSize = 20f;
        stateLabel.color = new Color(0.9f, 0.75f, 0.1f);
        stateLabel.text = "0";

        // Selected highlight (full-card overlay, inactive by default)
        GameObject highlightGO = CreateChildRect(root.transform, "SelectedHighlight");
        var highlightRect = (RectTransform)highlightGO.transform;
        highlightRect.anchorMin = Vector2.zero;
        highlightRect.anchorMax = Vector2.one;
        highlightRect.offsetMin = Vector2.zero;
        highlightRect.offsetMax = Vector2.zero;
        var highlightImage = highlightGO.AddComponent<Image>();
        highlightImage.color = new Color(1f, 0.9f, 0.2f, 0.35f);
        highlightGO.transform.SetSiblingIndex(0); // behind icon/labels
        highlightGO.SetActive(false);

        var cardUI = root.AddComponent<CreatureCardUI>();
        var serialized = new SerializedObject(cardUI);
        serialized.FindProperty("icon").objectReferenceValue = icon;
        serialized.FindProperty("nameLabel").objectReferenceValue = nameLabel;
        serialized.FindProperty("stateLabel").objectReferenceValue = stateLabel;
        serialized.FindProperty("button").objectReferenceValue = button;
        serialized.FindProperty("selectedHighlight").objectReferenceValue = highlightGO;
        serialized.ApplyModifiedProperties();

        GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(root, CardPrefabPath);
        Object.DestroyImmediate(root);

        return savedPrefab.GetComponent<CreatureCardUI>();
    }

    // --- Creature list panel (ScrollRect) --------------------------------

    private static Transform EnsureCreatureListPanel(Transform canvas)
    {
        Transform existingContent = canvas.Find("CreatureListPanel/Viewport/Content");
        if (existingContent != null) return existingContent;

        GameObject panel = CreateChildRect(canvas, "CreatureListPanel");
        var panelRect = (RectTransform)panel.transform;
        panelRect.anchorMin = new Vector2(0.03f, 0.06f);
        panelRect.anchorMax = new Vector2(0.97f, 0.62f);
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;
        var panelBg = panel.AddComponent<Image>();
        panelBg.color = new Color(0f, 0f, 0f, 0.15f);
        var scrollRect = panel.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;

        GameObject viewport = CreateChildRect(panel.transform, "Viewport");
        var viewportRect = (RectTransform)viewport.transform;
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = Vector2.zero;
        viewportRect.offsetMax = Vector2.zero;
        viewport.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.01f);
        viewport.AddComponent<RectMask2D>();

        GameObject content = CreateChildRect(viewport.transform, "Content");
        var contentRect = (RectTransform)content.transform;
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = new Vector2(0f, 0f);

        var grid = content.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(200f, 260f);
        grid.spacing = new Vector2(20f, 20f);
        grid.padding = new RectOffset(10, 10, 10, 10);
        grid.childAlignment = TextAnchor.UpperLeft;

        var fitter = content.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        scrollRect.viewport = viewportRect;
        scrollRect.content = contentRect;

        return content.transform;
    }

    // --- 6 fixed train slots -------------------------------------------

    private static TrainSlotUI[] EnsureTrainSlots(Transform canvas)
    {
        Transform panelTransform = canvas.Find("TrainSlotsPanel");
        GameObject panel;
        if (panelTransform == null)
        {
            panel = CreateChildRect(canvas, "TrainSlotsPanel");
            var panelRect = (RectTransform)panel.transform;
            panelRect.anchorMin = new Vector2(0.03f, 0.68f);
            panelRect.anchorMax = new Vector2(0.97f, 0.9f);
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;

            var layout = panel.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 20f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
        }
        else
        {
            panel = panelTransform.gameObject;
        }

        var slots = new TrainSlotUI[6];
        for (int i = 0; i < 6; i++)
        {
            string slotName = $"Slot{i}";
            Transform existingSlot = panel.transform.Find(slotName);
            GameObject slotGO;
            if (existingSlot == null)
            {
                slotGO = CreateChildRect(panel.transform, slotName);
                var slotRect = (RectTransform)slotGO.transform;
                slotRect.sizeDelta = new Vector2(160f, 220f);

                var bg = slotGO.AddComponent<Image>();
                bg.color = new Color(0.7f, 0.8f, 0.9f, 1f);
                var button = slotGO.AddComponent<Button>();
                button.targetGraphic = bg;

                GameObject iconGO = CreateChildRect(slotGO.transform, "Icon");
                var iconRect = (RectTransform)iconGO.transform;
                iconRect.anchorMin = new Vector2(0.5f, 1f);
                iconRect.anchorMax = new Vector2(0.5f, 1f);
                iconRect.pivot = new Vector2(0.5f, 1f);
                iconRect.anchoredPosition = new Vector2(0f, -14f);
                iconRect.sizeDelta = new Vector2(110f, 110f);
                var icon = iconGO.AddComponent<Image>();
                icon.preserveAspect = true;

                GameObject roleGO = CreateChildRect(slotGO.transform, "RoleLabel");
                var roleRect = (RectTransform)roleGO.transform;
                roleRect.anchorMin = new Vector2(0.5f, 1f);
                roleRect.anchorMax = new Vector2(0.5f, 1f);
                roleRect.pivot = new Vector2(0.5f, 1f);
                roleRect.anchoredPosition = new Vector2(0f, -140f);
                roleRect.sizeDelta = new Vector2(150f, 60f);
                var roleLabel = roleGO.AddComponent<TextMeshProUGUI>();
                roleLabel.alignment = TextAlignmentOptions.Center;
                roleLabel.fontSize = 20f;
                roleLabel.text = SlotRoleNames[i];

                var slotUI = slotGO.AddComponent<TrainSlotUI>();
                var serialized = new SerializedObject(slotUI);
                serialized.FindProperty("icon").objectReferenceValue = icon;
                serialized.FindProperty("roleLabel").objectReferenceValue = roleLabel;
                serialized.FindProperty("button").objectReferenceValue = button;
                serialized.ApplyModifiedProperties();
            }
            else
            {
                slotGO = existingSlot.gameObject;
            }

            // Refresh the role label on every run (not just creation) so a
            // slot built by an earlier version keeps up with SlotRoleNames.
            var existingRole = slotGO.transform.Find("RoleLabel")?.GetComponent<TMP_Text>();
            if (existingRole != null) existingRole.text = SlotRoleNames[i];

            slots[i] = slotGO.GetComponent<TrainSlotUI>();
        }

        return slots;
    }

    // --- Power-up picker (the train's one special cart) -----------------

    private static PowerUpCardUI[] EnsurePowerUpPicker(Transform canvas)
    {
        Transform panelTransform = canvas.Find("PowerUpPanel");
        GameObject panel;
        if (panelTransform == null)
        {
            panel = CreateChildRect(canvas, "PowerUpPanel");
            var panelRect = (RectTransform)panel.transform;
            panelRect.anchorMin = new Vector2(0.35f, 0.905f);
            panelRect.anchorMax = new Vector2(0.97f, 0.99f);
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;

            var layout = panel.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 16f;
            layout.childAlignment = TextAnchor.MiddleRight;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
        }
        else
        {
            panel = panelTransform.gameObject;
        }

        var cards = new PowerUpCardUI[PowerUpChoices.Length];
        for (int i = 0; i < PowerUpChoices.Length; i++)
        {
            PowerUpType type = PowerUpChoices[i];
            string cardName = $"PowerUp_{type}";
            Transform existing = panel.transform.Find(cardName);
            GameObject cardGO;
            if (existing == null)
            {
                cardGO = CreateChildRect(panel.transform, cardName);
                var cardRect = (RectTransform)cardGO.transform;
                cardRect.sizeDelta = new Vector2(150f, 84f);

                var bg = cardGO.AddComponent<Image>();
                bg.color = new Color(0.82f, 0.82f, 0.82f, 1f);
                var button = cardGO.AddComponent<Button>();
                button.targetGraphic = bg;

                GameObject swatchGO = CreateChildRect(cardGO.transform, "Swatch");
                var swatchRect = (RectTransform)swatchGO.transform;
                swatchRect.anchorMin = new Vector2(0f, 0.5f);
                swatchRect.anchorMax = new Vector2(0f, 0.5f);
                swatchRect.pivot = new Vector2(0f, 0.5f);
                swatchRect.anchoredPosition = new Vector2(12f, 0f);
                swatchRect.sizeDelta = new Vector2(40f, 40f);
                var swatch = swatchGO.AddComponent<Image>();

                GameObject nameGO = CreateChildRect(cardGO.transform, "NameLabel");
                var nameRect = (RectTransform)nameGO.transform;
                nameRect.anchorMin = new Vector2(0f, 0f);
                nameRect.anchorMax = new Vector2(1f, 1f);
                nameRect.offsetMin = new Vector2(60f, 0f);
                nameRect.offsetMax = new Vector2(-8f, 0f);
                var nameLabel = nameGO.AddComponent<TextMeshProUGUI>();
                nameLabel.alignment = TextAlignmentOptions.Left;
                nameLabel.fontSize = 20f;
                nameLabel.color = Color.black;
                nameLabel.text = type.ToString();

                GameObject highlightGO = CreateChildRect(cardGO.transform, "SelectedHighlight");
                var highlightRect = (RectTransform)highlightGO.transform;
                highlightRect.anchorMin = Vector2.zero;
                highlightRect.anchorMax = Vector2.one;
                highlightRect.offsetMin = Vector2.zero;
                highlightRect.offsetMax = Vector2.zero;
                var highlightImage = highlightGO.AddComponent<Image>();
                highlightImage.color = new Color(1f, 0.9f, 0.2f, 0.35f);
                highlightImage.raycastTarget = false;
                highlightGO.transform.SetSiblingIndex(0); // behind swatch/label
                highlightGO.SetActive(false);

                var cardUI = cardGO.AddComponent<PowerUpCardUI>();
                var serialized = new SerializedObject(cardUI);
                serialized.FindProperty("powerUp").enumValueIndex = (int)type;
                serialized.FindProperty("swatch").objectReferenceValue = swatch;
                serialized.FindProperty("nameLabel").objectReferenceValue = nameLabel;
                serialized.FindProperty("button").objectReferenceValue = button;
                serialized.FindProperty("selectedHighlight").objectReferenceValue = highlightGO;
                serialized.ApplyModifiedProperties();
            }
            else
            {
                cardGO = existing.gameObject;
            }

            cards[i] = cardGO.GetComponent<PowerUpCardUI>();
        }

        return cards;
    }

    // --- Coins label -----------------------------------------------------

    private static TMP_Text EnsureCoinsLabel(Transform canvas)
    {
        Transform existing = canvas.Find("CoinsLabel");
        if (existing != null) return existing.GetComponent<TMP_Text>();

        GameObject go = CreateChildRect(canvas, "CoinsLabel");
        var rect = (RectTransform)go.transform;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(40f, -30f);
        rect.sizeDelta = new Vector2(400f, 60f);

        var label = go.AddComponent<TextMeshProUGUI>();
        label.alignment = TextAlignmentOptions.Left;
        label.fontSize = 36f;
        label.color = new Color(0.95f, 0.8f, 0.15f);
        label.text = "Coins: 0";

        return label;
    }

    // --- Start ride button -------------------------------------------------

    private static Button EnsureStartRideButton(Transform canvas)
    {
        Transform existing = canvas.Find("StartRideButton");
        if (existing != null) return existing.GetComponent<Button>();

        GameObject go = CreateChildRect(canvas, "StartRideButton");
        var rect = (RectTransform)go.transform;
        rect.anchorMin = new Vector2(1f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(1f, 0f);
        rect.anchoredPosition = new Vector2(-40f, 40f);
        rect.sizeDelta = new Vector2(300f, 80f);

        var bg = go.AddComponent<Image>();
        bg.color = new Color(0.2f, 0.75f, 0.3f);
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
        label.fontSize = 28f;
        label.color = Color.white;
        label.text = "Start Ride";

        return button;
    }

    // --- GarageController -------------------------------------------------

    private static void EnsureGarageController(CreatureRoster roster, Transform listContent, CreatureCardUI cardPrefab,
        TrainSlotUI[] slots, PowerUpCardUI[] powerUpCards, TMP_Text coinsLabel, Button startButton)
    {
        GameObject go = GameObject.Find("GarageController");
        if (go == null)
        {
            go = new GameObject("GarageController");
            Undo.RegisterCreatedObjectUndo(go, "Create GarageController");
        }

        var controller = go.GetComponent<GarageController>();
        if (controller == null) controller = Undo.AddComponent<GarageController>(go);

        var serialized = new SerializedObject(controller);
        serialized.FindProperty("creatureRoster").objectReferenceValue = roster;
        serialized.FindProperty("creatureListContent").objectReferenceValue = listContent;
        serialized.FindProperty("creatureCardPrefab").objectReferenceValue = cardPrefab;

        var slotsProp = serialized.FindProperty("trainSlots");
        slotsProp.arraySize = slots.Length;
        for (int i = 0; i < slots.Length; i++)
            slotsProp.GetArrayElementAtIndex(i).objectReferenceValue = slots[i];

        var powerUpProp = serialized.FindProperty("powerUpCards");
        powerUpProp.arraySize = powerUpCards.Length;
        for (int i = 0; i < powerUpCards.Length; i++)
            powerUpProp.GetArrayElementAtIndex(i).objectReferenceValue = powerUpCards[i];

        serialized.FindProperty("coinsLabel").objectReferenceValue = coinsLabel;
        serialized.FindProperty("startRideButton").objectReferenceValue = startButton;
        serialized.FindProperty("coasterSceneName").stringValue = "SampleScene";
        serialized.ApplyModifiedProperties();
    }

    // --- Helpers -----------------------------------------------------------

    private static GameObject CreateChildRect(Transform parent, string name)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parentPath = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
        string leaf = System.IO.Path.GetFileName(path);
        if (!AssetDatabase.IsValidFolder(parentPath)) EnsureFolder(parentPath);
        AssetDatabase.CreateFolder(parentPath, leaf);
    }
}
