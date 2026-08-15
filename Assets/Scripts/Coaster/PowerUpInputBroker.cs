using UnityEngine;
using UnityEngine.InputSystem;

// Bridges the "Activate" input action to a broadcastable event, the same
// shape as CoasterInputBroker.OnSwipePerformed. Kept separate from
// CoasterInputBroker on purpose: that class is a continuous drag-velocity
// state machine (DragPosition/DragContact, _isDragging); a discrete button
// press is a different interaction shape entirely and doesn't belong mixed
// into it. Any number of PowerUpCarts can subscribe to OnActivatePressed.
public class PowerUpInputBroker : MonoBehaviour
{
    [SerializeField] private InputActionProperty activateAction;

    public System.Action OnActivatePressed;

    private void OnEnable()
    {
        activateAction.action?.Enable();
        if (activateAction.action != null) activateAction.action.performed += OnActivatePerformed;
    }

    private void OnDisable()
    {
        if (activateAction.action != null) activateAction.action.performed -= OnActivatePerformed;
    }

    // Named handler (not a lambda) so the OnDisable unsubscription above
    // actually works -- same reasoning CoasterInputBroker calls out.
    private void OnActivatePerformed(InputAction.CallbackContext context) => OnActivatePressed?.Invoke();
}
