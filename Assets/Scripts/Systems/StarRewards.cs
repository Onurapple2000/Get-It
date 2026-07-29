using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Yıldız kilometre taşı hediyeleri. Oyuncunun TOPLAM yıldızı belirli eşiklere ulaştıkça ödül verir:
///  - "10 dk sınırsız can" (LivesManager.GrantUnlimited)
///  - güç-up kazanımı (PowerUpInventory'ye eklenir)
/// Her hediye BİR kez verilir (claimed sayacı PlayerPrefs'te). Success sonrası ve menü açılışında çağrılır
/// (idempotent). Yeni verilen hediyelerin açıklama listesini döndürür (UI/toast için).
/// </summary>
public static class StarRewards
{
    public enum Kind { UnlimitedLives, PowerUp }

    public struct Gift
    {
        public int stars;          // gereken toplam yıldız
        public Kind kind;
        public int minutes;        // UnlimitedLives için süre
        public PowerUpType power;  // PowerUp için tür
        public string label;
    }

    // Eşikler ARTAN sırada olmalı (claimed sayacı buna dayanır).
    static readonly Gift[] Gifts =
    {
        new Gift { stars = 10, kind = Kind.UnlimitedLives, minutes = 10, label = "10 dk SINIRSIZ CAN" },
        new Gift { stars = 20, kind = Kind.PowerUp, power = PowerUpType.SizeBurst, label = "Büyüme güç-up" },
        new Gift { stars = 30, kind = Kind.PowerUp, power = PowerUpType.Speed,     label = "Hız güç-up" },
        new Gift { stars = 45, kind = Kind.UnlimitedLives, minutes = 10, label = "10 dk SINIRSIZ CAN" },
        new Gift { stars = 60, kind = Kind.PowerUp, power = PowerUpType.Magnet,    label = "Mıknatıs güç-up" },
        new Gift { stars = 80, kind = Kind.UnlimitedLives, minutes = 10, label = "10 dk SINIRSIZ CAN" },
        new Gift { stars = 100, kind = Kind.PowerUp, power = PowerUpType.SizeBurst, label = "Büyüme güç-up" },
    };

    const string KEY_CLAIMED = "StarGiftsClaimed";   // verilmiş hediye sayısı (0..Gifts.Length)

    /// <summary>Toplam yıldıza göre hak edilen ama henüz verilmemiş hediyeleri verir. Yeni verilenlerin
    /// açıklamalarını döndürür (boşsa yeni hediye yok).</summary>
    public static List<string> CheckAndGrant()
    {
        var granted = new List<string>();
        int total = StarManager.Total();
        int claimed = Mathf.Clamp(PlayerPrefs.GetInt(KEY_CLAIMED, 0), 0, Gifts.Length);

        // Eşikler artan → hak edilen sayısı = stars<=total olan hediye adedi.
        int eligible = 0;
        for (int i = 0; i < Gifts.Length; i++) if (total >= Gifts[i].stars) eligible++;

        for (int i = claimed; i < eligible; i++)
        {
            Grant(Gifts[i]);
            granted.Add(Gifts[i].label);
        }
        if (eligible > claimed)
        {
            PlayerPrefs.SetInt(KEY_CLAIMED, eligible);
            PlayerPrefs.Save();
        }
        return granted;
    }

    static void Grant(Gift g)
    {
        switch (g.kind)
        {
            case Kind.UnlimitedLives:
                if (LivesManager.Instance != null)
                    LivesManager.Instance.GrantUnlimited(System.TimeSpan.FromMinutes(g.minutes));
                break;
            case Kind.PowerUp:
                PowerUpInventory.Add(g.power, 1);
                break;
        }
    }
}
