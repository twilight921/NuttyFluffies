using UnityEngine;
using UnityEngine.InputSystem;

public class CoasterInputBroker : MonoBehaviour
{
    [Header("Input Action Asset References")]
    [SerializeField] private InputActionProperty dragContactAction;
    [SerializeField] private InputActionProperty dragPositionAction;

    public System.Action<float> OnSwipePerformed; 
    public System.Action OnSwipeEnded;

    private Vector2 _lastInputPosition;
    private bool _isDragging = false;

    private void Start()
    {
        // Check if actions were forgotten in the inspector
        if (dragContactAction.action == null) Debug.LogError("[InputBroker] Drag Contact Action is missing reference!");
        if (dragPositionAction.action == null) Debug.LogError("[InputBroker] Drag Position Action is missing reference!");
    }

    private void OnEnable()
    {
        dragContactAction.action?.Enable();
        dragPositionAction.action?.Enable();

        if (dragContactAction.action != null)
        {
            dragContactAction.action.started += OnDragStarted;
            dragContactAction.action.canceled += OnDragCanceled;
        }
    }

    private void OnDisable()
    {
        if (dragContactAction.action != null)
        {
            dragContactAction.action.started -= OnDragStarted;
            dragContactAction.action.canceled -= OnDragCanceled;
        }
    }

    // Named handlers so OnDisable's -= actually removes the same delegate
    // instance OnEnable's += added. Inline lambdas create a new delegate
    // each time, so the old -= was a no-op and every re-enable stacked
    // another duplicate subscription, causing multiplied swipe events.
    private void OnDragStarted(InputAction.CallbackContext context) => StartDrag();
    private void OnDragCanceled(InputAction.CallbackContext context) => EndDrag();

    private void StartDrag()
    {
        _isDragging = true;
        Debug.Log("[InputBroker] Click/Touch Detected! Starting drag...");
        
        if (dragPositionAction.action != null)
        {
            _lastInputPosition = dragPositionAction.action.ReadValue<Vector2>();
        }
    }

    private void EndDrag()
    {
        _isDragging = false;
        Debug.Log("[InputBroker] Click/Touch Released.");
        OnSwipeEnded?.Invoke();
    }

    private void Update()
    {
        if (!_isDragging || dragPositionAction.action == null) return;

        Vector2 currentInputPosition = dragPositionAction.action.ReadValue<Vector2>();
        float deltaX = currentInputPosition.x - _lastInputPosition.x;

        if (Mathf.Abs(deltaX) > 0.01f && Time.deltaTime > 0f)
        {
            float swipeVelocity = deltaX / Time.deltaTime;
            OnSwipePerformed?.Invoke(swipeVelocity);
        }

        _lastInputPosition = currentInputPosition;
    }
}
