using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Yıldız → güç-up ödülleri (2026-08-06 kullanıcı, PERİYODİK): oyuncunun TOPLAM yıldızına göre her N yıldızda 1 adet:
///   • her 5 yıldız  → +1 Hız
///   • her 7 yıldız  → +1 Mıknatıs
///   • her 10 yıldız → +1 Büyütme
///   • her 20 yıldız → +1 SÜPER
/// Her tür için kaç adet verildiği PlayerPrefs'te sayılır (idempotent). Kazanılanlar PowerUpInventory'ye eklenir.
/// CheckAndGrant, success sonrası ve menü açılışında çağrılır; yeni verilenlerin ("2x Hız" vb.) listesini döndürür.
/// </summary>
public static class StarRewards
{
    // Yeni eşikler (2026-08-18 kullanıcı): her 20★→Hız, 25★→Mıknatıs, 30★→Büyütme, 50★→Süper.
    static readonly (PowerUpType type, int per, string label)[] Rules =
    {
        (PowerUpType.Speed,     20, "Hız"),
        (PowerUpType.Magnet,    25, "Mıknatıs"),
        (PowerUpType.SizeBurst, 30, "Büyütme"),
        (PowerUpType.Super,     50, "SÜPER"),
    };

    static string KeyGranted(PowerUpType t) => $"StarGift_{t}_Granted";

    const string KEY_FIRSTGIFT = "FirstGiftGranted";
    static readonly PowerUpType[] AllPowers = { PowerUpType.Speed, PowerUpType.Magnet, PowerUpType.SizeBurst, PowerUpType.Super };

    /// <summary>Yeni oyuncuya BİR KEZ başlangıç hediyesi: her powerup'tan 3 (idempotent, PlayerPrefs bayrağı).</summary>
    public static void GrantFirstLaunchGift()
    {
        if (PlayerPrefs.GetInt(KEY_FIRSTGIFT, 0) == 1) return;
        PlayerPrefs.SetInt(KEY_FIRSTGIFT, 1);
        foreach (var t in AllPowers) PowerUpInventory.Add(t, 3);
        PlayerPrefs.Save();
    }

    /// <summary>Toplam yıldıza göre hak edilen ama verilmemiş güç-up'ları verir. Yeni verilenleri (tür, adet) döndürür
    /// (success ekranı uçuş animasyonu bunu kullanır).</summary>
    public static List<(PowerUpType type, int count)> CheckAndGrant()
    {
        var granted = new List<(PowerUpType, int)>();
        int total = PlayerProfile.EarnedStars;   // BİRİKİMLİ (tekrar oynayınca artar; üst sınır yok)
        bool any = false;
        foreach (var r in Rules)
        {
            int earned  = total / r.per;                              // toplam yıldızdan hak edilen adet
            int already = PlayerPrefs.GetInt(KeyGranted(r.type), 0);
            if (earned > already)
            {
                int add = earned - already;
                PowerUpInventory.Add(r.type, add);
                PlayerPrefs.SetInt(KeyGranted(r.type), earned);
                granted.Add((r.type, add));
                any = true;
            }
        }
        if (any) PlayerPrefs.Save();
        return granted;
    }

    // Güç-up adı SEÇİLİ DİLE göre (success ödül metni vb.). Türkçe sabitler kaldırıldı → Loc.T.
    public static string Label(PowerUpType t) => t switch
    {
        PowerUpType.Speed => Loc.T("pwSpeed"), PowerUpType.Magnet => Loc.T("pwMagnet"),
        PowerUpType.SizeBurst => Loc.T("pwSize"), PowerUpType.Super => Loc.T("pwSuper"), _ => "",
    };

    /// <summary>(tür,adet) listesini metin etiketlerine çevirir ("2x Mıknatıs" vb.).</summary>
    public static List<string> Format(List<(PowerUpType type, int count)> granted)
    {
        var l = new List<string>();
        if (granted != null) foreach (var g in granted) l.Add(g.count > 1 ? $"{g.count}x {Label(g.type)}" : Label(g.type));
        return l;
    }

    /// <summary>Bir sonraki ödüle kaç yıldız kaldı (UI ipucu için) — en yakın eşik.</summary>
    public static int StarsToNextGift()
    {
        int total = PlayerProfile.EarnedStars, best = int.MaxValue;
        foreach (var r in Rules) best = Mathf.Min(best, r.per - (total % r.per));
        return best == int.MaxValue ? 0 : best;
    }
}
