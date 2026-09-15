using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Ortak bildirim kartı (2026-09-15, v2): buton gibi görünmesin diye ahşap çerçeve YOK — düz, yumuşak köşeli,
/// duruma göre renkli kart (yeşil=olumlu, kırmızı=olumsuz, koyu=bilgi), solda yuvarlak ikon rozeti, hafif gölge.
/// Üstten süzülerek gelir, kısa durur, geri süzülüp söner. Kendi canvas'ında (8000) → her şeyin üstünde; unscaled.
/// Kullanım: ToastUI.Show("Alındı!", null, ToastUI.Style.Success) · ToastUI.Show("+50", UiButtons.Coin(), ToastUI.Style.Reward)
/// </summary>
public static class ToastUI
{
    public enum Style { Info, Success, Error, Reward }

    static GameObject current;

    public static void Show(string msg, Sprite icon = null, Style style = Style.Info)
    {
        var host = ToastHost.Get(); if (host == null) return;
        if (current != null) Object.Destroy(current);

        Color bg, badge, text;
        string glyph;
        switch (style)
        {
            case Style.Success: bg = new Color(0.16f, 0.55f, 0.30f, 0.97f); badge = new Color(0.10f, 0.40f, 0.20f); text = Color.white; glyph = "✓"; break;
            case Style.Reward:  bg = new Color(0.16f, 0.55f, 0.30f, 0.97f); badge = new Color(1f, 0.85f, 0.30f);    text = Color.white; glyph = "+"; break;
            case Style.Error:   bg = new Color(0.72f, 0.22f, 0.20f, 0.97f); badge = new Color(0.50f, 0.12f, 0.10f); text = Color.white; glyph = "!"; break;
            default:            bg = new Color(0.14f, 0.13f, 0.12f, 0.96f); badge = new Color(0.30f, 0.27f, 0.24f); text = new Color(1f, 0.96f, 0.88f); glyph = "i"; break;
        }
        bool reward = style == Style.Reward;
        float h = reward ? 124f : 96f;

        // Kök (konum + animasyon)
        var root = new GameObject("Toast", typeof(RectTransform), typeof(CanvasGroup));
        current = root;
        var rt = (RectTransform)root.transform; rt.SetParent(host.transform, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, reward ? 0.5f : 1f); rt.pivot = new Vector2(0.5f, reward ? 0.5f : 1f);
        rt.anchoredPosition = reward ? new Vector2(0, 140f) : new Vector2(0, -120f);   // ödül: ortada; diğerleri: üstte (safe area altı)

        // Gölge (kart arkasında, hafif aşağı-sağa)
        var sh = new GameObject("Shadow", typeof(RectTransform), typeof(Image));
        var srt = (RectTransform)sh.transform; srt.SetParent(rt, false);
        srt.anchorMin = Vector2.zero; srt.anchorMax = Vector2.one; srt.offsetMin = new Vector2(4, -8); srt.offsetMax = new Vector2(4, -8);
        var si = sh.GetComponent<Image>(); si.sprite = UiButtons.Rounded(); si.type = Image.Type.Sliced; si.color = new Color(0, 0, 0, 0.28f); si.raycastTarget = false;

        // Kart
        var card = new GameObject("Card", typeof(RectTransform), typeof(Image));
        var crt = (RectTransform)card.transform; crt.SetParent(rt, false);
        crt.anchorMin = Vector2.zero; crt.anchorMax = Vector2.one; crt.offsetMin = crt.offsetMax = Vector2.zero;
        var ci = card.GetComponent<Image>(); ci.sprite = UiButtons.Rounded(); ci.type = Image.Type.Sliced; ci.color = bg; ci.raycastTarget = false;

        // İçerik: [rozet+ikon] yazı
        var row = new GameObject("Row", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
        var rrt = (RectTransform)row.transform; rrt.SetParent(crt, false);
        rrt.anchorMin = rrt.anchorMax = new Vector2(0.5f, 0.5f); rrt.pivot = new Vector2(0.5f, 0.5f);
        var hl = row.GetComponent<HorizontalLayoutGroup>();
        hl.childAlignment = TextAnchor.MiddleCenter; hl.spacing = 16f;
        hl.childControlWidth = true; hl.childControlHeight = true; hl.childForceExpandWidth = false; hl.childForceExpandHeight = false;
        row.GetComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

        // Rozet: dolu daire + (ikon ya da işaret)
        float bsz = h * 0.66f;
        var bg1 = new GameObject("Badge", typeof(RectTransform), typeof(Image), typeof(LayoutElement)); bg1.transform.SetParent(rrt, false);
        var bi = bg1.GetComponent<Image>(); bi.sprite = UiButtons.Disc(); bi.color = badge; bi.raycastTarget = false;
        var ble = bg1.GetComponent<LayoutElement>(); ble.preferredWidth = bsz; ble.preferredHeight = bsz;
        if (icon != null)
        {
            var ig = new GameObject("Icon", typeof(RectTransform), typeof(Image)); ig.transform.SetParent(bg1.transform, false);
            var irt = (RectTransform)ig.transform; irt.anchorMin = new Vector2(0.15f, 0.15f); irt.anchorMax = new Vector2(0.85f, 0.85f); irt.offsetMin = irt.offsetMax = Vector2.zero;
            var ii = ig.GetComponent<Image>(); ii.sprite = icon; ii.preserveAspect = true; ii.raycastTarget = false;
        }
        else
        {
            var gt = new GameObject("G", typeof(RectTransform)).AddComponent<TextMeshProUGUI>(); gt.transform.SetParent(bg1.transform, false);
            var grt = gt.rectTransform; grt.anchorMin = Vector2.zero; grt.anchorMax = Vector2.one; grt.offsetMin = grt.offsetMax = Vector2.zero;
            gt.text = glyph; gt.fontSize = bsz * 0.62f; gt.fontStyle = FontStyles.Bold; gt.alignment = TextAlignmentOptions.Center;
            gt.color = Color.white; gt.raycastTarget = false;
        }

        // Yazı
        var t = new GameObject("T", typeof(RectTransform)).AddComponent<TextMeshProUGUI>(); t.transform.SetParent(rrt, false);
        t.text = msg; t.fontSize = reward ? 50f : 34f; t.fontStyle = FontStyles.Bold; t.color = text; t.raycastTarget = false;
        t.alignment = TextAlignmentOptions.Left; t.enableWordWrapping = false;
        Loc.ApplyDir(t);

        Canvas.ForceUpdateCanvases();
        const float maxW = 940f;   // 1080 referansta sağ-sol 70 pay → hiçbir cihazda taşmaz
        float w = Mathf.Clamp(LayoutUtility.GetPreferredWidth(rrt) + 80f, 320f, maxW);
        if (LayoutUtility.GetPreferredWidth(rrt) + 80f > maxW)
        {
            // Uzun mesaj (ör. "İndirme başarısız — ...") tek satıra sığmıyor → yazıyı sar, kartı satır sayısı kadar büyüt
            // (kullanıcı 2026-09-16: sağdan soldan taşan kısım görünmüyordu).
            float textW = maxW - 80f - bsz - hl.spacing;
            t.enableWordWrapping = true;
            var tle = t.gameObject.AddComponent<LayoutElement>(); tle.preferredWidth = textW;
            t.rectTransform.sizeDelta = new Vector2(textW, 0f);
            t.ForceMeshUpdate();
            float th = t.GetPreferredValues(msg, textW, 0f).y;
            row.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            h = Mathf.Max(h, th + 40f);
            w = maxW;
        }
        rt.sizeDelta = new Vector2(w, h);

        host.StartCoroutine(Run(root, rt, root.GetComponent<CanvasGroup>(), reward));
    }

    static IEnumerator Run(GameObject go, RectTransform rt, CanvasGroup cg, bool reward)
    {
        Vector2 p0 = rt.anchoredPosition;
        Vector2 pIn = reward ? p0 : p0 + new Vector2(0, 90f);   // üstten süzül
        float t = 0f; cg.alpha = 0f;
        while (t < 0.24f)
        {
            t += Mathf.Min(Time.unscaledDeltaTime, 0.05f); float k = Mathf.SmoothStep(0f, 1f, t / 0.24f);
            rt.anchoredPosition = Vector2.Lerp(pIn, p0, k); cg.alpha = k;
            if (reward) { float s = k < 0.7f ? Mathf.Lerp(0.7f, 1.08f, k / 0.7f) : Mathf.Lerp(1.08f, 1f, (k - 0.7f) / 0.3f); rt.localScale = Vector3.one * s; }
            yield return null;
        }
        rt.anchoredPosition = p0; rt.localScale = Vector3.one; cg.alpha = 1f;
        float hold = reward ? 1.5f : 1.4f; t = 0f;
        while (t < hold) { t += Mathf.Min(Time.unscaledDeltaTime, 0.05f); yield return null; }
        t = 0f;
        while (t < 0.32f)
        {
            t += Mathf.Min(Time.unscaledDeltaTime, 0.05f); float k = t / 0.32f;
            rt.anchoredPosition = Vector2.Lerp(p0, reward ? p0 + new Vector2(0, 60f) : pIn, k); cg.alpha = 1f - k;
            yield return null;
        }
        if (go != null) { if (current == go) current = null; Object.Destroy(go); }
    }

    class ToastHost : MonoBehaviour
    {
        static ToastHost inst;
        public static ToastHost Get()
        {
            if (inst != null) return inst;
            var go = new GameObject("ToastCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            var cv = go.GetComponent<Canvas>(); cv.renderMode = RenderMode.ScreenSpaceOverlay; cv.sortingOrder = 8000;
            var sc = go.GetComponent<CanvasScaler>();
            sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; sc.referenceResolution = new Vector2(1080, 1920); sc.matchWidthOrHeight = 0.5f;
            Object.DontDestroyOnLoad(go);
            inst = go.AddComponent<ToastHost>();
            return inst;
        }
    }
}
