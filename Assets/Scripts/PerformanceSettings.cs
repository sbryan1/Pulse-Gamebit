using UnityEngine;

// Phone performance settings, applied automatically when the game starts.
// Nothing to attach: just keep this file anywhere in Assets (not in an Editor folder).
public static class PerformanceSettings
{
    // Phones default to 30 fps in Unity; this asks for smooth 60.
    const int TargetFps = 60;

    // Phone screens are often 2400x1080 or sharper. Drawing every pixel of the glowing,
    // overlapping menu art is the main cost, so cap the drawing height. 1080 still looks sharp.
    const int MaxRenderHeight = 1080;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Apply()
    {
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = TargetFps;

#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
        int w = Screen.width, h = Screen.height;
        int shortSide = Mathf.Min(w, h);
        if (shortSide > MaxRenderHeight)
        {
            float k = MaxRenderHeight / (float)shortSide;
            Screen.SetResolution(Mathf.RoundToInt(w * k), Mathf.RoundToInt(h * k), true);
        }

        Screen.sleepTimeout = SleepTimeout.NeverSleep; // don't dim the screen while Stockfish thinks
#endif
    }
}
