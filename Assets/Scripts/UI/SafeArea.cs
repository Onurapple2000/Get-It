using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Bir tam-ekran RectTransform'u cihazın GÜVENLİ ALANINA (Screen.safeArea — çentik, punch-hole, gesture/home barı,
/// yuvarlak köşeler) oturtur. Kontroller (butonlar, skor, HUD, patika) bunun altına konur → hiçbir cihazda kenarlara
/// taşmaz / çentik altında kalmaz. Arka planlar tam-ekran (bunun DIŞINDA, Canvas kökünde) kalır.
/// Çözünürlük/oryantasyon değişince kendini günceller.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class SafeArea : MonoBehaviour
{
    RectTransform rt;
    Rect lastSafe;
    Vector2Int lastRes;

    void Awake() { rt = (RectTransform)transform; Apply(); }

    void Update()
    {
        if (Screen.safeArea != lastSafe || Screen.width != lastRes.x || Screen.height != lastRes.y)
            Apply();
    }

    void Apply()
    {
        lastSafe = Screen.safeArea;
        lastRes = new Vector2Int(Screen.width, Screen.height);
        if (Screen.width <= 0 || Screen.height <= 0) return;

        Rect sa = Screen.safeArea;
        float xMin = sa.xMin, xMax = sa.xMax, yMin = sa.yMin, yMax = sa.yMax;

        // ÜST kamera cutout'unu (punch-hole/çentik) HER ZAMAN dışarıda bırak. Bazı cihazlar (ör. bazı Xiaomi'ler,
        // tam-ekran modunda) orta-üstteki punch-hole'u Screen.safeArea'ya DAHİL ETMEZ → başlıklar deliğe girer.
        // Screen.cutouts ile üst cutout'ların altına çekiyoruz (kamera için ayrılmış üst satır her ekranda korunur).
        var cutouts = Screen.cutouts;
        if (cutouts != null)
        {
            float h = Screen.height;
            foreach (var c in cutouts)
                if (c.yMax >= h - 1f && c.center.y > h * 0.5f)   // yalnız üst kenara değen cutout'lar
                    yMax = Mathf.Min(yMax, c.yMin);
        }

        Vector2 min = new Vector2(xMin, yMin);
        Vector2 max = new Vector2(xMax, yMax);
        min.x /= Screen.width; min.y /= Screen.height;
        max.x /= Screen.width; max.y /= Screen.height;

        // Geçersiz (0 boyut / NaN) durumda tam ekrana düş.
        if (float.IsNaN(min.x) || max.x <= min.x || max.y <= min.y)
        { min = Vector2.zero; max = Vector2.one; }

        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}

/// <summary>
/// Bir Canvas altında (bir kez oluşturulan) güvenli-alan KONTROL katmanını döndürür. Runtime UI kuran her yer
/// (`canvas.transform` yerine) bunu parent alır → tüm kontroller güvenli alana oturur. Arka planlar Canvas köküne.
/// </summary>
public static class UiRoot
{
    public const string NAME = "SafeArea";

    /// <summary>
    /// Ana oyun/menü canvas'ını GÜVENİLİR bulur: yalnız AKTİF + ScreenSpace + (tercihen) CanvasScaler'lı KÖK canvas.
    /// FindAnyObjectByType&lt;Canvas&gt; rastgele/yanlış canvas (kapalı PauseMenu overlay'i, HardLevelIntro/Tutorial
    /// geçici canvas'ları) döndürebiliyor → HUD yanlış canvas'a bağlanıp GÖRÜNMEYEBİLİYORDU (aralıklı bug). Bu, hep
    /// aynı doğru canvas'ı verir.
    /// </summary>
    public static Canvas GameCanvas()
    {
        // ⚠️ BUG FIX (2026-08-22): eskiden "ilk bulunan CanvasScaler'lı canvas" döndürülüyordu. Ama
        // HardLevelIntro ve FoodsL1Tutorial da KENDİ CanvasScaler'lı canvas'larını yaratıyor ve
        // FindObjectsByType SIRA GARANTİSİ VERMİYOR → HUD bazen bu GEÇİCİ canvas'a bağlanıyor, intro
        // yok edilince HUD da onunla siliniyordu ("güç-up'larım bazen görünmüyor" bug'ı; hard levellarda
        // intro olduğu için daha sık).
        // ÇÖZÜM: geçici overlay'ler bilerek YÜKSEK sortingOrder kullanır (intro 6000, pause 7000); kalıcı
        // ana canvas 0'dır → adaylar arasında EN DÜŞÜK sortingOrder'lı olanı seç (scaler'lı olan öncelikli).
        var all = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        Canvas best = null; long bestScore = long.MinValue;
        foreach (var c in all)
        {
            var root = c.rootCanvas;
            if (root == null || !root.isActiveAndEnabled) continue;
            if (root.renderMode != RenderMode.ScreenSpaceOverlay && root.renderMode != RenderMode.ScreenSpaceCamera) continue;
            long score = (root.GetComponent<CanvasScaler>() != null ? 1_000_000L : 0L) - root.sortingOrder;
            if (score > bestScore) { bestScore = score; best = root; }
        }
        return best;
    }

    public static RectTransform SafeContent(Canvas canvas)
    {
        if (canvas == null) return null;
        var existing = canvas.transform.Find(NAME) as RectTransform;
        if (existing != null) return existing;

        var go = new GameObject(NAME, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(canvas.transform, false);
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        go.AddComponent<SafeArea>();
        return rt;
    }
}
