using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Pulse Gambit — plays a pulse / motion-blur shockwave on the board when a piece is captured.
///
/// Usage from your capture code:
///     CapturePulse.Instance?.Play(capturedSquareWorldPos);
///     CapturePulse.Instance?.Play(pos, CapturePulse.StrengthFor('q'));   // bigger hit for a queen
///
/// Three layers, each optional:
///   1. Shockwave ring  — an expanding distortion + radial blur on the board (CaptureShockwave.shader).
///   2. Screen pulse    — a URP Volume (Motion Blur, Chromatic Aberration, Lens Distortion…) faded in/out.
///   3. Camera punch    — a short camera kick; this also makes URP's camera Motion Blur actually show.
/// </summary>
public class CapturePulse : MonoBehaviour
{
    public static CapturePulse Instance { get; private set; }

    [Header("Shockwave ring")]
    [Tooltip("Material using PulseGambit/CaptureShockwave.")]
    [SerializeField] private Material shockwaveMaterial;
    [Tooltip("Diameter of the shockwave in world units. ~3–4 squares looks good.")]
    [SerializeField] private float shockwaveSize = 4f;
    [SerializeField] private float shockwaveDuration = 0.55f;
    [Tooltip("Lift above the board surface to avoid z-fighting.")]
    [SerializeField] private float surfaceOffset = 0.01f;
    [Tooltip("Board's up direction. Vector3.up for a 3D board on XZ, Vector3.back for a 2D board on XY.")]
    [SerializeField] private Vector3 boardNormal = Vector3.back;
    [Tooltip("2D only: sorting layer for the shockwave. Must draw after the Camera Sorting Layer Texture's Foremost Sorting Layer.")]
    [SerializeField] private string sortingLayerName = "CaptureFX";
    [Tooltip("2D only: order within the sorting layer. Keep it above your board and pieces.")]
    [SerializeField] private int sortingOrder = 1000;
    [SerializeField] private AnimationCurve radiusOverTime = new AnimationCurve(
        new Keyframe(0f, 0f, 0f, 3f), new Keyframe(1f, 1f, 0f, 0f));      // fast start, eases out
    [SerializeField] private AnimationCurve fadeOverTime = new AnimationCurve(
        new Keyframe(0f, 1f), new Keyframe(0.6f, 0.85f), new Keyframe(1f, 0f));

    [Header("Screen pulse (optional)")]
    [Tooltip("A URP Volume (Is Global, Weight 0) holding Motion Blur / Chromatic Aberration / Lens Distortion.")]
    [SerializeField] private Volume pulseVolume;
    [SerializeField] private float pulseDuration = 0.35f;
    [SerializeField] private AnimationCurve pulseWeight = new AnimationCurve(
        new Keyframe(0f, 0f), new Keyframe(0.12f, 1f), new Keyframe(1f, 0f));

    [Header("Camera punch (optional)")]
    [Tooltip("Usually the camera itself or its parent rig.")]
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private float punchDistance = 0.12f;
    [SerializeField] private float punchDuration = 0.2f;

    [Header("Debug")]
    [Tooltip("Logs every Play() call to the Console.")]
    [SerializeField] private bool debugLog = true;
    [Tooltip("Press P in Play mode to fire a pulse at this transform (e.g. your Board). Empty = centre of the screen.")]
    [SerializeField] private Transform testTarget;

    private static readonly int RadiusId    = Shader.PropertyToID("_Radius");
    private static readonly int FadeId       = Shader.PropertyToID("_Fade");
    private static readonly int DistortionId = Shader.PropertyToID("_Distortion");
    private static readonly int BlurId       = Shader.PropertyToID("_BlurStrength");
    private static readonly int GlowId       = Shader.PropertyToID("_Glow");

    private readonly Queue<MeshRenderer> pool = new Queue<MeshRenderer>();
    private MaterialPropertyBlock mpb;
    private float baseDistortion, baseBlur, baseGlow;

    private Coroutine pulseRoutine, punchRoutine;
    private Vector3 cameraRestPos;
    private bool punching;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        mpb = new MaterialPropertyBlock();
        if (shockwaveMaterial != null)
        {
            baseDistortion = shockwaveMaterial.GetFloat(DistortionId);
            baseBlur       = shockwaveMaterial.GetFloat(BlurId);
            baseGlow       = shockwaveMaterial.GetFloat(GlowId);
        }
        if (pulseVolume != null) pulseVolume.weight = 0f;
        if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;

        // Pulse Gambit's board is always flat on the XY plane, facing the camera.
        if (FindFirstObjectByType<BoardGenerator>() != null) boardNormal = Vector3.back;

        // The 2D Renderer only lets objects on a LATER sorting layer read the board's screen copy.
        if (sortingLayerName == "Default" || SortingLayer.NameToID(sortingLayerName) == 0)
            Debug.LogWarning("[CapturePulse] Sorting Layer Name is '" + sortingLayerName + "'. The board ripple " +
                             "(BoardWave) still works, but for the blur ring create a sorting layer called " +
                             "'CaptureFX' below Default in Tags and Layers, and type its name here.", this);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>Play the capture effect centred on a world position (the captured square).</summary>
    /// <param name="strength">1 = normal. Try 0.7 for pawns, 1.6 for a queen.</param>
    private void Update()
    {
        bool pressed = false;
#if ENABLE_INPUT_SYSTEM
        pressed |= UnityEngine.InputSystem.Keyboard.current != null &&
                   UnityEngine.InputSystem.Keyboard.current.pKey.wasPressedThisFrame;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        pressed |= Input.GetKeyDown(KeyCode.P);
#endif
        if (pressed) TestPulse();
    }

    [ContextMenu("Test pulse")]
    public void TestPulse()
    {
        Vector3 pos;
        if (testTarget != null) pos = testTarget.position;
        else
        {
            Camera cam = Camera.main;
            pos = cam != null ? cam.ViewportToWorldPoint(new Vector3(0.5f, 0.5f, -cam.transform.position.z)) : Vector3.zero;
            pos.z = 0f;
        }
        Play(pos, 1.5f);
    }

    public void Play(Vector3 worldPos, float strength = 1f)
    {
        if (debugLog)
        {
            Camera cam = Camera.main;
            string onScreen = "no Main Camera";
            if (cam != null)
            {
                Vector3 vp = cam.WorldToViewportPoint(worldPos);
                bool visible = vp.z > 0 && vp.x >= 0 && vp.x <= 1 && vp.y >= 0 && vp.y <= 1;
                float viewHeight = cam.orthographic ? cam.orthographicSize * 2f : float.NaN;
                onScreen = (visible ? "on screen" : "OFF SCREEN") + $" (viewport {vp.x:0.00},{vp.y:0.00}), camera view height {viewHeight:0.##} units";
            }
            Debug.Log($"[CapturePulse] Play at {worldPos}, strength {strength:0.00}, ring size {shockwaveSize * Mathf.Lerp(1f, strength, 0.5f):0.##} units, {onScreen}" +
                      (shockwaveMaterial == null ? " — NO MATERIAL ASSIGNED" : ""), this);
        }

        // Shockwave through the board itself: squares and pieces lift and glow as it passes.
        BoardWave.AddRipple(worldPos, strength);

        if (shockwaveMaterial != null)
            StartCoroutine(ShockwaveRoutine(worldPos, strength));

        if (pulseVolume != null)
        {
            if (pulseRoutine != null) StopCoroutine(pulseRoutine);
            pulseRoutine = StartCoroutine(VolumeRoutine(strength));
        }

        if (cameraTransform != null && punchDistance > 0f)
        {
            if (punchRoutine != null) StopCoroutine(punchRoutine);
            punchRoutine = StartCoroutine(PunchRoutine(worldPos, strength));
        }
    }

    /// <summary>Handy scaling by captured piece (FEN letter, either case).</summary>
    public static float StrengthFor(char piece)
    {
        switch (char.ToLowerInvariant(piece))
        {
            case 'p': return 0.7f;
            case 'n':
            case 'b': return 1.0f;
            case 'r': return 1.25f;
            case 'q': return 1.6f;
            default:  return 1.0f;
        }
    }

    // ---------------------------------------------------------------- shockwave

    private IEnumerator ShockwaveRoutine(Vector3 worldPos, float strength)
    {
        Vector3 n = boardNormal.sqrMagnitude > 0f ? boardNormal.normalized : Vector3.up;
        MeshRenderer r = GetQuad();
        Transform t = r.transform;
        t.SetPositionAndRotation(worldPos + n * surfaceOffset, Quaternion.LookRotation(-n));
        t.localScale = Vector3.one * shockwaveSize * Mathf.Lerp(1f, strength, 0.5f);
        r.gameObject.SetActive(true);

        float duration = shockwaveDuration * Mathf.Lerp(1f, strength, 0.25f);
        for (float time = 0f; time < duration; time += Time.unscaledDeltaTime)
        {
            float u = time / duration;
            r.GetPropertyBlock(mpb);
            mpb.SetFloat(RadiusId, radiusOverTime.Evaluate(u));
            mpb.SetFloat(FadeId, fadeOverTime.Evaluate(u));
            mpb.SetFloat(DistortionId, baseDistortion * strength);
            mpb.SetFloat(BlurId, baseBlur * strength);
            mpb.SetFloat(GlowId, baseGlow * strength);
            r.SetPropertyBlock(mpb);
            yield return null;
        }

        r.gameObject.SetActive(false);
        pool.Enqueue(r);
    }

    private MeshRenderer GetQuad()
    {
        if (pool.Count > 0) return pool.Dequeue();

        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = "CaptureShockwave";
        go.transform.SetParent(transform, false);
        Destroy(go.GetComponent<Collider>());   // must not block board raycasts

        MeshRenderer r = go.GetComponent<MeshRenderer>();
        r.sharedMaterial = shockwaveMaterial;
        r.shadowCastingMode = ShadowCastingMode.Off;
        r.receiveShadows = false;
        r.sortingLayerName = sortingLayerName;
        r.sortingOrder = sortingOrder;
        go.SetActive(false);
        return r;
    }

    // ---------------------------------------------------------------- volume pulse

    private IEnumerator VolumeRoutine(float strength)
    {
        float peak = Mathf.Clamp01(strength);
        for (float time = 0f; time < pulseDuration; time += Time.unscaledDeltaTime)
        {
            pulseVolume.weight = pulseWeight.Evaluate(time / pulseDuration) * peak;
            yield return null;
        }
        pulseVolume.weight = 0f;
        pulseRoutine = null;
    }

    // ---------------------------------------------------------------- camera punch

    private IEnumerator PunchRoutine(Vector3 worldPos, float strength)
    {
        if (!punching) cameraRestPos = cameraTransform.localPosition;
        punching = true;

        // Kick the camera toward the captured square, then spring back.
        Vector3 toTarget = (worldPos - cameraTransform.position).normalized;
        Vector3 kickWorld = toTarget * punchDistance * strength;
        Vector3 kickLocal = cameraTransform.parent != null
            ? cameraTransform.parent.InverseTransformVector(kickWorld)
            : kickWorld;

        for (float time = 0f; time < punchDuration; time += Time.unscaledDeltaTime)
        {
            float u = time / punchDuration;
            float env = Mathf.Sin(u * Mathf.PI) * (1f - u);     // sharp in, damped out
            cameraTransform.localPosition = cameraRestPos + kickLocal * env * 2f;
            yield return null;
        }

        cameraTransform.localPosition = cameraRestPos;
        punching = false;
        punchRoutine = null;
    }
}
