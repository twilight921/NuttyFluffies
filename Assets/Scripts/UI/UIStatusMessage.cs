using UnityEngine;
using TMPro;

// One-line feedback strip: Show() flashes a coloured message, holds it for a
// few seconds, then fades back to a dim idle hint. Lives on the label itself.
public class UIStatusMessage : MonoBehaviour
{
    public static readonly Color Neutral = new Color(1f, 1f, 1f, 1f);
    public static readonly Color Good = new Color(0.55f, 1f, 0.6f, 1f);
    public static readonly Color Warn = new Color(1f, 0.85f, 0.3f, 1f);
    public static readonly Color Bad = new Color(1f, 0.5f, 0.45f, 1f);

    [SerializeField] private TMP_Text label;
    [SerializeField] private string idleText = "";
    [SerializeField] private float holdSeconds = 3.5f;
    [SerializeField] private float fadeSeconds = 0.6f;
    [SerializeField, Range(0f, 1f)] private float idleAlpha = 0.7f;

    private float _timer;
    private bool _showingMessage;

    private void Awake()
    {
        if (label == null) label = GetComponent<TMP_Text>();
    }

    private void OnEnable() => ShowIdle();

    public void Show(string message, Color color)
    {
        if (label == null) return;
        label.text = message;
        label.color = color;
        _timer = holdSeconds + fadeSeconds;
        _showingMessage = true;
    }

    private void ShowIdle()
    {
        if (label == null) return;
        _showingMessage = false;
        label.text = idleText;
        label.color = new Color(1f, 1f, 1f, idleAlpha);
    }

    private void Update()
    {
        if (!_showingMessage) return;
        _timer -= Time.unscaledDeltaTime;
        if (_timer <= 0f) { ShowIdle(); return; }
        if (_timer < fadeSeconds)
        {
            Color c = label.color;
            c.a = Mathf.Lerp(idleAlpha, 1f, _timer / fadeSeconds);
            label.color = c;
        }
    }
}
