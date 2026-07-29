using UnityEngine;

/// <summary>
/// Deliği 45° (ayarlanabilir) açıdan takip eden kamera. Deliği HER ZAMAN ekran merkezinde tutar
/// (XZ anında takip). Delik KÜÇÜKKEN yakın durur (küçük nesneler net), BÜYÜDÜKÇE uzaklaşır;
/// bu zoom değişimi yumuşaktır. Mesafe = baseDistance + (currentSize - baseSize) * distancePerSize.
/// </summary>
public class HoleCamera : MonoBehaviour
{
    [Tooltip("Takip edilecek delik. Boşsa otomatik bulunur.")]
    public HoleController hole;

    [Header("Açı & mesafe")]
    [Tooltip("Bakış açısı (derece). 45 = izometrik benzeri.")]
    public float angle = 45f;
    [Tooltip("Delik temel boyuttayken mesafe (yakınlık). Küçük = başta daha yakın; delik büyüdükçe distancePerSize ile uzaklaşır.")]
    public float baseDistance = 10f;
    [Tooltip("Delik boyutu temelin üstüne çıktıkça eklenecek mesafe (zoom-out). Yüksek = büyüdükçe daha belirgin uzaklaşır. 2026-07-23: 6→3 (kullanıcı: daha az zoom-out).")]
    public float distancePerSize = 3f;
    [Tooltip("Mesafenin hesaplandığı temel delik boyutu.")]
    public float baseSize = 1.5f;
    [Tooltip("Maksimum mesafe (çok uzaklaşmayı sınırla).")]
    public float maxDistance = 45f;

    [Header("Yumuşatma")]
    [Tooltip("Zoom (mesafe) değişimi yumuşatma süresi (sn). Konum anında takip edilir.")]
    public float zoomSmoothTime = 0.25f;
    [Tooltip("Deliğin XZ'sini takip et (kapalıysa hep origin'e bakar).")]
    public bool followPosition = true;

    [Header("Açılış (intro fly-in)")]
    [Tooltip("Oyun başında kamera bu UZAK mesafeden başlar (tüm alanı/heykelleri gösterir).")]
    public float introDistance = 42f;
    [Tooltip("Uzaktan normal pozisyona inme süresi (sn).")]
    public float introDuration = 3f;

    float currentDist;
    float distVel;
    float introStart;

    void Start()
    {
        if (hole == null) hole = FindFirstObjectByType<HoleController>();
        introStart = Time.time;
        currentDist = introDistance;   // uzaktan başla
        Apply();
    }

    void LateUpdate()
    {
        if (hole == null) { hole = FindFirstObjectByType<HoleController>(); if (hole == null) return; }

        float it = (Time.time - introStart) / Mathf.Max(0.01f, introDuration);
        if (it < 1f)
        {
            // Açılış: uzaktan normal mesafeye yumuşakça gel (ease-in-out)
            currentDist = Mathf.Lerp(introDistance, TargetDistance(), Mathf.SmoothStep(0f, 1f, it));
        }
        else
        {
            currentDist = Mathf.SmoothDamp(currentDist, TargetDistance(), ref distVel, zoomSmoothTime);
        }
        Apply();
    }

    float TargetDistance()
    {
        if (hole == null) return baseDistance;
        return Mathf.Min(maxDistance, baseDistance + Mathf.Max(0f, hole.currentSize - baseSize) * distancePerSize);
    }

    void Apply()
    {
        // Bakış noktası = deliğin XZ'si (y=0). Konum ANINDA buna kilitlenir → delik hep merkezde.
        Vector3 look = followPosition ? hole.transform.position : Vector3.zero;
        look.y = 0f;
        float rad = angle * Mathf.Deg2Rad;
        Vector3 offset = new Vector3(0f, Mathf.Sin(rad), -Mathf.Cos(rad)) * currentDist;
        transform.position = look + offset;
        transform.rotation = Quaternion.Euler(angle, 0f, 0f);
    }
}
