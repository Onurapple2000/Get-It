using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// HARD level başında görünen tam-ekran intro: prosedürel KURUKAFA + "HARD LEVEL" yazısı (kırmızı-siyah).
/// Kısa pop-in → 1sn sabit dur → 2sn boyunca BÜYÜYEREK kaybol → kendini yok et. Oyun girişini engellemez
/// (blocksRaycasts kapalı; oyun dokunmayı doğrudan Input'tan okur). GameManager.Start HARD level'da çağırır.
/// Menü/HUD üstünde durması için ayrı Canvas (yüksek sortingOrder).
/// </summary>
public class HardLevelIntro : MonoBehaviour
{
    public static void Show()
    {
        if (FindAnyObjectByType<HardLevelIntro>() != null) return;   // çift gösterme
        new GameObject("HardLevelIntro").AddComponent<HardLevelIntro>();
    }

    void Start() => StartCoroutine(Run());

    IEnumerator Run()
    {
        // ── Overlay canvas (her şeyin üstünde) ──
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 6000;
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight = 0.5f;

        // Büyüyüp solacak grup
        var group = new GameObject("Group", typeof(RectTransform), typeof(CanvasGroup));
        var g = (RectTransform)group.transform; g.SetParent(transform, false);
        g.anchorMin = g.anchorMax = new Vector2(0.5f, 0.5f); g.pivot = new Vector2(0.5f, 0.5f);
        g.anchoredPosition = Vector2.zero; g.sizeDelta = new Vector2(920, 760);
        var cg = group.GetComponent<CanvasGroup>();
        cg.interactable = false; cg.blocksRaycasts = false;   // oyun girişini engelleme

        BuildSkull(g, new Vector2(0f, 175f));
        BuildTitle(g, new Vector2(0f, -185f));

        // ── Animasyon ──
        g.localScale = Vector3.one * 0.65f; cg.alpha = 0f;
        float t = 0f;
        while (t < 0.25f) { t += Time.deltaTime; float k = Mathf.Clamp01(t / 0.25f); g.localScale = Vector3.one * Mathf.Lerp(0.65f, 1f, k); cg.alpha = k; yield return null; }
        g.localScale = Vector3.one; cg.alpha = 1f;

        yield return new WaitForSeconds(1f);   // 1sn sabit görün

        t = 0f;
        while (t < 2f)                          // 2sn büyüyerek kaybol
        {
            t += Time.deltaTime; float e = Mathf.SmoothStep(0f, 1f, t / 2f);
            g.localScale = Vector3.one * Mathf.Lerp(1f, 2.7f, e);
            cg.alpha = 1f - e;
            yield return null;
        }
        Destroy(gameObject);
    }

    // ── "HARD LEVEL" — kırmızı(üst)→siyah(alt) dikey degrade + siyah kontur ──
    void BuildTitle(RectTransform parent, Vector2 pos)
    {
        var go = new GameObject("Title", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var txt = go.AddComponent<TextMeshProUGUI>();
        txt.text = "HARD LEVEL";
        txt.fontSize = 158; txt.fontStyle = FontStyles.Bold; txt.alignment = TextAlignmentOptions.Center;
        txt.raycastTarget = false;
        txt.enableVertexGradient = true;
        txt.colorGradient = new VertexGradient(
            new Color(1f, 0.20f, 0.16f), new Color(1f, 0.20f, 0.16f),   // üst: canlı kırmızı
            new Color(0.14f, 0.02f, 0.02f), new Color(0.14f, 0.02f, 0.02f)); // alt: siyaha yakın
        var rt = txt.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f); rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos; rt.sizeDelta = new Vector2(900, 240);

        // Siyah kontur (okunurluk) — materyal örneği (global materyali etkilemez)
        var mat = txt.fontMaterial;
        mat.EnableKeyword(ShaderUtilities.Keyword_Outline);
        mat.SetColor(ShaderUtilities.ID_OutlineColor, Color.black);
        mat.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.22f);
    }

    // ── Prosedürel kurukafa (beyaz yüz + siyah göz/burun/diş) ──
    void BuildSkull(RectTransform parent, Vector2 center)
    {
        var root = new GameObject("Skull", typeof(RectTransform));
        var r = (RectTransform)root.transform; r.SetParent(parent, false);
        r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f); r.pivot = new Vector2(0.5f, 0.5f);
        r.anchoredPosition = center; r.sizeDelta = new Vector2(300, 320);

        Color bone  = new Color(0.96f, 0.94f, 0.88f);
        Color black = new Color(0.05f, 0.04f, 0.05f);

        Disc(r, bone,  new Vector2(0f,  40f), new Vector2(238, 220));   // kafatası (üst geniş)
        Disc(r, bone,  new Vector2(0f, -78f), new Vector2(156, 138));   // çene (alt dar)
        Disc(r, black, new Vector2(-54f, 46f), new Vector2(78, 82));    // sol göz
        Disc(r, black, new Vector2( 54f, 46f), new Vector2(78, 82));    // sağ göz
        Disc(r, black, new Vector2(0f,  -6f), new Vector2(38, 50));     // burun (dikey oval)
        // dişler (alt çenede 3 siyah çubuk)
        Box(r, black, new Vector2(-34f, -96f), new Vector2(16, 46));
        Box(r, black, new Vector2(  0f, -100f), new Vector2(16, 50));
        Box(r, black, new Vector2( 34f, -96f), new Vector2(16, 46));
    }

    Image Disc(RectTransform parent, Color c, Vector2 pos, Vector2 size)
    {
        var img = NewImage(parent, DiscSprite(), c, pos, size);
        img.type = Image.Type.Simple; img.preserveAspect = false;
        return img;
    }

    Image Box(RectTransform parent, Color c, Vector2 pos, Vector2 size)
        => NewImage(parent, null, c, pos, size);   // sprite yok → dolu beyaz kutu (tint'lenir)

    static Image NewImage(RectTransform parent, Sprite s, Color c, Vector2 pos, Vector2 size)
    {
        var go = new GameObject("El", typeof(RectTransform), typeof(Image));
        var rt = (RectTransform)go.transform; rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f); rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos; rt.sizeDelta = size;
        var img = go.GetComponent<Image>();
        if (s != null) img.sprite = s;
        img.color = c; img.raycastTarget = false;
        return img;
    }

    // Anti-aliased dolu daire sprite'ı (runtime üretilir, tekrar kullanılır).
    static Sprite _disc;
    static Sprite DiscSprite()
    {
        if (_disc != null) return _disc;
        const int R = 128, D = R * 2;
        var tex = new Texture2D(D, D, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var px = new Color32[D * D];
        for (int y = 0; y < D; y++)
            for (int x = 0; x < D; x++)
            {
                float dx = x - R + 0.5f, dy = y - R + 0.5f;
                float a = Mathf.Clamp01(R - Mathf.Sqrt(dx * dx + dy * dy));   // 1px kenar yumuşatma
                px[y * D + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
        tex.SetPixels32(px); tex.Apply();
        _disc = Sprite.Create(tex, new Rect(0, 0, D, D), new Vector2(0.5f, 0.5f), 100f);
        return _disc;
    }
}
