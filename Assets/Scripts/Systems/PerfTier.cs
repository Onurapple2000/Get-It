using System.Collections;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// PERFORMANS KADEMESİ (2026-09-16 — Redmi Note 8'de "oynanamaz takılma" raporu üzerine).
/// İki kademe: High (mevcut ayarlar, 14T Pro sınıfı) / Low (zayıf cihaz). Low'da:
///   • URP: Resources/Mobile_Low_RPAsset (gölge kapalı, HDR kapalı, renderScale 0.7, MSAA yok)
///   • 30 fps sabit + fizik 30 Hz (fixedDeltaTime 1/30) + çözücü iterasyonu 4 → CPU yükü ~yarı
/// Karar sırası: oyuncu tercihi (Ayarlar toggle: AÇIK = Low zorla, KAPALI = High zorla) → otomatik cihaz sezgisi (RAM ≤ 4 GB
/// ya da eski GPU ailesi) → oyun içi FPS bekçisi (otomatik modda, 10 sn ortalama < 20 fps → Low'a düş ve KALICI yap).
/// PlayerPrefs: perf_user (0 otomatik, 1 Low zorla, 2 High zorla), perf_auto (1 = bekçi Low'a düşürdü).
/// </summary>
public static class PerfTier
{
    public enum Tier { High, Low }
    public static Tier Current { get; private set; } = Tier.High;
    public static string Reason { get; private set; } = "-";

    const string KeyUser = "perf_user", KeyAuto = "perf_auto";
    static UniversalRenderPipelineAsset highAsset;   // açılıştaki (Quality) asset — geri dönüş için

    /// <summary>
    /// Ayarlar toggle'ı. Getter = GERÇEK durum (otomatik Low'daysa AÇIK görünür). Setter: AÇIK → Low zorla (perf_user=1);
    /// KAPALI → High zorla (perf_user=2) + bekçi kaydı silinir → sezgi/bekçi bir daha devreye girmez (oyuncunun kararı üstün).
    /// </summary>
    public static bool UserLow
    {
        get => Current == Tier.Low;
        set
        {
            PlayerPrefs.SetInt(KeyUser, value ? 1 : 2);
            if (!value) PlayerPrefs.DeleteKey(KeyAuto);
            PlayerPrefs.Save(); Apply(Decide());
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Init()
    {
        highAsset = QualitySettings.renderPipeline as UniversalRenderPipelineAsset
                    ?? GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
        Apply(Decide());
        var go = new GameObject("_PerfTier"); Object.DontDestroyOnLoad(go); go.AddComponent<Watchdog>();
    }

    static Tier Decide()
    {
        int user = PlayerPrefs.GetInt(KeyUser, 0);   // 0 otomatik, 1 Low zorla, 2 High zorla
        if (user == 1) { Reason = "kullanıcı: düşük"; return Tier.Low; }
        if (user == 2) { Reason = "kullanıcı: yüksek"; return Tier.High; }
        if (PlayerPrefs.GetInt(KeyAuto, 0) == 1) { Reason = "fps bekçisi (kalıcı)"; return Tier.Low; }
        if (LooksWeak(out var why)) { Reason = why; return Tier.Low; }
        Reason = "cihaz güçlü"; return Tier.High;
    }

    // Zayıf cihaz sezgisi: 4 GB ve altı RAM  VEYA  eski GPU aileleri (2019 öncesi/giriş segmenti).
    // ⚠️ \b şart: "Mali-G7[0-2]" deseni "Mali-G720"nin başını da yakalıyordu → 14T Pro zayıf sanıldı (2026-09-16).
    static readonly Regex WeakGpu = new Regex(
        @"Adreno \(TM\) (3\d\d|4\d\d|5\d\d|60\d|61\d)\b|Mali-(4\d\d|T\d\d\d|G3\d|G5[0-2]|G7[0-2])\b|Mali-G57 MC1\b|PowerVR|Intel", RegexOptions.IgnoreCase);
    static bool LooksWeak(out string why)
    {
        int ram = SystemInfo.systemMemorySize;   // MB
        string gpu = SystemInfo.graphicsDeviceName ?? "";
        if (ram > 0 && ram <= 4200) { why = $"RAM {ram} MB"; return true; }
        if (IsWeakGpuName(gpu)) { why = "GPU " + gpu; return true; }
        why = null; return false;
    }
    /// <summary>Test edilebilir GPU adı sezgisi.</summary>
    public static bool IsWeakGpuName(string gpu) => !string.IsNullOrEmpty(gpu) && WeakGpu.IsMatch(gpu);

    static void Apply(Tier t)
    {
        Current = t;
        if (t == Tier.Low)
        {
            var low = Resources.Load<UniversalRenderPipelineAsset>("Mobile_Low_RPAsset");
            if (low != null) QualitySettings.renderPipeline = low;
            Application.targetFrameRate = 30;
            Time.fixedDeltaTime = 1f / 30f;
            Physics.defaultSolverIterations = 4;
        }
        else
        {
            if (highAsset != null) QualitySettings.renderPipeline = highAsset;
            Application.targetFrameRate = 60;
            Time.fixedDeltaTime = 0.02f;
            Physics.defaultSolverIterations = 6;
        }
        Debug.Log($"[PerfTier] {t} ({Reason}) — GPU: {SystemInfo.graphicsDeviceName}, RAM: {SystemInfo.systemMemorySize} MB, API: {SystemInfo.graphicsDeviceType}");
    }

    /// <summary>
    /// Oyun sahnesinde FPS bekçisi (yalnız OTOMATİK moddayken): level başladıktan 3 sn sonra, 10 sn ortalama &lt; 20 fps
    /// → Low (kalıcı, perf_auto). Kullanıcı elle seçim yaptıysa (perf_user≠0) hiç karışmaz.
    /// </summary>
    class Watchdog : MonoBehaviour
    {
        float accum; int frames; float window, warm; LevelManager seen;
        void Update()
        {
            var lm = LevelManager.Instance;
            if (Current == Tier.Low || lm == null || PlayerPrefs.GetInt(KeyUser, 0) != 0) { accum = 0; frames = 0; window = 0; warm = 0; seen = null; return; }
            if (seen != lm) { seen = lm; accum = 0; frames = 0; window = 0; warm = 0; }   // yeni level → ısınma sıfırla
            float dt = Time.unscaledDeltaTime;
            if (dt > 0.5f) return;                         // yükleme/duraklama sıçramalarını sayma
            warm += dt; if (warm < 3f) return;             // spawn + shader ısınması ölçüme girmesin
            accum += dt; frames++; window += dt;
            if (window < 10f) return;
            float fps = frames / accum;
            accum = 0; frames = 0; window = 0;
            if (fps < 20f)
            {
                PlayerPrefs.SetInt(KeyAuto, 1); PlayerPrefs.Save();
                Reason = $"fps bekçisi ({fps:F0} fps)";
                Apply(Tier.Low);
            }
        }
    }
}
