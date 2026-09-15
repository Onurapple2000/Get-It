using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Paylaşılan UI buton yardımcısı: burrow rect görselini (pill, 3.75:1) Simple olarak kullanır (9-slice
/// köşe taşması olmadan), sola opsiyonel ikon + ortalı yazı koyar. Prosedürel ikonlar: Play, Refresh, Power.
/// Butonlar pill oranına yakın boyutlandırılmalı (ör. 460×122) ki görsel bozulmasın.
/// </summary>
public static class UiButtons
{
    static Sprite _rect, _circle, _play, _refresh, _power, _gear, _coin, _video;
    public static Sprite Rect()    => _rect    ? _rect    : (_rect    = Resources.Load<Sprite>("burrow_button_empty_rect"));

    /// <summary>
    /// Ahşap dikdörtgen çerçeveyi bir Image'a BOZULMADAN uygular (2026-09-15 görsel cila).
    /// Sorun: sprite 1985×530 ve importta 9-slice border'ı (212px) tanımlı olduğu halde her yerde
    /// <c>Image.Type.Simple</c> kullanılıyordu → geniş butonlarda tüm resim yatay gerilip çerçeve deforme oluyordu.
    /// Çözüm: <c>Sliced</c> + <c>pixelsPerUnitMultiplier</c> = spriteYüksekliği / rectYüksekliği → sprite'ın DİKEY
    /// boyutu rect'e birebir oturur, köşeler kare kalır, yalnız ORTA bant yatay uzar (9-slice'ın amacı).
    /// Multiplier verilmezse Unity kenarları eksen başına ayrı ölçekler → köşeler EZİLİR; o yüzden şart.
    /// </summary>
    public static void ApplyFrame(Image im, float rectHeight)
    {
        var r = Rect(); if (im == null || r == null) return;
        im.sprite = r;
        im.type = Image.Type.Sliced;
        im.fillCenter = true;
        // ⚠️ PİKSEL DEĞİL, CANVAS BİRİMİ: Android'de texture override (max 1024) sprite'ı 1985→1024 küçültür;
        // r.rect.height cihazda 273 olur ama Unity PPU'yu da orantılı düşürdüğü için BİRİM yüksekliği hep 530 kalır.
        // Pikselden hesaplanınca cihazda çarpan yarıya iniyor → kenarlar 2× kalın, orta bant yok oluyor, koyu yazı
        // koyu ip dokusunun üstünde GÖRÜNMEZ oluyordu (2026-09-15 cihaz testi). im.pixelsPerUnit canvas'ı hesaba katar.
        float ppu = im.pixelsPerUnit > 0f ? im.pixelsPerUnit : 1f;
        float spriteHUnits = r.rect.height / ppu;            // her platformda ≈ 530
        im.pixelsPerUnitMultiplier = rectHeight > 1f ? spriteHUnits / rectHeight : 1f;
    }
    public static Sprite Circle()  => _circle  ? _circle  : (_circle  = Resources.Load<Sprite>("burrow_button_empty_circle"));
    public static Sprite Play()    => _play    ? _play    : (_play    = MakePlay(64));
    public static Sprite Refresh() => _refresh ? _refresh : (_refresh = MakeRefresh(72));
    public static Sprite Power()   => _power   ? _power   : (_power   = MakePower(72));
    public static Sprite Gear()    => _gear    ? _gear    : (_gear    = MakeGear(80));
    public static Sprite Coin()    => _coin    ? _coin    : (_coin    = MakeCoin(80));    // altın coin (para birimi)
    public static Sprite Video()   => _video   ? _video   : (_video   = MakeVideo(80));   // ▶ çerçeve = "reklam izle"
    static Sprite _rounded, _disc;
    /// <summary>Yumuşak köşeli düz kutu (9-slice) — bildirim kartı/panel için; ahşap çerçeve DEĞİL.</summary>
    public static Sprite Rounded() => _rounded ? _rounded : (_rounded = MakeRounded(96, 36));
    /// <summary>Dolu daire — ikon rozeti/işaret arkası.</summary>
    public static Sprite Disc()    => _disc    ? _disc    : (_disc    = MakeDisc(96));

    // Anti-aliased yuvarlatılmış dikdörtgen, kenar = yarıçap → Sliced ile her boyutta köşeler bozulmaz.
    static Sprite MakeRounded(int s, int r)
    {
        var t = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var px = new Color32[s * s];
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float cx = Mathf.Clamp(x + 0.5f, r, s - r), cy = Mathf.Clamp(y + 0.5f, r, s - r);
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
                float a = Mathf.Clamp01(r - d + 0.5f);
                px[y * s + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
        t.SetPixels32(px); t.Apply();
        return Sprite.Create(t, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(r, r, r, r));
    }

    static Sprite MakeDisc(int s)
    {
        var t = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var px = new Color32[s * s]; float c = s * 0.5f, rad = c - 1f;
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(c, c));
                px[y * s + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(rad - d + 0.5f) * 255));
            }
        t.SetPixels32(px); t.Apply();
        return Sprite.Create(t, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f);
    }

    // Çark (ayarlar) ikonu: halka + 8 diş + merkez delik.
    static Sprite MakeGear(int s)
    {
        var t = NewTex(s);
        for (int y = 0; y < s; y++) for (int x = 0; x < s; x++)
        {
            float u = (x + 0.5f) / s, v = (y + 0.5f) / s, dx = u - 0.5f, dy = v - 0.5f;
            float r = Mathf.Sqrt(dx * dx + dy * dy);
            float ang = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg; ang = (ang + 360f) % 360f;
            bool ring = r > 0.16f && r < 0.32f;                                  // gövde halkası
            float nearest = Mathf.Round(ang / 45f) * 45f;                        // 8 diş, her 45°
            bool tooth = Mathf.Abs(Mathf.DeltaAngle(ang, nearest)) < 15f && r >= 0.30f && r < 0.44f;
            bool hole = r < 0.13f;                                               // merkez boşluk
            t.SetPixel(x, y, ((ring || tooth) && !hole) ? Color.white : Clear);
        }
        return Finish(t, s);
    }

    /// <summary>Pill buton: rect görsel (Simple) + sola ikon + ortalı yazı. Button döner.
    /// <paramref name="centered"/>=true → ikon+yazı butonda H+V ORTALI grup (reklam/coin butonları; taşmasız).</summary>
    public static Button Build(Transform parent, Vector2 anchor, Vector2 pos, Vector2 size,
                               string label, Sprite icon, Color tint, float fontSize = 40, bool centered = false)
    {
        var go = new GameObject("Btn_" + label, typeof(RectTransform), typeof(Image), typeof(Button));
        var rt = (RectTransform)go.transform; rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = anchor; rt.pivot = anchor;
        rt.anchoredPosition = pos; rt.sizeDelta = size;
        var im = go.GetComponent<Image>();
        ApplyFrame(im, size.y);   // 9-slice, bozulmasız çerçeve
        im.color = tint;
        var btn = go.GetComponent<Button>();
        btn.onClick.AddListener(() => AudioManager.Instance?.PlayUiClick());   // her buton tık sesi
        float h = size.y;

        // ── ORTALI grup: ikon+yazı butonun tam ORTASINDA (yatay+dikey), TMP autosize → taşma yok ──
        if (centered)
        {
            var grp = new GameObject("Content", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            var grt = (RectTransform)grp.transform; grt.SetParent(rt, false);
            // Kalın buton çerçevesinden İYİCE İÇERİDE dur (yatay+dikey) → yazı/ikon çerçeveye binmesin.
            float padX = Mathf.Clamp(size.x * 0.14f, 20f, 46f), padY = Mathf.Clamp(size.y * 0.18f, 12f, 22f);
            grt.anchorMin = Vector2.zero; grt.anchorMax = Vector2.one; grt.offsetMin = new Vector2(padX, padY); grt.offsetMax = new Vector2(-padX, -padY);
            var hlg = grp.GetComponent<HorizontalLayoutGroup>();
            hlg.childAlignment = TextAnchor.MiddleCenter; hlg.spacing = 8f;
            hlg.childControlWidth = true; hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;
            if (icon != null)
            {
                var ig = new GameObject("Icon", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
                ig.transform.SetParent(grt, false);
                var iimg = ig.GetComponent<Image>(); iimg.sprite = icon; iimg.preserveAspect = true; iimg.raycastTarget = false;
                var le = ig.GetComponent<LayoutElement>(); le.preferredWidth = h * 0.58f; le.preferredHeight = h * 0.58f;
            }
            if (!string.IsNullOrEmpty(label))
            {
                var t2 = NewText(grt, fontSize);
                t2.text = label; t2.color = new Color(0.22f, 0.15f, 0.08f); t2.alignment = TextAlignmentOptions.Center;
                t2.enableAutoSizing = true; t2.fontSizeMin = Mathf.Max(12f, fontSize * 0.45f); t2.fontSizeMax = fontSize;
                t2.enableWordWrapping = false; t2.overflowMode = TextOverflowModes.Ellipsis;
                t2.gameObject.AddComponent<LayoutElement>().flexibleWidth = 0f;   // sıkı grup → ikon+yazı birlikte ORTALI (kenara yapışmaz)
            }
            return btn;
        }

        if (icon != null)
        {
            var ig = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            var ir = (RectTransform)ig.transform; ir.SetParent(rt, false);
            ir.anchorMin = ir.anchorMax = new Vector2(0f, 0.5f); ir.pivot = new Vector2(0f, 0.5f);
            float s = h * 0.5f;
            ir.anchoredPosition = new Vector2(h * 0.42f, 0f); ir.sizeDelta = new Vector2(s, s);
            var iimg = ig.GetComponent<Image>(); iimg.sprite = icon; iimg.preserveAspect = true; iimg.raycastTarget = false;
        }

        var t = NewText(rt, fontSize);
        t.text = label; t.color = new Color(0.22f, 0.15f, 0.08f);
        // Taşma önleme (lokalizasyon: EN/TR uzunluk farkı): tek satır + otomatik küçülme (alana sığar, kaymaz).
        t.enableAutoSizing = true; t.fontSizeMin = Mathf.Max(14f, fontSize * 0.5f); t.fontSizeMax = fontSize;
        t.enableWordWrapping = false; t.overflowMode = TextOverflowModes.Ellipsis;
        var tr = t.rectTransform;
        // Kalın çerçeveden uzak dur: yatayda daha fazla iç boşluk.
        float pad = Mathf.Min(size.x * 0.14f, 30f);
        tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one;
        tr.offsetMin = new Vector2(icon != null ? h * 1.0f : pad, 8f);
        tr.offsetMax = new Vector2(-pad, -8f);
        t.alignment = TextAlignmentOptions.Center;
        return btn;
    }

    static TMP_Text NewText(Transform parent, float size)
    {
        var go = new GameObject("T", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.fontSize = size; t.fontStyle = FontStyles.Bold; t.raycastTarget = false;
        t.isRightToLeftText = Loc.Current == Language.Arabic;   // Arapça sağdan-sola
        return t;
    }

    // ── PROSEDÜREL İKONLAR (beyaz, şeffaf zemin) ───────────────────────────────
    static Sprite MakePlay(int s)
    {
        var t = NewTex(s);
        Vector2 a = new(0.30f, 0.20f), b = new(0.30f, 0.80f), c = new(0.78f, 0.50f);
        for (int y = 0; y < s; y++) for (int x = 0; x < s; x++)
        {
            var p = new Vector2((x + 0.5f) / s, (y + 0.5f) / s);
            t.SetPixel(x, y, InTri(p, a, b, c) ? Color.white : Clear);
        }
        return Finish(t, s);
    }

    static Sprite MakePower(int s)
    {
        var t = NewTex(s);
        for (int y = 0; y < s; y++) for (int x = 0; x < s; x++)
        {
            float u = (x + 0.5f) / s, v = (y + 0.5f) / s, dx = u - 0.5f, dy = v - 0.5f;
            float r = Mathf.Sqrt(dx * dx + dy * dy), ang = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
            bool ring = r > 0.27f && r < 0.38f && !(ang > 60f && ang < 120f);   // üstte boşluk
            bool bar = Mathf.Abs(dx) < 0.055f && v > 0.5f && v < 0.86f;          // üst dikey çubuk
            t.SetPixel(x, y, (ring || bar) ? Color.white : Clear);
        }
        return Finish(t, s);
    }

    static Sprite MakeRefresh(int s)
    {
        var t = NewTex(s);
        Vector2 ah0 = new(0.60f, 0.78f), ah1 = new(0.86f, 0.78f), ah2 = new(0.73f, 0.97f); // ok ucu (üst)
        for (int y = 0; y < s; y++) for (int x = 0; x < s; x++)
        {
            float u = (x + 0.5f) / s, v = (y + 0.5f) / s, dx = u - 0.5f, dy = v - 0.5f;
            float r = Mathf.Sqrt(dx * dx + dy * dy), ang = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
            bool ring = r > 0.27f && r < 0.38f && !(ang > 40f && ang < 110f);   // üst-sağda boşluk
            bool head = InTri(new Vector2(u, v), ah0, ah1, ah2);
            t.SetPixel(x, y, (ring || head) ? Color.white : Clear);
        }
        return Finish(t, s);
    }

    // Altın coin: dış kenar (koyu) + gövde (altın) + iç halka oluğu + sol-üst parlama. Renkli çizilir (tint YOK).
    static readonly Color CoinBody = new(1f, 0.80f, 0.22f, 1f);
    static readonly Color CoinRim  = new(0.78f, 0.52f, 0.07f, 1f);
    static readonly Color CoinLite = new(1f, 0.94f, 0.62f, 1f);
    static Sprite MakeCoin(int s)
    {
        var t = NewTex(s);
        for (int y = 0; y < s; y++) for (int x = 0; x < s; x++)
        {
            float u = (x + 0.5f) / s, v = (y + 0.5f) / s, dx = u - 0.5f, dy = v - 0.5f;
            float r = Mathf.Sqrt(dx * dx + dy * dy);
            Color c = Clear;
            if (r < 0.47f)
            {
                c = CoinBody;
                if (r > 0.38f) c = CoinRim;                                   // dış kenar
                else if (r > 0.29f && r < 0.33f) c = CoinRim;                 // iç halka oluğu
                float hx = u - 0.37f, hy = v - 0.63f;                         // sol-üst parlama
                if (hx * hx + hy * hy < 0.018f && r < 0.36f) c = CoinLite;
            }
            t.SetPixel(x, y, c);
        }
        return Finish(t, s);
    }

    // "Reklam izle" video ikonu: yuvarlak köşeli çerçeve (TV/video) + ortada play üçgeni. Beyaz.
    static Sprite MakeVideo(int s)
    {
        var t = NewTex(s);
        Vector2 a = new(0.42f, 0.35f), b = new(0.42f, 0.65f), c = new(0.66f, 0.50f);   // play üçgeni
        for (int y = 0; y < s; y++) for (int x = 0; x < s; x++)
        {
            float u = (x + 0.5f) / s, v = (y + 0.5f) / s;
            bool inFrame = u > 0.12f && u < 0.88f && v > 0.26f && v < 0.74f;
            bool onBorder = inFrame && (u < 0.19f || u > 0.81f || v < 0.32f || v > 0.68f);   // çerçeve bandı
            bool tri = InTri(new Vector2(u, v), a, b, c);
            t.SetPixel(x, y, (onBorder || tri) ? Color.white : Clear);
        }
        return Finish(t, s);
    }

    static readonly Color Clear = new(0, 0, 0, 0);
    static Texture2D NewTex(int s) => new(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
    static Sprite Finish(Texture2D t, int s) { t.Apply(); return Sprite.Create(t, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f); }

    static bool InTri(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        float d1 = Sign(p, a, b), d2 = Sign(p, b, c), d3 = Sign(p, c, a);
        bool neg = d1 < 0 || d2 < 0 || d3 < 0, pos = d1 > 0 || d2 > 0 || d3 > 0;
        return !(neg && pos);
    }
    static float Sign(Vector2 p, Vector2 a, Vector2 b) => (p.x - b.x) * (a.y - b.y) - (a.x - b.x) * (p.y - b.y);
}
