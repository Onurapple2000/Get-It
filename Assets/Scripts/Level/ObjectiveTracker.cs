using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Kota/Hedef sistemi (Sprint 1 çekirdeği). Level'da "tüm nesneleri yeme" yerine **belirli
/// türlerden belirli sayıda** yutmak gerekir. HUD'u prosedürel kurar (üst-orta hedef çubuğu:
/// her hedef için NESNE RESMİ + kalan sayı), yutulunca nesnenin **resmiyle ghost** sayaca uçar,
/// varınca sayı düşer, 0 olunca **yeşil ✓** belirir, tüm hedefler bitince GameManager.TriggerSuccess().
/// İkonlar runtime'da prefab thumbnail'ından üretilir (IconRenderer) — manuel asset gerekmez.
///
/// Ayrıca her yutulan nesne için delik üzerinde **+skor popup** (GameManager çağırır).
/// Bomba ve süre fail GameManager'da. Hedefler şimdilik inspector'dan; veri-odaklı LevelData sonraki faz.
/// </summary>
public class ObjectiveTracker : MonoBehaviour
{
    [System.Serializable]
    public class Objective
    {
        [Tooltip("PhysicsSwallowable.ResolvedType ile eşleşen tür (ör. FlowerPot, Tree, StreetLamp).")]
        public string objectType = "FlowerPot";
        [Tooltip("Bu türden kaç tane yutulması gerekiyor.")]
        public int required = 3;
        [Tooltip("HUD ikonu (opsiyonel). Boşsa runtime'da nesnenin thumbnail'ı üretilir.")]
        public Sprite icon;

        [HideInInspector] public int remaining;   // mantıksal kalan (spawn'da düşer; fazla ghost engellenir)
        [HideInInspector] public int shown;        // gösterilen sayı (ghost varınca düşer)
        [HideInInspector] public Image iconImage;
        [HideInInspector] public TMP_Text countText;
        [HideInInspector] public GameObject checkMark;
    }

    [Header("Hedefler (bu level)")]
    public List<Objective> objectives = new List<Objective>();

    [Header("HUD yerleşim")]
    public Canvas canvas;                                  // boşsa sahnede aranır
    public Vector2 topOffset = new Vector2(0f, -18f);
    public float entrySize = 80f;
    public float iconSize = 64f;
    public float spacing = 14f;

    [Header("Ghost uçuş")]
    public float ghostFlyTime = 0.6f;
    public float ghostStartSize = 80f;
    public float ghostEndSize = 32f;

    Camera cam;
    RectTransform barRect;
    Sprite checkSprite;
    int totalShownRemaining;

    void Awake()
    {
        if (canvas == null) canvas = FindAnyObjectByType<Canvas>();
        cam = Camera.main;
        if (cam == null) cam = FindAnyObjectByType<Camera>();
        checkSprite = GenerateCheck(96, new Color(0.25f, 1f, 0.45f));
    }

    void Start()
    {
        // Veri-odaklı: LevelManager varsa hedefler aktif LevelData'dan gelir (inspector listesini ezer).
        if (LevelManager.Instance != null)
        {
            var fromLevel = LevelManager.Instance.BuildObjectives();
            if (fromLevel.Count > 0) objectives = fromLevel;
        }
        BuildHud();
    }

    /// <summary>Oyun bitince (success/fail) hedef barını gizle → end-ekran başlıklarıyla çakışmasın.</summary>
    public void HideBar()
    {
        if (barRect != null) barRect.gameObject.SetActive(false);
    }

    // ── HUD KURULUM ───────────────────────────────────────────────────────────
    void BuildHud()
    {
        if (canvas == null) { Debug.LogWarning("[ObjectiveTracker] Canvas bulunamadı — HUD kurulamadı."); return; }

        var bar = new GameObject("ObjectiveBar", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
        barRect = bar.GetComponent<RectTransform>();
        barRect.SetParent(UiRoot.SafeContent(canvas), false);   // üst çentik altına inmesin
        barRect.anchorMin = barRect.anchorMax = new Vector2(0.5f, 1f);
        barRect.pivot = new Vector2(0.5f, 1f);
        barRect.anchoredPosition = topOffset;

        var hlg = bar.GetComponent<HorizontalLayoutGroup>();
        hlg.spacing = spacing;
        hlg.childAlignment = TextAnchor.UpperCenter;
        hlg.childControlWidth = hlg.childControlHeight = false;
        hlg.childForceExpandWidth = hlg.childForceExpandHeight = false;

        var fitter = bar.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // Türden ikon üret (cache: aynı tür bir kez render edilsin)
        var iconCache = new Dictionary<string, Sprite>(System.StringComparer.OrdinalIgnoreCase);

        totalShownRemaining = 0;
        foreach (var o in objectives)
        {
            o.remaining = o.shown = Mathf.Max(0, o.required);
            totalShownRemaining += o.shown;
            if (o.icon == null) o.icon = GetOrRenderIcon(o.objectType, iconCache);
            BuildEntry(o);
            RefreshEntry(o);
        }
    }

    Sprite GetOrRenderIcon(string type, Dictionary<string, Sprite> cache)
    {
        if (cache.TryGetValue(type, out var s)) return s;
        GameObject src = FindSceneSource(type);
        s = src != null ? IconRenderer.Render(src) : null;
        cache[type] = s;
        return s;
    }

    static GameObject FindSceneSource(string type)
    {
        var all = FindObjectsByType<PhysicsSwallowable>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (var p in all)
            if (string.Equals(p.ResolvedType, type, System.StringComparison.OrdinalIgnoreCase))
                return p.gameObject;
        return null;
    }

    void BuildEntry(Objective o)
    {
        // Pano (yarı saydam koyu)
        var entry = new GameObject("Obj_" + o.objectType, typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        var er = entry.GetComponent<RectTransform>();
        er.SetParent(barRect, false);
        er.sizeDelta = new Vector2(entrySize, entrySize);
        var le = entry.GetComponent<LayoutElement>();
        le.preferredWidth = entrySize; le.preferredHeight = entrySize;
        var bg = entry.GetComponent<Image>();
        bg.sprite = RoundedSprite();
        bg.type = Image.Type.Sliced;
        bg.color = new Color(1f, 1f, 1f, 0.5f);             // %50 opak beyaz, yuvarlak köşe
        bg.raycastTarget = false;
        // 3D his: hafif drop shadow (öne çıkık kart)
        var sh = entry.AddComponent<Shadow>();
        sh.effectColor = new Color(0f, 0f, 0f, 0.45f);
        sh.effectDistance = new Vector2(3f, -4f);

        // İkon (nesne resmi)
        var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        var ir = iconGo.GetComponent<RectTransform>();
        ir.SetParent(er, false);
        ir.anchorMin = ir.anchorMax = new Vector2(0.5f, 1f);
        ir.pivot = new Vector2(0.5f, 1f);
        ir.anchoredPosition = new Vector2(0f, -3f);
        ir.sizeDelta = new Vector2(iconSize, iconSize);   // ikon boyutu aynı kalır
        o.iconImage = iconGo.GetComponent<Image>();
        o.iconImage.raycastTarget = false;
        o.iconImage.preserveAspect = true;
        if (o.icon != null) { o.iconImage.sprite = o.icon; o.iconImage.color = Color.white; }
        else o.iconImage.color = new Color(0.6f, 0.6f, 0.6f);   // ikon üretilemezse gri kutu

        // Sayı (panonun altı)
        var count = NewText("Count", er, 22, FontStyles.Bold, TextAlignmentOptions.Center);
        var cr = count.rectTransform;
        cr.anchorMin = cr.anchorMax = new Vector2(0.5f, 0f);
        cr.pivot = new Vector2(0.5f, 0f);
        cr.anchoredPosition = new Vector2(0f, 2f);
        cr.sizeDelta = new Vector2(entrySize, 24f);
        count.color = new Color(0.15f, 0.12f, 0.08f);   // koyu (beyaz panoda okunur)
        o.countText = count;

        // Yeşil ✓ (sprite — başta gizli, ikon üzerine biner)
        var checkGo = new GameObject("Check", typeof(RectTransform), typeof(Image));
        var ckr = checkGo.GetComponent<RectTransform>();
        ckr.SetParent(er, false);
        ckr.anchorMin = ckr.anchorMax = new Vector2(0.5f, 0.5f);
        ckr.pivot = new Vector2(0.5f, 0.5f);
        ckr.anchoredPosition = Vector2.zero;
        ckr.sizeDelta = new Vector2(entrySize * 0.7f, entrySize * 0.7f);
        var checkImg = checkGo.GetComponent<Image>();
        checkImg.sprite = checkSprite;
        checkImg.raycastTarget = false;
        checkImg.preserveAspect = true;
        o.checkMark = checkGo;
        o.checkMark.SetActive(false);
    }

    // Yuvarlak köşeli 9-slice pano sprite'ı (beyaz; renk/alpha Image'tan gelir). Cache'lenir.
    static Sprite roundedSprite;
    static Sprite RoundedSprite()
    {
        if (roundedSprite != null) return roundedSprite;
        const int s = 48, r = 14;
        var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var px = new Color32[s * s];
        Vector2 c = new Vector2((s - 1) * 0.5f, (s - 1) * 0.5f);
        Vector2 b = new Vector2(s * 0.5f - r, s * 0.5f - r);
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                Vector2 p = new Vector2(x, y) - c;
                Vector2 d = new Vector2(Mathf.Abs(p.x) - b.x, Mathf.Abs(p.y) - b.y);
                float outside = new Vector2(Mathf.Max(d.x, 0f), Mathf.Max(d.y, 0f)).magnitude;
                float dist = outside + Mathf.Min(Mathf.Max(d.x, d.y), 0f) - r;
                float a = Mathf.Clamp01(0.5f - dist);
                px[y * s + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        tex.SetPixels32(px); tex.Apply();
        roundedSprite = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f, 0,
                                      SpriteMeshType.FullRect, new Vector4(r, r, r, r));
        return roundedSprite;
    }

    static TMP_Text NewText(string name, Transform parent, float size, FontStyles style, TextAlignmentOptions align)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.fontSize = size; t.fontStyle = style; t.alignment = align;
        t.raycastTarget = false;
        return t;
    }

    void RefreshEntry(Objective o)
    {
        bool done = o.shown <= 0;
        o.countText.text = done ? "" : o.shown.ToString();
        if (o.checkMark.activeSelf != done) o.checkMark.SetActive(done);
    }

    // ── YUTMA RAPORU ──────────────────────────────────────────────────────────
    /// <summary>GameManager, hedef-olmayanlar dahil her (bomba olmayan) yutulan nesne için çağırır.</summary>
    /// <summary>animate=false: yoğun/hızlı yutmada (throttle) UÇUŞ animasyonu spawn'lanmaz → ilerleme HEMEN uygulanır
    /// (GameObject/coroutine spike'ı olmaz). Sayaç + kazanma koşulu her iki durumda da doğru işler.</summary>
    public void ReportSwallow(string type, Vector3 worldPos, bool animate = true)
    {
        Objective o = Find(type);
        if (o == null || o.remaining <= 0) return;   // hedef değil ya da bu türden gerekli sayı tamam

        o.remaining--;                                // mantıksal: fazla ghost spawn'ı engelle
        if (animate) StartCoroutine(GhostFly(o, worldPos));
        else ApplyProgress(o);                        // uçuş yok → ilerlemeyi anında say
    }

    // Ghost varışı = gösterilen sayıyı düşür + ✓ + hepsi bitince success. Hem animasyonlu (GhostFly sonunda) hem
    // animasyonsuz (throttle) yoldan çağrılır → sayaç/kazanma tek yerden, tutarlı.
    void ApplyProgress(Objective o)
    {
        o.shown = Mathf.Max(0, o.shown - 1);
        RefreshEntry(o);
        totalShownRemaining = Mathf.Max(0, totalShownRemaining - 1);
        if (totalShownRemaining == 0 && GameManager.Instance != null)
            GameManager.Instance.TriggerSuccess();
    }

    Objective Find(string type)
    {
        for (int i = 0; i < objectives.Count; i++)
            if (string.Equals(objectives[i].objectType, type, System.StringComparison.OrdinalIgnoreCase))
                return objectives[i];
        return null;
    }

    IEnumerator GhostFly(Objective o, Vector3 worldPos)
    {
        // Ghost = nesnenin resmi; yutulma konumunun ekran noktasından sayaca uçar (Screen Space Overlay).
        var go = new GameObject("Ghost", typeof(RectTransform), typeof(Image));
        var gr = go.GetComponent<RectTransform>();
        gr.SetParent(canvas.transform, false);
        var img = go.GetComponent<Image>();
        img.raycastTarget = false;
        img.preserveAspect = true;
        if (o.icon != null) { img.sprite = o.icon; img.color = Color.white; }
        else img.color = new Color(0.8f, 0.8f, 0.8f);

        Vector3 startScreen = cam != null ? cam.WorldToScreenPoint(worldPos) : Vector3.zero;
        startScreen.z = 0f;
        Vector3 endScreen = o.iconImage.rectTransform.position;   // overlay: position = ekran pikseli

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(0.01f, ghostFlyTime);
            float e = 1f - (1f - t) * (1f - t);     // ease-out
            gr.position = Vector3.Lerp(startScreen, endScreen, e);
            float s = Mathf.Lerp(ghostStartSize, ghostEndSize, e);
            gr.sizeDelta = new Vector2(s, s);
            yield return null;
        }
        Destroy(go);

        // Varış: gösterilen sayıyı düşür, ✓, hepsi bitince success (ApplyProgress) + pulse (animasyonlu yola özel)
        ApplyProgress(o);
        StartCoroutine(Pulse(o.iconImage.rectTransform));
    }

    static IEnumerator Pulse(RectTransform rt)
    {
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / 0.22f;
            float s = 1f + 0.35f * Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI);
            rt.localScale = Vector3.one * s;
            yield return null;
        }
        rt.localScale = Vector3.one;
    }

    // ── SKOR POPUP (delik üzerinde +skor, yukarı buharlaşır) ───────────────────
    public void SpawnScorePopup(int amount, Vector3 worldPos)
    {
        if (canvas == null || amount == 0) return;
        StartCoroutine(ScorePopup(amount, worldPos));
    }

    IEnumerator ScorePopup(int amount, Vector3 worldPos)
    {
        var t = NewText("ScorePopup", canvas.transform, 30, FontStyles.Bold, TextAlignmentOptions.Center);
        t.text = "+" + amount;
        t.color = new Color(1f, 0.95f, 0.45f);
        t.rectTransform.sizeDelta = new Vector2(140f, 44f);

        Vector3 baseScreen = cam != null ? cam.WorldToScreenPoint(worldPos + Vector3.up * 0.5f) : Vector3.zero;
        baseScreen.z = 0f;

        const float dur = 0.85f;
        float k = 0f;
        while (k < 1f)
        {
            k += Time.deltaTime / dur;
            t.rectTransform.position = baseScreen + new Vector3(0f, k * 75f, 0f);
            var c = t.color; c.a = 1f - Mathf.Clamp01(k); t.color = c;
            yield return null;
        }
        Destroy(t.gameObject);
    }

    // ── Prosedürel check sprite (font glyph'ine bağımlı değil) ─────────────────
    static Sprite GenerateCheck(int size, Color color)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        Vector2 p0 = new Vector2(0.20f, 0.52f), p1 = new Vector2(0.42f, 0.30f), p2 = new Vector2(0.80f, 0.74f);
        const float half = 0.085f, edge = 0.02f;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            Vector2 uv = new Vector2((x + 0.5f) / size, (y + 0.5f) / size);
            float d = Mathf.Min(DistSeg(uv, p0, p1), DistSeg(uv, p1, p2));
            // GLSL-tarzı eşik: d<=half-edge → 1 (içeride), d>=half → 0 (dışarıda), arada AA.
            float a = 1f - Smooth01(half - edge, half, d);
            tex.SetPixel(x, y, a > 0f ? new Color(color.r, color.g, color.b, a * color.a) : new Color(0, 0, 0, 0));
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    static float DistSeg(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a, ap = p - a;
        float t = Mathf.Clamp01(Vector2.Dot(ap, ab) / Vector2.Dot(ab, ab));
        return Vector2.Distance(p, a + ab * t);
    }

    // GLSL smoothstep: edge0'da 0, edge1'de 1 (Mathf.SmoothStep interpolasyon yapar, eşik DEĞİL).
    static float Smooth01(float edge0, float edge1, float x)
    {
        float t = Mathf.Clamp01((x - edge0) / (edge1 - edge0));
        return t * t * (3f - 2f * t);
    }
}
