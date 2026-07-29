using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Sprint 6 — Ses & Haptik. GERÇEK ses dosyaları `Assets/Resources/Audio/` altından yüklenir; dosya yoksa
/// RUNTIME'da PROSEDÜREL sentezlenen sese düşülür (fallback → oyun sessiz kalmaz).
///  • Oyun müziği: `Resources/Audio/Music/game_loop1..N` arasından RASTGELE biri, level bitene kadar DÖNGÜ.
///    Menü müziği: `Resources/Audio/Music/world_map`.
///  • Tek-atım sesler (`Resources/Audio/SFX/`): small/medium/big_object_fall (yutma), level_success, level_fail,
///    powerups (tüm güçler), exit_game. Yoksa: bomba/ui prosedürel.
///  • AudioSource HAVUZU (one-shot çakışması) + ayrı MÜZİK kanalı. Ayarlar PlayerPrefs'te kalıcı (SFX/Müzik/Titreşim).
///  • Haptik: Android'de KISA titreşim (VibrationEffect / legacy), editör/masaüstü no-op.
///  • Oto-bootstrap (DontDestroyOnLoad) — sahneye elle eklenmez. Erişim: <c>AudioManager.Instance?.X()</c>.
/// Merkezî tetik: <see cref="GameManager.ReportSwallowed"/>. Müzik: LevelManager.Start / MainMenuController.Start.
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    // ── Ayarlar (PlayerPrefs, kalıcı) ─────────────────────────────────────────
    const string SFX_KEY = "opt_sfx", MUSIC_KEY = "opt_music", HAPTIC_KEY = "opt_haptic";

    public static bool SfxOn
    {
        get => PlayerPrefs.GetInt(SFX_KEY, 1) == 1;
        set { PlayerPrefs.SetInt(SFX_KEY, value ? 1 : 0); PlayerPrefs.Save(); }
    }
    public static bool MusicOn
    {
        get => PlayerPrefs.GetInt(MUSIC_KEY, 1) == 1;
        set { PlayerPrefs.SetInt(MUSIC_KEY, value ? 1 : 0); PlayerPrefs.Save(); Instance?.ApplyMusicState(); }
    }
    public static bool HapticOn
    {
        get => PlayerPrefs.GetInt(HAPTIC_KEY, 1) == 1;
        set { PlayerPrefs.SetInt(HAPTIC_KEY, value ? 1 : 0); PlayerPrefs.Save(); }
    }

    // ── Ses kanalları ─────────────────────────────────────────────────────────
    const int SAMPLE_RATE = 44100;
    const int POOL = 12;             // eşzamanlı SFX başlığı (hızlı yutma serilerine pay)
    AudioSource[] pool;
    int poolIdx;
    AudioSource music;
    float lastSwallowSfx = -1f;      // yutma sesi hız sınırı (voice flood + makineli-tüfek önleme)

    // Klipler (dosya → yoksa prosedürel fallback)
    AudioClip popSmall, popMed, boom, bombFx, uiClick, success, fail;
    AudioClip swSmall, swMed, swBig;   // yutma sesi tier'ları (kullanıcı dosyaları: swallow_small/medium/big)
    AudioClip powerupClip, exitCue, musicLoop;
    AudioClip[] gameLoops;   // oyun içi rastgele döngü müzikleri

    // ── Bootstrap ─────────────────────────────────────────────────────────────
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        if (Instance != null) return;
        var go = new GameObject("AudioManager");
        DontDestroyOnLoad(go);
        go.AddComponent<AudioManager>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        BuildClips();
        BuildSources();
    }

    void BuildSources()
    {
        pool = new AudioSource[POOL];
        for (int i = 0; i < POOL; i++)
        {
            var src = gameObject.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.spatialBlend = 0f;   // 2D
            src.priority = 160;      // SFX düşük öncelik → asla müziği kesmez (voice-steal önceliği düşük)
            pool[i] = src;
        }
        music = gameObject.AddComponent<AudioSource>();
        music.playOnAwake = false;
        music.spatialBlend = 0f;
        music.loop = true;           // arka müzik oyun boyunca KESİNTİSİZ döner
        music.priority = 0;          // EN YÜKSEK öncelik → SFX çokken bile müzik voice'i asla çalınmaz
        music.volume = 0.55f;   // arka müzik seviyesi (biraz kısıldı)
        // Müzik ayrı kanalda; SFX havuzda PlayOneShot ile → aynı anda çalar, biri diğerini durdurmaz.
        // clip: sahne (menü/oyun) PlayGameplayMusic/PlayMenuMusic ile belirler.
    }

    void ApplyMusicState()
    {
        if (music == null) return;
        if (MusicOn && music.clip != null) { if (!music.isPlaying) music.Play(); }
        else music.Stop();
    }

    // ── Genel çalma yardımcısı ────────────────────────────────────────────────
    void Play(AudioClip clip, float volume, float pitch)
    {
        if (!SfxOn || clip == null) return;
        var src = pool[poolIdx];
        poolIdx = (poolIdx + 1) % POOL;
        src.pitch = pitch;
        src.PlayOneShot(clip, volume);
    }

    static float Rand(float a, float b) => UnityEngine.Random.Range(a, b);

    // ── PUBLIC API: SFX ───────────────────────────────────────────────────────

    // Yutma sesi = tek "plop" klibi, boyut KADEMESİNE göre AYRIK perdeden çalınır. Kademe boyut ORANINDAN gelir:
    // her kademe ~%45 daha büyük boyut sınıfı. Aynı sınıftaki nesneler (ör. domates/donut) BİREBİR aynı sesi çıkarır;
    // kademeler arası net/geniş perde farkı. Her level ihtiyacı kadar kademe kullanır (dünyadan bağımsız, oran-tabanlı).
    const float SW_RATIO = 1.45f;                                       // kademe başına boyut oranı (~%45)
    // Kademe → perde MAJÖR PENTATONİK gam (küçük→tiz, büyük→pes). Pentatonikte her nota birbiriyle uyumlu →
    // hızlı oynarken ard arda gelen farklı boyut sesleri FALSO YAPMAZ, melodik/hoş bir akış olur.
    // Oranlar: 6'lı(1.682) / 5'li(1.498) / majör3'lü(1.26) / majör2'li(1.122) / kök(1.0).
    static readonly float[] SW_PITCH = { 1.682f, 1.498f, 1.260f, 1.122f, 1.000f };
    float swRef = 0.4f;   // en küçük nesne referansı (kademe 0)
    bool swRangeReady;

    void EnsureSwallowRange()
    {
        if (swRangeReady) return;
        swRangeReady = true;
        var sizes = new List<float>();
        var all = PhysicsSwallowable.All;
        for (int i = 0; i < all.Count; i++)
        {
            var s = all[i];
            if (s == null || s.isBomb) continue;
            sizes.Add(s.SwallowSize);
        }
        if (sizes.Count == 0) { swRef = 0.4f; return; }
        sizes.Sort();
        swRef = sizes[Mathf.Clamp(Mathf.RoundToInt((sizes.Count - 1) * 0.05f), 0, sizes.Count - 1)];  // ~%5 yüzdelik = en küçük referans
        if (swRef < 0.05f) swRef = 0.05f;
    }

    /// <summary>Yeni level: boyut referansını yeniden ölçtür (AudioManager kalıcı olduğundan eski level'dan taşınmasın).</summary>
    public void ResetSwallowRange() { swRangeReady = false; }

    /// <summary>Nesne yutuldu: boyut KADEMESİNE göre 3 AYRI ses (küçük/orta/büyük). Kademe = round(log(boyut/enKüçük)/
    /// log(%45)) 0..4 → b0,b1=küçük, b2=orta, b3,b4=büyük. Çok hızlı seride ses hız-sınırlanır; haptik her yutmada.</summary>
    public void PlaySwallow(float size)
    {
        EnsureSwallowRange();
        int b = 0;
        if (size > swRef)
            b = Mathf.Clamp(Mathf.RoundToInt(Mathf.Log(size / swRef) / Mathf.Log(SW_RATIO)), 0, SW_PITCH.Length - 1);

        Haptic(b >= 3 ? HapticKind.Medium : HapticKind.Light);        // büyük kademe → daha güçlü haptik

        if (Time.unscaledTime - lastSwallowSfx < 0.025f) return;      // çok yakın → bu sesi atla (his sürekli kalır)
        lastSwallowSfx = Time.unscaledTime;

        // 3 ayrı tier sesi (kullanıcı dosyaları). Aynı kademe = aynı ses; hafif jitter robotik tekrarı kırar.
        AudioClip clip = b <= 1 ? swSmall : (b == 2 ? swMed : swBig);
        Play(clip, 0.6f, Rand(0.99f, 1.01f));
    }

    /// <summary>Güç-up alındı (tüm türler tek ses: powerups). Dosya yoksa prosedürel çan.</summary>
    public void PlayPowerup()
    {
        if (powerupClip != null) Play(powerupClip, 0.8f, Rand(0.99f, 1.02f));
        else Play(MakeBell("sfx_kling", 1180f, 0.55f), 0.7f, 1f);   // fallback
        Haptic(HapticKind.Medium);
    }

    /// <summary>Bomba patlaması (prosedürel) + güçlü haptik.</summary>
    public void PlayBomb() { Play(bombFx, 1f, 1f); Haptic(HapticKind.Heavy); }

    /// <summary>UI buton tık (+ hafif dokunma titreşimi).</summary>
    public void PlayUiClick() { Play(uiClick, 0.6f, Rand(0.98f, 1.02f)); Haptic(HapticKind.Light); }

    /// <summary>Level başarı jingle'ı (müziği durdurur ki net duyulsun).</summary>
    public void PlaySuccess() { StopMusic(); Play(success, 0.9f, 1f); Haptic(HapticKind.Medium); }

    /// <summary>Level başarısız jingle'ı (müziği durdurur).</summary>
    public void PlayFail() { StopMusic(); Play(fail, 0.9f, 1f); Haptic(HapticKind.Heavy); }

    /// <summary>Oyundan çıkış / geri sesi (kısa prosedürel cue).</summary>
    public void PlayExit() { Play(exitCue, 0.7f, 1f); }

    // ── PUBLIC API: MÜZİK ─────────────────────────────────────────────────────
    /// <summary>Klibi müzik kanalına koy ve (MusicOn ise) çal. Aynı klip çalıyorsa dokunmaz.</summary>
    public void PlayMusic(AudioClip clip)
    {
        if (music == null || clip == null) return;
        if (music.clip == clip && music.isPlaying) return;
        music.clip = clip;
        ApplyMusicState();
    }

    /// <summary>Oyun içi müzik: game_loop'lardan RASTGELE biri, level bitene kadar döngü. Yoksa prosedürel.</summary>
    public void PlayGameplayMusic()
    {
        AudioClip clip = (gameLoops != null && gameLoops.Length > 0)
            ? gameLoops[UnityEngine.Random.Range(0, gameLoops.Length)]
            : musicLoop;
        PlayMusic(clip);
    }

    /// <summary>Ana menü / dünya haritası müziği: `world_map`. Yoksa prosedürel.</summary>
    public void PlayMenuMusic()
    {
        var clip = Resources.Load<AudioClip>("Audio/Music/world_map");
        PlayMusic(clip != null ? clip : musicLoop);
    }

    public void StopMusic() { if (music != null) music.Stop(); }

    // ═══════════════════════════════════════════════════════════════════════════
    //  KLİP YÜKLEME (dosya → yoksa prosedürel fallback)
    // ═══════════════════════════════════════════════════════════════════════════
    void BuildClips()
    {
        // Tek-atım sesler: Resources/Audio/SFX/{ad} varsa onu, yoksa prosedürel fallback.
        popSmall = Or("small_object_fall",  MakePop("sfx_pop_s", 620f, 900f, 0.10f));
        popMed   = Or("medium_object_fall", MakePop("sfx_pop_m", 360f, 520f, 0.14f));
        boom     = Or("big_object_fall",    MakeBoom("sfx_boom"));
        // Yutma tier sesleri (kullanıcı dosyaları). Yoksa prosedürel fallback.
        swSmall  = Or("swallow_small",  MakePop("sw_s", 640f, 920f, 0.10f));
        swMed    = Or("swallow_medium", MakePop("sw_m", 380f, 540f, 0.14f));
        swBig    = Or("swallow_big",    MakeBoom("sw_b"));
        success  = Or("level_success",      MakeArpeggio("sfx_win",  new[] { 523.25f, 659.25f, 783.99f, 1046.5f }, 0.11f, false));
        fail     = Or("level_fail",         MakeArpeggio("sfx_lose", new[] { 440f, 349.23f, 261.63f }, 0.16f, true));
        bombFx   = Or("bomb",               MakeExplosion("sfx_bomb"));   // dosya yok → prosedürel
        uiClick  = Or("ui_click",           MakeClick("sfx_ui"));         // dosya yok → prosedürel

        // Güç sesi: dosya varsa yükle (yoksa PlayPowerup fallback çan)
        powerupClip = Resources.Load<AudioClip>("Audio/SFX/powerups");
        // Çıkış sesi: kısa prosedürel "geri" cue'su (yumuşak inen iki ton). İstenirse Resources/Audio/SFX/exit ile ezilir.
        exitCue = Or("exit", MakeArpeggio("sfx_exit", new[] { 523.25f, 349.23f }, 0.08f, false));

        // Müzik: oyun içi rastgele döngüler + prosedürel fallback
        gameLoops = LoadGameLoops();
        musicLoop = MakeMusic("music_loop");
    }

    /// <summary>Resources/Audio/SFX/{name} dosyası varsa onu, yoksa prosedürel fallback klibi döndürür.</summary>
    static AudioClip Or(string name, AudioClip fallback)
    {
        var loaded = Resources.Load<AudioClip>("Audio/SFX/" + name);
        return loaded != null ? loaded : fallback;
    }

    static AudioClip[] LoadGameLoops()
    {
        var list = new List<AudioClip>();
        for (int i = 1; i <= 8; i++)
        {
            var c = Resources.Load<AudioClip>($"Audio/Music/game_loop{i}");
            if (c != null) list.Add(c);
        }
        return list.ToArray();
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  PROSEDÜREL SES SENTEZİ (fallback — dosya yoksa)
    // ═══════════════════════════════════════════════════════════════════════════
    static AudioClip MakeClip(string name, float[] data)
    {
        var clip = AudioClip.Create(name, data.Length, 1, SAMPLE_RATE, false);
        clip.SetData(data, 0);
        return clip;
    }

    static int Samples(float seconds) => Mathf.CeilToInt(seconds * SAMPLE_RATE);

    static AudioClip MakePop(string name, float f0, float f1, float dur)
    {
        int n = Samples(dur);
        var d = new float[n];
        double phase = 0;
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / n;
            float f = Mathf.Lerp(f0, f1, t);
            phase += 2.0 * Math.PI * f / SAMPLE_RATE;
            float env = Mathf.Exp(-6f * t) * (1f - Mathf.Exp(-60f * t));
            d[i] = Mathf.Sin((float)phase) * env * 0.9f;
        }
        return MakeClip(name, d);
    }

    static AudioClip MakeBoom(string name)
    {
        int n = Samples(0.38f);
        var d = new float[n];
        double p1 = 0, p2 = 0;
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / n;
            float f = Mathf.Lerp(150f, 60f, Mathf.Sqrt(t));
            p1 += 2.0 * Math.PI * f / SAMPLE_RATE;
            p2 += 2.0 * Math.PI * (f * 1.5f) / SAMPLE_RATE;
            float env = Mathf.Exp(-5.5f * t) * (1f - Mathf.Exp(-80f * t));
            d[i] = (Mathf.Sin((float)p1) * 0.85f + Mathf.Sin((float)p2) * 0.2f) * env;
        }
        return MakeClip(name, d);
    }

    static AudioClip MakeBell(string name, float baseFreq, float dur)
    {
        int n = Samples(dur);
        var d = new float[n];
        float[] ratios = { 1f, 2.0f, 2.76f, 5.4f };
        float[] amps   = { 1f, 0.55f, 0.35f, 0.18f };
        float[] decays = { 5f, 6f, 8f, 11f };
        var phase = new double[ratios.Length];
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / n;
            float s = 0f;
            for (int k = 0; k < ratios.Length; k++)
            {
                phase[k] += 2.0 * Math.PI * (baseFreq * ratios[k]) / SAMPLE_RATE;
                s += Mathf.Sin((float)phase[k]) * amps[k] * Mathf.Exp(-decays[k] * t);
            }
            d[i] = s * (1f - Mathf.Exp(-120f * t)) * 0.4f;
        }
        return MakeClip(name, d);
    }

    // 2026-07-24: yeniden yazıldı (eski ses ince/vızıltılıydı). Katmanlar: derin sub-bass BOOM (110→32Hz pitch düşüşü),
    // iki-kademeli low-pass GÜMBÜRTÜ kuyruğu (yumuşak, tıslamayan), keskin ilk ÇATLAMA (transient) + sıcak tanh
    // doygunluk (sert kırpma yok). ~0.9s. Gerçek patlama hissi: anlık atak → gövde → uzun sönümlenen rumble.
    static AudioClip MakeExplosion(string name)
    {
        int n = Samples(0.9f);
        var d = new float[n];
        double subPhase = 0;
        float lp1 = 0f, lp2 = 0f;   // iki kademeli low-pass gürültü (derin gümbürtü)
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / n;

            // 1) DERİN BOOM — sub-bass sinüs, 110Hz→32Hz aşağı süpürme (pitch-drop = patlama karakteri).
            float subFreq = Mathf.Lerp(110f, 32f, Mathf.Sqrt(t));
            subPhase += 2.0 * Math.PI * subFreq / SAMPLE_RATE;
            float boom = Mathf.Sin((float)subPhase) * Mathf.Exp(-4.5f * t);

            // 2) GÜMBÜRTÜ — iki kademe low-pass beyaz gürültü (yumuşak, derin), uzun kuyruk.
            float white = Rand(-1f, 1f);
            lp1 = Mathf.Lerp(lp1, white, 0.15f);
            lp2 = Mathf.Lerp(lp2, lp1, 0.35f);
            float rumble = lp2 * Mathf.Exp(-3.0f * t) * (1f - Mathf.Exp(-120f * t));

            // 3) İLK ÇATLAMA — çok kısa keskin transient (vuruş hissi).
            float crack = Rand(-1f, 1f) * Mathf.Exp(-90f * t) * 0.5f;

            float s = boom * 0.9f + rumble * 0.95f + crack * 0.45f;
            d[i] = (float)Math.Tanh(s * 1.5);   // sıcak doygunluk (warm), sert kırpma yok
        }
        return MakeClip(name, d);
    }

    static AudioClip MakeClick(string name)
    {
        int n = Samples(0.045f);
        var d = new float[n];
        double phase = 0;
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / n;
            phase += 2.0 * Math.PI * 1500f / SAMPLE_RATE;
            d[i] = (Mathf.Sin((float)phase) * 0.6f + Rand(-0.25f, 0.25f)) * Mathf.Exp(-45f * t);
        }
        return MakeClip(name, d);
    }

    static AudioClip MakeArpeggio(string name, float[] notes, float noteDur, bool sad)
    {
        int per = Samples(noteDur);
        int n = per * notes.Length;
        var d = new float[n];
        for (int k = 0; k < notes.Length; k++)
        {
            double phase = 0;
            float f = notes[k];
            for (int j = 0; j < per; j++)
            {
                float t = (float)j / per;
                phase += 2.0 * Math.PI * f / SAMPLE_RATE;
                float tri = Mathf.Asin(Mathf.Sin((float)phase)) * (2f / Mathf.PI);
                float env = (1f - Mathf.Exp(-40f * t)) * Mathf.Exp(-3.5f * t);
                float vib = sad ? (1f + 0.02f * Mathf.Sin(t * 40f)) : 1f;
                d[k * per + j] = tri * env * 0.5f * vib;
            }
        }
        return MakeClip(name, d);
    }

    static AudioClip MakeMusic(string name)
    {
        float step = 60f / 96f / 2f;
        float[] seq = { 220f, 261.63f, 329.63f, 392f, 329.63f, 261.63f, 293.66f, 349.23f };
        int per = Samples(step);
        int n = per * seq.Length * 2;
        var d = new float[n];
        for (int bar = 0; bar < 2; bar++)
            for (int k = 0; k < seq.Length; k++)
            {
                double phase = 0, phase2 = 0;
                float f = seq[k];
                int off = (bar * seq.Length + k) * per;
                for (int j = 0; j < per; j++)
                {
                    float t = (float)j / per;
                    phase  += 2.0 * Math.PI * f / SAMPLE_RATE;
                    phase2 += 2.0 * Math.PI * (f * 0.5f) / SAMPLE_RATE;
                    float env = (1f - Mathf.Exp(-25f * t)) * Mathf.Exp(-2.2f * t);
                    d[off + j] += (Mathf.Sin((float)phase) * 0.6f + Mathf.Sin((float)phase2) * 0.35f) * env * 0.5f;
                }
            }
        return MakeClip(name, d);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  HAPTİK
    // ═══════════════════════════════════════════════════════════════════════════
    public enum HapticKind { Light, Medium, Heavy }

    public void Haptic(HapticKind kind)
    {
        if (!HapticOn) return;
        // Süre+genlik cihazda HİSSEDİLİR olmalı: 12/25ms Xiaomi'de hissedilmiyordu → 30/50/90ms + sabit genlik
        // (default -1 bazı cihazlarda çok zayıf).
        long ms; int amp;
        switch (kind)
        {
            case HapticKind.Light:  ms = 30; amp = 160; break;
            case HapticKind.Medium: ms = 50; amp = 220; break;
            default:                ms = 90; amp = 255; break;
        }
        VibrateMs(ms, amp);
    }

    /// <summary>Güçlü, uzun, ÇİFT-darbe titreşim — cihazda haptik testini doğrulamak için (HapticOn'a bakmaz).</summary>
    public void HapticTest()
    {
        VibrateStrongTest();
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    static AndroidJavaObject _vibrator;
    static AndroidJavaObject _vibAttrs;   // AudioAttributes (SONIFICATION) → Android 12+ "usage gating" bypass
    static int _sdk = -1;
    static bool _hasVibrator;         // cihazda titreşim motoru var mı
    static bool _hasAmplitude;        // genlik kontrolü destekleniyor mu (yoksa DEFAULT_AMPLITUDE kullan)
    static bool _diagLogged;          // teşhis logu 1 kez

    static void EnsureVibrator()
    {
        if (_vibrator != null || _sdk == -2) return;
        try
        {
            using var ver = new AndroidJavaClass("android.os.Build$VERSION");
            _sdk = ver.GetStatic<int>("SDK_INT");

            using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            var activity = player.GetStatic<AndroidJavaObject>("currentActivity");

            // Android 12+ (API 31): "vibrator" servisi deprecated → VibratorManager'dan default vibrator al.
            if (_sdk >= 31)
            {
                var mgr = activity.Call<AndroidJavaObject>("getSystemService", "vibrator_manager");
                if (mgr != null) _vibrator = mgr.Call<AndroidJavaObject>("getDefaultVibrator");
            }
            // Eski API veya VibratorManager alınamadıysa: legacy "vibrator" servisi.
            if (_vibrator == null)
                _vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");

            if (_vibrator != null)
            {
                try { _hasVibrator = _vibrator.Call<bool>("hasVibrator"); } catch { _hasVibrator = true; }
                if (_sdk >= 26)
                    try { _hasAmplitude = _vibrator.Call<bool>("hasAmplitudeControl"); } catch { _hasAmplitude = false; }
            }

            // AudioAttributes: USAGE_MEDIA + CONTENT_TYPE_SONIFICATION. Android 12+'ta attribute'suz titreşim
            // "usage=unknown" sayılıp sistemce sessize indirilebiliyor; bu, titreşimi ses efekti gibi sınıflar → çalar.
            if (_sdk >= 26)
            {
                try
                {
                    using var attrClass = new AndroidJavaClass("android.media.AudioAttributes");
                    int usageMedia = attrClass.GetStatic<int>("USAGE_MEDIA");
                    int ctSon = attrClass.GetStatic<int>("CONTENT_TYPE_SONIFICATION");
                    using var builder = new AndroidJavaObject("android.media.AudioAttributes$Builder");
                    builder.Call<AndroidJavaObject>("setUsage", usageMedia).Dispose();
                    builder.Call<AndroidJavaObject>("setContentType", ctSon).Dispose();
                    _vibAttrs = builder.Call<AndroidJavaObject>("build");
                }
                catch (Exception e) { Debug.LogWarning("[AudioManager] AudioAttributes kurulamadı: " + e.Message); }
            }
        }
        catch (Exception e) { Debug.LogWarning("[AudioManager] Vibrator init başarısız: " + e.Message); _sdk = -2; }

        if (!_diagLogged)
        {
            _diagLogged = true;
            Debug.Log($"[AudioManager] Haptik teşhis → sdk={_sdk} vibratorObj={(_vibrator != null)} " +
                      $"hasVibrator={_hasVibrator} hasAmplitude={_hasAmplitude}");
        }
    }

    static void VibrateMs(long ms, int amplitude)
    {
        EnsureVibrator();
        if (_vibrator == null || !_hasVibrator) return;
        try
        {
            if (_sdk >= 26)
            {
                // Genlik kontrolü yoksa DEFAULT_AMPLITUDE (-1) ver; aksi halde bazı cihazlar sessiz kalır.
                int amp = _hasAmplitude ? Mathf.Clamp(amplitude, 1, 255) : -1;
                using var effClass = new AndroidJavaClass("android.os.VibrationEffect");
                var eff = effClass.CallStatic<AndroidJavaObject>("createOneShot", ms, amp);
                VibrateEffect(eff);
            }
            else _vibrator.Call("vibrate", ms);
        }
        catch (Exception e) { Debug.LogWarning("[AudioManager] vibrate başarısız: " + e.Message); }
    }

    // VibrationEffect'i attribute'lu overload'la çal (gating bypass). Attr yoksa düz overload'a düş.
    static void VibrateEffect(AndroidJavaObject eff)
    {
        if (_vibAttrs != null) _vibrator.Call("vibrate", eff, _vibAttrs);
        else _vibrator.Call("vibrate", eff);
    }

    static void VibrateStrongTest()
    {
        EnsureVibrator();
        if (_vibrator == null || !_hasVibrator) return;
        try
        {
            if (_sdk >= 26)
            {
                // Çift darbe, tam genlik: [bekle 0, titret 200ms, dur 120ms, titret 300ms], tekrar yok (-1).
                long[] timings = { 0, 200, 120, 300 };
                int[] amps = { 0, 255, 0, 255 };
                using var effClass = new AndroidJavaClass("android.os.VibrationEffect");
                AndroidJavaObject eff = _hasAmplitude
                    ? effClass.CallStatic<AndroidJavaObject>("createWaveform", timings, amps, -1)
                    : effClass.CallStatic<AndroidJavaObject>("createWaveform", timings, -1);
                VibrateEffect(eff);
                Debug.Log("[AudioManager] HapticTest → güçlü çift-darbe gönderildi.");
            }
            else { _vibrator.Call("vibrate", new long[] { 0, 200, 120, 300 }, -1); }
        }
        catch (Exception e) { Debug.LogWarning("[AudioManager] HapticTest başarısız: " + e.Message); }
    }
#else
    static void VibrateMs(long ms, int amplitude) { /* editör/masaüstü: no-op */ }
    static void VibrateStrongTest() { /* editör/masaüstü: no-op */ }
#endif
}
