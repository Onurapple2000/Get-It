using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Yutma anı cilası (Sprint 7): nesne deliğe düşünce delik ağzında kısa bir TOZ PUFU + parlak PARILTI (glint).
/// Tamamen prosedürel (asset yok), HAVUZLANMIŞ (GC yok) ve throttle'lı — GameManager'ın "show" bayrağıyla
/// çağrılır, aynı anda en çok POOL_SIZE burst çalışır. Perf-güvenli: küçük parçacık sayıları, billboard,
/// mobil-dostu Sprites/Default (alpha) materyali (additive shader-strip riski YOK — bkz. mobil crash geçmişi).
///
/// TASARIM: toz = küçük, hızlı DIŞA saçılır, çabuk söner, YÜKSELMEZ + KÜÇÜLÜR (duman DEĞİL).
/// parıltı = parlak beyaz-sarı 4-uçlu glint, yerçekimiyle yay çizip düşer → "ışıldama" hissi.
/// </summary>
public class SwallowVFX : MonoBehaviour
{
    const int POOL_SIZE = 6;

    static SwallowVFX _instance;
    readonly List<ParticleSystem> _pool = new List<ParticleSystem>();
    int _next;

    public static void Play(Vector3 worldPos, float sizeScale = 1f)
    {
        if (_instance == null) Bootstrap();
        _instance.PlayInternal(worldPos, sizeScale);
    }

    static void Bootstrap()
    {
        var go = new GameObject("SwallowVFX");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<SwallowVFX>();
        _instance.BuildPool();
    }

    Material _dustMat, _glintMat;

    void BuildPool()
    {
        var sh = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Hidden/InternalErrorShader");
        _dustMat  = new Material(sh) { mainTexture = SoftCircle(32) };
        _glintMat = new Material(sh) { mainTexture = Glint(48) };
        for (int i = 0; i < POOL_SIZE; i++) _pool.Add(BuildBurst());
    }

    void PlayInternal(Vector3 pos, float sizeScale)
    {
        if (_pool.Count == 0) return;
        var ps = _pool[_next];
        _next = (_next + 1) % _pool.Count;
        ps.transform.position = pos + Vector3.up * 0.12f;
        ps.transform.localScale = Vector3.one * Mathf.Clamp(sizeScale, 0.6f, 2.2f);
        ps.Clear(true);
        ps.Play(true);   // child (glint) da oynar
    }

    ParticleSystem BuildBurst()
    {
        var root = new GameObject("burst");
        root.transform.SetParent(transform, false);
        var dust = MakeDust(root.transform);
        MakeGlint(root.transform);
        return dust;
    }

    // ── TOZ: küçük, hızlı dışa saçılan, çabuk sönen, KÜÇÜLEN puf (yükselmez) ──
    ParticleSystem MakeDust(Transform parent)
    {
        var go = new GameObject("Dust");
        go.transform.SetParent(parent, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);   // AddComponent oto-oynatır → duration set'ten ÖNCE durdur (uyarı fix)
        var main = ps.main;
        main.playOnAwake = false; main.loop = false; main.duration = 0.4f;
        main.maxParticles = 20;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.18f, 0.32f);   // KISA (duman gibi lingerlamaz)
        main.startSpeed = new ParticleSystem.MinMaxCurve(2.0f, 4.2f);        // hızlı DIŞA
        main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.28f);       // küçük tanecikler
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1f, 0.98f, 0.9f, 0.55f), new Color(0.96f, 0.92f, 0.82f, 0.45f));
        main.gravityModifier = 0.12f;   // hafif AŞAĞI çöker (yükselmez → duman değil)

        var em = ps.emission; em.rateOverTime = 0f;
        em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)10) });
        var sh = ps.shape;   // yassı disk gibi dışa saçıl (delik ağzı düzleminde)
        sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Hemisphere; sh.radius = 0.08f;
        FadeOut(ps, 0.06f, 0.35f);
        ShrinkSize(ps, 1f, 0.35f);   // BÜYÜMEZ, küçülüp dağılır (toz dissipasyonu)

        var r = go.GetComponent<ParticleSystemRenderer>();
        r.material = _dustMat; r.renderMode = ParticleSystemRenderMode.Billboard; r.sortingOrder = 150;
        return ps;
    }

    // ── PARILTI: parlak beyaz-sarı 4-uçlu glint; yerçekimiyle yay çizer ──
    ParticleSystem MakeGlint(Transform parent)
    {
        var go = new GameObject("Glint");
        go.transform.SetParent(parent, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);   // AddComponent oto-oynatır → duration set'ten ÖNCE durdur (uyarı fix)
        var main = ps.main;
        main.playOnAwake = false; main.loop = false; main.duration = 0.5f;
        main.maxParticles = 14;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.28f, 0.5f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(2.8f, 5.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.14f, 0.30f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1f, 1f, 0.85f, 1f), new Color(1f, 0.92f, 0.55f, 1f));   // parlak beyaz→sarı
        main.gravityModifier = 1.1f;   // yay çizip düşer

        var em = ps.emission; em.rateOverTime = 0f;
        em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)8) });
        var sh = ps.shape;
        sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Hemisphere; sh.radius = 0.06f;
        FadeOut(ps, 0.03f, 0.5f);
        // Twinkle: hafif boyut titremesi yerine sona doğru küçülüp sön (parlak → nokta).
        ShrinkSize(ps, 1f, 0.2f);

        var r = go.GetComponent<ParticleSystemRenderer>();
        r.material = _glintMat; r.renderMode = ParticleSystemRenderMode.Billboard; r.sortingOrder = 152;
        return ps;
    }

    static void FadeOut(ParticleSystem ps, float fadeIn, float holdTo)
    {
        var col = ps.colorOverLifetime; col.enabled = true;
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, fadeIn),
                    new GradientAlphaKey(1f, holdTo), new GradientAlphaKey(0f, 1f) });
        col.color = new ParticleSystem.MinMaxGradient(g);
    }

    static void ShrinkSize(ParticleSystem ps, float start, float end)
    {
        var sol = ps.sizeOverLifetime; sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f,
            new AnimationCurve(new Keyframe(0f, start), new Keyframe(1f, end)));
    }

    // Yumuşak dolu daire (toz taneciği)
    static Texture2D _soft;
    static Texture2D SoftCircle(int size)
    {
        if (_soft != null) return _soft;
        var t = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        float r = size * 0.5f;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r)) / r;
            float a = Mathf.Clamp01(1f - d); a *= a;
            t.SetPixel(x, y, new Color(1f, 1f, 1f, a));
        }
        t.Apply();
        _soft = t;
        return _soft;
    }

    // 4-uçlu parlak glint (eksen boyunca uzayan ışıma + parlak çekirdek)
    static Texture2D _glint;
    static Texture2D Glint(int size)
    {
        if (_glint != null) return _glint;
        var t = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        float c = (size - 1) * 0.5f;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dx = (x - c) / c, dy = (y - c) / c;
            float ax = Mathf.Abs(dx), ay = Mathf.Abs(dy);
            float beam = Mathf.Max(0f, 1f - (ax * 7f + ay)) + Mathf.Max(0f, 1f - (ay * 7f + ax));
            float core = Mathf.Max(0f, 1f - Mathf.Sqrt(dx * dx + dy * dy) * 2.4f);
            float a = Mathf.Clamp01(beam * 0.8f + core);
            t.SetPixel(x, y, new Color(1f, 1f, 0.95f, a));
        }
        t.Apply();
        _glint = t;
        return _glint;
    }
}
