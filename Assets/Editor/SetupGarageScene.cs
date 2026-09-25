using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;

// One-shot editor utility: builds the entire Garage (shop/loadout) scene UI --
// TMP essentials import, EventSystem (new Input System UI module), Canvas,
// sky background, top bar (Back / Coins / title / level picker), the 6 train
// slots, the power-up picker (Rocket / Jump Jet / Magnet -- the train's one
// special cart), the scrollable creature list, a bottom bar with a status
// strip + Start Ride button, and the GarageController tying it all together.
// Must run with Garage.unity open/active.
//
// Idempotent REBUILD: every run deletes the previously-built UI elements by
// name and recreates them with the current layout/style, so re-running after
// a layout change upgrades an existing scene in place. The controller and the
// level picker (SetupLevelSelectUI, invoked at the end) are re-wired each run.
//
// Layout (1920x1080 reference, top-anchored bands):
//   TopBar        0..100    Back | Coins pill | "Garage" | level picker
//   Train         108..386  header + 6 slots
//   Power-Up      396..512  header + 3 picker cards
//   Creatures     524..950  header + scrollable card grid
//   BottomBar     last 120  status strip | Start Ride
public static class SetupGarageScene
{
    private const string CreatureRosterPath = "Assets/ScriptableObjects/Creatures/CreatureRoster.asset";
    private const string CardPrefabPath = "Assets/MyPrefabs/CreatureCard.prefab";
    private const string SkyBgPath = "Assets/Art/Background/sky_bg.png";

    private static readonly string[] SlotRoleNames =
        { "Lead", "Power-Up", "Plain", "Plain", "Plain", "Plain" };

    private static readonly PowerUpType[] PowerUpChoices =
        { PowerUpType.Rocket, PowerUpType.JumpJet, PowerUpType.Magnet };

    // Names this builder owns (removed + recreated every run).
    private static readonly string[] OwnedElements =
    {
        "Background", "TopBar", "TrainHeader", "TrainSlotsPanel", "PowerUpHeader", "PowerUpPanel",
        "CreatureHeader", "CreatureListPanel", "BottomBar",
        // Pre-rework top-level elements, so an old scene upgrades cleanly.
        "CoinsLabel", "StartRideButton",
    };

    [MenuItem("NuttyFluffies/Setup Garage Scene")]
    public static void Execute()
    {
        EnsureTmpEssentials();
        EnsureEventSystem();

        Transform canvas = EnsureCanvas();
        foreach (string owned in OwnedElements) MenuUiKit.Remove(canvas, owned);

        EnsureBackground(canvas);
        BuildTopBar(canvas, out Button backButton, out TMP_Text coinsLabel);
        TrainSlotUI[] slots = BuildTrainSection(canvas);
        PowerUpCardUI[] powerUpCards = BuildPowerUpSection(canvas);
        Transform listContent = BuildCreatureSection(canvas);
        CreatureCardUI cardPrefab = EnsureCreatureCardPrefab();
        BuildBottomBar(canvas, out Button startButton, out UIStatusMessage status);

        var roster = AssetDatabase.LoadAssetAtPath<CreatureRoster>(CreatureRosterPath);
        if (roster == null)
            Debug.LogError($"[SetupGarageScene] Missing {CreatureRosterPath} -- run NuttyFluffies/Setup Creature Roster first.");

        EnsureGarageController(roster, listContent, cardPrefab, slots, powerUpCards, coinsLabel, startButton, backButton, status);

        // Re-place/restyle the level picker inside the fresh top bar.
        SetupLevelSelectUI.Execute();

        EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
        Debug.Log("[SetupGarageScene] Garage scene rebuilt: background, top bar (back/coins/level picker), 6 train slots, power-up picker, creature list, status strip, start-ride button, GarageController wired. Save the scene.");
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

    // --- Background (same sky as the Main Menu) ---------------------------

    private static void EnsureBackground(Transform canvas)
    {
        GameObject go = MenuUiKit.Child(canvas, "Background");
        MenuUiKit.Fill(go);
        var bg = MenuUiKit.AddImage(go, Color.white, raycast: false);
        bg.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SkyBgPath);
        bg.preserveAspect = false;
        go.transform.SetAsFirstSibling();
    }

    // --- Top bar: Back | Coins | title | (level picker, added by SetupLevelSelectUI)

    private static void BuildTopBar(Transform canvas, out Button backButton, out TMP_Text coinsLabel)
    {
        GameObject bar = MenuUiKit.Child(canvas, "TopBar");
        MenuUiKit.TopBand(bar, 0f, 100f);
        MenuUiKit.AddImage(bar, MenuUiKit.Panel);

        backButton = MenuUiKit.MakeButton(bar.transform, "BackButton", "< Menu", MenuUiKit.Slate, 30f, out GameObject backGO);
        MenuUiKit.Place(backGO, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(30f, 0f), new Vector2(170f, 64f));

        GameObject pill = MenuUiKit.Child(bar.transform, "CoinsPill");
        MenuUiKit.Place(pill, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(230f, 0f), new Vector2(290f, 64f));
        MenuUiKit.AddImage(pill, MenuUiKit.PanelDark, raycast: false);
        GameObject coinsGO = MenuUiKit.Child(pill.transform, "CoinsLabel");
        MenuUiKit.Fill(coinsGO, 12f, 0f, 12f, 0f);
        coinsLabel = MenuUiKit.AddLabel(coinsGO, "Coins: 0", 36f, MenuUiKit.Gold, TextAlignmentOptions.Center, FontStyles.Bold);

        GameObject title = MenuUiKit.Child(bar.transform, "TitleLabel");
        MenuUiKit.Place(title, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-110f, 0f), new Vector2(420f, 64f));
        MenuUiKit.AddLabel(title, "GARAGE", 52f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
    }

    // --- Section header helper ---------------------------------------------

    private static void BuildHeader(Transform canvas, string name, string text, float y)
    {
        GameObject go = MenuUiKit.Child(canvas, name);
        MenuUiKit.TopBand(go, y, 34f, 40f);
        MenuUiKit.AddLabel(go, text, 26f, Color.white, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
    }

    // --- 6 fixed train slots -------------------------------------------

    private static TrainSlotUI[] BuildTrainSection(Transform canvas)
    {
        BuildHeader(canvas, "TrainHeader", "YOUR TRAIN", 108f);

        GameObject panel = MenuUiKit.Child(canvas, "TrainSlotsPanel");
        MenuUiKit.TopBand(panel, 146f, 240f, 40f);
        var layout = panel.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 24f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        var slots = new TrainSlotUI[6];
        for (int i = 0; i < slots.Length; i++)
        {
            GameObject slotGO = MenuUiKit.Child(panel.transform, $"Slot{i}");
            ((RectTransform)slotGO.transform).sizeDelta = new Vector2(190f, 232f);

            var bg = MenuUiKit.AddImage(slotGO, MenuUiKit.Card);
            var button = slotGO.AddComponent<Button>();
            button.targetGraphic = bg;
            MenuUiKit.StyleButton(button);
            MenuUiKit.AddSelectionOutline(slotGO, 3f);

            // Accent strip: gold for the lead cart, the power-up colour-ish blue for the special cart.
            GameObject accent = MenuUiKit.Child(slotGO.transform, "RoleAccent");
            MenuUiKit.Place(accent, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 8f));
            MenuUiKit.AddImage(accent, i == 0 ? MenuUiKit.Gold : i == 1 ? new Color(0.35f, 0.6f, 1f) : new Color(0.7f, 0.72f, 0.78f), raycast: false);

            GameObject roleGO = MenuUiKit.Child(slotGO.transform, "RoleLabel");
            MenuUiKit.Place(roleGO, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Vector2(-16f, 32f));
            var roleLabel = MenuUiKit.AddLabel(roleGO, $"{i + 1}. {SlotRoleNames[i]}", 24f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);

            GameObject iconGO = MenuUiKit.Child(slotGO.transform, "Icon");
            MenuUiKit.Place(iconGO, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -54f), new Vector2(120f, 120f));
            var icon = MenuUiKit.AddImage(iconGO, Color.white, raycast: false);
            icon.preserveAspect = true;

            GameObject nameGO = MenuUiKit.Child(slotGO.transform, "NameLabel");
            MenuUiKit.Place(nameGO, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Vector2(-16f, 36f));
            var nameLabel = MenuUiKit.AddLabel(nameGO, "Empty", 26f, new Color(0.85f, 0.88f, 0.95f), TextAlignmentOptions.Center);
            MenuUiKit.AutoSize(nameLabel, 16f, 26f);

            var slotUI = slotGO.AddComponent<TrainSlotUI>();
            var so = new SerializedObject(slotUI);
            MenuUiKit.Wire(so, "icon", icon);
            MenuUiKit.Wire(so, "roleLabel", roleLabel);
            MenuUiKit.Wire(so, "button", button);
            MenuUiKit.Wire(so, "nameLabel", nameLabel);
            so.ApplyModifiedProperties();

            slots[i] = slotUI;
        }

        return slots;
    }

    // --- Power-up picker (the train's one special cart) -----------------

    private static PowerUpCardUI[] BuildPowerUpSection(Transform canvas)
    {
        BuildHeader(canvas, "PowerUpHeader", "POWER-UP CART", 396f);

        GameObject panel = MenuUiKit.Child(canvas, "PowerUpPanel");
        MenuUiKit.TopBand(panel, 434f, 80f, 40f);
        var layout = panel.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 24f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        var cards = new PowerUpCardUI[PowerUpChoices.Length];
        for (int i = 0; i < PowerUpChoices.Length; i++)
        {
            PowerUpType type = PowerUpChoices[i];
            GameObject cardGO = MenuUiKit.Child(panel.transform, $"PowerUp_{type}");
            ((RectTransform)cardGO.transform).sizeDelta = new Vector2(250f, 76f);

            var bg = MenuUiKit.AddImage(cardGO, MenuUiKit.Card);
            var button = cardGO.AddComponent<Button>();
            button.targetGraphic = bg;
            MenuUiKit.StyleButton(button);
            MenuUiKit.AddSelectionOutline(cardGO, 4f);

            GameObject swatchGO = MenuUiKit.Child(cardGO.transform, "Swatch");
            MenuUiKit.Place(swatchGO, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(16f, 0f), new Vector2(44f, 44f));
            var swatch = MenuUiKit.AddImage(swatchGO, Color.white, raycast: false);

            GameObject nameGO = MenuUiKit.Child(cardGO.transform, "NameLabel");
            MenuUiKit.Fill(nameGO, 74f, 0f, 10f, 0f);
            var nameLabel = MenuUiKit.AddLabel(nameGO, type.ToString(), 28f, Color.white, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);

            GameObject highlightGO = MenuUiKit.Child(cardGO.transform, "SelectedHighlight");
            MenuUiKit.Fill(highlightGO);
            MenuUiKit.AddImage(highlightGO, new Color(1f, 0.9f, 0.3f, 0.18f), raycast: false);
            highlightGO.SetActive(false);

            var cardUI = cardGO.AddComponent<PowerUpCardUI>();
            var so = new SerializedObject(cardUI);
            so.FindProperty("powerUp").enumValueIndex = (int)type;
            MenuUiKit.Wire(so, "swatch", swatch);
            MenuUiKit.Wire(so, "nameLabel", nameLabel);
            MenuUiKit.Wire(so, "button", button);
            MenuUiKit.Wire(so, "selectedHighlight", highlightGO);
            so.ApplyModifiedProperties();

            cards[i] = cardUI;
        }

        return cards;
    }

    // --- Creature list panel (ScrollRect) --------------------------------

    private static Transform BuildCreatureSection(Transform canvas)
    {
        BuildHeader(canvas, "CreatureHeader", "CREATURES", 524f);

        GameObject panel = MenuUiKit.Child(canvas, "CreatureListPanel");
        // Below the header, above the 120px bottom bar (+10px gap each side).
        MenuUiKit.Fill(panel, 40f, 130f, 40f, 566f);
        MenuUiKit.AddImage(panel, MenuUiKit.Panel);
        var scrollRect = panel.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 30f;

        GameObject viewport = MenuUiKit.Child(panel.transform, "Viewport");
        var viewportRect = MenuUiKit.Fill(viewport);
        viewport.AddComponent<RectMask2D>();

        GameObject content = MenuUiKit.Child(viewport.transform, "Content");
        var contentRect = MenuUiKit.Place(content, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, Vector2.zero);

        var grid = content.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(200f, 300f);
        grid.spacing = new Vector2(18f, 18f);
        grid.padding = new RectOffset(18, 18, 14, 14);
        grid.childAlignment = TextAnchor.UpperCenter;

        var fitter = content.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        // Slim scrollbar on the right edge so overflow is discoverable.
        GameObject sb = MenuUiKit.Child(panel.transform, "Scrollbar");
        var sbRect = (RectTransform)sb.transform;
        sbRect.anchorMin = new Vector2(1f, 0f);
        sbRect.anchorMax = new Vector2(1f, 1f);
        sbRect.pivot = new Vector2(1f, 0.5f);
        sbRect.offsetMin = new Vector2(-16f, 8f);
        sbRect.offsetMax = new Vector2(-4f, -8f);
        var trackImage = MenuUiKit.AddImage(sb, new Color(0f, 0f, 0f, 0.3f));
        var scrollbar = sb.AddComponent<Scrollbar>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;

        GameObject sliding = MenuUiKit.Child(sb.transform, "SlidingArea");
        MenuUiKit.Fill(sliding);
        GameObject handle = MenuUiKit.Child(sliding.transform, "Handle");
        MenuUiKit.Fill(handle);
        var handleImage = MenuUiKit.AddImage(handle, new Color(1f, 1f, 1f, 0.55f));
        scrollbar.handleRect = (RectTransform)handle.transform;
        scrollbar.targetGraphic = handleImage;

        scrollRect.viewport = viewportRect;
        scrollRect.content = contentRect;
        scrollRect.verticalScrollbar = scrollbar;
        scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
        scrollRect.verticalScrollbarSpacing = -3f;

        return content.transform;
    }

    // --- Creature card prefab -------------------------------------------
    // Rebuilt and re-saved over the existing prefab every run (GUID is
    // preserved by SaveAsPrefabAsset), so card layout changes propagate.

    private static CreatureCardUI EnsureCreatureCardPrefab()
    {
        EnsureFolder("Assets/MyPrefabs");

        GameObject root = new GameObject("CreatureCard", typeof(RectTransform));
        ((RectTransform)root.transform).sizeDelta = new Vector2(200f, 300f);

        var bg = MenuUiKit.AddImage(root, MenuUiKit.Card);
        var button = root.AddComponent<Button>();
        button.targetGraphic = bg;
        MenuUiKit.StyleButton(button);
        MenuUiKit.AddSelectionOutline(root, 4f);

        // Tier strip (colour set from CreatureDefinition.tier at bind time)
        GameObject tierBarGO = MenuUiKit.Child(root.transform, "TierBar");
        MenuUiKit.Place(tierBarGO, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 10f));
        var tierBar = MenuUiKit.AddImage(tierBarGO, Color.white, raycast: false);

        GameObject iconGO = MenuUiKit.Child(root.transform, "Icon");
        MenuUiKit.Place(iconGO, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -26f), new Vector2(150f, 150f));
        var icon = MenuUiKit.AddImage(iconGO, Color.white, raycast: false);
        icon.preserveAspect = true;

        GameObject nameGO = MenuUiKit.Child(root.transform, "NameLabel");
        MenuUiKit.Place(nameGO, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -184f), new Vector2(-16f, 36f));
        var nameLabel = MenuUiKit.AddLabel(nameGO, "Name", 28f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
        MenuUiKit.AutoSize(nameLabel, 16f, 28f);

        GameObject tierLabelGO = MenuUiKit.Child(root.transform, "TierLabel");
        MenuUiKit.Place(tierLabelGO, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -222f), new Vector2(-16f, 26f));
        var tierLabel = MenuUiKit.AddLabel(tierLabelGO, "Common", 20f, Color.white, TextAlignmentOptions.Center);

        // State pill ("OWNED" / "Buy: 300")
        GameObject pill = MenuUiKit.Child(root.transform, "StatePill");
        MenuUiKit.Place(pill, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 12f), new Vector2(-24f, 40f));
        MenuUiKit.AddImage(pill, MenuUiKit.PanelDark, raycast: false);
        GameObject stateGO = MenuUiKit.Child(pill.transform, "StateLabel");
        MenuUiKit.Fill(stateGO, 6f, 0f, 6f, 0f);
        var stateLabel = MenuUiKit.AddLabel(stateGO, "0", 24f, MenuUiKit.Gold, TextAlignmentOptions.Center, FontStyles.Bold);

        // Selected tint (full-card overlay, inactive by default); the outline
        // on the root does the heavy lifting.
        GameObject highlightGO = MenuUiKit.Child(root.transform, "SelectedHighlight");
        MenuUiKit.Fill(highlightGO);
        MenuUiKit.AddImage(highlightGO, new Color(1f, 0.9f, 0.3f, 0.14f), raycast: false);
        highlightGO.SetActive(false);

        var cardUI = root.AddComponent<CreatureCardUI>();
        var so = new SerializedObject(cardUI);
        MenuUiKit.Wire(so, "icon", icon);
        MenuUiKit.Wire(so, "nameLabel", nameLabel);
        MenuUiKit.Wire(so, "stateLabel", stateLabel);
        MenuUiKit.Wire(so, "button", button);
        MenuUiKit.Wire(so, "selectedHighlight", highlightGO);
        MenuUiKit.Wire(so, "tierLabel", tierLabel);
        MenuUiKit.Wire(so, "tierBar", tierBar);
        so.ApplyModifiedProperties();

        GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(root, CardPrefabPath);
        Object.DestroyImmediate(root);

        return savedPrefab.GetComponent<CreatureCardUI>();
    }

    // --- Bottom bar: status strip + Start Ride -----------------------------

    private static void BuildBottomBar(Transform canvas, out Button startButton, out UIStatusMessage status)
    {
        GameObject bar = MenuUiKit.Child(canvas, "BottomBar");
        MenuUiKit.Place(bar, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(0f, 120f));
        MenuUiKit.AddImage(bar, MenuUiKit.Panel, raycast: false);

        GameObject statusGO = MenuUiKit.Child(bar.transform, "StatusLabel");
        MenuUiKit.Fill(statusGO, 40f, 0f, 400f, 0f);
        var statusLabel = MenuUiKit.AddLabel(statusGO, "", 32f, Color.white, TextAlignmentOptions.MidlineLeft);
        MenuUiKit.AutoSize(statusLabel, 20f, 32f);
        status = statusGO.AddComponent<UIStatusMessage>();
        var so = new SerializedObject(status);
        MenuUiKit.Wire(so, "label", statusLabel);
        so.FindProperty("idleText").stringValue = "Pick a creature, then tap a train cart to place it.";
        so.ApplyModifiedProperties();

        startButton = MenuUiKit.MakeButton(bar.transform, "StartRideButton", "Start Ride", MenuUiKit.Green, 40f, out GameObject startGO);
        MenuUiKit.Place(startGO, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-40f, 0f), new Vector2(320f, 84f));
    }

    // --- GarageController -------------------------------------------------

    private static void EnsureGarageController(CreatureRoster roster, Transform listContent, CreatureCardUI cardPrefab,
        TrainSlotUI[] slots, PowerUpCardUI[] powerUpCards, TMP_Text coinsLabel, Button startButton,
        Button backButton, UIStatusMessage status)
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
        serialized.FindProperty("backButton").objectReferenceValue = backButton;
        serialized.FindProperty("statusMessage").objectReferenceValue = status;
        serialized.FindProperty("coasterSceneName").stringValue = "SampleScene";
        serialized.FindProperty("menuSceneName").stringValue = "MainMenu";
        serialized.ApplyModifiedProperties();
    }

    // --- Helpers -----------------------------------------------------------

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parentPath = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
        string leaf = System.IO.Path.GetFileName(path);
        if (!AssetDatabase.IsValidFolder(parentPath)) EnsureFolder(parentPath);
        AssetDatabase.CreateFolder(parentPath, leaf);
    }
}
