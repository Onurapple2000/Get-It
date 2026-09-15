using System;
using UnityEngine;
#if GOOGLE_MOBILE_ADS
using GoogleMobileAds.Api;
#endif

/// <summary>
/// Sprint 8 — AdMob reklam yönetimi. TEK giriş noktası: oyunun geri kalanı yalnız bu facade'ı çağırır,
/// SDK (Google Mobile Ads) namespace'ini asla görmez. Böylece SDK içe aktarılmadan da proje derlenir.
///
/// SDK YOKKEN (define <c>GOOGLE_MOBILE_ADS</c> tanımsız): tüm çağrılar zararsız no-op'tur —
/// ödüllü "yok" sayılır (buton gizlenir), geçiş reklamı anında kapanmış gibi callback'i çağırır,
/// banner hiçbir şey yapmaz. Oyun akışı bozulmaz.
///
/// SDK VARKEN: <c>.unitypackage</c> içe aktarıldıktan sonra Player Settings → Scripting Define Symbols'a
/// <c>GOOGLE_MOBILE_ADS</c> eklenir (Android+iOS). Kod aynen çalışır; şu an TEST reklam id'leri kullanılır.
/// Yayın öncesi (Sprint 9) <see cref="AndroidAppId"/> vb. gerçek id'lerle değiştirilir.
/// </summary>
public class AdManager : MonoBehaviour
{
    public static AdManager Instance { get; private set; }

    // ─────────────────────────────────────────────────────────────────────────
    // Reklam ID'leri — ŞU AN GOOGLE RESMİ TEST ID'LERİ (hesap gerekmez).
    // Yayın öncesi kendi AdMob birim id'lerinizle değiştirin (test → prod).
    // ─────────────────────────────────────────────────────────────────────────
    // 2026-08-23: gerçek AdMob birimleri girildi → PROD. Dev/editör'de hâlâ TEST id'leri kullanılır:
    // gerçek reklamlara kendi tıklamaların "geçersiz trafik" sayılır ve hesabın askıya alınabilir.
#if DEVELOPMENT_BUILD || UNITY_EDITOR
    const bool UseTestIds = true;    // dev/test build → Google'ın test reklamları (güvenli)
#else
    const bool UseTestIds = false;   // release → gerçek reklamlar
#endif

    // Geliştirme kolaylığı: Google'ın TEST "playable" reklamları bu cihazın WebView'ında (TrustedTypePolicy hatası)
    // DONABİLİYOR. Dev/test'te reklamı GERÇEKTEN göstermeden ödülü anında ver / geçişi anında geç → ekonomi akışını
    // donmadan test et. YALNIZ test id'leriyle etkili; prod'a geçince (UseTestIds=false) OTOMATİK kapanır.
    // Gerçek (video) test reklamlarını görmek istersen bunu false yap.
    public static bool DevSimulateAds = false;

    // Test (Google)                              // Prod (AdMob panelinden — Sprint 9'da doldur)
    const string TestRewardedAndroid     = "ca-app-pub-3940256099942544/5224354917";
    const string TestRewardedIOS         = "ca-app-pub-3940256099942544/1712485313";
    const string TestInterstitialAndroid = "ca-app-pub-3940256099942544/1033173712";
    const string TestInterstitialIOS     = "ca-app-pub-3940256099942544/4411468910";
    const string TestBannerAndroid       = "ca-app-pub-3940256099942544/6300978111";
    const string TestBannerIOS           = "ca-app-pub-3940256099942544/2934735716";

    // GERÇEK AdMob birimleri (2026-08-23). Uygulama: GET IT (Android) · App ID ca-app-pub-4966860327538658~8282831570
    // ⚠️ App ID (~) Unity'de "Assets → Google Mobile Ads → Settings"e girilir; aşağıdakiler BİRİM id'leri (/).
    const string ProdRewardedAndroid     = "ca-app-pub-4966860327538658/7200799716";
    const string ProdInterstitialAndroid = "ca-app-pub-4966860327538658/3701422886";
    const string ProdBannerAndroid       = "ca-app-pub-4966860327538658/8402200741";

    // iOS: Apple Developer hesabı açılıp AdMob'a iOS uygulaması eklenince doldurulacak.
    // BOŞ kalırsa iOS'ta reklam gösterilmez (çökme olmaz) — Android yayınını engellemez.
    const string ProdRewardedIOS         = "";
    const string ProdInterstitialIOS     = "";
    const string ProdBannerIOS           = "";

    static string Pick(string testA, string testI, string prodA, string prodI)
    {
#if UNITY_IOS
        return UseTestIds ? testI : prodI;
#else
        return UseTestIds ? testA : prodA;
#endif
    }
    static string RewardedId     => Pick(TestRewardedAndroid, TestRewardedIOS, ProdRewardedAndroid, ProdRewardedIOS);
    static string InterstitialId => Pick(TestInterstitialAndroid, TestInterstitialIOS, ProdInterstitialAndroid, ProdInterstitialIOS);
    static string BannerId       => Pick(TestBannerAndroid, TestBannerIOS, ProdBannerAndroid, ProdBannerIOS);

    // ── Frekans / sıklık (GDD: "reklam baskı kurmaz") ──
    const int InterstitialEveryNLevels = 3;    // her 3 levelda bir
    const float InterstitialMinGapSec  = 90f;  // iki geçiş reklamı arası en az bu kadar saniye
    const string KEY_LEVEL_COUNT = "Ad_LevelCounter";

    float lastInterstitialTime = -999f;
    bool initialized;

    // SDK derlemede mi? (buton göster/gizle için oyunun sorabileceği tek bayrak)
    public static bool SdkPresent
    {
#if GOOGLE_MOBILE_ADS
        get => true;
#else
        get => false;
#endif
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        if (Instance == null)
            new GameObject("AdManager").AddComponent<AdManager>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        Init();
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  Facade — oyunun kullandığı genel API (SDK yokken zararsız)
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>Ödüllü reklam gösterilebilir mi (yüklü + hazır)? Buton göstermek için kullan.</summary>
    public bool RewardedReady
    {
#if GOOGLE_MOBILE_ADS
        get => (UseTestIds && DevSimulateAds) || (rewarded != null && rewarded.CanShowAd());
#else
        get => UseTestIds && DevSimulateAds;
#endif
    }

    /// <summary>
    /// Ödüllü reklam göster. Kullanıcı ödülü hak ederse <paramref name="onReward"/> çağrılır.
    /// Reklam yoksa/hata olursa <paramref name="onUnavailable"/> çağrılır. İkisi de reklam kapandıktan sonra.
    /// </summary>
    public void ShowRewarded(string placement, Action onReward, Action onUnavailable = null)
    {
        if (UseTestIds && DevSimulateAds) { Debug.Log($"[Ads] DevSimulate: ödül anında verildi ({placement})"); onReward?.Invoke(); return; }
#if GOOGLE_MOBILE_ADS
        if (rewarded == null || !rewarded.CanShowAd())
        {
            Debug.Log($"[Ads] Ödüllü hazır değil ({placement}) → yeniden yükle");
            LoadRewarded();
            onUnavailable?.Invoke();
            return;
        }
        bool earned = false;
        Debug.Log($"[Ads] Ödüllü Show çağrıldı ({placement})");
        rewarded.Show(_ => { earned = true; Debug.Log("[Ads] Ödül kazanıldı"); });
        pendingRewarded = () => { if (earned) onReward?.Invoke(); else onUnavailable?.Invoke(); };
#else
        onUnavailable?.Invoke();
#endif
    }

    /// <summary>
    /// Level bittiğinde (kazan/tekrar) çağır. Sayaç <see cref="InterstitialEveryNLevels"/>'e ulaşıp
    /// frekans penceresi uygunsa geçiş reklamını gösterir; her durumda reklam KAPANINCA
    /// <paramref name="onClosed"/> çağrılır (sahne yüklemeyi buna bağla). SDK yoksa anında çağrılır.
    /// </summary>
    public void NotifyLevelEndAndMaybeInterstitial(Action onClosed)
    {
        NotifyLevelEnd();
        if (InterstitialDue()) ShowInterstitial(onClosed);
        else onClosed?.Invoke();
    }

    /// <summary>Her level sonunda çağır — geçiş reklamı sayacını artırır (reklamı göstermez).</summary>
    public void NotifyLevelEnd()
    {
        PlayerPrefs.SetInt(KEY_LEVEL_COUNT, PlayerPrefs.GetInt(KEY_LEVEL_COUNT, 0) + 1);
    }

    /// <summary>Şu an geçiş reklamı gösterilmeli mi (sayaç doldu + frekans ok + reklam hazır)? Coinle-geç teklifi için.</summary>
    public bool InterstitialDue()
    {
#if GOOGLE_MOBILE_ADS
        if (UseTestIds && DevSimulateAds) return false;
        bool gapOk = Time.realtimeSinceStartup - lastInterstitialTime >= InterstitialMinGapSec;
        return PlayerPrefs.GetInt(KEY_LEVEL_COUNT, 0) >= InterstitialEveryNLevels && gapOk && interstitial != null && interstitial.CanShowAd();
#else
        return false;
#endif
    }

    /// <summary>Geçiş reklamını göster; kapanınca <paramref name="onClosed"/>. Hazır değilse anında çağırır.</summary>
    public void ShowInterstitial(Action onClosed)
    {
#if GOOGLE_MOBILE_ADS
        if (interstitial != null && interstitial.CanShowAd())
        {
            MarkInterstitialShown();
            pendingInterstitialClosed = onClosed;
            Debug.Log("[Ads] Geçiş Show çağrıldı");
            interstitial.Show();
            return;
        }
#endif
        onClosed?.Invoke();
    }

    /// <summary>Sayacı sıfırla + frekans zamanını şimdi yap (reklam gösterildi VEYA coinle atlandı sayılır).</summary>
    public void MarkInterstitialShown()
    {
        PlayerPrefs.SetInt(KEY_LEVEL_COUNT, 0);
        lastInterstitialTime = Time.realtimeSinceStartup;
    }

    public void ShowBanner()
    {
#if GOOGLE_MOBILE_ADS
        if (banner == null) LoadBanner();
        banner?.Show();
#endif
    }

    public void HideBanner()
    {
#if GOOGLE_MOBILE_ADS
        banner?.Hide();
#endif
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  SDK implementasyonu (yalnız GOOGLE_MOBILE_ADS tanımlıyken derlenir)
    // ═══════════════════════════════════════════════════════════════════════════
#if GOOGLE_MOBILE_ADS
    RewardedAd rewarded;
    InterstitialAd interstitial;
    BannerView banner;
    Action pendingRewarded;              // reklam kapanınca (ana thread'de) çalışacak ödül-sonucu
    Action pendingInterstitialClosed;    // geçiş reklamı kapanınca

    void Init()
    {
        if (initialized) return;
        initialized = true;
        // KRİTİK: reklam event'leri Unity ANA THREAD'inde tetiklensin. Yoksa OnAdFullScreenContentClosed
        // arka thread'de çalışır → SceneManager.LoadScene / UI (SetActive, AddLife) sessizce çalışmaz,
        // reklam kapansa da oyun DONUK kalır ("kapatma/geri tuşu çıkmadı" belirtisi). 2026-08-17 cihaz testi.
        MobileAds.RaiseAdEventsOnUnityMainThread = true;

        // GELİŞTİRİCİ TEST CİHAZLARI (2026-09-15): bu cihazlarda GERÇEK birim id'leriyle bile Google TEST reklamı
        // gösterilir → kendi telefonumuzdan gerçek gösterim/tıklama gitmez (geçersiz trafik = AdMob hesabı kapanır).
        // Karma id'yi SDK logcat'te söyler: "Use RequestConfiguration...setTestDeviceIds(..."XXXX")".
        // AdMob konsolundaki GAID kaydı bu cihazda TANINMADI; SDK'nın beklediği karma id budur. Yeni cihaz → listeye ekle.
        MobileAds.SetRequestConfiguration(new RequestConfiguration
        {
            TestDeviceIds = new System.Collections.Generic.List<string>
            {
                "E50776C1C8E78B8153E50205399CCF0D",   // Onur — Xiaomi 2407FPN8EG (rothko)
            }
        });

        // NOT (Sprint 9): UMP/GDPR consent akışı burada çağrılmalı. Google UMP paketi
        // (GoogleMobileAds.Ump.Api → ConsentInformation.Update + ConsentForm.LoadAndShow)
        // AdMob panelinde "Privacy & messaging" mesajı oluşturulunca aktif edilir; şimdilik
        // test id'leriyle doğrudan init yeterli. iOS ATT izni Info.plist'e eklendi (aşağı bkz.).
        MobileAds.Initialize(_ =>
        {
            Debug.Log("[Ads] MobileAds init tamam → reklamları yükle");
            LoadRewarded();
            LoadInterstitial();
            LoadBanner();
        });
    }

    AdRequest NewRequest() => new AdRequest();

    void LoadRewarded()
    {
        rewarded?.Destroy(); rewarded = null;
        RewardedAd.Load(RewardedId, NewRequest(), (ad, err) =>
        {
            if (err != null || ad == null) { Debug.LogWarning($"[Ads] Ödüllü yüklenemedi: {err}"); return; }
            rewarded = ad;
            Debug.Log("[Ads] Ödüllü YÜKLENDİ");
            ad.OnAdFullScreenContentOpened += () => Debug.Log("[Ads] Ödüllü AÇILDI (ekranda)");
            ad.OnAdImpressionRecorded += () => Debug.Log("[Ads] Ödüllü impression");
            ad.OnAdFullScreenContentClosed += () => { Debug.Log("[Ads] Ödüllü KAPANDI"); var p = pendingRewarded; pendingRewarded = null; p?.Invoke(); LoadRewarded(); };
            ad.OnAdFullScreenContentFailed += e => { Debug.LogWarning($"[Ads] Ödüllü gösterim hatası: {e}"); var p = pendingRewarded; pendingRewarded = null; p?.Invoke(); LoadRewarded(); };
        });
    }

    void LoadInterstitial()
    {
        interstitial?.Destroy(); interstitial = null;
        InterstitialAd.Load(InterstitialId, NewRequest(), (ad, err) =>
        {
            if (err != null || ad == null) { Debug.LogWarning($"[Ads] Geçiş yüklenemedi: {err}"); return; }
            interstitial = ad;
            Debug.Log("[Ads] Geçiş YÜKLENDİ");
            ad.OnAdFullScreenContentOpened += () => Debug.Log("[Ads] Geçiş AÇILDI (ekranda)");
            ad.OnAdImpressionRecorded += () => Debug.Log("[Ads] Geçiş impression");
            ad.OnAdFullScreenContentClosed += () => { Debug.Log("[Ads] Geçiş KAPANDI"); var p = pendingInterstitialClosed; pendingInterstitialClosed = null; p?.Invoke(); LoadInterstitial(); };
            ad.OnAdFullScreenContentFailed += e => { Debug.LogWarning($"[Ads] Geçiş gösterim hatası: {e}"); var p = pendingInterstitialClosed; pendingInterstitialClosed = null; p?.Invoke(); LoadInterstitial(); };
        });
    }

    void LoadBanner()
    {
        banner?.Destroy();
        banner = new BannerView(BannerId, AdSize.Banner, AdPosition.Bottom);
        banner.LoadAd(NewRequest());
    }
#else
    void Init() { initialized = true; }   // SDK yok → sessiz
#endif
}
