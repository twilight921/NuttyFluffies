using UnityEngine;

// Companion to StuntDetector, same subscribe pattern CreaturePassenger already
// uses for OnStunt -- kept as its own component rather than folded into
// StuntDetector so the physics detector stays audio-agnostic, and separate
// from CreaturePassenger since the sting plays for every stunt regardless of
// which creature (if any) is scoring off it. One generic sting for all five
// stunt types for now; a distinct sting per type is future work.
[RequireComponent(typeof(StuntDetector))]
[RequireComponent(typeof(SfxOneShot))]
public class StuntSfx : MonoBehaviour
{
    private StuntDetector _stuntDetector;
    private SfxOneShot _sfx;

    private void Awake()
    {
        _stuntDetector = GetComponent<StuntDetector>();
        _sfx = GetComponent<SfxOneShot>();
        _stuntDetector.OnStunt += HandleStunt;
    }

    private void OnDestroy()
    {
        if (_stuntDetector != null) _stuntDetector.OnStunt -= HandleStunt;
    }

    private void HandleStunt(HeartActionType action) => _sfx.Play();
}
