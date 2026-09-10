using UnityEngine;

// Thin one-shot sound player: wraps a single AudioClip and fires it via
// AudioSource.PlayClipAtPoint, which spawns its own short-lived AudioSource
// and self-destroys once the clip finishes. That independence matters here:
// Pickup deactivates its own GameObject the instant it's collected, so a
// clip tied to an AudioSource living on this same object would get cut off
// before anyone heard it. No pooling, no randomized pitch -- callers just
// Play() and this reports the rest.
public class SfxOneShot : MonoBehaviour
{
    [SerializeField] private AudioClip clip;
    [SerializeField, Range(0f, 1f)] private float volume = 1f;

    public void Play()
    {
        if (clip == null) return;
        AudioSource.PlayClipAtPoint(clip, transform.position, volume);
    }
}
