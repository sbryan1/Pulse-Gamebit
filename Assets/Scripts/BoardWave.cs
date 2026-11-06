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

    [Header("Capture shockwave")]
    [Tooltip("How fast the capture shockwave spreads, in squares per second.")]
    public float rippleSpeed = 10f;
    [Tooltip("Thickness of the shockwave crest, in squares.")]
    public float rippleWidth = 0.8f;
    [Tooltip("How high the shockwave lifts squares and pieces, compared with the normal wave.")]
    public float rippleHeight = 2.5f;
    [Tooltip("Seconds until the shockwave has faded out.")]
    public float rippleLife = 0.8f;
    public Color rippleGlow = new Color32(255, 40, 200, 255);
    [Range(0f, 1f)] public float rippleGlowStrength = 0.75f;

    struct Ripple { public Vector2 center; public float start, strength; }
    readonly List<Ripple> ripples = new();
    float lastRipple; // shockwave amount at the last position Height() looked at

    /// <summary>Sends a shockwave through the board from a world position. Called by CapturePulse.</summary>
    public static void AddRipple(Vector3 worldPos, float strength = 1f)
    {
        if (active == null || !active.isActiveAndEnabled) return;
        active.ripples.Add(new Ripple { center = worldPos, start = Time.time, strength = strength });
    }

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

        // Drop finished shockwaves (backwards loop, no garbage).
        for (int i = ripples.Count - 1; i >= 0; i--)
            if (time - ripples[i].start > rippleLife) ripples.RemoveAt(i);

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
        lastRipple = ripples.Count > 0 ? RippleAt(x, y, time) : 0f;
        return Mathf.Lerp(rings, rolls, diagonalMix) + lastRipple * rippleHeight;
    }

    // Sum of all capture shockwaves at a point (x, y in squares).
    float RippleAt(float x, float y, float time)
    {
        float s = board.squareSize;
        float total = 0f;
        for (int i = 0; i < ripples.Count; i++)
        {
            var r = ripples[i];
            float age = time - r.start;
            if (age < 0f || age > rippleLife) continue;

            float dx = x - r.center.x / s, dy = y - r.center.y / s;
            float d = Mathf.Sqrt(dx * dx + dy * dy);
            float front = age * rippleSpeed;                         // ring radius, in squares
            float k = (d - front) / Mathf.Max(0.05f, rippleWidth);
            float crest = Mathf.Exp(-k * k);                         // smooth bump on the ring
            float u = age / rippleLife;
            float fade = (1f - u) * (1f - u);
            float falloff = 1f / (1f + d * 0.25f);                   // strongest near the capture
            total += crest * fade * falloff * r.strength;
        }
        return total;
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

        float hw = Mathf.Clamp(h, -1f, 1f); // keep normal shading in range; the shockwave glows separately
        float light = 1f + hw * shading;
        Color c = new Color(baseColor.r * light, baseColor.g * light, baseColor.b * light, baseColor.a);
        c = Color.Lerp(c, crestGlow, Mathf.Max(0f, hw) * crestGlowStrength);
        c = Color.Lerp(c, rippleGlow, Mathf.Clamp01(lastRipple * rippleGlowStrength));
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
