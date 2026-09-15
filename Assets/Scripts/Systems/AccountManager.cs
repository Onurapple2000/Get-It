using System;
using System.Threading.Tasks;
using UnityEngine;
using Unity.Services.Core;
using Unity.Services.Authentication;
#if UNITY_ANDROID && GPGS_ENABLED
using GooglePlayGames;
using GooglePlayGames.BasicApi;
#endif

/// <summary>
/// Sprint 10 — Kalıcı hesap kimliği (UGS Authentication). İlk açılışta arka planda **anonim** giriş yapar
/// (oyuncu login ekranı GÖRMEZ). Bu kimlik altında ilerleme buluta (Cloud Save) yazılır. İstenince/satın
/// almada Google Play Games / Apple hesabına <b>link</b> edilerek cihaz-ötesi taşınır (Adım 3).
///
/// ÇEVRİMDIŞI DAYANIKLILIĞI: init/giriş başarısız olursa (internet yok vb.) oyun YEREL cache ile normal
/// çalışmaya devam eder; sadece bulut senkronu o oturumda yapılmaz. Bağlanınca <see cref="OnSignedIn"/>
/// tetiklenir → CloudSyncService (Adım 2) buradan yükleme/birleştirme yapar.
///
/// Diğer manager'lar gibi otomatik kurulur (RuntimeInitializeOnLoadMethod) + sahneler arası yaşar.
/// </summary>
public class AccountManager : MonoBehaviour
{
    public static AccountManager Instance { get; private set; }

    /// <summary>Anonim veya bağlı giriş başarılı mı (bu oturumda buluta erişilebilir mi)?</summary>
    public bool IsSignedIn { get; private set; }
    /// <summary>UGS oyuncu kimliği (giriş sonrası). Bulut kaydı bu kimliğe bağlıdır.</summary>
    public string PlayerId { get; private set; }
    /// <summary>Giriş tamamlanınca tetiklenir — CloudSyncService buluttan yükleme/birleştirmeyi buna bağlar.</summary>
    public event Action OnSignedIn;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        if (Instance == null)
            new GameObject("AccountManager").AddComponent<AccountManager>();
    }

    async void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        await InitAndSignIn();
    }

    async Task InitAndSignIn()
    {
        try
        {
            if (UnityServices.State != ServicesInitializationState.Initialized)
                await UnityServices.InitializeAsync();

            if (!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync();

            IsSignedIn = true;
            PlayerId = AuthenticationService.Instance.PlayerId;
            RefreshLinkState();
            Debug.Log($"[Account] Anonim giriş tamam. PlayerId={PlayerId} (linked={IsLinked})");
            OnSignedIn?.Invoke();
#if UNITY_ANDROID && GPGS_ENABLED
            if (!IsLinked) StartGooglePlayGamesLink(false);   // açılışta SESSİZ otomatik bağla/restore (oyuncu Play Games'e girişliyse)
#endif
        }
        catch (Exception e)
        {
            IsSignedIn = false;
            Debug.LogWarning($"[Account] UGS başlatma/giriş başarısız (çevrimdışı olabilir): {e.Message}. Yerel cache ile devam.");
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  ADIM 3 — Hesap bağlama (Google Play Games / Apple)
    //  Anonim kimliği harici hesaba LINK eder → uninstall/cihaz değişiminde ilerleme taşınır.
    //  UGS link çağrıları burada; provider token'ı ilgili platform SDK'sından gelmeli (aşağıda TODO).
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>Kimlik harici bir hesaba bağlı mı (anonim değil)? Bağlıysa uninstall/cihaz değişimini aşar.</summary>
    public bool IsLinked { get; private set; }
    /// <summary>Bağlama başarılı olunca tetiklenir — UI'da "bağlı" durumunu göstermek için.</summary>
    public event Action OnAccountLinked;

    void RefreshLinkState()
    {
        try
        {
            var ids = AuthenticationService.Instance.PlayerInfo?.Identities;
            IsLinked = ids != null && ids.Count > 0;
        }
        catch { IsLinked = false; }
    }

    /// <summary>Google Play Games server auth code ile bağla (Android). authCode = GPGS plugin'inden alınır.</summary>
    public async Task<bool> LinkGooglePlayGamesAsync(string authCode)
    {
        try
        {
            await AuthenticationService.Instance.LinkWithGooglePlayGamesAsync(authCode);
            RefreshLinkState();
            Debug.Log("[Account] Google Play Games bağlandı.");
            OnAccountLinked?.Invoke();
            return true;
        }
        catch (Exception e) { Debug.LogWarning($"[Account] Google link başarısız: {e.Message}"); return false; }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  HESAP + VERİ SİLME (Google Play zorunluluğu — "OAuth ile hesap oluşturma" beyan edildiği için
    //  uygulama içi silme yolu ŞART; web karşılığı: onurapple2000.github.io/delete-account.html)
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Bulut kaydını + UGS hesabını + yerel veriyi siler, ardından temiz bir anonim oturum açar.
    /// SIRA ÖNEMLİ: hesap silinince buluta erişilemez → önce bulut verisi, sonra hesap.
    /// </summary>
    public async Task<bool> DeleteAccountAndDataAsync()
    {
        if (!IsSignedIn) { Debug.LogWarning("[Account] Giriş yok — silme yapılamaz."); return false; }
        try
        {
            // 1) Bulut kaydını sil (hesap hâlâ geçerliyken)
            try { await Unity.Services.CloudSave.CloudSaveService.Instance.Data.Player.DeleteAllAsync(); }
            catch (Exception e) { Debug.LogWarning($"[Account] Bulut verisi silinemedi: {e.Message}"); }

            // 2) UGS hesabını sil (bu işlem oturumu da kapatır)
            await AuthenticationService.Instance.DeleteAccountAsync();
            IsSignedIn = false; IsLinked = false; PlayerId = null;

            // 3) Yerel veriyi temizle (ilerleme + ayarlar + isim)
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();

            Debug.Log("[Account] Hesap + bulut + yerel veri silindi.");

            // 4) Temiz başlangıç: yeni anonim kimlik (oyun çalışmaya devam etsin)
            await InitAndSignIn();
            return true;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Account] Hesap silme başarısız: {e.Message}");
            return false;
        }
    }

    /// <summary>Apple idToken ile bağla (iOS). idToken = Sign in with Apple akışından alınır.</summary>
    public async Task<bool> LinkAppleAsync(string idToken)
    {
        try
        {
            await AuthenticationService.Instance.LinkWithAppleAsync(idToken);
            RefreshLinkState();
            Debug.Log("[Account] Apple bağlandı.");
            OnAccountLinked?.Invoke();
            return true;
        }
        catch (Exception e) { Debug.LogWarning($"[Account] Apple link başarısız: {e.Message}"); return false; }
    }

    /// <summary>
    /// Ayarlar'daki "İlerlemeyi Kaydet / Hesaba Bağla" butonunun çağıracağı platform giriş akışı.
    /// Android → Google Play Games (RequestServerSideAccess → authCode) → UGS link; hesap zaten bağlıysa
    ///           mevcut hesaba giriş (silip-kurunca eski ilerleme geri gelir).
    /// iOS     → Sign in with Apple → idToken → LinkAppleAsync (henüz kurulmadı).
    /// </summary>
    public void LinkCurrentPlatform()
    {
        if (!IsSignedIn) { Debug.LogWarning("[Account] Giriş yok — link atlandı."); return; }
        if (IsLinked)    { Debug.Log("[Account] Zaten bağlı."); return; }
#if UNITY_ANDROID && GPGS_ENABLED
        StartGooglePlayGamesLink(true);   // buton → giriş ekranını GÖSTER (ManuallyAuthenticate)
#elif UNITY_ANDROID
        Debug.LogWarning("[Account] Android link: GPGS plugin veya GPGS_ENABLED define eksik (Adım 3 kurulumu tamamlanmadı).");
#elif UNITY_IOS
        Debug.LogWarning("[Account] iOS link: Sign in with Apple henüz kurulmadı (Adım 3 devam).");
#else
        Debug.LogWarning("[Account] Bu platformda hesap bağlama desteklenmiyor.");
#endif
    }

#if UNITY_ANDROID && GPGS_ENABLED
    bool _gpgsActivated;

    /// <summary>Play Games'e giriş → server auth code → önce LINK; "zaten bağlı" ise mevcut hesaba SIGN-IN (eski bulut kaydı geri gelir).</summary>
    // interactive=true (buton): giriş ekranını GÖSTER (ManuallyAuthenticate).
    // interactive=false (açılış): SESSİZ dene (Authenticate) — girişli değilse nag yapma.
    async void StartGooglePlayGamesLink(bool interactive)
    {
        if (!_gpgsActivated) { PlayGamesPlatform.Activate(); _gpgsActivated = true; }

        // 1) Play Games girişi
        var signIn = new TaskCompletionSource<SignInStatus>();
        Action<SignInStatus> onAuth = status => signIn.TrySetResult(status);
        if (interactive) PlayGamesPlatform.Instance.ManuallyAuthenticate(onAuth);
        else            PlayGamesPlatform.Instance.Authenticate(onAuth);
        var signInStatus = await signIn.Task;
        if (signInStatus != SignInStatus.Success)
        {
            string mode = interactive ? "manual" : "silent";
            Debug.LogWarning($"[Account] Play Games giriş yok/iptal ({mode}): {signInStatus}");
            return;
        }

        // 2) LINK dene (anonim ilerlemeyi Google hesabına bağla)
        var code = await RequestServerAuthCodeAsync();
        if (string.IsNullOrEmpty(code)) { Debug.LogWarning("[Account] authCode boş geldi."); return; }
        try
        {
            await AuthenticationService.Instance.LinkWithGooglePlayGamesAsync(code);
            RefreshLinkState();
            Debug.Log("[Account] Google Play Games bağlandı (link).");
            OnAccountLinked?.Invoke();
            return;
        }
        catch (AuthenticationException e) when (e.ErrorCode == AuthenticationErrorCodes.AccountAlreadyLinked)
        {
            Debug.Log("[Account] Bu Google hesabı zaten bir UGS hesabına bağlı → mevcut hesaba giriş yapılıyor (ilerleme geri gelecek).");
        }
        catch (Exception e) { Debug.LogWarning($"[Account] Google link başarısız: {e.Message}"); return; }

        // 3) Zaten bağlı → anonim oturumu KAPAT, sonra o hesaba GİRİŞ.
        //    (UGS: girişliyken SignIn atılamaz → SignOut şart. Yerel PlayerPrefs bundan etkilenmez;
        //     giriş sonrası PullMergePush eski bulut kaydını geri getirir.)
        AuthenticationService.Instance.SignOut();
        var code2 = await RequestServerAuthCodeAsync();
        if (string.IsNullOrEmpty(code2)) { Debug.LogWarning("[Account] authCode(2) boş geldi."); return; }
        try
        {
            await AuthenticationService.Instance.SignInWithGooglePlayGamesAsync(code2);
            IsSignedIn = true;
            PlayerId = AuthenticationService.Instance.PlayerId;
            RefreshLinkState();
            Debug.Log($"[Account] Google Play Games ile giriş (mevcut hesap). PlayerId={PlayerId}");
            OnAccountLinked?.Invoke();
            OnSignedIn?.Invoke();   // CloudSync yeniden pull etsin → eski ilerleme geri
        }
        catch (Exception e) { Debug.LogWarning($"[Account] Google sign-in başarısız: {e.Message}"); }
    }

    /// <summary>GPGS RequestServerSideAccess'i await edilebilir yapar (tek kullanımlık server auth code).</summary>
    Task<string> RequestServerAuthCodeAsync()
    {
        var tcs = new TaskCompletionSource<string>();
        PlayGamesPlatform.Instance.RequestServerSideAccess(true, code => tcs.TrySetResult(code));
        return tcs.Task;
    }
#endif
}
