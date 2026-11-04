using System.Collections.Generic;
using UnityEngine;

// Makes the chess board a 3D scene: tilts the camera into perspective and rolls
// a wave across the squares. Pieces, move highlights and captured pieces ride
// the wave, so they always stay on their squares.
// Add this to the same GameObject as BoardGenerator (or any object in the chess scene).
[DefaultExecutionOrder(1000)] // run after the other scripts have placed things this frame
public class BoardWave : MonoBehaviour
{
    [Header("Camera")]
    [Tooltip("How far the camera leans back from looking straight down, in degrees.")]
    [Range(0f, 60f)] public float tiltAngle = 28f;
    [Range(20f, 80f)] public float fieldOfView = 42f;
    [Tooltip("Extra room around the board. Raise it if the board touches the screen edges.")]
    public float framing = 1.12f;

    [Header("Wave")]
    [Tooltip("How high the crests rise toward the camera, in squares.")]
    public float amplitude = 0.3f;
    [Tooltip("Distance between crests, in squares.")]
    public float wavelength = 4f;
    [Tooltip("How fast the wave rolls outward.")]
    public float speed = 2.2f;
    [Tooltip("0 = rings spreading from the center, 1 = straight waves rolling across.")]
    [Range(0f, 1f)] public float diagonalMix = 0.35f;

    [Header("Light")]
    [Tooltip("How much darker troughs and brighter crests get.")]
    [Range(0f, 0.6f)] public float shading = 0.28f;
    public Color crestGlow = new Color32(40, 240, 255, 255);
    [Range(0f, 1f)] public float crestGlowStrength = 0.3f;

    BoardGenerator board;
    PieceManager pieces;
    Transform markers;

    // Remembers each object's resting depth, so the wave offset never piles up.
    // The wave only changes depth (z); x and y are always left to the other scripts.
    class Tracked { public float baseZ, appliedZ; public SpriteRenderer sr; public bool srChecked; }

    // Differences smaller than this are rounding noise from Unity, not a real move.
    const float MoveTolerance = 0.002f;
    static BoardWave active;
    readonly Dictionary<Transform, Tracked> tracked = new();
    readonly Dictionary<SpriteRenderer, Color> squareColors = new();
    readonly List<Transform> scratch = new();
    int cleanupTimer;

    void Awake()
    {
        // Two BoardWaves would each treat the other's lift as a real move and stack them,
        // sending the board flying. Only the first one runs.
        if (active != null && active != this)
        {
            Debug.LogWarning("Extra BoardWave on '" + name + "' disabled. Keep only one in the scene.");
            enabled = false;
            return;
        }
        active = this;
    }

    void OnDestroy() { if (active == this) active = null; }

    void Start()
    {
        board = GetComponent<BoardGenerator>();
        if (board == null) board = FindFirstObjectByType<BoardGenerator>();
        pieces = FindFirstObjectByType<PieceManager>();
        SetupCamera();
    }

    void SetupCamera()
    {
        var cam = Camera.main;
        if (cam == null || board == null) return;

        float s = board.squareSize;
        float halfBoard = 4f * s * framing;
        float distance = halfBoard / Mathf.Tan(fieldOfView * 0.5f * Mathf.Deg2Rad) + amplitude * s;
        float t = tiltAngle * Mathf.Deg2Rad;

        // The camera sits below and in front of the board, looking at its center.
        cam.orthographic = false;
        cam.fieldOfView = fieldOfView;
        cam.transform.position = new Vector3(0f, -Mathf.Sin(t) * distance, -Mathf.Cos(t) * distance);
        cam.transform.rotation = Quaternion.LookRotation(-cam.transform.position, Vector3.up);
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = distance * 4f;
    }

    void LateUpdate()
    {
        if (board == null) return;
        if (markers == null) { var m = GameObject.Find("MoveMarkers"); if (m != null) markers = m.transform; }

        float time = Time.time;

        // Index loops instead of foreach: foreach over a Transform creates garbage
        // every frame, which causes stutters on phones.

        // Board squares: rise, fall and catch the light.
        var bt = board.transform;
        for (int i = 0; i < bt.childCount; i++)
        {
            var sq = bt.GetChild(i);
            float h = Apply(sq, time);
            Shade(sq, h);
        }

        // Pieces (and pieces being zapped), plus selection highlights, ride along.
        if (pieces != null) { var pt = pieces.transform; for (int i = 0; i < pt.childCount; i++) Apply(pt.GetChild(i), time); }
        if (markers != null) for (int i = 0; i < markers.childCount; i++) Apply(markers.GetChild(i), time);

        if (++cleanupTimer > 120) { cleanupTimer = 0; RemoveDestroyed(); }
    }

    // Moves one object to its wave height and returns that height (-1..1).
    float Apply(Transform tr, float time)
    {
        Vector3 p = tr.position;
        if (!tracked.TryGetValue(tr, out var info))
        {
            info = new Tracked { baseZ = p.z, appliedZ = p.z };
            tracked[tr] = info;
        }
        else if (Mathf.Abs(p.z - info.appliedZ) > MoveTolerance)
        {
            // Another script set a new depth (a fresh placement): that's the new resting depth.
            info.baseZ = p.z;
        }

        float h = Height(p, time);
        float z = info.baseZ - h * amplitude * board.squareSize;
        tr.position = new Vector3(p.x, p.y, z);
        info.appliedZ = z;
        return h;
    }

    float Height(Vector3 pos, float time)
    {
        float s = board.squareSize;
        float x = pos.x / s, y = pos.y / s;
        float k = 2f * Mathf.PI / Mathf.Max(0.5f, wavelength);

        float rings = Mathf.Sin(Mathf.Sqrt(x * x + y * y) * k - time * speed);
        float rolls = Mathf.Sin((x * 0.8f + y * 0.6f) * k - time * speed * 0.7f);
        return Mathf.Lerp(rings, rolls, diagonalMix);
    }

    void Shade(Transform sq, float h)
    {
        var info = tracked[sq];
        if (!info.srChecked) { info.sr = sq.GetComponent<SpriteRenderer>(); info.srChecked = true; }
        var sr = info.sr;
        if (sr == null) return;
        if (!squareColors.TryGetValue(sr, out var baseColor))
        {
            baseColor = sr.color;
            squareColors[sr] = baseColor;
        }

        float light = 1f + h * shading;
        Color c = new Color(baseColor.r * light, baseColor.g * light, baseColor.b * light, baseColor.a);
        c = Color.Lerp(c, crestGlow, Mathf.Max(0f, h) * crestGlowStrength);
        c.a = baseColor.a;
        sr.color = c;
    }

    void RemoveDestroyed()
    {
        scratch.Clear();
        foreach (var tr in tracked.Keys) if (tr == null) scratch.Add(tr);
        foreach (var tr in scratch) tracked.Remove(tr);
    }

    void OnDisable()
    {
        // Put everything back flat if the wave is switched off.
        foreach (var kv in tracked)
            if (kv.Key != null)
            {
                var p = kv.Key.position;
                kv.Key.position = new Vector3(p.x, p.y, kv.Value.baseZ);
            }
        foreach (var kv in squareColors) if (kv.Key != null) kv.Key.color = kv.Value;
        tracked.Clear();
    }
}
