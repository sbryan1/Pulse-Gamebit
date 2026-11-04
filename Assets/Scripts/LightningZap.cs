using System.Collections.Generic;
using UnityEngine;

// Capture effect: lightning arcs from the attacking piece into the captured one,
// the victim flickers and shakes, then bursts into a flash and a spray of sparks.
// Start one with LightningZap.Strike(...). It cleans itself (and the victim) up.
public class LightningZap : MonoBehaviour
{
    public float boltTime = 0.32f;      // how long the lightning crackles
    public float dissolveTime = 0.28f;  // how long the piece takes to burst apart
    public int sparkCount = 18;

    class Bolt { public LineRenderer glow, core; public Vector3 a, b; public float jag; public bool roams; }
    class Spark { public Transform tr; public SpriteRenderer sr; public Vector3 vel; public float age, life; }

    Color color;
    float size;
    Vector3 source, center;
    GameObject victim;
    Vector3 victimScale;
    Renderer[] victimRenderers;
    SpriteRenderer[] victimSprites;
    Material mat;

    readonly List<Bolt> bolts = new();
    readonly List<Spark> sparks = new();
    SpriteRenderer flash;
    float t, nextJag;
    bool burst;

    static Sprite softDot;

    public static void Strike(Vector3 source, GameObject victim, Color color, float squareSize)
    {
        if (victim == null) return;
        var go = new GameObject("LightningZap");
        go.AddComponent<LightningZap>().Init(source, victim, color, squareSize);
    }

    void Init(Vector3 src, GameObject v, Color c, float squareSize)
    {
        source = src;
        victim = v;
        color = c;
        size = squareSize;
        center = v.transform.position;
        victimScale = v.transform.localScale;

        victimRenderers = v.GetComponentsInChildren<Renderer>();
        victimSprites = v.GetComponentsInChildren<SpriteRenderer>();
        foreach (var r in victimRenderers) r.sortingOrder += 4; // draw over the piece sliding in

        // Reuse the pieces' own material so this works in any render pipeline.
        var sr = v.GetComponent<SpriteRenderer>();
        mat = sr != null ? sr.sharedMaterial : new Material(Shader.Find("Sprites/Default"));
        if (softDot == null) softDot = MakeSoftDot(64);

        Vector3 hit = center + new Vector3(0, 0, -0.3f);

        // Main bolt from the attacker into the victim
        AddBolt(source + new Vector3(0, 0, -0.3f), hit, 0.22f, false);
        // A second, thinner strand of the same bolt
        AddBolt(source + new Vector3(0, 0, -0.3f), hit, 0.32f, false, 0.6f);
        // Short arcs crawling over the victim
        for (int i = 0; i < 3; i++) AddBolt(hit, hit, 0.18f, true, 0.7f);

        Rejag();
    }

    void AddBolt(Vector3 a, Vector3 b, float jag, bool roams, float widthScale = 1f)
    {
        var bolt = new Bolt
        {
            a = a, b = b, jag = jag, roams = roams,
            glow = MakeLine(0.16f * size * widthScale, new Color(color.r, color.g, color.b, 0.45f), 9),
            core = MakeLine(0.04f * size * widthScale, Color.white, 10),
        };
        bolts.Add(bolt);
    }

    LineRenderer MakeLine(float width, Color c, int order)
    {
        var go = new GameObject("Bolt");
        go.transform.SetParent(transform, false);
        var lr = go.AddComponent<LineRenderer>();
        lr.sharedMaterial = mat;
        lr.useWorldSpace = true;
        lr.widthMultiplier = width;
        lr.numCapVertices = 2;
        lr.numCornerVertices = 2;
        lr.startColor = lr.endColor = c;
        lr.sortingOrder = order;
        return lr;
    }

    // Builds a fresh jagged path for every bolt, which makes them flicker.
    void Rejag()
    {
        foreach (var bolt in bolts)
        {
            Vector3 a = bolt.a, b = bolt.b;
            if (bolt.roams)
            {
                a = center + (Vector3)(Random.insideUnitCircle.normalized * 0.42f * size) + new Vector3(0, 0, -0.3f);
                b = center + (Vector3)(Random.insideUnitCircle * 0.2f * size) + new Vector3(0, 0, -0.3f);
            }

            Vector3 dir = b - a;
            float dist = dir.magnitude;
            Vector3 perp = dist > 0.0001f ? new Vector3(-dir.y, dir.x, 0) / dist : Vector3.right;
            int n = Mathf.Clamp(Mathf.CeilToInt(dist / (0.14f * size)), 4, 28);

            var pts = new Vector3[n + 1];
            for (int i = 0; i <= n; i++)
            {
                float k = i / (float)n;
                float taper = Mathf.Sin(k * Mathf.PI);               // pinned at both ends
                float offset = Random.Range(-1f, 1f) * bolt.jag * size * taper;
                pts[i] = Vector3.Lerp(a, b, k) + perp * offset;
            }

            bolt.glow.positionCount = bolt.core.positionCount = pts.Length;
            bolt.glow.SetPositions(pts);
            bolt.core.SetPositions(pts);
        }
    }

    void Update()
    {
        t += Time.deltaTime;

        // ---- Phase 1: lightning crackles, victim flickers and shakes ----
        if (t < boltTime)
        {
            if (t >= nextJag) { Rejag(); nextJag = t + 0.035f; }

            float flicker = Random.Range(0.55f, 1f);
            SetBoltAlpha(flicker);

            if (victim != null)
            {
                victim.transform.position = center + (Vector3)(Random.insideUnitCircle * 0.05f * size);
                Color tint = Random.value > 0.5f ? Color.white : Color.Lerp(Color.white, color, 0.7f);
                foreach (var s in victimSprites) s.color = tint;
            }
            return;
        }

        // ---- Phase 2: flash, sparks, the piece bursts apart ----
        if (!burst) Burst();

        float k = Mathf.Clamp01((t - boltTime) / dissolveTime);
        SetBoltAlpha(Mathf.Clamp01(1f - k * 3f));

        if (victim != null)
        {
            victim.transform.position = center;
            float grow = k < 0.25f ? Mathf.Lerp(1f, 1.2f, k / 0.25f) : Mathf.Lerp(1.2f, 0f, (k - 0.25f) / 0.75f);
            victim.transform.localScale = victimScale * grow;
            Color c = Color.Lerp(Color.white, color, k);
            c.a = 1f - k;
            foreach (var s in victimSprites) s.color = c;
        }

        if (flash != null)
        {
            float f = Mathf.Clamp01((t - boltTime) / 0.22f);
            flash.transform.localScale = Vector3.one * size * Mathf.Lerp(0.6f, 2.4f, f);
            flash.color = new Color(color.r, color.g, color.b, 0.9f * (1f - f));
        }

        bool sparksAlive = false;
        foreach (var s in sparks)
        {
            if (s.tr == null) continue;
            s.age += Time.deltaTime;
            float life = s.age / s.life;
            if (life >= 1f) { s.tr.gameObject.SetActive(false); continue; }
            sparksAlive = true;
            s.vel *= Mathf.Exp(-4f * Time.deltaTime); // drag
            s.tr.position += s.vel * Time.deltaTime;
            var c = Color.Lerp(Color.white, color, life);
            c.a = 1f - life;
            s.sr.color = c;
        }

        if (k >= 1f && !sparksAlive)
        {
            if (victim != null) Destroy(victim);
            Destroy(gameObject);
        }
    }

    void Burst()
    {
        burst = true;

        flash = MakeSprite("Flash", 11);
        flash.transform.position = center + new Vector3(0, 0, -0.35f);

        for (int i = 0; i < sparkCount; i++)
        {
            var sr = MakeSprite("Spark", 12);
            sr.transform.position = center + new Vector3(0, 0, -0.4f);
            sr.transform.localScale = Vector3.one * size * Random.Range(0.08f, 0.16f);
            Vector2 dir = Random.insideUnitCircle.normalized;
            sparks.Add(new Spark
            {
                tr = sr.transform,
                sr = sr,
                vel = (Vector3)(dir * Random.Range(2.5f, 6f) * size),
                life = Random.Range(0.3f, 0.55f),
            });
        }
    }

    SpriteRenderer MakeSprite(string name, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = softDot;
        sr.sharedMaterial = mat;
        sr.sortingOrder = order;
        return sr;
    }

    void SetBoltAlpha(float a)
    {
        foreach (var bolt in bolts)
        {
            bolt.glow.startColor = bolt.glow.endColor = new Color(color.r, color.g, color.b, 0.45f * a);
            bolt.core.startColor = bolt.core.endColor = new Color(1, 1, 1, a);
        }
    }

    static Sprite MakeSoftDot(int res)
    {
        var tex = new Texture2D(res, res);
        float r = res / 2f;
        for (int y = 0; y < res; y++)
            for (int x = 0; x < res; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r)) / r;
                float a = Mathf.Clamp01(1f - d);
                tex.SetPixel(x, y, new Color(1, 1, 1, a * a));
            }
        tex.Apply();
        tex.filterMode = FilterMode.Bilinear;
        return Sprite.Create(tex, new Rect(0, 0, res, res), new Vector2(0.5f, 0.5f), res);
    }
}
