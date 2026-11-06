using System;
using UnityEngine;

/// <summary>
/// Global on/off state for music and move sounds.
/// Saved with PlayerPrefs, so choices carry over between scenes and play sessions.
/// (Named GameAudioSettings to avoid clashing with UnityEngine.AudioSettings.)
/// </summary>
public static class GameAudioSettings
{
    public enum Channel { Music, MoveSounds }

    private const string MusicKey = "settings.musicEnabled";
    private const string MoveSoundsKey = "settings.moveSoundsEnabled";

    /// <summary>Raised whenever a channel is switched on or off.</summary>
    public static event Action<Channel, bool> Changed;

    public static bool MusicEnabled
    {
        get => PlayerPrefs.GetInt(MusicKey, 1) == 1;
        set => Set(Channel.Music, MusicKey, value);
    }

    public static bool MoveSoundsEnabled
    {
        get => PlayerPrefs.GetInt(MoveSoundsKey, 1) == 1;
        set => Set(Channel.MoveSounds, MoveSoundsKey, value);
    }

    public static bool IsEnabled(Channel channel) =>
        channel == Channel.Music ? MusicEnabled : MoveSoundsEnabled;

    private static void Set(Channel channel, string key, bool value)
    {
        if (PlayerPrefs.GetInt(key, 1) == (value ? 1 : 0)) return;
        PlayerPrefs.SetInt(key, value ? 1 : 0);
        PlayerPrefs.Save();
        Changed?.Invoke(channel, value);
    }
}
