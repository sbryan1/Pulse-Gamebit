using UnityEngine;

// Phone performance settings, applied automatically when the game starts.
// Nothing to attach: just keep this file anywhere in Assets (not in an Editor folder).
public static class PerformanceSettings
{
    // Phones default to 30 fps in Unity; this asks for smooth 60.
    const int TargetFps = 60;

    // Phone screens are often 2400x1080 or sharper. Drawing every pixel of the glowing,
    // overlapping menu art is the main cost, so cap the drawing size of the short side.
    // 1080 still looks sharp.
    const int MaxShortSide = 1080;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Apply()
    {
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = TargetFps;

#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
        Screen.sleepTimeout = SleepTimeout.NeverSleep; // don't dim the screen while Stockfish thinks
#endif
    }

    // Called by ScreenLayout at startup and every time the phone rotates, so the
    // drawing size always matches the screen's current shape (no stretching).
    public static void ApplyResolution(bool portrait)
    {
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
        // The panel's real size, unaffected by any earlier SetResolution.
        int a = Display.main.systemWidth, b = Display.main.systemHeight;
        int longSide = Mathf.Max(a, b), shortSide = Mathf.Min(a, b);

        float k = shortSide > MaxShortSide ? MaxShortSide / (float)shortSide : 1f;
        int s = Mathf.RoundToInt(shortSide * k), l = Mathf.RoundToInt(longSide * k);

        if (portrait) Screen.SetResolution(s, l, true);
        else          Screen.SetResolution(l, s, true);
#endif
    }
}
