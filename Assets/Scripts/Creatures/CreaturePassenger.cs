using UnityEngine;

// Shows a CreatureDefinition on a cart's "Passenger" child, the same
// data-asset-drives-a-component split TrackTypeApplier uses for track types.
// Deliberately its own GameObject/SpriteRenderer separate from CartBody, so
// assigning a passenger never fights with CartTintReceiver's track-color tint
// or PowerUpCart's identity tint on the body underneath it.
[ExecuteAlways]
[RequireComponent(typeof(SpriteRenderer))]
public class CreaturePassenger : MonoBehaviour
{
    [SerializeField] private CreatureDefinition creature;

    private SpriteRenderer _spriteRenderer;
    private StuntDetector _stuntDetector;
    private bool _isSubscribedToStunts;

    private void Awake()
    {
        _spriteRenderer = GetComponent<SpriteRenderer>();
        Apply();

        // StuntDetector lives on the cart root (this GameObject's parent),
        // one level up from this "Passenger" child -- see SetupCreatures.cs.
        _stuntDetector = GetComponentInParent<StuntDetector>();
        if (_stuntDetector != null && !_isSubscribedToStunts)
        {
            _stuntDetector.OnStunt += HandleStunt;
            _isSubscribedToStunts = true;
        }
    }

    private void OnDestroy()
    {
        if (_isSubscribedToStunts && _stuntDetector != null)
        {
            _stuntDetector.OnStunt -= HandleStunt;
        }
    }

    // Lets a designer see the swap live in edit mode when reassigning the SO.
    private void OnValidate() => Apply();

    // Reads whichever creature is currently assigned at the moment the stunt
    // fires (not whatever was assigned at subscribe time), so a runtime
    // loadout swap via SetCreature still scores correctly.
    private void HandleStunt(HeartActionType action)
    {
        if (creature == null || creature.heartAction == HeartActionType.None) return;
        if (creature.heartAction != action) return;
        RunStats.Instance?.AddStuntHeart(creature.stuntHeartBonus);
    }

    // Runtime loadout swap: lets TrainLoadoutApplier assign a creature to this
    // cart's Passenger at Awake, before this component's own Apply() would
    // otherwise fire (whichever definition was serialized in the scene).
    public void SetCreature(CreatureDefinition newCreature)
    {
        creature = newCreature;
        Apply();
    }

    private void Apply()
    {
        if (creature == null) return;
        if (_spriteRenderer == null) _spriteRenderer = GetComponent<SpriteRenderer>();
        if (_spriteRenderer == null) return;

        if (creature.passengerSprite != null) _spriteRenderer.sprite = creature.passengerSprite;
        _spriteRenderer.color = creature.passengerTintColor;
    }
}
