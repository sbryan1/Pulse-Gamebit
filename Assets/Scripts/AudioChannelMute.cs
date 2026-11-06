using UnityEngine;

/// <summary>
/// Put this on any AudioSource (your music player, your move-sound source).
/// It mutes/unmutes the source to match the settings, so your existing
/// Play()/PlayOneShot() code doesn't need to change.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class AudioChannelMute : MonoBehaviour
{
    [SerializeField] private GameAudioSettings.Channel channel = GameAudioSettings.Channel.MoveSounds;

    private AudioSource source;

    private void Awake()
    {
        source = GetComponent<AudioSource>();
    }

    private void OnEnable()
    {
        GameAudioSettings.Changed += OnSettingChanged;
        Apply(GameAudioSettings.IsEnabled(channel));
    }

    private void OnDisable()
    {
        GameAudioSettings.Changed -= OnSettingChanged;
    }

    private void OnSettingChanged(GameAudioSettings.Channel changed, bool enabled)
    {
        if (changed == channel) Apply(enabled);
    }

    private void Apply(bool enabled)
    {
        // mute (rather than Stop) so music resumes in place when switched back on
        source.mute = !enabled;
    }
}
