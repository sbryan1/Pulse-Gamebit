using UnityEngine;
using UnityEngine.SceneManagement;

// Plays the background music on a seamless loop across every scene.
// Nothing to set up: it starts by itself when the game launches and keeps playing
// when you move between the main menu and the chess scene.
// The track is loaded from Assets/Resources/Music/NeonCrown.
// Any other AudioSource in a scene that plays the same track is switched off, so the
// music never plays twice and the Music toggle in Settings always controls it.
public class MusicPlayer : MonoBehaviour
{
    public static MusicPlayer Instance { get; private set; }

    const string TrackPath = "Music/NeonCrown";
    const string VolumeKey = "PulseGambit.MusicVolume";

    AudioSource source;

    // Music volume from 0 to 1, remembered between sessions.
    public static float Volume
    {
        get => PlayerPrefs.GetFloat(VolumeKey, 0.5f);
        set
        {
            PlayerPrefs.SetFloat(VolumeKey, Mathf.Clamp01(value));
            if (Instance != null) Instance.source.volume = Mathf.Clamp01(value);
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (Instance != null) return;
        var clip = Resources.Load<AudioClip>(TrackPath);
        if (clip == null)
        {
            Debug.LogWarning("Background music not found at Assets/Resources/" + TrackPath);
            return;
        }

        var go = new GameObject("MusicPlayer");
        DontDestroyOnLoad(go);
        Instance = go.AddComponent<MusicPlayer>();

        var src = go.AddComponent<AudioSource>();
        src.clip = clip;
        src.loop = true;
        src.playOnAwake = false;
        src.spatialBlend = 0f;
        src.priority = 0;           // never cut off by sound effects
        src.volume = Volume;
        src.Play();
        Instance.source = src;

        SceneManager.sceneLoaded += (s, m) => { if (Instance != null) Instance.SilenceDuplicates(); };
        Instance.SilenceDuplicates(); // the scene that was already loaded at startup
    }

    // Stops scene objects that play the same track as this player.
    void SilenceDuplicates()
    {
        if (source == null) return;
        var clip = source.clip;
        foreach (var other in FindObjectsByType<AudioSource>())
        {
            if (other == source) continue;
            if (other.clip == clip || other.resource == clip)
            {
                other.Stop();
                other.enabled = false;
                Debug.Log("[MusicPlayer] Turned off duplicate music on '" + other.gameObject.name +
                          "'. The global MusicPlayer already plays this track.", other);
            }
        }
    }
}
