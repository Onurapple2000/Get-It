using UnityEngine;

/// <summary>
/// Bomba yutulunca delik konumunda çalan prosedürel patlama: ateş topu (kısa, parlak burst) +
/// yükselen hafif duman. Tamamen koddan kurulur (asset/prefab gerekmez), oynayıp kendini yok eder.
/// Kullanım: ExplosionEffect.Spawn(holeWorldPos);
/// </summary>
public class ExplosionEffect : MonoBehaviour
{
    public static void Spawn(Vector3 pos)
    {
        var go = new GameObject("BombExplosion");
        go.transform.position = pos;
        go.AddComponent<ExplosionEffect>();
    }

    void Awake()
    {
        var tex = SoftCircle(64);
        var mat = new Material(Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Hidden/InternalErrorShader")) { mainTexture = tex };

        BuildFire(mat);
        BuildSparks(mat);
        BuildSmoke(mat);

        Destroy(gameObject, 3.0f);
    }

    // ── ATEŞ TOPU (kısa, parlak, dışa) ────────────────────────────────────────
    void BuildFire(Material mat)
    {
        var ps = NewPS("Fire", mat);
        var main = ps.main;
        main.duration = 0.4f; main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.22f, 0.45f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(2.5f, 6.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.45f, 0.95f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1f, 0.85f, 0.30f), new Color(1f, 0.45f, 0.05f));
        main.gravityModifier = 0.15f;

        Burst(ps, 30);
        Sphere(ps, 0.3f);
        FadeColor(ps, new Color(1f, 0.9f, 0.4f), new Color(0.8f, 0.15f, 0.05f),
                  fadeIn: 0.1f, holdTo: 0.5f);
        GrowSize(ps, 0.6f, 1.1f, 0.5f);
        ps.Play();
    }

    // ── KIVILCIMLAR (küçük, hızlı saçılan) ────────────────────────────────────
    void BuildSparks(Material mat)
    {
        var ps = NewPS("Sparks", mat);
        var main = ps.main;
        main.duration = 0.4f; main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.55f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(5f, 11f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.18f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1f, 0.95f, 0.55f), new Color(1f, 0.7f, 0.2f));
        main.gravityModifier = 0.6f;

        Burst(ps, 22);
        Sphere(ps, 0.15f);
        FadeColor(ps, new Color(1f, 0.95f, 0.6f), new Color(1f, 0.5f, 0.1f), 0.05f, 0.5f);
        ps.Play();
    }

    // ── HAFİF DUMAN (delikten yükselir) ───────────────────────────────────────
    void BuildSmoke(Material mat)
    {
        var ps = NewPS("Smoke", mat);
        var main = ps.main;
        main.duration = 1.2f; main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.1f, 2.0f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.4f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.6f, 1.1f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.32f, 0.30f, 0.30f, 0.55f), new Color(0.18f, 0.17f, 0.17f, 0.55f));
        main.gravityModifier = -0.06f;   // yukarı yüksel

        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)10), new ParticleSystem.Burst(0.15f, (short)8) });

        var shape = ps.shape;
        shape.enabled = true; shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 18f; shape.radius = 0.3f; shape.rotation = new Vector3(-90f, 0f, 0f); // yukarı

        // hafif yükselen + yavaş dönen, büyüyüp solan duman
        FadeColor(ps, new Color(0.30f, 0.29f, 0.29f), new Color(0.16f, 0.15f, 0.15f),
                  fadeIn: 0.18f, holdTo: 0.5f);
        GrowSize(ps, 0.7f, 1.8f, 1f);
        var rot = ps.rotationOverLifetime; rot.enabled = true;
        rot.z = new ParticleSystem.MinMaxCurve(-0.8f, 0.8f);
        ps.Play();
    }

    // ── yardımcılar ───────────────────────────────────────────────────────────
    ParticleSystem NewPS(string name, Material mat)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);   // AddComponent oto-oynatır → duration set'ten ÖNCE durdur (uyarı fix)
        var main = ps.main;
        main.playOnAwake = false;
        main.maxParticles = 200;
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.material = mat;
        r.renderMode = ParticleSystemRenderMode.Billboard;
        r.sortingOrder = 200;
        return ps;
    }

    static void Burst(ParticleSystem ps, int count)
    {
        var e = ps.emission;
        e.rateOverTime = 0f;
        e.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
    }

    static void Sphere(ParticleSystem ps, float radius)
    {
        var s = ps.shape;
        s.enabled = true; s.shapeType = ParticleSystemShapeType.Sphere; s.radius = radius;
    }

    static void FadeColor(ParticleSystem ps, Color a, Color b, float fadeIn, float holdTo)
    {
        var col = ps.colorOverLifetime; col.enabled = true;
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(a, 0f), new GradientColorKey(b, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, fadeIn),
                    new GradientAlphaKey(1f, holdTo), new GradientAlphaKey(0f, 1f) });
        col.color = new ParticleSystem.MinMaxGradient(g);
    }

    static void GrowSize(ParticleSystem ps, float start, float peak, float end)
    {
        var sol = ps.sizeOverLifetime; sol.enabled = true;
        var c = new AnimationCurve(
            new Keyframe(0f, start), new Keyframe(0.35f, peak), new Keyframe(1f, end));
        sol.size = new ParticleSystem.MinMaxCurve(1f, c);
    }

    static Texture2D SoftCircle(int size)
    {
        var t = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        float r = size * 0.5f;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r)) / r;
            float a = Mathf.Clamp01(1f - d); a *= a;   // yumuşak kenar
            t.SetPixel(x, y, new Color(1f, 1f, 1f, a));
        }
        t.Apply();
        return t;
    }
}
