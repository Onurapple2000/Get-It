using UnityEngine;

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
        Vector2 min = sa.position;
        Vector2 max = sa.position + sa.size;
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
