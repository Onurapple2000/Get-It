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
    static Sprite _rect, _circle, _play, _refresh, _power, _gear;
    public static Sprite Rect()    => _rect    ? _rect    : (_rect    = Resources.Load<Sprite>("burrow_button_empty_rect"));
    public static Sprite Circle()  => _circle  ? _circle  : (_circle  = Resources.Load<Sprite>("burrow_button_empty_circle"));
    public static Sprite Play()    => _play    ? _play    : (_play    = MakePlay(64));
    public static Sprite Refresh() => _refresh ? _refresh : (_refresh = MakeRefresh(72));
    public static Sprite Power()   => _power   ? _power   : (_power   = MakePower(72));
    public static Sprite Gear()    => _gear    ? _gear    : (_gear    = MakeGear(80));

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

    /// <summary>Pill buton: rect görsel (Simple) + sola ikon + ortalı yazı. Button döner.</summary>
    public static Button Build(Transform parent, Vector2 anchor, Vector2 pos, Vector2 size,
                               string label, Sprite icon, Color tint, float fontSize = 40)
    {
        var go = new GameObject("Btn_" + label, typeof(RectTransform), typeof(Image), typeof(Button));
        var rt = (RectTransform)go.transform; rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = anchor; rt.pivot = anchor;
        rt.anchoredPosition = pos; rt.sizeDelta = size;
        var im = go.GetComponent<Image>();
        var r = Rect(); if (r != null) { im.sprite = r; im.type = Image.Type.Simple; }
        im.color = tint;

        float h = size.y;
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
        tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one;
        tr.offsetMin = new Vector2(icon != null ? h * 1.0f : 12f, 0f);
        tr.offsetMax = new Vector2(-12f, 0f);
        t.alignment = TextAlignmentOptions.Center;
        var btn = go.GetComponent<Button>();
        btn.onClick.AddListener(() => AudioManager.Instance?.PlayUiClick());   // her buton tık sesi
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
