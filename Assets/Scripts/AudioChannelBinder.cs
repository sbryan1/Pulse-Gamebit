using System.Collections.Generic;
using UnityEngine;

namespace PulseGambit.Settings
{
    /// <summary>
    /// Optional. Most audio is handled automatically by AudioAutoMuter (looping = Music,
    /// one-shot = Move Sounds). Add this only to override that guess, e.g. music that doesn't
    /// loop, or a looping sound that should count as a move sound.
    /// Applies to the listed sources, or to every AudioSource on this object and its children.
    /// </summary>
    [AddComponentMenu("Pulse Gambit/Audio Channel Binder")]
    public class AudioChannelBinder : MonoBehaviour
    {
        [Tooltip("Which setting controls these sources.")]
        public AudioChannel channel = AudioChannel.Music;

        [Tooltip("Leave empty to use every AudioSource on this GameObject and its children.")]
        public AudioSource[] sources;

        static readonly Dictionary<AudioSource, AudioChannel> registry = new Dictionary<AudioSource, AudioChannel>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => registry.Clear();

        internal static bool TryGetChannel(AudioSource src, out AudioChannel ch) => registry.TryGetValue(src, out ch);

        void Awake()
        {
            if (sources == null || sources.Length == 0)
                sources = GetComponentsInChildren<AudioSource>(true);
        }

        void OnEnable()
        {
            foreach (var s in sources)
                if (s != null) registry[s] = channel;
            GameAudioSettings.Changed += Apply;
            Apply();
        }

        void OnDisable()
        {
            GameAudioSettings.Changed -= Apply;
            foreach (var s in sources)
                if (s != null) registry.Remove(s);
        }

        void Apply()
        {
            bool on = GameAudioSettings.IsOn(channel);
            foreach (var s in sources)
                if (s != null) s.mute = !on;
        }
    }
}
