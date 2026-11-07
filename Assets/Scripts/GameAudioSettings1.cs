using System;
using UnityEngine;

namespace PulseGambit.Settings
{
    public enum AudioChannel { Music, MoveSounds }

    /// <summary>
    /// Global, persistent on/off switches for music and move sounds.
    /// Saved in PlayerPrefs, so the choice carries between scenes and sessions.
    /// </summary>
    public static class GameAudioSettings
    {
        const string MusicKey = "PulseGambit.MusicOn";
        const string MoveSoundsKey = "PulseGambit.MoveSoundsOn";

        /// <summary>Raised whenever either setting changes.</summary>
        public static event Action Changed;

        static bool? musicOn;
        static bool? moveSoundsOn;

        // Reset statics when entering Play Mode (supports "Enter Play Mode Options" with domain reload off).
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Changed = null;
            musicOn = null;
            moveSoundsOn = null;
        }

        public static bool MusicOn
        {
            get
            {
                if (musicOn == null) musicOn = PlayerPrefs.GetInt(MusicKey, 1) == 1;
                return musicOn.Value;
            }
            set
            {
                if (MusicOn == value) return;
                musicOn = value;
                PlayerPrefs.SetInt(MusicKey, value ? 1 : 0);
                PlayerPrefs.Save();
                Changed?.Invoke();
            }
        }

        public static bool MoveSoundsOn
        {
            get
            {
                if (moveSoundsOn == null) moveSoundsOn = PlayerPrefs.GetInt(MoveSoundsKey, 1) == 1;
                return moveSoundsOn.Value;
            }
            set
            {
                if (MoveSoundsOn == value) return;
                moveSoundsOn = value;
                PlayerPrefs.SetInt(MoveSoundsKey, value ? 1 : 0);
                PlayerPrefs.Save();
                Changed?.Invoke();
            }
        }

        public static bool IsOn(AudioChannel channel) =>
            channel == AudioChannel.Music ? MusicOn : MoveSoundsOn;

        public static void Set(AudioChannel channel, bool on)
        {
            if (channel == AudioChannel.Music) MusicOn = on;
            else MoveSoundsOn = on;
        }

        /// <summary>
        /// Convenience for move/capture/check sounds: plays only if move sounds are on.
        /// Use this instead of source.PlayOneShot(clip) in your board code.
        /// </summary>
        public static void PlayMoveSound(AudioSource source, AudioClip clip, float volume = 1f)
        {
            if (!MoveSoundsOn || source == null || clip == null) return;
            source.PlayOneShot(clip, volume);
        }
    }
}
