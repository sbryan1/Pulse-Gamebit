using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace PulseGambit.Settings
{
    /// <summary>
    /// Builds the rounded / outlined / glow sprites and UI elements for the neon look at runtime,
    /// so no textures or prefabs need to be imported.
    /// </summary>
    internal static class NeonUI
    {
        public const int GlowPad = 28;
        static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

        // ---------- Sprites ----------

        public static Sprite Fill(int radius) => Get("fill" + radius, () => MakeRounded(radius, 0f));
        public static Sprite Stroke(int radius, float width) => Get("stroke" + radius + "_" + width, () => MakeRounded(radius, width));
        public static Sprite Glow(int radius) => Get("glow" + radius, () => MakeGlow(radius));

        static Sprite Get(string key, System.Func<Sprite> make)
        {
            if (cache.TryGetValue(key, out var s) && s != null) return s;
            s = make();
            cache[key] = s;
            return s;
        }

        static float SdRoundBox(Vector2 p, Vector2 b, float r)
        {
            Vector2 q = new Vector2(Mathf.Abs(p.x), Mathf.Abs(p.y)) - b + new Vector2(r, r);
            Vector2 qMax = new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f));
            return qMax.magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - r;
        }

        static Texture2D NewTex(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            tex.hideFlags = HideFlags.DontSave;
            return tex;
        }

        static Sprite MakeRounded(int radius, float stroke)
        {
            int size = radius * 2 + 4;
            float half = size / 2f;
            Vector2 box = new Vector2(radius + 1, radius + 1);
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                Vector2 p = new Vector2(x + 0.5f - half, y + 0.5f - half);
                float d = SdRoundBox(p, box, radius);
                float a = stroke <= 0f
                    ? Mathf.Clamp01(0.5f - d)
                    : Mathf.Clamp01(0.5f - (Mathf.Abs(d + stroke * 0.5f) - stroke * 0.5f));
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
            var tex = NewTex(size);
            tex.SetPixels32(px);
            tex.Apply();
            float border = radius + 1;
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(border, border, border, border));
        }

        static Sprite MakeGlow(int radius)
        {
            int size = 2 * (GlowPad + radius + 2);
            float half = size / 2f;
            Vector2 box = new Vector2(radius + 1, radius + 1);
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                Vector2 p = new Vector2(x + 0.5f - half, y + 0.5f - half);
                float d = SdRoundBox(p, box, radius);
                float a = d <= 0f ? 1f : Mathf.Pow(1f - Mathf.Clamp01(d / GlowPad), 2.2f);
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
            var tex = NewTex(size);
            tex.SetPixels32(px);
            tex.Apply();
            float border = GlowPad + radius + 1;
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(border, border, border, border));
        }

        // ---------- Font ----------

        public static Font DefaultFont()
        {
            Font f = null;
#if UNITY_2022_2_OR_NEWER
            f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
#else
            f = Resources.GetBuiltinResource<Font>("Arial.ttf");
#endif
            if (f == null) f = Font.CreateDynamicFontFromOSFont("Arial", 32);
            return f;
        }

        // ---------- Elements ----------

        public static RectTransform Rect(string name, Transform parent, Vector2 size, Vector2 pos)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5; // UI
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
            return rt;
        }

        public static RectTransform Stretch(RectTransform rt, float pad = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(-pad, -pad);
            rt.offsetMax = new Vector2(pad, pad);
            return rt;
        }

        /// <summary>Adds a stretched child image layer (drawn in order of creation).</summary>
        public static Image Layer(RectTransform parent, string name, Sprite sprite, Color color, float pad = 0f, bool raycast = false)
        {
            var rt = Stretch(Rect(name, parent, Vector2.zero, Vector2.zero), pad);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.type = Image.Type.Sliced;
            img.color = color;
            img.raycastTarget = raycast;
            return img;
        }

        public static Image GlowLayer(RectTransform parent, int radius, Color color) =>
            Layer(parent, "Glow", Glow(radius), color, GlowPad + 1);

        public static Text Label(Transform parent, string name, string content, Font font, int size,
            Color color, TextAnchor align, Vector2 rectSize, Vector2 pos)
        {
            var rt = Rect(name, parent, rectSize, pos);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = font;
            t.text = content;
            t.fontSize = size;
            t.fontStyle = FontStyle.Bold;
            t.alignment = align;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;

            // Two soft outlines fake a neon bloom around the letters.
            var inner = rt.gameObject.AddComponent<Outline>();
            inner.effectDistance = new Vector2(1.5f, -1.5f);
            var outer = rt.gameObject.AddComponent<Outline>();
            outer.effectDistance = new Vector2(3.5f, -3.5f);
            SetTextColor(t, color);
            return t;
        }

        public static void SetTextColor(Text t, Color c)
        {
            t.color = c;
            var outlines = t.GetComponents<Outline>();
            if (outlines.Length > 0) outlines[0].effectColor = WithA(c, 0.45f * c.a);
            if (outlines.Length > 1) outlines[1].effectColor = WithA(c, 0.18f * c.a);
        }

        public static Color WithA(Color c, float a) => new Color(c.r, c.g, c.b, a);

        /// <summary>A button whose transparent fill brightens on hover/press with the given tint.</summary>
        public static Button HoverButton(Image fill, Color tint, float normalA, float hoverA, float pressA)
        {
            fill.color = Color.white;
            fill.raycastTarget = true;
            var b = fill.gameObject.AddComponent<Button>();
            b.targetGraphic = fill;
            var cb = b.colors;
            cb.normalColor = WithA(tint, normalA);
            cb.highlightedColor = WithA(tint, hoverA);
            cb.pressedColor = WithA(tint, pressA);
            cb.selectedColor = WithA(tint, normalA);
            cb.disabledColor = WithA(tint, 0f);
            cb.colorMultiplier = 1f;
            cb.fadeDuration = 0.08f;
            b.colors = cb;
            var nav = b.navigation;
            nav.mode = Navigation.Mode.None;
            b.navigation = nav;
            return b;
        }
    }
}
