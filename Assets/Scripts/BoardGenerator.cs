using UnityEngine;

public class BoardGenerator : MonoBehaviour
{
    public Color lightColor = new Color(0.93f, 0.85f, 0.71f);
    public Color darkColor  = new Color(0.71f, 0.53f, 0.39f);
    public float squareSize = 1f;
    public Color backgroundColor = new Color32(8, 6, 26, 255); // deep neon navy
    Sprite squareSprite;

    // True when the player is Black: the board is turned so Black sits at the bottom.
    [HideInInspector] public bool flipped;

    void Awake()
    {
        flipped = !GameSettings.PlayerIsWhite;
    }

    void Start()
    {
        squareSprite = MakeSquareSprite();
        BuildBoard();
        SetupCamera();
        // Only needed without BoardWave (which sets up its own 3D camera).
        if (GetComponent<BoardWave>() == null) ScreenLayout.Changed += SetupCamera;
    }

    void OnDestroy() => ScreenLayout.Changed -= SetupCamera;

    void BuildBoard()
    {
        for (int file = 0; file < 8; file++)       // 0 = a ... 7 = h
        {
            for (int rank = 0; rank < 8; rank++)   // 0 = rank 1 ... 7 = rank 8
            {
                var go = new GameObject(SquareName(file, rank));
                go.transform.SetParent(transform);
                go.transform.position = SquareToWorld(file, rank);

                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = squareSprite;
                // a1 is dark, so (file + rank) even = dark
                sr.color = ((file + rank) % 2 == 0) ? darkColor : lightColor;

                // scale the 1x1 unit sprite to the square size
                go.transform.localScale = new Vector3(squareSize, squareSize, 1f);
            }
        }
    }

    // Converts file/rank indices to a world position.
    // White at the bottom, or Black at the bottom when flipped.
    public Vector3 SquareToWorld(int file, int rank)
    {
        if (flipped) { file = 7 - file; rank = 7 - rank; }
        float offset = 3.5f * squareSize;
        return new Vector3(file * squareSize - offset, rank * squareSize - offset, 0f);
    }

    // Converts a name like "e4" to a world position.
    public Vector3 SquareToWorld(string square)
    {
        int file = square[0] - 'a';
        int rank = square[1] - '1';
        return SquareToWorld(file, rank);
    }

    string SquareName(int file, int rank)
    {
        return "" + (char)('a' + file) + (char)('1' + rank);
    }

    // Makes a plain white 1x1 unit sprite so no image assets are needed.
    Sprite MakeSquareSprite()
    {
        var tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        tex.filterMode = FilterMode.Point;
        return Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
    }

    void SetupCamera()
    {
        var cam = Camera.main;
        if (cam == null) return;
        cam.orthographic = true;
        // Fits the board plus room for captured pieces: beside it in landscape,
        // above and below it in portrait.
        cam.orthographicSize = ScreenLayout.IsPortrait
            ? Mathf.Max(5.6f * squareSize, 4.5f * squareSize / Mathf.Max(0.01f, cam.aspect))
            : 4.5f * squareSize;
        cam.transform.position = new Vector3(0, 0, -10);
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = backgroundColor;



    }
}
