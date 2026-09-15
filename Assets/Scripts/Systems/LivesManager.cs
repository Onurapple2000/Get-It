using System;
using UnityEngine;

/// <summary>
/// Kalıcı can ekonomisi (Sprint 2). Canlar PlayerPrefs'te + gerçek zamanla (UTC DateTime) saklanır;
/// uygulama kapalıyken bile **30 dakikada 1 can** yenilenir (offline hesap), max 5. Sahneler arası yaşar
/// (DontDestroyOnLoad) ve her sahnede otomatik kurulur (RuntimeInitializeOnLoadMethod).
///
/// Anahtar fikir: <c>lives</c> + <c>anchor</c> (yenilenmekte olan canın başladığı UTC an). Dolu değilken
/// anchor+30dk = sonraki can zamanı; can verilince anchor 30dk ilerler. Doluyken sayaç durur.
/// </summary>
public class LivesManager : MonoBehaviour
{
    public const int MaxLives = 5;
    // Can yenilenme süresi. Dev/editör'de hızlı (test kolaylığı), RELEASE'de gerçek değer —
    // LevelManager.UnlockedIndex ile aynı desen: dev değerinin yayına sızması imkânsız.
#if DEVELOPMENT_BUILD || UNITY_EDITOR
    public const int RegenSeconds = 30;     // dev/test: 30 sn
#else
    public const int RegenSeconds = 1800;   // release: 30 dk
#endif

    const string KEY_LIVES  = "Lives_Count";
    const string KEY_ANCHOR = "Lives_AnchorTicks";
    const string KEY_UNLIMITED = "Lives_UnlimitedUntilTicks";   // yıldız hediyesi: sınırsız can bitiş UTC

    public static LivesManager Instance { get; private set; }

    int lives;
    long anchorTicks;   // yenilenmekte olan canın başlangıç UTC tick'i (dolu değilken anlamlı)
    long unlimitedUntilTicks;   // bu ana kadar sınırsız can (yıldız hediyesi)

    public int Lives { get { Recalculate(); return lives; } }
    public bool UnlimitedActive => DateTime.UtcNow.Ticks < unlimitedUntilTicks;
    public bool HasLife => UnlimitedActive || Lives > 0;
    public bool IsFull => Lives >= MaxLives;

    /// <summary>Sınırsız can bitişine kalan saniye (aktif değilse 0).</summary>
    public int UnlimitedSecondsLeft
    {
        get { long rem = unlimitedUntilTicks - DateTime.UtcNow.Ticks; return rem <= 0 ? 0 : (int)(rem / TimeSpan.TicksPerSecond); }
    }

    /// <summary>Süreli sınırsız can hediyesi ver (yıldız ödülü). Aktifse üstüne EKLER.</summary>
    public void GrantUnlimited(TimeSpan duration)
    {
        long now = DateTime.UtcNow.Ticks;
        long baseTicks = unlimitedUntilTicks > now ? unlimitedUntilTicks : now;
        unlimitedUntilTicks = baseTicks + duration.Ticks;
        PlayerPrefs.SetString(KEY_UNLIMITED, unlimitedUntilTicks.ToString());
        PlayerPrefs.Save();
        OnChanged?.Invoke();
    }

    /// <summary>Can değişince (yenilendi/harcandı) tetiklenir — UI bağlamak için.</summary>
    public event Action OnChanged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        if (Instance == null)
            new GameObject("LivesManager").AddComponent<LivesManager>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        Load();
    }

    void Load()
    {
        if (!PlayerPrefs.HasKey(KEY_LIVES))
        {
            lives = MaxLives;
            anchorTicks = DateTime.UtcNow.Ticks;
            Save();
            return;
        }
        lives = Mathf.Clamp(PlayerPrefs.GetInt(KEY_LIVES, MaxLives), 0, MaxLives);
        anchorTicks = long.TryParse(PlayerPrefs.GetString(KEY_ANCHOR, ""), out var t)
            ? t : DateTime.UtcNow.Ticks;
        long.TryParse(PlayerPrefs.GetString(KEY_UNLIMITED, ""), out unlimitedUntilTicks);
        Recalculate();
    }

    void Save()
    {
        PlayerPrefs.SetInt(KEY_LIVES, lives);
        PlayerPrefs.SetString(KEY_ANCHOR, anchorTicks.ToString());
        PlayerPrefs.Save();
    }

    /// <summary>Geçen gerçek süreye göre kazanılan canları uygular.</summary>
    void Recalculate()
    {
        if (lives >= MaxLives) return;

        long now = DateTime.UtcNow.Ticks;
        if (now < anchorTicks) { anchorTicks = now; Save(); return; }   // saat geri alınmış → koru

        long perTick = (long)RegenSeconds * TimeSpan.TicksPerSecond;
        int gained = (int)((now - anchorTicks) / perTick);
        if (gained <= 0) return;

        int before = lives;
        lives = Mathf.Min(MaxLives, lives + gained);
        anchorTicks = lives >= MaxLives ? now : anchorTicks + (long)gained * perTick;
        Save();
        if (lives != before) OnChanged?.Invoke();
    }

    /// <summary>Bir can harca (fail). Doluyken çağrılırsa yenilenme sayacı şimdi başlar.</summary>
    public void LoseLife()
    {
        if (UnlimitedActive) return;   // sınırsız can aktif → can eksilmez
        Recalculate();
        if (lives >= MaxLives) anchorTicks = DateTime.UtcNow.Ticks;   // dolu→azalıyor: sayaç başlasın
        lives = Mathf.Max(0, lives - 1);
        Save();
        OnChanged?.Invoke();
    }

    /// <summary>Bir sonraki cana kalan saniye (dolu ise 0).</summary>
    public int SecondsToNextLife()
    {
        Recalculate();
        if (lives >= MaxLives) return 0;
        long now = DateTime.UtcNow.Ticks;
        long target = anchorTicks + (long)RegenSeconds * TimeSpan.TicksPerSecond;
        long rem = target - now;
        return rem <= 0 ? 0 : (int)(rem / TimeSpan.TicksPerSecond);
    }

    /// <summary>mm:ss biçiminde sonraki can geri sayımı.</summary>
    public string NextLifeClock()
    {
        int s = SecondsToNextLife();
        return $"{s / 60:00}:{s % 60:00}";
    }

    /// <summary>(Sprint 8 reklam ödülü vb. için) doğrudan can ekle.</summary>
    public void AddLife(int n = 1)
    {
        Recalculate();
        lives = Mathf.Min(MaxLives, lives + Mathf.Max(0, n));
        Save();
        OnChanged?.Invoke();
    }
}
