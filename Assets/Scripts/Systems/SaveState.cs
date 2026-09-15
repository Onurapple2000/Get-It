using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Sprint 10 — Bulut senkronu için tüm kalıcı ilerlemenin tek serileştirilebilir aynası.
/// PlayerPrefs YEREL doğruluk kaynağı kalır; bu sınıf yalnız bilinen anahtarları toplayıp (Capture)
/// buluta yazılabilir JSON'a çevirir ve buluttan gelince geri uygular (Apply). Çakışmada <see cref="Merge"/>
/// ilerlemeyi KAYBETMEYECEK şekilde birleştirir (max kazanır; noAds OR).
///
/// JsonUtility Dictionary desteklemez → tüm harita alanları <see cref="IntKV"/> listeleriyle tutulur.
/// </summary>
[Serializable]
public class SaveState
{
    [Serializable]
    public struct IntKV { public string k; public int v; public IntKV(string k, int v) { this.k = k; this.v = v; } }

    public int schema = 1;
    public long stampTicks;   // son güncelleme UTC (bilgi amaçlı)

    // Profil
    public string name = "";
    public bool nameChosen;
    public int lang;          // Loc dili (Language enum int)
    public bool langChosen;   // dil seçildi mi (reinstall'da ilk-açılış dil ekranı bir daha çıkmasın)
    public int coins;
    public int totalScore;
    public int earnedStars;   // birikimli ödül yıldızı
    public bool noAds;

    // Harita alanları
    public List<IntKV> stars = new();       // "W{w}_L{l}"  → en iyi yıldız
    public List<IntKV> unlocks = new();      // "W{w}"        → açılmış level index
    public List<IntKV> powerups = new();     // PowerUpType   → envanter adedi
    public List<IntKV> starGifts = new();    // PowerUpType   → verilmiş yıldız-hediye adedi

    // Canlar
    public int livesCount = LivesManager.MaxLives;
    public string livesAnchor = "";
    public string livesUnlimited = "";

    static readonly PowerUpType[] PU = { PowerUpType.Speed, PowerUpType.Magnet, PowerUpType.SizeBurst, PowerUpType.Super };

    // ── Anahtar üreticileri (mevcut sistemlerle birebir aynı olmalı) ──
    static string StarKey(int w, int l) => $"Stars_W{w}_L{l}";
    static string UnlockKey(int w)      => $"World{w}_Unlocked";
    static string PuKey(PowerUpType t)  => $"PowerInv_{t}";
    static string GiftKey(PowerUpType t)=> $"StarGift_{t}_Granted";

    // ═══ YEREL → SaveState ═══
    public static SaveState CaptureLocal()
    {
        var s = new SaveState
        {
            stampTicks = DateTime.UtcNow.Ticks,
            name = PlayerPrefs.GetString("PlayerName", ""),
            nameChosen = PlayerPrefs.GetInt("PlayerNameChosen", 0) == 1,
            lang = PlayerPrefs.GetInt("Language", 0),
            langChosen = PlayerPrefs.GetInt("LanguageChosen", 0) == 1,
            coins = PlayerPrefs.GetInt("Coins", 0),
            totalScore = PlayerPrefs.GetInt("TotalScore", 0),
            earnedStars = PlayerPrefs.GetInt("EarnedStars", 0),
            noAds = PlayerPrefs.GetInt("NoAds", 0) == 1,
            livesCount = PlayerPrefs.GetInt("Lives_Count", LivesManager.MaxLives),
            livesAnchor = PlayerPrefs.GetString("Lives_AnchorTicks", ""),
            livesUnlimited = PlayerPrefs.GetString("Lives_UnlimitedUntilTicks", ""),
        };

        for (int w = 0; w < WorldCatalog.Count; w++)
        {
            int u = PlayerPrefs.GetInt(UnlockKey(w), 0);
            if (u > 0) s.unlocks.Add(new IntKV($"W{w}", u));
            int n = WorldCatalog.PlayableLevels(w);
            for (int l = 0; l < n; l++)
            {
                int v = PlayerPrefs.GetInt(StarKey(w, l), 0);
                if (v > 0) s.stars.Add(new IntKV($"W{w}_L{l}", v));
            }
        }
        foreach (var t in PU)
        {
            int c = PlayerPrefs.GetInt(PuKey(t), 0);
            if (c > 0) s.powerups.Add(new IntKV(t.ToString(), c));
            int g = PlayerPrefs.GetInt(GiftKey(t), 0);
            if (g > 0) s.starGifts.Add(new IntKV(t.ToString(), g));
        }
        return s;
    }

    // ═══ SaveState → YEREL ═══
    public void ApplyLocal()
    {
        PlayerPrefs.SetString("PlayerName", name ?? "");
        PlayerPrefs.SetInt("PlayerNameChosen", nameChosen ? 1 : 0);
        if (langChosen) { PlayerPrefs.SetInt("Language", lang); PlayerPrefs.SetInt("LanguageChosen", 1); }
        PlayerPrefs.SetInt("Coins", Mathf.Max(0, coins));
        PlayerPrefs.SetInt("TotalScore", Mathf.Max(0, totalScore));
        PlayerPrefs.SetInt("EarnedStars", Mathf.Max(0, earnedStars));
        PlayerPrefs.SetInt("NoAds", noAds ? 1 : 0);
        PlayerPrefs.SetInt("Lives_Count", Mathf.Clamp(livesCount, 0, LivesManager.MaxLives));
        if (!string.IsNullOrEmpty(livesAnchor)) PlayerPrefs.SetString("Lives_AnchorTicks", livesAnchor);
        if (!string.IsNullOrEmpty(livesUnlimited)) PlayerPrefs.SetString("Lives_UnlimitedUntilTicks", livesUnlimited);

        foreach (var kv in unlocks)
        {
            if (int.TryParse(kv.k.Substring(1), out int w))   // "W{w}"
                PlayerPrefs.SetInt(UnlockKey(w), kv.v);
        }
        foreach (var kv in stars)
            PlayerPrefs.SetInt("Stars_" + kv.k, kv.v);        // kv.k = "W{w}_L{l}" → "Stars_W{w}_L{l}"
        foreach (var kv in powerups)
            if (Enum.TryParse(kv.k, out PowerUpType t)) PlayerPrefs.SetInt(PuKey(t), kv.v);
        foreach (var kv in starGifts)
            if (Enum.TryParse(kv.k, out PowerUpType t)) PlayerPrefs.SetInt(GiftKey(t), kv.v);

        PlayerPrefs.Save();
    }

    // ═══ Çakışma birleştirme (ilerleme kaybetme: max kazanır) ═══
    public static SaveState Merge(SaveState local, SaveState cloud)
    {
        if (cloud == null) return local;
        if (local == null) return cloud;

        var m = new SaveState
        {
            stampTicks = DateTime.UtcNow.Ticks,
            // isim: hangisi seçilmişse o; ikisi de seçiliyse en son güncellenen
            nameChosen = local.nameChosen || cloud.nameChosen,
            name = local.nameChosen ? local.name : (cloud.nameChosen ? cloud.name : local.name),
            langChosen = local.langChosen || cloud.langChosen,
            lang = local.langChosen ? local.lang : (cloud.langChosen ? cloud.lang : local.lang),
            coins = Mathf.Max(local.coins, cloud.coins),           // oyuncu-lehine (tüketilebilir; log yok)
            totalScore = Mathf.Max(local.totalScore, cloud.totalScore),
            earnedStars = Mathf.Max(local.earnedStars, cloud.earnedStars),
            noAds = local.noAds || cloud.noAds,                    // bir kez alındıysa hep açık
            livesCount = Mathf.Max(local.livesCount, cloud.livesCount),
            livesAnchor = !string.IsNullOrEmpty(local.livesAnchor) ? local.livesAnchor : cloud.livesAnchor,
            livesUnlimited = MaxTickString(local.livesUnlimited, cloud.livesUnlimited),
        };
        m.stars     = MergeMax(local.stars, cloud.stars);
        m.unlocks   = MergeMax(local.unlocks, cloud.unlocks);
        m.powerups  = MergeMax(local.powerups, cloud.powerups);
        m.starGifts = MergeMax(local.starGifts, cloud.starGifts);
        return m;
    }

    static List<IntKV> MergeMax(List<IntKV> a, List<IntKV> b)
    {
        var d = new Dictionary<string, int>();
        foreach (var kv in a) d[kv.k] = kv.v;
        foreach (var kv in b) d[kv.k] = d.TryGetValue(kv.k, out var cur) ? Mathf.Max(cur, kv.v) : kv.v;
        var outp = new List<IntKV>(d.Count);
        foreach (var p in d) outp.Add(new IntKV(p.Key, p.Value));
        return outp;
    }

    static string MaxTickString(string a, string b)
    {
        long.TryParse(a, out long la); long.TryParse(b, out long lb);
        long m = Math.Max(la, lb);
        return m > 0 ? m.ToString() : "";
    }

    public string ToJson() => JsonUtility.ToJson(this);
    public static SaveState FromJson(string json)
    {
        if (string.IsNullOrEmpty(json)) return null;
        try { return JsonUtility.FromJson<SaveState>(json); }
        catch { return null; }
    }
}
