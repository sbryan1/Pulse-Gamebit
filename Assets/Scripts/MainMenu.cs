using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

// Builds the Pulse Gambit main menu from code: neon grid, glowing pulse line
// with a spark that travels along it, the logo, and the menu buttons.
// Put this on an empty GameObject in a scene called "MainMenu".
// Art is loaded from Assets/Resources/Menu/.
public class MainMenu : MonoBehaviour
{
    [Tooltip("Name of your chess scene, exactly as listed in Build Settings.")]
    public string gameSceneName = "SampleScene";

    [Tooltip("How fast the spark runs along the pulse line, in pixels per second.")]
    public float sparkSpeed = 900f;

    static readonly Color Cyan     = new Color32(40, 240, 255, 255);
    static readonly Color Lavender = new Color32(185, 140, 255, 255);
    static readonly Color Magenta  = new Color32(255, 40, 200, 255);
    static readonly Color Ground   = new Color32(8, 6, 26, 255);

    // The main pulse line from Pulse.png, in 1920x1080 UI space (center origin, y up).
    static readonly Vector2[] PulsePath =
    {
        new Vector2(-960f, 180f), new Vector2(-852f, 180f), new Vector2(-840f, 187f), new Vector2(-828f, 190f),
        new Vector2(-810f, 189f), new Vector2(-798f, 184f), new Vector2(-732f, 180f), new Vector2(-715f, 158f),
        new Vector2(-686f, 282f), new Vector2(-658f, 81f), new Vector2(-638f, 180f), new Vector2(-564f, 180f),
        new Vector2(-554f, 186f), new Vector2(-545f, 191f), new Vector2(-535f, 194f), new Vector2(-516f, 197f),
        new Vector2(-497f, 194f), new Vector2(-487f, 191f), new Vector2(-478f, 186f), new Vector2(-468f, 180f),
        new Vector2(-336f, 180f), new Vector2(-317f, 153f), new Vector2(-281f, 392f), new Vector2(-242f, -24f),
        new Vector2(-218f, 180f), new Vector2(-120f, 180f), new Vector2(120f, 180f), new Vector2(218f, 180f),
        new Vector2(242f, -24f), new Vector2(281f, 392f), new Vector2(317f, 153f), new Vector2(336f, 180f),
        new Vector2(468f, 180f), new Vector2(478f, 186f), new Vector2(487f, 191f), new Vector2(497f, 194f),
        new Vector2(516f, 197f), new Vector2(535f, 194f), new Vector2(545f, 191f), new Vector2(554f, 186f),
        new Vector2(564f, 180f), new Vector2(638f, 180f), new Vector2(658f, 81f), new Vector2(686f, 282f),
        new Vector2(715f, 158f), new Vector2(732f, 180f), new Vector2(792f, 180f), new Vector2(804f, 187f),
        new Vector2(816f, 190f), new Vector2(834f, 189f), new Vector2(846f, 184f), new Vector2(960f, 180f),
    };

    Font font;
    Text levelLabel;
    Image pulseImage;
    Image sparkImage;
    float[] cumulative;
    float sparkDistance;

    // ---------- Build ----------

    void Start()
    {
        font = Resources.Load<Font>("Menu/MenuFont");
        if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        var cam = Camera.main;
        if (cam != null)
        {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Ground;
        }

        EnsureEventSystem();
        var canvas = BuildCanvas();

        // Grid tiles across the whole screen, whatever its shape.
        var grid = MakeImage("Grid", canvas.transform, LoadSprite("Menu/GridCell", Vector4.zero, TextureWrapMode.Repeat));
        grid.type = Image.Type.Tiled;
        Stretch(grid.rectTransform);

        // Everything else is laid out on a 1920x1080 frame kept centered.
        var art = new GameObject("Art", typeof(RectTransform)).GetComponent<RectTransform>();
        art.SetParent(canvas.transform, false);
        art.sizeDelta = new Vector2(1920, 1080);

        pulseImage = MakeImage("Pulse", art, LoadSprite("Menu/Pulse"));
        Stretch(pulseImage.rectTransform);

        sparkImage = MakeImage("Spark", art, LoadSprite("Menu/Spark"));
        sparkImage.rectTransform.sizeDelta = new Vector2(72, 72);

        var logo = MakeImage("Logo", art, LoadSprite("Menu/Logo"));
        Stretch(logo.rectTransform);

        var frame = LoadSprite("Menu/ButtonFrame", new Vector4(50, 50, 50, 50));
        var size = new Vector2(560, 104);

        MakeButton(art, frame, "PLAY", Cyan, new Vector2(0, -150), size, Play);
        BuildLevelRow(art, frame, new Vector2(0, -270), size);
        MakeButton(art, frame, "QUIT", Magenta, new Vector2(0, -390), size, Quit);

        PreparePath();
    }

    void BuildLevelRow(Transform parent, Sprite frame, Vector2 pos, Vector2 size)
    {
        var row = MakeImage("EngineLevel", parent, frame);
        row.type = Image.Type.Sliced;
        row.color = new Color(Lavender.r, Lavender.g, Lavender.b, 0.8f);
        row.raycastTarget = false;
        row.rectTransform.anchoredPosition = pos;
        row.rectTransform.sizeDelta = size;

        levelLabel = MakeText(row.transform, "", 40, Lavender);
        RefreshLevel();

        MakeArrow(row.transform, "<", new Vector2(-size.x / 2 + 50, 0), () => ChangeLevel(-1));
        MakeArrow(row.transform, ">", new Vector2(size.x / 2 - 50, 0), () => ChangeLevel(+1));
    }

    // ---------- Actions ----------

    void Play()
    {
        if (!Application.CanStreamedLevelBeLoaded(gameSceneName))
        {
            Debug.LogError("Scene '" + gameSceneName + "' isn't in Build Settings. " +
                           "Add it in File > Build Settings, or fix the Game Scene Name on MainMenu.");
            return;
        }
        SceneManager.LoadScene(gameSceneName);
    }

    void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    void ChangeLevel(int delta)
    {
        GameSettings.SkillLevel = Mathf.Clamp(GameSettings.SkillLevel + delta, 0, 20);
        RefreshLevel();
    }

    void RefreshLevel() => levelLabel.text = "ENGINE LEVEL  " + GameSettings.SkillLevel;

    // ---------- Animation ----------

    void PreparePath()
    {
        cumulative = new float[PulsePath.Length];
        for (int i = 1; i < PulsePath.Length; i++)
            cumulative[i] = cumulative[i - 1] + Vector2.Distance(PulsePath[i - 1], PulsePath[i]);
    }

    void Update()
    {
        if (cumulative == null) return;

        // The pulse line breathes gently.
        float breath = 0.82f + 0.18f * Mathf.Sin(Time.time * 2.2f);
        pulseImage.color = new Color(1, 1, 1, breath);

        // The spark runs left to right, then waits a moment off screen before the next run.
        float total = cumulative[cumulative.Length - 1];
        sparkDistance += sparkSpeed * Time.deltaTime;
        if (sparkDistance > total + 600f) sparkDistance = 0f;

        Vector2 p = PointAt(Mathf.Min(sparkDistance, total));
        sparkImage.rectTransform.anchoredPosition = p;
        float edgeFade = Mathf.Clamp01((960f - Mathf.Abs(p.x)) / 220f);
        sparkImage.color = new Color(1, 1, 1, sparkDistance > total ? 0f : edgeFade);
    }

    Vector2 PointAt(float d)
    {
        for (int i = 1; i < cumulative.Length; i++)
        {
            if (d <= cumulative[i])
            {
                float seg = cumulative[i] - cumulative[i - 1];
                float t = seg > 0 ? (d - cumulative[i - 1]) / seg : 0;
                return Vector2.Lerp(PulsePath[i - 1], PulsePath[i], t);
            }
        }
        return PulsePath[PulsePath.Length - 1];
    }

    // ---------- UI helpers ----------

    Canvas BuildCanvas()
    {
        var go = new GameObject("MenuCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 1f; // keep the full height on any screen
        return canvas;
    }

    void EnsureEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>() != null) return;
        var go = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
        go.AddComponent<InputSystemUIInputModule>();
#else
        go.AddComponent<StandaloneInputModule>();
#endif
    }

    void MakeButton(Transform parent, Sprite frame, string label, Color color, Vector2 pos, Vector2 size,
                    UnityEngine.Events.UnityAction onClick)
    {
        var img = MakeImage(label + "Button", parent, frame);
        img.type = Image.Type.Sliced;
        img.raycastTarget = true;
        img.rectTransform.anchoredPosition = pos;
        img.rectTransform.sizeDelta = size;

        var button = img.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.targetGraphic = img;
        button.onClick.AddListener(onClick);

        var neon = img.gameObject.AddComponent<NeonButton>();
        neon.frame = img;
        neon.color = color;

        MakeText(img.transform, label, 44, color);
    }

    void MakeArrow(Transform parent, string symbol, Vector2 pos, UnityEngine.Events.UnityAction onClick)
    {
        var hit = MakeImage("Arrow" + symbol, parent, null);
        hit.color = new Color(0, 0, 0, 0); // invisible, but still catches clicks
        hit.raycastTarget = true;
        hit.rectTransform.anchoredPosition = pos;
        hit.rectTransform.sizeDelta = new Vector2(90, 104);

        var button = hit.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.targetGraphic = hit;
        button.onClick.AddListener(onClick);

        hit.gameObject.AddComponent<NeonButton>();
        MakeText(hit.transform, symbol, 48, Lavender);
    }

    Text MakeText(Transform parent, string s, int size, Color color)
    {
        var go = new GameObject("Label", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<Text>();
        t.font = font;
        t.fontSize = size;
        t.text = s;
        t.alignment = TextAnchor.MiddleCenter;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.color = Color.Lerp(color, Color.white, 0.75f);
        t.raycastTarget = false;

        // Soft colored halo behind the letters
        var glow = go.AddComponent<Outline>();
        glow.effectColor = new Color(color.r, color.g, color.b, 0.35f);
        glow.effectDistance = new Vector2(2, -2);

        Stretch(t.rectTransform);
        return t;
    }

    Image MakeImage(string name, Transform parent, Sprite sprite)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.sprite = sprite;
        img.raycastTarget = false;
        return img;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static Sprite LoadSprite(string path, Vector4 border = default, TextureWrapMode wrap = TextureWrapMode.Clamp)
    {
        var tex = Resources.Load<Texture2D>(path);
        if (tex == null)
        {
            Debug.LogError("Missing menu art: Assets/Resources/" + path + ".png");
            return null;
        }
        tex.wrapMode = wrap;
        return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f),
                             100f, 0, SpriteMeshType.FullRect, border);
    }
}
