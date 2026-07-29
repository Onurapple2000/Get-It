using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Fizik-temelli yutulabilir nesne. Script'li emme/tipping yok — tamamen fizik.
///  - Yutulma: nesnenin TAMAMI (tüm collider'larının tepesi) yer seviyesinin altına inince →
///    HoleController.Grow() + despawn.
///  - Anti-burial: nesne delik ÜZERİNDE DEĞİLKEN bir kısmı yer altında kalırsa (delik uzaklaşıp
///    nesneyi yüzey altında bırakınca) yumuşakça yukarı itilir → gömülü kalmaz. Delik üzerindeyken
///    muaf, böylece sıkışma/yutulma davranışı bozulmaz.
/// </summary>
public class PhysicsSwallowable : MonoBehaviour
{
    [Header("Yutulma")]
    public float growAmount = 0.075f;
    public int scoreValue = 10;

    [Header("Kota/Hedef sistemi")]
    [Tooltip("Nesne türü (hedef eşleştirme için). Boşsa gameObject adından türetilir (ör. 'FlowerPot').")]
    public string objectType = "";
    [Tooltip("Bomba mı? Yutulursa level anında kaybedilir (sadece bombsEnabled level'larda spawn edilir).")]
    public bool isBomb = false;

    [Header("Güç-Up (Sprint 4)")]
    [Tooltip("Yutulunca otomatik verilen geçici güç. None = normal nesne.")]
    public PowerUpType powerUp = PowerUpType.None;

    /// <summary>Sahnedeki tüm aktif yutulabilirler (mıknatıs gücü yakındakileri çekmek için tarar).</summary>
    public static readonly List<PhysicsSwallowable> All = new List<PhysicsSwallowable>();
    void OnEnable() { All.Add(this); }
    void OnDisable() { All.Remove(this); }

    /// <summary>Hedef eşleştirmede kullanılan tür anahtarı. objectType boşsa ad temizlenir.</summary>
    public string ResolvedType =>
        !string.IsNullOrEmpty(objectType) ? objectType : CleanName(gameObject.name);

    static string CleanName(string n)
    {
        // "FlowerPot (Clone)", "FlowerPot (1)" → "FlowerPot"
        int p = n.IndexOf(" (");
        return p >= 0 ? n.Substring(0, p) : n;
    }
    [Tooltip("Yer (zemin) seviyesi Y. Nesnenin tamamı bunun altına inince yutulur.")]
    public float groundLevel = 0f;
    [Tooltip("Yutulma için küçük tolerans.")]
    public float swallowMargin = 0.02f;

    [Header("Anti-burial (gömülmeyi önle — doğal devrilerek)")]
    [Tooltip("Delik üzerinde değilken bu kadar yer altına inerse çıkış kuvveti uygulanır.")]
    public float buryMargin = 0.15f;
    [Tooltip("Gömülü alt noktaya uygulanan yukarı ivme (yerçekimini yenmeli, >9.81).")]
    public float risePush = 18f;
    [Tooltip("Deliğe DOĞRU yatay ivme — gömülü nesneyi delik boşluğuna geri sokar (zeminden değil, delikten çıkar).")]
    public float lateralPush = 6f;
    [Tooltip("Delik yarıçapına eklenen pay; bundan uzaktaysa 'delik üzerinde değil' sayılır.")]
    public float holeClearance = 0.1f;

    [Header("Delik emişi (hızlı/seri yutma)")]
    [Tooltip("Nesne deliğe girmeye başlayınca (tabanı zemin altına geçince) uygulanan EK AŞAĞI ivme " +
             "(yerçekimine eklenir). Yüksek = daha seri yutma. Sığmayan büyük nesneleri etkilemez (tabanları zeminde kalır).")]
    public float holeSuction = 28f;
    [Tooltip("Delik üzerindeyken merkeze DOĞRU hafif yatay çekim → nesneler kenara takılmadan boşluğa akar (seri yutma).")]
    public float holeCentering = 4f;
    [Tooltip("UZUN nesne güvenlik ağı: nesne delik üstünde VE tabanı zemin altına bu kadar inip dibe oturmuşsa " +
             "(tepesi zemin üstünde kalsa bile) yutulur. Çukurdan uzun nesnelerin (ör. sokak lambası) delikle " +
             "'gezmesini' önler. Değer < çukur derinliği olmalı; sığmayan geniş nesne bu derinliğe inemez → tetiklenmez.")]
    public float tallSwallowDepth = 3.5f;

    [Header("Dönüş")]
    [Tooltip("Maks açısal hız (rad/s). Düşük = takla yerine yumuşak devrilme. Unity varsayılanı 50.")]
    public float maxAngularSpeed = 5f;

    [Header("Donuk başlangıç (performans + kayma yok)")]
    [Tooltip("Açıkken nesne KİNEMATİK (donuk) başlar; delik yaklaşınca uyanıp dinamikleşir. " +
             "Lag yok, başlangıçta kayma yok, kuleler yerinde durur.")]
    public bool freezeUntilNear = true;
    [Tooltip("Uyanma payı (delik yarıçapına eklenir). 0 = nesne merkezi delik sınırını geçince uyanır (~%50).")]
    public float wakeMargin = 0f;
    [Tooltip("Kendi başına uyanmaz; dışarıdan WakeNow ile uyanır (ör. tepsi üstündeki nesneler tepsiyle uyansın).")]
    public bool externalWake = false;
    [Tooltip("Bu nesne uyanınca birlikte uyandırılacak diğerleri (ör. tepsi → üstündeki nesneler).")]
    public TrayGroup group;

    [Tooltip("Uyanınca (dinamikleşince) uygulanan lineer damping. Yüksek = yere düşen/devrilen nesne zeminde " +
             "daha az KAYAR (zemin sürtünmesiz olduğundan kayma buradan sönümlenir). Delik emişi (holeSuction) " +
             "bunu yenecek kadar güçlü olduğundan yutulma yavaşlamaz.")]
    public float wakeLinearDamping = 1.6f;

    // Kare başına uyanma kotası (tüm nesneler paylaşır; spike'ı birkaç kareye yayar).
    const int WAKE_BUDGET_PER_FRAME = 8;
    static int s_wakeFrame = -1, s_wakeUsed;

    HoleController hole;
    Rigidbody rb;
    Collider[] cols;
    bool frozen;
    float objHalf = 0.5f;   // nesnenin yatay yarı-genişliği (uyanma eşiği için)
    bool swallowed;
    public bool IsSwallowed => swallowed;
    public Rigidbody Body => rb;

    /// <summary>Nesnenin yatay çapı (2×yatay yarı-genişlik) — Start'ta ölçekli bounds'tan.</summary>
    public float SwallowSize { get; private set; } = 1f;

    /// <summary>Spawn'da uygulanan görsel ölçek varyantı (nesnenin kendi normaline göre kaç kat). 1 = normal.
    /// Yutma sesinin küçük/orta/büyük tier'ı buna göre seçilir (dünyadan bağımsız, "aynı nesnenin büyüğü/küçüğü").</summary>
    public float SizeFactor { get; private set; } = 1f;

    /// <summary>
    /// Spawn'da uygulanan görsel ölçek varyantını PUAN ve BÜYÜMEYE de yansıt: aynı prefab'ın büyük ölçekli
    /// kopyası daha çok puan verir + deliği daha çok büyütür (küçük olan az). LevelManager spawn yardımcıları
    /// nesneyi <c>localScale *= f</c> ile ölçekledikten sonra çağırır. Faktör ses tier'ı için de saklanır.
    /// </summary>
    public void ApplySizeFactor(float f)
    {
        if (f <= 0.01f) return;
        SizeFactor *= f;                                  // ses tier'ı için sakla (f≈1 olsa da)
        if (Mathf.Abs(f - 1f) < 0.001f) return;
        scoreValue = Mathf.Max(1, Mathf.RoundToInt(scoreValue * f));
        growAmount *= f;
    }

    void Awake()
    {
        // Donuk başla — ilk fizik adımından ÖNCE (Awake) kinematik yap → spawn anında tek-kare itme/kayma olmaz.
        rb = GetComponent<Rigidbody>();
        if (freezeUntilNear && rb != null)
        {
            rb.isKinematic = true;
            frozen = true;
        }
    }

    void Start()
    {
        hole = FindFirstObjectByType<HoleController>();
        if (rb == null) rb = GetComponent<Rigidbody>();
        cols = GetComponentsInChildren<Collider>();
        // Takla (somersault) önlemek için açısal hızı sınırla → yumuşak devrilme
        if (rb != null) rb.maxAngularVelocity = maxAngularSpeed;

        // Yatay yarı-genişlik (uyanma eşiği + delik yeterince büyük mü kontrolü için)
        if (cols != null && cols.Length > 0)
        {
            Bounds bb = cols[0].bounds;
            for (int i = 1; i < cols.Length; i++) bb.Encapsulate(cols[i].bounds);
            objHalf = Mathf.Max(bb.extents.x, bb.extents.z);
            SwallowSize = 2f * objHalf;   // yatay çap → ses tier'ı (küçük/orta/büyük)
        }
    }

    /// <summary>Donuk nesneyi hemen dinamikleştir (dışarıdan/grup tarafından). Patlamasın diye yumuşak ayrılma.</summary>
    public void WakeNow()
    {
        if (!frozen) return;
        frozen = false;
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.maxDepenetrationVelocity = 0.35f;
            rb.linearDamping = Mathf.Max(rb.linearDamping, wakeLinearDamping);   // zeminde fazla kaymasın (kayma sönümlensin)
        }
    }

    void FixedUpdate()
    {
        if (swallowed || cols == null || cols.Length == 0) return;

        // Donuk: yalnızca (a) delik nesneyi yutacak kadar BÜYÜK ve (b) nesne merkezi delik sınırını
        // geçtiğinde (~tabanının %50'si delikte) uyan. Aksi halde TAM STABİL kal (erken itme/çekme yok).
        if (frozen)
        {
            if (externalWake) return;   // dışarıdan uyandırılır (tepsi grubu) → kendi başına canlanma
            if (hole == null) return;
            float R = hole.currentSize * 0.5f;
            float ddx = transform.position.x - hole.transform.position.x;
            float ddz = transform.position.z - hole.transform.position.z;
            // Delik KENARI nesnenin kenarına yaklaşınca uyan (biraz erken) → nesne zeminde DİNAMİKLEŞİR;
            // delik altına girdikçe çukur duvarı onu DEVİRİR (topple), ve ancak fiziksel olarak SIĞIYORSA
            // içeri düşer. Boyut-geçidi + devrilme artık GERÇEK fizikten doğar (eski heuristik "bigEnough"
            // kaldırıldı — o yüzden hem "her boyut giriyor" hem "dik düşüyor" oluyordu).
            float wake = R + objHalf + 0.3f + wakeMargin;
            if (ddx * ddx + ddz * ddz <= wake * wake)
            {
                // ⚠️ KARE BAŞINA UYANMA KOTASI (perf): hız+mıknatısla öbeğe dalınca onlarca nesne AYNI karede uyanıp
                // (kinematik→dinamik + depenetration) fizik solver'ı spike'lıyordu → atlama. Kota dolduysa bu nesne
                // KİNEMATİK kalır, sonraki karede tekrar dener (delik hâlâ üstünde → birkaç kare içinde uyanır, his aynı).
                int f = Time.frameCount;
                if (f != s_wakeFrame) { s_wakeFrame = f; s_wakeUsed = 0; }
                if (s_wakeUsed >= WAKE_BUDGET_PER_FRAME) return;   // bu kare kotası dolu → beklet
                s_wakeUsed++;

                WakeNow();
                if (group != null) group.WakeAll();   // tepsi uyandı → üstündeki nesneler de uyansın
            }
            else return;
        }

        // Tüm aktif collider'ların tepe ve taban Y'si
        float topY = float.NegativeInfinity;
        float bottomY = float.PositiveInfinity;
        for (int i = 0; i < cols.Length; i++)
        {
            var c = cols[i];
            if (c == null || !c.enabled) continue;
            var b = c.bounds;
            if (b.max.y > topY) topY = b.max.y;
            if (b.min.y < bottomY) bottomY = b.min.y;
        }

        // Yutulma: tamamı yer altında (kısa/orta nesneler)
        if (topY < groundLevel - swallowMargin)
        {
            PerformSwallow();
            return;
        }

        if (hole != null && rb != null && !rb.isKinematic)
        {
            Vector3 hp = hole.transform.position;
            float dx = transform.position.x - hp.x;
            float dz = transform.position.z - hp.z;
            float distXZ = Mathf.Sqrt(dx * dx + dz * dz);
            float holeR = hole.currentSize * 0.5f;

            bool overHole = distXZ <= holeR + holeClearance;

            // DELİK EMİŞİ: nesne delik üzerinde VE tabanı zemin altına geçmişse (deliğe girmeye başladı) →
            // ekstra aşağı ivme + merkeze hafif çekim → seri yutma. Sığmayan büyük nesne zeminde durur
            // (tabanı zemin altına inmez) → tetiklenmez, itilmez.
            bool entering = bottomY < groundLevel - swallowMargin;
            if (overHole && entering)
            {
                Vector3 pull = Vector3.down * holeSuction;
                if (holeCentering > 0f)
                {
                    Vector3 inward = new Vector3(-dx, 0f, -dz);
                    if (inward.sqrMagnitude > 0.0001f) pull += inward.normalized * holeCentering;
                }
                rb.AddForce(pull, ForceMode.Acceleration);
            }

            // UZUN NESNE GÜVENLİK AĞI: nesne delik üstünde, tabanı çukura derin inmiş VE aşağı düşme durmuş
            // (dibe/duvara oturdu) → tepesi hâlâ zemin üstünde olsa bile "tamamı altında" koşulu asla sağlanmaz.
            // Bu durumda yut; yoksa çukurdan uzun nesne (sokak lambası) delikle birlikte gezer. Sığmayan GENİŞ
            // nesne bu derinliğe inemez (zeminde/rim'de durur) → tetiklenmez, boyut-geçidi korunur.
            bool deepIn = bottomY < groundLevel - tallSwallowDepth;
            bool settledDown = rb.linearVelocity.y > -0.5f;   // aşağı hız bitti → dibe oturdu
            if (overHole && deepIn && settledDown)
            {
                PerformSwallow();
                return;
            }

            // Anti-burial: delik üzerinde DEĞİLKEN ve bir kısmı yer altındaysa yukarı it
            bool submerged = bottomY < groundLevel - buryMargin;
            // Aktif olarak deliğe düşüyorsa anti-burial karışmasın (sadece çökmüş/gömülü nesneye)
            bool descending = rb.linearVelocity.y < -1f;

            if (!overHole && submerged && !descending)
            {
                // Gömülü ALT noktaya uygulanan yukarı + deliğe DOĞRU kuvvet → tork yaratır: tepe rim'e
                // takılıyken gövde delik boşluğuna çekilir, nesne delikten içeri devrilip oradan çıkar
                // (zeminden yukarı fırlamaz — yer iki yönde de geçirimsiz).
                Vector3 toward = new Vector3(-dx, 0f, -dz);
                toward = toward.sqrMagnitude > 0.0001f ? toward.normalized : Vector3.forward;
                Vector3 pushPos = new Vector3(transform.position.x, bottomY, transform.position.z);
                Vector3 force = Vector3.up * risePush + toward * lateralPush;
                rb.AddForceAtPosition(force, pushPos, ForceMode.Acceleration);
            }
        }
    }

    /// <summary>Yutulma işlemi: delik büyür + GameManager'a bildir (skor/kota/bomba) + despawn.</summary>
    void PerformSwallow()
    {
        if (swallowed) return;
        swallowed = true;
        if (hole != null) hole.Grow(growAmount);
        var gm = GameManager.Instance;
        if (gm != null) gm.ReportSwallowed(this, transform.position);
        Destroy(gameObject);
    }
}
