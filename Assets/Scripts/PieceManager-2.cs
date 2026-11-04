using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Spawns and moves chess pieces on the BoardGenerator board.
// Works with no art: pieces fall back to colored circles with a letter.
// Assign sprites later to swap in real piece art.
public class PieceManager : MonoBehaviour
{
    public BoardGenerator board;

    [Tooltip("Optional. Order: K Q R B N P (white), then k q r b n p (black). Leave empty to use placeholders.")]
    public Sprite[] pieceSprites = new Sprite[12];

    public Color whiteColor = new Color(0.97f, 0.97f, 0.95f);
    public Color blackColor = new Color(0.12f, 0.12f, 0.12f);

    [Header("Animation")]
    [Tooltip("Seconds a piece takes to slide to its new square. 0 = instant.")]
    public float slideTime = 0.16f;
    [Tooltip("Strike captured pieces with lightning instead of removing them instantly.")]
    public bool lightningCaptures = true;
    public Color whiteBoltColor = new Color32(40, 240, 255, 255);   // used when White captures
    public Color blackBoltColor = new Color32(255, 40, 200, 255);   // used when Black captures

    const string Order = "KQRBNPkqrbnp";
    const float CaptureArrivalDelay = 0.12f; // attacker waits for the bolt to land first

    readonly Dictionary<GameObject, Coroutine> slides = new();

    readonly Dictionary<string, GameObject> pieces = new();
    Sprite circleSprite;
    Font font;

    [Tooltip("Folder inside Assets/Resources to auto-load sprites from when the array is empty.")]
    public string spriteFolder = "NeonPieces";

    static readonly string[] SpriteNames =
    {
        "white_king", "white_queen", "white_rook", "white_bishop", "white_knight", "white_pawn",
        "black_king", "black_queen", "black_rook", "black_bishop", "black_knight", "black_pawn",
    };

    void Awake()
    {
        if (board == null) board = FindFirstObjectByType<BoardGenerator>();

        circleSprite = MakeCircleSprite(64);
        // Unity 2022.2+. On older versions use "Arial.ttf".
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        LoadSpritesFromResources();
    }

    // Fills any empty slots in pieceSprites from Assets/Resources/<spriteFolder>/.
    void LoadSpritesFromResources()
    {
        if (pieceSprites == null || pieceSprites.Length < 12) pieceSprites = new Sprite[12];

        for (int i = 0; i < 12; i++)
        {
            if (pieceSprites[i] != null) continue;
            pieceSprites[i] = Resources.Load<Sprite>(spriteFolder + "/" + SpriteNames[i]);
            if (pieceSprites[i] == null)
                Debug.LogWarning("Missing sprite: Resources/" + spriteFolder + "/" + SpriteNames[i] +
                                 " (is its Texture Type set to Sprite?)");
        }
    }

    // ---------- Setup ----------

    public void LoadFen(string fen)
    {
        Clear();
        string placement = fen.Split(' ')[0];
        int rank = 7, file = 0;
        foreach (char c in placement)
        {
            if (c == '/') { rank--; file = 0; }
            else if (char.IsDigit(c)) file += c - '0';
            else { Spawn(c, file, rank); file++; }
        }
    }

    public void Clear()
    {
        StopAllCoroutines();
        slides.Clear();
        foreach (var go in pieces.Values) Destroy(go);
        pieces.Clear();
    }

    // ---------- Moves ----------

    // Applies a UCI move like "e2e4", "e1g1" (castling) or "e7e8q" (promotion).
    // Returns true if the move captured a piece (including en passant).
    public bool ApplyUciMove(string uci)
    {
        if (uci == null || uci.Length < 4) return false;
        bool didCapture = false;

        string from = uci.Substring(0, 2);
        string to   = uci.Substring(2, 2);

        if (!pieces.TryGetValue(from, out var piece))
        {
            Debug.LogWarning("No piece on " + from + " for move " + uci);
            return false;
        }

        char type = piece.name[0];
        char lower = char.ToLower(type);
        Vector3 startPos = SquarePosition(from);

        // Normal capture
        if (pieces.TryGetValue(to, out var captured))
        {
            Zap(captured, piece);
            pieces.Remove(to);
            didCapture = true;
        }
        // En passant: pawn moves diagonally onto an empty square
        else if (lower == 'p' && from[0] != to[0])
        {
            string epSquare = "" + to[0] + from[1];
            if (pieces.TryGetValue(epSquare, out var epPawn))
            {
                Zap(epPawn, piece);
                pieces.Remove(epSquare);
                didCapture = true;
            }
        }

        pieces.Remove(from);

        // Castling: king moves two files, so move the rook too
        if (lower == 'k' && Mathf.Abs(to[0] - from[0]) == 2)
        {
            bool kingside = to[0] > from[0];
            string rookFrom = "" + (kingside ? 'h' : 'a') + from[1];
            string rookTo   = "" + (kingside ? 'f' : 'd') + from[1];
            MovePiece(rookFrom, rookTo);
        }

        // Promotion: replace the pawn with the new piece
        if (uci.Length == 5)
        {
            Destroy(piece);
            char promo = char.IsUpper(type) ? char.ToUpper(uci[4]) : char.ToLower(uci[4]);
            var promoted = Spawn(promo, to[0] - 'a', to[1] - '1');
            Slide(promoted, startPos, didCapture ? CaptureArrivalDelay : 0f);
        }
        else
        {
            Place(piece, type, to);
            Slide(piece, startPos, didCapture ? CaptureArrivalDelay : 0f);
        }

        return didCapture;
    }

    // ---------- Capture effect & sliding ----------

    void Zap(GameObject victim, GameObject attacker)
    {
        if (!lightningCaptures) { Destroy(victim); return; }
        bool whiteAttacks = char.IsUpper(attacker.name[0]);
        LightningZap.Strike(attacker.transform.position, victim,
                            whiteAttacks ? whiteBoltColor : blackBoltColor, board.squareSize);
    }

    Vector3 SquarePosition(string square) => board.SquareToWorld(square) + new Vector3(0, 0, -0.1f);

    // The piece is already logically on its new square; this just animates it there.
    void Slide(GameObject go, Vector3 from, float delay)
    {
        if (go == null || slideTime <= 0f) return;
        Vector3 to = go.transform.position;
        if (slides.TryGetValue(go, out var running) && running != null) StopCoroutine(running);
        go.transform.position = from;
        slides[go] = StartCoroutine(SlideRoutine(go, from, to, delay));
    }

    IEnumerator SlideRoutine(GameObject go, Vector3 from, Vector3 to, float delay)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);
        float t = 0f;
        while (t < 1f && go != null)
        {
            t += Time.deltaTime / slideTime;
            float e = 1f - Mathf.Pow(1f - Mathf.Clamp01(t), 3f); // ease out
            go.transform.position = Vector3.LerpUnclamped(from, to, e);
            yield return null;
        }
        if (go != null)
        {
            go.transform.position = to;
            slides.Remove(go);
        }
    }

    void MovePiece(string from, string to)
    {
        if (!pieces.TryGetValue(from, out var go)) return;
        pieces.Remove(from);
        Place(go, go.name[0], to);
        Slide(go, SquarePosition(from), 0f);
    }

    void Place(GameObject go, char type, string square)
    {
        pieces[square] = go;
        go.name = type + "_" + square;
        go.transform.position = SquarePosition(square);
    }

    // ---------- Spawning ----------

    GameObject Spawn(char type, int file, int rank)
    {
        var go = new GameObject();
        go.transform.SetParent(transform);

        bool white = char.IsUpper(type);
        float size = board.squareSize;

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sortingOrder = 1; // draw above the squares

        int i = Order.IndexOf(type);
        Sprite custom = (pieceSprites != null && i >= 0 && i < pieceSprites.Length) ? pieceSprites[i] : null;

        if (custom != null)
        {
            sr.sprite = custom;
            float largest = Mathf.Max(custom.bounds.size.x, custom.bounds.size.y);
            go.transform.localScale = Vector3.one * (size * 0.9f / largest);
        }
        else
        {
            sr.sprite = circleSprite;
            sr.color = white ? whiteColor : blackColor;
            go.transform.localScale = Vector3.one * size * 0.8f;
            AddLabel(go, char.ToUpper(type).ToString(), white ? blackColor : whiteColor);
        }

        Place(go, type, "" + (char)('a' + file) + (char)('1' + rank));
        return go;
    }

    void AddLabel(GameObject parent, string text, Color color)
    {
        var label = new GameObject("Label");
        label.transform.SetParent(parent.transform, false);
        label.transform.localPosition = new Vector3(0, 0, -0.01f);

        var tm = label.AddComponent<TextMesh>();
        tm.text = text;
        tm.font = font;
        tm.fontSize = 64;
        tm.characterSize = 0.1f; // tweak if letters look too big or small
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.color = color;

        var mr = label.GetComponent<MeshRenderer>();
        mr.material = font.material;
        mr.sortingOrder = 2;
    }

    Sprite MakeCircleSprite(int res)
    {
        var tex = new Texture2D(res, res);
        float r = res / 2f;
        for (int y = 0; y < res; y++)
            for (int x = 0; x < res; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
                tex.SetPixel(x, y, d <= r - 1 ? Color.white : Color.clear);
            }
        tex.Apply();
        tex.filterMode = FilterMode.Bilinear;
        return Sprite.Create(tex, new Rect(0, 0, res, res), new Vector2(0.5f, 0.5f), res);
    }
}
