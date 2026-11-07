using UnityEngine;
using UnityEngine.UI;

// Gives the game scene the same background as the main menu:
// the deep navy ground color with the neon grid tiled across the whole screen.
// The grid is drawn behind the board, pieces and everything else.
//
// Needs nothing set up: GameController adds it automatically.
// The grid art is loaded from Assets/Resources/Menu/GridCell.png, the same file the menu uses.
public class MenuBackground : MonoBehaviour
{
    // Same as MainMenu's Ground color.
    public Color groundColor = new Color32(8, 6, 26, 255);

    Camera cam;
    Canvas canvas;
    CanvasScaler scaler;

    void Start()
    {
        cam = Camera.main;
        if (cam == null) { Debug.LogWarning("MenuBackground: no Main Camera found."); return; }

        var tex = Resources.Load<Texture2D>("Menu/GridCell");
        if (tex == null) { Debug.LogError("Missing menu art: Assets/Resources/Menu/GridCell.png"); return; }
        tex.wrapMode = TextureWrapMode.Repeat;
        var sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f),
                                   100f, 0, SpriteMeshType.FullRect);

        // A camera-space canvas (not an overlay) so it can sit behind the board.
        var go = new GameObject("BackgroundCanvas", typeof(Canvas), typeof(CanvasScaler));
        canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = cam;
        canvas.sortingOrder = -1000; // drawn before the board squares and pieces

        // Same scaling as the main menu, so the grid cells are the same size on screen.
        scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        ApplyScale();
        ScreenLayout.Changed += ApplyScale;

        var gridGo = new GameObject("Grid", typeof(RectTransform));
        gridGo.transform.SetParent(go.transform, false);
        var grid = gridGo.AddComponent<Image>();
        grid.sprite = sprite;
        grid.type = Image.Type.Tiled;
        grid.raycastTarget = false;
        var rt = grid.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        LateUpdate();
    }

    void OnDestroy() => ScreenLayout.Changed -= ApplyScale;

    // Matches MainMenu.ApplyScale.
    void ApplyScale()
    {
        if (scaler == null) return;
        if (ScreenLayout.IsPortrait)
        {
            scaler.referenceResolution = new Vector2(900, 1600);
            scaler.matchWidthOrHeight = 0f;
        }
        else
        {
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 1f;
        }
    }

    void LateUpdate()
    {
        if (cam == null) return;

        // Other scripts (BoardGenerator, BoardWave) set up the camera too, so keep the ground color applied.
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = groundColor;

        // Keep the grid near the far end of the view, well behind the board,
        // even when BoardWave moves the camera after a screen rotation.
        if (canvas != null)
            canvas.planeDistance = Mathf.Max(cam.nearClipPlane + 1f, cam.farClipPlane * 0.9f);
    }
}
