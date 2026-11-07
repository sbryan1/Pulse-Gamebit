using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Shows captured pieces in two trays, plus a "+N" material lead.
//   Landscape: to the right of the board - bottom right for White, top right for Black.
//   Portrait:  below the board for White, above the board for Black.
// White's tray holds the black pieces White has captured, and the other way round.
// Switches layout by itself when the phone rotates.
// Captured pieces fly off the board into place and are sorted pawns -> queen.
//
// Needs nothing set up: GameController adds it automatically, and it listens to
// PieceManager.OnPieceCaptured / OnCleared. Drop it on any object to tweak settings.
public class CapturedPiecesDisplay : MonoBehaviour
{
    public PieceManager pieces;   // found automatically if left empty
    public BoardGenerator board;  // found automatically if left empty

    [Header("Layout (measured in board squares)")]
    [Tooltip("Size of a captured piece compared to a full square.")]
    public float iconSize = 0.5f;
    [Tooltip("Distance between pieces in a row. Less than iconSize makes them overlap slightly.")]
    public float spacing = 0.42f;
    public float rowSpacing = 0.55f;
    [Tooltip("Gap between the board's right edge and the first captured piece.")]
    public float gapFromBoard = 0.5f;
    public int maxPerRow = 8;
    [Tooltip("Portrait only: gap between the board's top/bottom edge and the captured pieces.")]
    public float portraitGapFromBoard = 0.3f;
    [Tooltip("Portrait only: most pieces in one row (the row spans the board's width).")]
    public int portraitMaxPerRow = 16;

    [Header("Animation")]
    [Tooltip("Wait before the piece appears, so the lightning strike plays first.")]
    public float appearDelay = 0.2f;
    public float flyTime = 0.35f;

    [Header("Material lead")]
    public bool showAdvantage = true;

    class Entry
    {
        public char type;
        public GameObject icon;
        public Vector3 baseScale;
        public float appearAt;
        public Coroutine fly;
    }

    readonly List<Entry> byWhite = new(); // black pieces captured by White
    readonly List<Entry> byBlack = new(); // white pieces captured by Black
    TextMesh advantageLabel;
    Font font;

    const int BaseSortingOrder = 4; // above squares (0), markers (1-3) and board pieces (1-2)

    void Awake()
    {
        if (pieces == null) pieces = FindAnyObjectByType<PieceManager>();
        if (board == null) board = FindAnyObjectByType<BoardGenerator>();
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }

    void OnEnable()
    {
        if (pieces == null) return;
        pieces.OnPieceCaptured += HandleCapture;
        pieces.OnCleared += ClearAll;
        ScreenLayout.Changed += HandleScreenChanged;
    }

    void OnDisable()
    {
        if (pieces == null) return;
        pieces.OnPieceCaptured -= HandleCapture;
        pieces.OnCleared -= ClearAll;
        ScreenLayout.Changed -= HandleScreenChanged;
    }

    // Phone rotated: slide every captured piece to its new spot.
    void HandleScreenChanged()
    {
        if (board == null) return;
        Relayout(true);
        Relayout(false);
        UpdateAdvantage();
    }

    public static int PieceValue(char type)
    {
        switch (char.ToLower(type))
        {
            case 'p': return 1;
            case 'n':
            case 'b': return 3;
            case 'r': return 5;
            case 'q': return 9;
            default:  return 0;
        }
    }

    // Positive = White is ahead.
    public int MaterialBalance()
    {
        int balance = 0;
        foreach (var e in byWhite) balance += PieceValue(e.type);
        foreach (var e in byBlack) balance -= PieceValue(e.type);
        return balance;
    }

    // ---------- Events ----------

    void HandleCapture(char type, Vector3 fromWorld)
    {
        if (board == null || pieces == null) return;

        bool whiteCaptured = char.IsLower(type); // a black piece was taken, so White captured it
        float s = board.squareSize;

        var icon = pieces.CreatePieceVisual(type, s * iconSize, BaseSortingOrder);
        icon.transform.SetParent(transform, false);
        var e = new Entry
        {
            type = type,
            icon = icon,
            baseScale = icon.transform.localScale,
            appearAt = Time.time + appearDelay,
        };

        // Start full-size where the piece stood, then shrink while flying to the tray.
        icon.transform.position = new Vector3(fromWorld.x, fromWorld.y, -0.3f);
        icon.transform.localScale = e.baseScale / Mathf.Max(0.01f, iconSize);
        icon.SetActive(false);

        (whiteCaptured ? byWhite : byBlack).Add(e);
        Relayout(whiteCaptured);
        UpdateAdvantage();
    }

    public void ClearAll()
    {
        foreach (var e in byWhite) if (e.icon != null) Destroy(e.icon);
        foreach (var e in byBlack) if (e.icon != null) Destroy(e.icon);
        byWhite.Clear();
        byBlack.Clear();
        StopAllCoroutines();
        if (advantageLabel != null) advantageLabel.gameObject.SetActive(false);
    }

    // ---------- Layout ----------

    void Relayout(bool whiteSide)
    {
        var list = whiteSide ? byWhite : byBlack;

        // Pawns first, then knights, bishops, rooks, queens - like chess.com / lichess.
        list.Sort((a, b) =>
        {
            int c = PieceValue(a.type).CompareTo(PieceValue(b.type));
            return c != 0 ? c : SubOrder(a.type).CompareTo(SubOrder(b.type));
        });

        for (int i = 0; i < list.Count; i++)
        {
            var e = list[i];
            SetSortingOrder(e.icon, BaseSortingOrder + i * 2); // later pieces overlap earlier ones
            if (e.fly != null) StopCoroutine(e.fly);
            e.fly = StartCoroutine(Fly(e, Slot(whiteSide, i)));
        }
    }

    static int SubOrder(char t) => char.ToLower(t) == 'n' ? 0 : 1; // knights before bishops

    // World position of the i-th captured piece in a tray.
    Vector3 Slot(bool whiteSide, int index)
    {
        float s = board.squareSize;

        if (ScreenLayout.IsPortrait)
        {
            // One row spanning the board's width, starting at the a-file.
            // White's tray sits below the board, Black's above; extra rows grow away from the board.
            float x0 = -4f * s + iconSize * s * 0.5f;
            int perRowP = Mathf.Clamp(Mathf.FloorToInt((8f - iconSize) / spacing) + 1, 3, portraitMaxPerRow);
            int rowP = index / perRowP;
            int colP = index % perRowP;
            float edge = 4f * s + portraitGapFromBoard * s + iconSize * s * 0.5f + rowP * rowSpacing * s;
            return new Vector3(x0 + colP * spacing * s, whiteSide ? -edge : edge, -0.1f);
        }

        float startX = 4f * s + gapFromBoard * s + iconSize * s * 0.5f; // board's right edge is at x = 4 squares
        int perRow = PiecesPerRow(startX);
        int row = index / perRow;
        int col = index % perRow;

        // White's tray starts level with rank 1 and grows upward; Black's starts at rank 8 and grows downward.
        float y = whiteSide ? -3.5f * s + row * rowSpacing * s
                            :  3.5f * s - row * rowSpacing * s;
        return new Vector3(startX + col * spacing * s, y, -0.1f);
    }

    // Fits as many pieces per row as the screen width allows (landscape phones vs. widescreen).
    int PiecesPerRow(float startX)
    {
        float s = board.squareSize;
        var cam = Camera.main;
        if (cam == null || !cam.orthographic) return maxPerRow;

        float rightEdge = cam.transform.position.x + cam.orthographicSize * cam.aspect;
        float room = rightEdge - startX - iconSize * s * 0.5f - 0.15f * s;
        int n = Mathf.FloorToInt(room / (spacing * s)) + 1;
        return Mathf.Clamp(n, 3, maxPerRow);
    }

    IEnumerator Fly(Entry e, Vector3 target)
    {
        // New pieces wait for the lightning strike before appearing.
        while (Time.time < e.appearAt) yield return null;
        if (e.icon == null) yield break;
        e.icon.SetActive(true);

        Transform t = e.icon.transform;
        Vector3 startPos = t.position;
        Vector3 startScale = t.localScale;
        float k = 0f;
        while (k < 1f && e.icon != null)
        {
            k += Time.deltaTime / Mathf.Max(0.01f, flyTime);
            float ease = 1f - Mathf.Pow(1f - Mathf.Clamp01(k), 3f); // ease out, like PieceManager's slide
            t.position = Vector3.LerpUnclamped(startPos, target, ease);
            t.localScale = Vector3.LerpUnclamped(startScale, e.baseScale, ease);
            yield return null;
        }
        if (e.icon != null)
        {
            t.position = target;
            t.localScale = e.baseScale;
        }
        e.fly = null;
    }

    static void SetSortingOrder(GameObject icon, int order)
    {
        var sr = icon.GetComponent<SpriteRenderer>();
        if (sr != null) sr.sortingOrder = order;
        foreach (var mr in icon.GetComponentsInChildren<MeshRenderer>(true))
            mr.sortingOrder = order + 1; // placeholder letter sits on top of its circle
    }

    // ---------- "+N" label ----------

    void UpdateAdvantage()
    {
        if (!showAdvantage) return;
        int balance = MaterialBalance();

        if (advantageLabel == null) advantageLabel = MakeLabel();
        if (balance == 0) { advantageLabel.gameObject.SetActive(false); return; }

        bool whiteAhead = balance > 0;
        var list = whiteAhead ? byWhite : byBlack;
        float s = board.squareSize;

        // Sits in the next free slot after the leader's captured pieces.
        Vector3 p = Slot(whiteAhead, list.Count);
        advantageLabel.transform.position = new Vector3(p.x - iconSize * s * 0.4f, p.y, -0.1f);
        advantageLabel.text = "+" + Mathf.Abs(balance);
        advantageLabel.color = whiteAhead ? pieces.whiteBoltColor : pieces.blackBoltColor;
        advantageLabel.gameObject.SetActive(true);
    }

    TextMesh MakeLabel()
    {
        var go = new GameObject("MaterialAdvantage");
        go.transform.SetParent(transform, false);
        go.transform.localScale = Vector3.one * board.squareSize;

        var tm = go.AddComponent<TextMesh>();
        tm.font = font;
        tm.fontSize = 64;
        tm.characterSize = 0.05f;
        tm.anchor = TextAnchor.MiddleLeft;
        tm.alignment = TextAlignment.Left;

        var mr = go.GetComponent<MeshRenderer>();
        mr.material = font.material;
        mr.sortingOrder = 50;
        go.SetActive(false);
        return tm;
    }
}
