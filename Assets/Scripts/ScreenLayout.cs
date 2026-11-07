using System;
using UnityEngine;

// Tells the rest of the game whether the screen is portrait or landscape,
// and raises Changed when the phone is rotated (or the Game view is resized in the editor).
// Nothing to attach: it starts itself when the game launches.
public static class ScreenLayout
{
    public static bool IsPortrait { get; private set; }

    /// <summary>Raised after the phone rotates or the screen size changes.</summary>
    public static event Action Changed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => Changed = null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        IsPortrait = Detect();
        PerformanceSettings.ApplyResolution(IsPortrait);
        var go = new GameObject("[ScreenLayout]") { hideFlags = HideFlags.HideInHierarchy };
        UnityEngine.Object.DontDestroyOnLoad(go);
        go.AddComponent<Watcher>();
    }

    static bool Detect()
    {
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
        // Screen.width/height can lag behind (or be fixed by SetResolution), so ask the OS.
        var o = Screen.orientation;
        if (o == ScreenOrientation.Portrait || o == ScreenOrientation.PortraitUpsideDown) return true;
        if (o == ScreenOrientation.LandscapeLeft || o == ScreenOrientation.LandscapeRight) return false;
#endif
        return Screen.height > Screen.width;
    }

    class Watcher : MonoBehaviour
    {
        int lastW, lastH;

        void Start() { lastW = Screen.width; lastH = Screen.height; }

        void Update()
        {
            bool portrait = Detect();
            bool turned = portrait != IsPortrait;
            bool resized = Screen.width != lastW || Screen.height != lastH;
            if (!turned && !resized) return;

            lastW = Screen.width;
            lastH = Screen.height;
            IsPortrait = portrait;
            if (turned) PerformanceSettings.ApplyResolution(portrait);

            // Also raised when only the size changed: on phones the new screen size can
            // arrive a frame after the rotation, and layouts need the final aspect ratio.
            Changed?.Invoke();
        }
    }
}
