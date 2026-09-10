using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Compact prev / label / next control for choosing which level the next ride
// builds. Writes the chosen index straight to LevelSelection (PlayerPrefs) --
// same fire-and-forget model as the power-up picker writing to GarageSave.
// Self-contained: GarageController doesn't need to know about it, it just has
// to exist in the Garage scene.
public class LevelSelectUI : MonoBehaviour
{
    [SerializeField] private LevelCatalog catalog;
    [SerializeField] private Button prevButton;
    [SerializeField] private Button nextButton;
    [SerializeField] private TMP_Text label;

    private int _index;

    private void Awake()
    {
        if (prevButton != null) prevButton.onClick.AddListener(() => Step(-1));
        if (nextButton != null) nextButton.onClick.AddListener(() => Step(1));
    }

    private void Start()
    {
        _index = LevelSelection.Index;
        Clamp();
        Refresh();
    }

    private void Step(int dir)
    {
        _index += dir;
        Clamp();
        LevelSelection.Index = _index;
        Refresh();
    }

    private void Clamp()
    {
        int count = catalog != null ? catalog.Count : 0;
        _index = count <= 0 ? 0 : Mathf.Clamp(_index, 0, count - 1);
    }

    private void Refresh()
    {
        if (label == null) return;
        if (catalog == null || catalog.Count == 0)
        {
            label.text = "No levels";
            return;
        }

        LevelDefinition def = catalog.Get(_index);
        string name = def != null ? def.displayName : "?";
        string world = def != null && !string.IsNullOrEmpty(def.worldName) ? $"{def.worldName} – " : "";
        label.text = $"{world}{name}   ({_index + 1}/{catalog.Count})";
    }
}
