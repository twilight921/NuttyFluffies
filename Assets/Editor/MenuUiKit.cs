using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;

// Shared look-and-feel + layout helpers for the Main Menu / Garage / Level
// Select builder scripts, so all three use one palette, one button style and
// the same anchoring helpers instead of three slightly-different copies.
public static class MenuUiKit
{
    public static readonly Color Panel = new Color(0.08f, 0.11f, 0.2f, 0.72f);
    public static readonly Color PanelDark = new Color(0f, 0f, 0f, 0.35f);
    public static readonly Color Card = new Color(0.17f, 0.22f, 0.34f, 1f);
    public static readonly Color Slate = new Color(0.32f, 0.42f, 0.6f, 1f);
    public static readonly Color Green = new Color(0.2f, 0.75f, 0.3f, 1f);
    public static readonly Color Red = new Color(0.85f, 0.25f, 0.2f, 1f);
    public static readonly Color Gold = new Color(1f, 0.85f, 0.15f, 1f);
    public static readonly Color SelectionOutline = new Color(1f, 0.9f, 0.3f, 1f);

    public static GameObject Child(Transform parent, string name)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    // Recursive lookup (Transform.Find only sees direct children).
    public static Transform FindDeep(Transform root, string name)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            if (t != root && t.name == name) return t;
        return null;
    }

    // Removes a previously-built element (wherever it sits) so the builder can
    // recreate it with the current layout. Returns whether anything was removed.
    public static bool Remove(Transform root, string name)
    {
        Transform t = FindDeep(root, name);
        if (t == null) return false;
        Object.DestroyImmediate(t.gameObject);
        return true;
    }

    // Anchored placement: anchors, pivot, position relative to the anchors, size.
    public static RectTransform Place(GameObject go, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPos, Vector2 size)
    {
        var r = (RectTransform)go.transform;
        r.anchorMin = anchorMin;
        r.anchorMax = anchorMax;
        r.pivot = pivot;
        r.anchoredPosition = anchoredPos;
        r.sizeDelta = size;
        return r;
    }

    // Full-stretch with insets: left, bottom, right, top (all positive = inward).
    public static RectTransform Fill(GameObject go, float left = 0f, float bottom = 0f, float right = 0f, float top = 0f)
    {
        var r = (RectTransform)go.transform;
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.pivot = new Vector2(0.5f, 0.5f);
        r.offsetMin = new Vector2(left, bottom);
        r.offsetMax = new Vector2(-right, -top);
        return r;
    }

    // Horizontal band pinned to the top of its parent: full width, given height, offset down by `y`.
    public static RectTransform TopBand(GameObject go, float y, float height, float sideInset = 0f)
    {
        var r = Place(go, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -y), new Vector2(-2f * sideInset, height));
        return r;
    }

    public static Image AddImage(GameObject go, Color color, bool raycast = true)
    {
        var img = go.GetComponent<Image>();
        if (img == null) img = go.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = raycast;
        return img;
    }

    public static TextMeshProUGUI AddLabel(GameObject go, string text, float size, Color color,
        TextAlignmentOptions align, FontStyles style = FontStyles.Normal)
    {
        var label = go.GetComponent<TextMeshProUGUI>();
        if (label == null) label = go.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = size;
        label.color = color;
        label.alignment = align;
        label.fontStyle = style;
        label.raycastTarget = false;
        label.textWrappingMode = TextWrappingModes.Normal;
        label.overflowMode = TextOverflowModes.Ellipsis;
        return label;
    }

    public static TextMeshProUGUI AddLabelChild(Transform parent, string name, string text, float size, Color color,
        TextAlignmentOptions align, FontStyles style = FontStyles.Normal)
    {
        GameObject go = Child(parent, name);
        return AddLabel(go, text, size, color, align, style);
    }

    public static void AutoSize(TMP_Text label, float min, float max)
    {
        label.enableAutoSizing = true;
        label.fontSizeMin = min;
        label.fontSizeMax = max;
    }

    // Button hover/press/disabled tints (multiplied over the Image colour) --
    // clearer than Unity's default near-invisible 0.96 highlight.
    public static void StyleButton(Button button)
    {
        var colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(0.92f, 0.92f, 0.92f, 1f);
        colors.pressedColor = new Color(0.72f, 0.72f, 0.72f, 1f);
        colors.selectedColor = Color.white; // no lingering tint after a click
        colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.6f);
        colors.fadeDuration = 0.08f;
        button.colors = colors;
    }

    // Disabled-by-default outline used as the selection / "armed" frame.
    public static Outline AddSelectionOutline(GameObject go, float distance = 4f)
    {
        var outline = go.GetComponent<Outline>();
        if (outline == null) outline = go.AddComponent<Outline>();
        outline.effectColor = SelectionOutline;
        outline.effectDistance = new Vector2(distance, distance);
        outline.useGraphicAlpha = false;
        outline.enabled = false;
        return outline;
    }

    // Fully styled button: background Image, Button, centered label.
    public static Button MakeButton(Transform parent, string name, string text, Color bg, float fontSize,
        out GameObject go, FontStyles style = FontStyles.Bold)
    {
        go = Child(parent, name);
        var image = AddImage(go, bg);
        var button = go.AddComponent<Button>();
        button.targetGraphic = image;
        StyleButton(button);

        GameObject labelGO = Child(go.transform, "Label");
        Fill(labelGO, 8f, 0f, 8f, 0f);
        AddLabel(labelGO, text, fontSize, Color.white, TextAlignmentOptions.Center, style);
        return button;
    }

    public static void Wire(SerializedObject so, string property, Object value)
    {
        var p = so.FindProperty(property);
        if (p == null) { Debug.LogWarning($"[MenuUiKit] {so.targetObject.GetType().Name} has no serialized field '{property}'."); return; }
        p.objectReferenceValue = value;
    }
}
