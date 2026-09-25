using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Cheap press/hover feedback for any uGUI Button: scales up on hover, down on
// press, and can be "punched" from code to acknowledge an action (e.g. a
// creature landing in a train slot). Purely cosmetic, unscaled-time driven so
// it also works while Time.timeScale is 0. Menu/Garage components call
// Ensure() on their own buttons, so no scene wiring is needed.
[DisallowMultipleComponent]
public class UIButtonFeedback : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    [SerializeField] private float hoverScale = 1.06f;
    [SerializeField] private float pressScale = 0.95f;
    [SerializeField] private float responsiveness = 18f;

    private Selectable _selectable;
    private Vector3 _baseScale = Vector3.one;
    private float _scale = 1f;
    private float _punch;
    private bool _hover;
    private bool _down;

    public static UIButtonFeedback Ensure(GameObject go)
    {
        if (go == null) return null;
        var existing = go.GetComponent<UIButtonFeedback>();
        return existing != null ? existing : go.AddComponent<UIButtonFeedback>();
    }

    private void Awake()
    {
        _selectable = GetComponent<Selectable>();
        _baseScale = transform.localScale;
    }

    private void OnDisable()
    {
        _hover = _down = false;
        _scale = 1f;
        _punch = 0f;
        transform.localScale = _baseScale;
    }

    // Brief overshoot-and-settle bump, e.g. Punch(0.12f).
    public void Punch(float amount = 0.12f) => _punch = amount;

    public void OnPointerEnter(PointerEventData eventData) => _hover = true;
    public void OnPointerExit(PointerEventData eventData) { _hover = false; _down = false; }
    public void OnPointerDown(PointerEventData eventData) => _down = true;
    public void OnPointerUp(PointerEventData eventData) => _down = false;

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        bool interactable = _selectable == null || _selectable.IsInteractable();

        float target = 1f;
        if (interactable) target = _down ? pressScale : (_hover ? hoverScale : 1f);

        _scale = Mathf.Lerp(_scale, target, 1f - Mathf.Exp(-responsiveness * dt));
        _punch = Mathf.MoveTowards(_punch, 0f, dt * 0.6f);

        transform.localScale = _baseScale * (_scale + _punch);
    }
}
