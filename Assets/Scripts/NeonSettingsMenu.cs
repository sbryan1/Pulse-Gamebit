using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
#endif

namespace PulseGambit.Settings
{
    /// <summary>
    /// Neon settings menu with Music and Move Sounds switches.
    /// Drop one on an empty GameObject in each scene (MainMenu and Board). It builds its own
    /// canvas at runtime. Open it with the built-in corner button, or call Open()/Toggle()
    /// from any of your own UI buttons.
    /// </summary>
    [AddComponentMenu("Pulse Gambit/Neon Settings Menu")]
    public class NeonSettingsMenu : MonoBehaviour
    {
        public enum Corner { TopRight, TopLeft, BottomRight, BottomLeft }

        [Header("Corner Button")]
        [Tooltip("Show a SETTINGS button in a screen corner. Turn off if you wire your own button to Open().")]
        public bool showCornerButton = true;
        public Corner buttonCorner = Corner.TopRight;
        public Vector2 buttonMargin = new Vector2(36, 36);

        [Header("Look")]
        [Tooltip("Optional: assign a neon/sci-fi font (e.g. Orbitron). Falls back to Unity's built-in font.")]
        public Font font;
        public Color primary = new Color(0.0f, 0.95f, 1.0f);   // cyan
        public Color accent = new Color(1.0f, 0.16f, 0.82f);   // magenta
        public Color offColor = new Color(0.42f, 0.38f, 0.58f); // dim violet
        public Color panelColor = new Color(0.035f, 0.015f, 0.09f, 0.97f);
        public int sortingOrder = 500;

        [Header("Behaviour")]
        public bool closeOnEscape = true;
        [Tooltip("Also open the menu with Escape when it's closed.")]
        public bool openWithEscape = false;
        [Tooltip("Set Time.timeScale to 0 while open (restored on close).")]
        public bool pauseTimeWhileOpen = false;

        public bool IsOpen { get; private set; }

        /// <summary>True while any settings menu is open. Check this in board input code
        /// (e.g. OnMouseDown / Physics raycasts) so clicks don't move pieces behind the menu.</summary>
        public static bool AnyOpen => openMenu != null && openMenu.IsOpen;
        static NeonSettingsMenu openMenu;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => openMenu = null;

        RectTransform root, overlay, panel;
        CanvasGroup overlayGroup;
        Image panelGlow;
        NeonSwitch musicSwitch, moveSwitch;
        float openT;
        float savedTimeScale = 1f;

        // ---------- Public API (hook these to your own buttons if you like) ----------

        public void Open() => SetOpen(true);
        public void Close() => SetOpen(false);
        public void Toggle() => SetOpen(!IsOpen);

        // ---------- Lifecycle ----------

        void Awake()
        {
            if (font == null) font = NeonUI.DefaultFont();
            EnsureEventSystem();
            Build();
            Refresh(true);
            overlay.gameObject.SetActive(false);
        }

        void OnEnable() => GameAudioSettings.Changed += OnSettingsChanged;

        void OnDisable()
        {
            GameAudioSettings.Changed -= OnSettingsChanged;
            if (IsOpen) SetOpen(false); // restore timescale etc. if the scene unloads while open
        }

        void OnDestroy()
        {
            if (openMenu == this) openMenu = null;
        }

        void Update()
        {
            if (EscapePressed())
            {
                if (IsOpen && closeOnEscape) Close();
                else if (!IsOpen && openWithEscape) Open();
            }

            float dt = Time.unscaledDeltaTime;
            float goal = IsOpen ? 1f : 0f;
            if (openT != goal)
            {
                openT = Mathf.MoveTowards(openT, goal, dt / 0.18f);
                float e = 1f - Mathf.Pow(1f - openT, 3f);
                overlayGroup.alpha = e;
                panel.localScale = Vector3.one * Mathf.Lerp(0.9f, 1f, e);
                if (openT <= 0f) overlay.gameObject.SetActive(false);
            }

            if (overlay.gameObject.activeSelf)
            {
                musicSwitch.Tick(dt);
                moveSwitch.Tick(dt);
                // Slow neon "breathing" on the panel border glow.
                panelGlow.color = NeonUI.WithA(accent, 0.42f + 0.14f * Mathf.Sin(Time.unscaledTime * 2.4f));
            }
        }

        void SetOpen(bool open)
        {
            if (IsOpen == open) return;
            IsOpen = open;

            if (open)
            {
                openMenu = this;
                overlay.gameObject.SetActive(true);
                Refresh(true);
                if (pauseTimeWhileOpen) { savedTimeScale = Time.timeScale; Time.timeScale = 0f; }
            }
            else
            {
                if (openMenu == this) openMenu = null;
                if (pauseTimeWhileOpen) Time.timeScale = savedTimeScale;
            }

            overlayGroup.blocksRaycasts = open;
            overlayGroup.interactable = open;
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        }

        void OnSettingsChanged() => Refresh(false);

        void Refresh(bool instant)
        {
            musicSwitch.Set(GameAudioSettings.MusicOn, instant);
            moveSwitch.Set(GameAudioSettings.MoveSoundsOn, instant);
        }

        // ---------- Build UI ----------

        void Build()
        {
            var canvasGO = new GameObject("NeonSettingsCanvas", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGO.layer = 5;
            canvasGO.transform.SetParent(transform, false);
            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            root = (RectTransform)canvasGO.transform;

            if (showCornerButton) BuildCornerButton();

            // Overlay container (fades as a group)
            overlay = NeonUI.Stretch(NeonUI.Rect("Overlay", root, Vector2.zero, Vector2.zero));
            overlayGroup = overlay.gameObject.AddComponent<CanvasGroup>();
            overlayGroup.alpha = 0f;

            // Dim backdrop - clicking it closes the menu. (Sibling of the panel, so panel clicks don't bubble to it.)
            var dim = NeonUI.Layer(overlay, "Backdrop", null, new Color(0f, 0f, 0.02f, 0.8f), 0f, true);
            dim.type = Image.Type.Simple;
            var dimBtn = dim.gameObject.AddComponent<Button>();
            dimBtn.transition = Selectable.Transition.None;
            dimBtn.onClick.AddListener(Close);

            // Panel
            panel = NeonUI.Rect("Panel", overlay, new Vector2(780, 580), Vector2.zero);
            panelGlow = NeonUI.GlowLayer(panel, 30, NeonUI.WithA(accent, 0.5f));
            var panelFill = NeonUI.Layer(panel, "Fill", NeonUI.Fill(30), panelColor, 0f, true); // blocks clicks
            panelFill.raycastTarget = true;
            NeonUI.Layer(panel, "InnerLine", NeonUI.Stroke(26, 1.5f), NeonUI.WithA(primary, 0.25f), -10f);
            NeonUI.Layer(panel, "Border", NeonUI.Stroke(30, 3.5f), accent);

            // Title
            NeonUI.Label(panel, "Title", "S E T T I N G S", font, 58, primary, TextAnchor.MiddleCenter,
                new Vector2(700, 90), new Vector2(0, 200));
            Divider(panel, new Vector2(0, 140), 560, accent);

            // Rows
            musicSwitch = BuildRow(panel, "MUSIC", new Vector2(0, 55),
                () => GameAudioSettings.MusicOn = !GameAudioSettings.MusicOn);
            moveSwitch = BuildRow(panel, "MOVE SOUNDS", new Vector2(0, -55),
                () => GameAudioSettings.MoveSoundsOn = !GameAudioSettings.MoveSoundsOn);

            Divider(panel, new Vector2(0, -140), 560, NeonUI.WithA(primary, 0.6f));

            // Back button
            var back = NeonUI.Rect("BackButton", panel, new Vector2(280, 76), new Vector2(0, -205));
            NeonUI.GlowLayer(back, 38, NeonUI.WithA(accent, 0.35f));
            var backFill = NeonUI.Layer(back, "Fill", NeonUI.Fill(38), Color.white);
            NeonUI.HoverButton(backFill, accent, 0.06f, 0.28f, 0.45f).onClick.AddListener(Close);
            NeonUI.Layer(back, "Border", NeonUI.Stroke(38, 3f), accent);
            NeonUI.Label(back, "Label", "B A C K", font, 34, accent, TextAnchor.MiddleCenter,
                new Vector2(280, 76), Vector2.zero);
        }

        void BuildCornerButton()
        {
            Vector2 a;
            switch (buttonCorner)
            {
                case Corner.TopLeft: a = new Vector2(0, 1); break;
                case Corner.BottomRight: a = new Vector2(1, 0); break;
                case Corner.BottomLeft: a = new Vector2(0, 0); break;
                default: a = new Vector2(1, 1); break;
            }
            var btn = NeonUI.Rect("SettingsButton", root, new Vector2(240, 66), Vector2.zero);
            btn.anchorMin = btn.anchorMax = btn.pivot = a;
            btn.anchoredPosition = new Vector2(a.x > 0.5f ? -buttonMargin.x : buttonMargin.x,
                                               a.y > 0.5f ? -buttonMargin.y : buttonMargin.y);

            NeonUI.GlowLayer(btn, 33, NeonUI.WithA(primary, 0.3f));
            var fill = NeonUI.Layer(btn, "Fill", NeonUI.Fill(33), Color.white);
            NeonUI.HoverButton(fill, primary, 0.08f, 0.28f, 0.45f).onClick.AddListener(Toggle);
            NeonUI.Layer(btn, "Border", NeonUI.Stroke(33, 3f), primary);
            NeonUI.Label(btn, "Label", "SETTINGS", font, 28, primary, TextAnchor.MiddleCenter,
                new Vector2(240, 66), Vector2.zero);
        }

        void Divider(RectTransform parent, Vector2 pos, float width, Color c)
        {
            var d = NeonUI.Rect("Divider", parent, new Vector2(width, 3), pos);
            NeonUI.Layer(d, "Glow", null, NeonUI.WithA(c, 0.18f), 4f).type = Image.Type.Simple;
            NeonUI.Layer(d, "Line", null, c).type = Image.Type.Simple;
        }

        NeonSwitch BuildRow(RectTransform parent, string label, Vector2 pos, UnityEngine.Events.UnityAction onClick)
        {
            // Whole row is the click target, with a subtle hover highlight.
            var row = NeonUI.Rect(label + "Row", parent, new Vector2(660, 92), pos);
            var hit = NeonUI.Layer(row, "Hover", NeonUI.Fill(20), Color.white);
            NeonUI.HoverButton(hit, primary, 0.0f, 0.08f, 0.16f).onClick.AddListener(onClick);

            NeonUI.Label(row, "Label", label, font, 38, new Color(0.92f, 0.9f, 1f), TextAnchor.MiddleLeft,
                new Vector2(380, 80), new Vector2(-120, 0));

            var sw = new NeonSwitch { onColor = primary, offColor = offColor };
            sw.status = NeonUI.Label(row, "Status", "ON", font, 30, primary, TextAnchor.MiddleRight,
                new Vector2(100, 80), new Vector2(130, 0));

            var track = NeonUI.Rect("Switch", row, new Vector2(132, 56), new Vector2(255, 0));
            sw.glow = NeonUI.GlowLayer(track, 28, primary);
            sw.fill = NeonUI.Layer(track, "Fill", NeonUI.Fill(28), primary);
            sw.stroke = NeonUI.Layer(track, "Border", NeonUI.Stroke(28, 3f), primary);

            sw.knob = NeonUI.Rect("Knob", track, new Vector2(40, 40), Vector2.zero);
            sw.knobGlow = NeonUI.Layer(sw.knob, "Glow", NeonUI.Glow(20), primary, NeonUI.GlowPad * 0.6f);
            sw.knobFill = NeonUI.Layer(sw.knob, "Fill", NeonUI.Fill(20), primary);
            return sw;
        }

        // ---------- Switch visual ----------

        class NeonSwitch
        {
            public Color onColor, offColor;
            public Image glow, fill, stroke, knobGlow, knobFill;
            public RectTransform knob;
            public Text status;
            float t, target;
            const float KnobTravel = 38f;

            public void Set(bool on, bool instant)
            {
                target = on ? 1f : 0f;
                status.text = on ? "ON" : "OFF";
                if (instant) t = target;
                Apply();
            }

            public void Tick(float dt)
            {
                if (t == target) return;
                t = Mathf.MoveTowards(t, target, dt / 0.15f);
                Apply();
            }

            void Apply()
            {
                float e = t * t * (3f - 2f * t); // smoothstep
                Color c = Color.Lerp(offColor, onColor, e);
                knob.anchoredPosition = new Vector2(Mathf.Lerp(-KnobTravel, KnobTravel, e), 0f);
                stroke.color = c;
                fill.color = NeonUI.WithA(c, Mathf.Lerp(0.06f, 0.22f, e));
                glow.color = NeonUI.WithA(onColor, 0.4f * e);
                knobFill.color = Color.Lerp(offColor, Color.Lerp(onColor, Color.white, 0.35f), e);
                knobGlow.color = NeonUI.WithA(onColor, 0.7f * e);
                NeonUI.SetTextColor(status, c);
            }
        }

        // ---------- Input / EventSystem ----------

        static bool EscapePressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) return true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.Escape)) return true;
#endif
            return false;
        }

        static void EnsureEventSystem()
        {
#if UNITY_2023_1_OR_NEWER
            if (Object.FindAnyObjectByType<EventSystem>() != null) return;
#else
            if (Object.FindObjectOfType<EventSystem>() != null) return;
#endif
            var go = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
            go.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
#else
            go.AddComponent<StandaloneInputModule>();
#endif
        }
    }
}
