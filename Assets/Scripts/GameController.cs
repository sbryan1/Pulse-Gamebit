using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// Player vs Stockfish. The player's side (White or Black) is picked in the main menu.
// Click one of your pieces to see its legal moves, then click a highlighted square.
// Pawns that reach the last rank become queens. Press Esc for the main menu.
public class GameController : MonoBehaviour
{
    public StockfishEngine engine;
    public PieceManager pieces;
    public BoardGenerator board;   // found automatically if left empty

    [Tooltip("Scene to return to when Esc is pressed.")]
    public string menuSceneName = "MainMenu";

    [Tooltip("Overwritten at Start by the level picked in the main menu.")]
    public int skillLevel = 10;
    public int moveTimeMs = 1000;
    [Tooltip("Pause before Stockfish starts thinking, so its reply doesn't feel instant.")]
    public float engineDelay = 0.3f;

    [Header("Sound")]
    [Tooltip("Left empty, these load from Assets/Resources/Sounds/Move and Capture.")]
    public AudioClip moveSound;
    public AudioClip captureSound;
    [Range(0f, 1f)] public float volume = 0.8f;
    AudioSource audioSource;

    bool PlayerIsWhite;

    static readonly Color Cyan     = new Color32(40, 240, 255, 255);
    static readonly Color Magenta  = new Color32(255, 40, 200, 255);
    static readonly Color Lavender = new Color32(185, 140, 255, 255);

    enum State { Waiting, PlayerTurn, EngineTurn, GameOver }
    State state = State.Waiting;

    // Every move played so far. Stockfish rebuilds the position from this list.
    readonly List<string> moves = new();
    List<string> legal = new();
    string selected;

    Transform markerRoot;
    GameObject selectMarker, lastFromMarker, lastToMarker;
    readonly List<GameObject> dots = new();
    Sprite squareSprite, dotSprite;
    Text status;
    CanvasScaler hudScaler;

    bool WhiteToMove => moves.Count % 2 == 0;
    bool PlayerToMove => WhiteToMove == PlayerIsWhite;
    string PositionCmd => "position startpos moves " + string.Join(" ", moves);

    // ---------- Setup ----------

    void Start()
    {
        if (board == null) board = FindAnyObjectByType<BoardGenerator>();
        if (pieces == null) pieces = FindAnyObjectByType<PieceManager>();
        if (engine == null) engine = FindAnyObjectByType<StockfishEngine>();

        // Status text first, so any problem below can be shown on screen.
        BuildHud();
        SetStatus("STARTING...", Lavender);

        skillLevel = GameSettings.SkillLevel;
        PlayerIsWhite = GameSettings.PlayerIsWhite;

        squareSprite = MakeSquareSprite();
        dotSprite = MakeDotSprite(64);
        markerRoot = new GameObject("MoveMarkers").transform;
        selectMarker   = MakeMarker(squareSprite, new Color(Cyan.r, Cyan.g, Cyan.b, 0.45f), 1f, 1);
        lastFromMarker = MakeMarker(squareSprite, new Color(Lavender.r, Lavender.g, Lavender.b, 0.3f), 1f, 1);
        lastToMarker   = MakeMarker(squareSprite, new Color(Lavender.r, Lavender.g, Lavender.b, 0.3f), 1f, 1);
        SetupAudio();

        // Captured pieces shown beside the board. Added automatically if not in the scene.
        if (FindAnyObjectByType<CapturedPiecesDisplay>() == null)
            gameObject.AddComponent<CapturedPiecesDisplay>();

        // Same neon grid background as the main menu. Added automatically if not in the scene.
        if (FindAnyObjectByType<MenuBackground>() == null)
            gameObject.AddComponent<MenuBackground>();

        pieces.LoadFen("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1");

        string path;
        try
        {
            path = EnginePath();
        }
        catch (System.Exception e)
        {
            Debug.LogError("Couldn't work out the engine path: " + e);
            SetStatus("ENGINE PATH ERROR: " + e.Message, Magenta);
            state = State.GameOver;
            return;
        }
        Debug.Log("Engine path: " + path + " exists: " + File.Exists(path));

        engine.OnBestMove    += OnEngineMove;
        engine.OnLegalMoves  += OnLegalMoves;
        engine.OnCheckStatus += OnCheckStatus;

        try
        {
            engine.StartEngine(path, skillLevel);
        }
        catch (System.Exception e)
        {
            // Shown on screen so problems are visible on a phone, where there's no Console.
            Debug.LogError("Stockfish failed to start: " + e.Message);
            GUIUtility.systemCopyBuffer = e.ToString();
            SetStatus("ENGINE FAILED TO START: " + e.Message +
                      (File.Exists(path) ? "" : " (FILE NOT FOUND)"), Magenta);
            state = State.GameOver;
            return;
        }

        RefreshPosition();
        Invoke(nameof(CheckEngineResponded), 5f);
    }

    // ---------- Engine location ----------

    // Step 4: where the Stockfish program lives on each platform.
    static string EnginePath()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        // Android installs Assets/Plugins/Android/libs/arm64-v8a/libstockfish.so into the
        // app's native-library folder, the one place an app may run its own programs.
        return Path.Combine(AndroidNativeLibraryDir(), "libstockfish.so");
#else
        string file = Application.platform == RuntimePlatform.WindowsEditor ||
                      Application.platform == RuntimePlatform.WindowsPlayer
                      ? "stockfish.exe" : "stockfish";
        return Path.Combine(Application.streamingAssetsPath, file);
#endif
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    static string AndroidNativeLibraryDir()
    {
        // Preferred: ask Android directly.
        try
        {
            using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            using var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
            using var appInfo = activity.Call<AndroidJavaObject>("getApplicationInfo");
            string dir = appInfo.Get<string>("nativeLibraryDir");
            if (!string.IsNullOrEmpty(dir)) return dir;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("nativeLibraryDir lookup failed, using fallback: " + e.Message);
        }

        // Fallback: the library folder sits next to the installed APK (".../base.apk" -> ".../lib/arm64").
        return Path.Combine(Path.GetDirectoryName(Application.dataPath), "lib", "arm64");
    }
#endif

    // ---------- Turn flow ----------

    // After every move: ask Stockfish which moves are legal now.
    void RefreshPosition()
    {
        state = State.Waiting;
        engine.RequestLegalMoves(PositionCmd);
    }

    // If Stockfish never answered, say so on screen instead of silently ignoring taps.
    void CheckEngineResponded()
    {
        if (state == State.Waiting && moves.Count == 0)
            SetStatus("ENGINE NOT RESPONDING", Magenta);
    }

    void OnLegalMoves(List<string> list)
    {
        legal = list;

        if (legal.Count == 0)
        {
            // No legal moves: checkmate if in check, otherwise stalemate.
            engine.RequestCheckStatus();
            return;
        }

        if (PlayerToMove)
        {
            state = State.PlayerTurn;
            SetStatus("YOUR MOVE", Cyan);
        }
        else
        {
            state = State.EngineTurn;
            SetStatus("STOCKFISH IS THINKING...", Magenta);
            Invoke(nameof(AskEngine), engineDelay);
        }
    }

    void AskEngine()
    {
        engine.Send(PositionCmd);
        engine.Send("go movetime " + moveTimeMs);
    }

    void OnEngineMove(string move)
    {
        if (state != State.EngineTurn) return;
        if (move == "(none)") { engine.RequestCheckStatus(); return; }
        PlayMove(move);
    }

    void PlayMove(string move)
    {
        bool captured = pieces.ApplyUciMove(move);
        PlaySound(captured ? captureSound : moveSound);
        moves.Add(move);
        ShowLastMove(move);
        ClearSelection();
        RefreshPosition();
    }

    void OnCheckStatus(bool inCheck)
    {
        state = State.GameOver;
        ClearSelection();

        if (!inCheck)        SetStatus("STALEMATE  -  DRAW      ESC: MENU", Lavender);
        else if (PlayerToMove) SetStatus("CHECKMATE  -  STOCKFISH WINS      ESC: MENU", Magenta);
        else                 SetStatus("CHECKMATE  -  YOU WIN!      ESC: MENU", Cyan);
    }

    // ---------- Input ----------

    void Update()
    {
#if ENABLE_INPUT_SYSTEM
        // Pointer covers mouse, touchscreen and pen, so taps work on phones too.
        // Android's Back button arrives as the Escape key.
        bool back = Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
        var pointer = Pointer.current;
        bool click = pointer != null && pointer.press.wasPressedThisFrame;
        Vector2 mouse = pointer != null ? pointer.position.ReadValue() : Vector2.zero;
#else
        bool back = Input.GetKeyDown(KeyCode.Escape);
        bool click = Input.GetMouseButtonDown(0);
        Vector2 mouse = Input.mousePosition;
        if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)
        {
            click = true;
            mouse = Input.GetTouch(0).position;
        }
#endif
        if (back && Application.CanStreamedLevelBeLoaded(menuSceneName))
        {
            SceneManager.LoadScene(menuSceneName);
            return;
        }

        if (click && state == State.PlayerTurn)
            HandleClick(ScreenToSquare(mouse));
    }

    void HandleClick(string square)
    {
        if (square == null) { ClearSelection(); return; }

        // Second click: try to move the selected piece there.
        if (selected != null)
        {
            string move = FindMove(selected, square);
            if (move != null) { PlayMove(move); return; }
        }

        // Otherwise select the clicked piece if it has any legal moves.
        if (legal.Exists(m => m.StartsWith(square))) Select(square);
        else ClearSelection();
    }

    string FindMove(string from, string to)
    {
        string plain = from + to;
        if (legal.Contains(plain)) return plain;
        if (legal.Contains(plain + "q")) return plain + "q"; // auto-promote to a queen
        return null;
    }

    string ScreenToSquare(Vector2 screen)
    {
        var cam = Camera.main;
        if (cam == null) return null;

        // Cast a ray from the mouse onto the board's plane (z = 0).
        // Works with a flat orthographic camera and with the tilted 3D camera from BoardWave.
        Ray ray = cam.ScreenPointToRay(screen);
        var boardPlane = new Plane(Vector3.forward, Vector3.zero);
        if (!boardPlane.Raycast(ray, out float enter)) return null;
        Vector3 w = ray.GetPoint(enter);
        float s = board.squareSize;
        int file = Mathf.FloorToInt((w.x + 4f * s) / s);
        int rank = Mathf.FloorToInt((w.y + 4f * s) / s);
        if (file < 0 || file > 7 || rank < 0 || rank > 7) return null;
        if (board.flipped) { file = 7 - file; rank = 7 - rank; } // Black at the bottom
        return "" + (char)('a' + file) + (char)('1' + rank);
    }

    // ---------- Highlights ----------

    void Select(string square)
    {
        ClearSelection();
        selected = square;
        Place(selectMarker, square, -0.05f);

        var targets = new HashSet<string>();
        foreach (var m in legal)
            if (m.StartsWith(square)) targets.Add(m.Substring(2, 2));

        foreach (var t in targets)
        {
            var dot = MakeMarker(dotSprite, new Color(Cyan.r, Cyan.g, Cyan.b, 0.85f), 0.32f, 3);
            Place(dot, t, -0.2f);
            dots.Add(dot);
        }
    }

    void ClearSelection()
    {
        selected = null;
        if (selectMarker != null) selectMarker.SetActive(false);
        foreach (var d in dots) Destroy(d);
        dots.Clear();
    }

    void ShowLastMove(string move)
    {
        Place(lastFromMarker, move.Substring(0, 2), -0.04f);
        Place(lastToMarker, move.Substring(2, 2), -0.04f);
    }

    GameObject MakeMarker(Sprite sprite, Color color, float scale, int order)
    {
        var go = new GameObject("Marker");
        go.transform.SetParent(markerRoot);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = color;
        sr.sortingOrder = order; // 1 = under pieces (they sit closer to the camera), 3 = on top
        go.transform.localScale = Vector3.one * board.squareSize * scale;
        go.SetActive(false);
        return go;
    }

    void Place(GameObject go, string square, float z)
    {
        go.transform.position = board.SquareToWorld(square) + new Vector3(0, 0, z);
        go.SetActive(true);
    }

    // ---------- Sound ----------

    void SetupAudio()
    {
        if (moveSound == null) moveSound = Resources.Load<AudioClip>("Sounds/Move");
        if (captureSound == null) captureSound = Resources.Load<AudioClip>("Sounds/Capture");
        if (moveSound == null || captureSound == null)
            Debug.LogWarning("Move/Capture sounds not found in Assets/Resources/Sounds/");

        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f; // 2D sound, same volume everywhere
    }

    void PlaySound(AudioClip clip)
    {
        if (clip == null || audioSource == null) return;
        audioSource.pitch = Random.Range(0.96f, 1.04f); // slight variation so repeats don't sound robotic
        audioSource.PlayOneShot(clip, volume);
    }

    // ---------- Status text ----------

    void BuildHud()
    {
        var canvasGo = new GameObject("HUD", typeof(Canvas), typeof(CanvasScaler));
        canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        hudScaler = canvasGo.GetComponent<CanvasScaler>();
        hudScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;

        var go = new GameObject("Status", typeof(RectTransform));
        go.transform.SetParent(canvasGo.transform, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(1, 1);
        rt.pivot = new Vector2(0.5f, 1);

        status = go.AddComponent<Text>();
        status.font = Resources.Load<Font>("Menu/MenuFont");
        if (status.font == null) status.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        status.fontSize = 40;
        status.alignment = TextAnchor.MiddleCenter;
        status.horizontalOverflow = HorizontalWrapMode.Overflow;
        status.raycastTarget = false;
        go.AddComponent<Outline>().effectDistance = new Vector2(2, -2);

        ApplyHudLayout();
        ScreenLayout.Changed += ApplyHudLayout;
    }

    void OnDestroy() => ScreenLayout.Changed -= ApplyHudLayout;

    // Landscape: one line across the top. Portrait: sized to the screen's width, wrapped
    // onto two lines if needed, and moved down clear of the Settings button.
    void ApplyHudLayout()
    {
        if (status == null || hudScaler == null) return;
        var rt = status.rectTransform;
        if (ScreenLayout.IsPortrait)
        {
            hudScaler.referenceResolution = new Vector2(1080, 1920);
            hudScaler.matchWidthOrHeight = 0f; // keep the full width
            rt.anchoredPosition = new Vector2(0, -130);
            rt.sizeDelta = new Vector2(-60, 120);
            status.horizontalOverflow = HorizontalWrapMode.Wrap;
        }
        else
        {
            hudScaler.referenceResolution = new Vector2(1920, 1080);
            hudScaler.matchWidthOrHeight = 1f;
            rt.anchoredPosition = new Vector2(0, -20);
            rt.sizeDelta = new Vector2(0, 80);
            status.horizontalOverflow = HorizontalWrapMode.Overflow;
        }
    }

    void SetStatus(string text, Color color)
    {
        if (status == null) return;
        status.text = text;
        status.color = Color.Lerp(color, Color.white, 0.7f);
        status.GetComponent<Outline>().effectColor = new Color(color.r, color.g, color.b, 0.4f);
    }

    // ---------- Sprites ----------

    static Sprite MakeSquareSprite()
    {
        var tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
    }

    static Sprite MakeDotSprite(int res)
    {
        var tex = new Texture2D(res, res);
        float r = res / 2f;
        for (int y = 0; y < res; y++)
            for (int x = 0; x < res; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
                tex.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(r - 1 - d)));
            }
        tex.Apply();
        tex.filterMode = FilterMode.Bilinear;
        return Sprite.Create(tex, new Rect(0, 0, res, res), new Vector2(0.5f, 0.5f), res);
    }
}
