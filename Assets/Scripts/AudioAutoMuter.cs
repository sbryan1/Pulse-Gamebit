using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PulseGambit.Settings
{
    /// <summary>
    /// Starts itself automatically (no setup needed) and keeps every AudioSource in the game
    /// muted/unmuted to match the settings menu - including music on DontDestroyOnLoad objects,
    /// sources on child objects, and the temporary sources made by AudioSource.PlayClipAtPoint.
    ///
    /// How a source is assigned a channel:
    ///   1. An AudioChannelBinder that lists it, or sits on it / one of its parents, decides.
    ///   2. Otherwise looping sources count as Music, non-looping sources as Move Sounds.
    /// Each source found is logged once to the Console so you can see what went where.
    /// </summary>
    [DefaultExecutionOrder(10000)]
    public class AudioAutoMuter : MonoBehaviour
    {
        /// <summary>Set false to disable automatic muting entirely (binders still work).</summary>
        public static bool Enabled = true;
        /// <summary>Log each newly found AudioSource and the channel it was given.</summary>
        public static bool LogSources = true;

        static AudioAutoMuter instance;
        readonly Dictionary<AudioSource, AudioChannel> cache = new Dictionary<AudioSource, AudioChannel>();
        readonly HashSet<string> logged = new HashSet<string>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            instance = null;
            Enabled = true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot()
        {
            if (instance != null) return;
            var go = new GameObject("[PulseGambit Audio Settings]");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<AudioAutoMuter>();
        }

        void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;
        void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;
        void OnSceneLoaded(Scene s, LoadSceneMode m) => cache.Clear();

        // Runs every frame after other scripts, so a sound started this frame is silenced
        // before any audio is mixed.
        void LateUpdate()
        {
            if (!Enabled) return;
            bool musicOn = GameAudioSettings.MusicOn;
            bool movesOn = GameAudioSettings.MoveSoundsOn;

#if UNITY_2023_1_OR_NEWER
            var sources = Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None);
#else
            var sources = Object.FindObjectsOfType<AudioSource>();
#endif
            foreach (var src in sources)
            {
                AudioChannel ch = Classify(src);
                bool on = ch == AudioChannel.Music ? musicOn : movesOn;
                if (src.mute == on) src.mute = !on;
            }

            if (cache.Count > 512) cache.Clear(); // drop destroyed one-shot sources
        }

        AudioChannel Classify(AudioSource src)
        {
            // Explicit binder registration always wins (checked every frame, it's a dictionary lookup).
            if (AudioChannelBinder.TryGetChannel(src, out var bound)) return bound;
            if (cache.TryGetValue(src, out var cached)) return cached;

            string reason;
            AudioChannel ch;
            var parentBinder = src.GetComponentInParent<AudioChannelBinder>();
            if (parentBinder != null) { ch = parentBinder.channel; reason = "AudioChannelBinder on parent"; }
            else if (src.loop) { ch = AudioChannel.Music; reason = "looping"; }
            else { ch = AudioChannel.MoveSounds; reason = "not looping"; }

            cache[src] = ch;
            if (LogSources && logged.Add(src.gameObject.name))
            {
                string clip = src.clip != null ? src.clip.name : "(no clip)";
                Debug.Log($"[Pulse Gambit Audio] '{src.gameObject.name}' clip {clip} -> {ch} ({reason})", src);
            }
            return ch;
        }
    }
}
