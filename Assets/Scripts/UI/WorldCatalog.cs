using UnityEngine;

/// <summary>
/// 18 dünyanın sırası, ikon dosya adı ve görünen adı + kilit/ilerleme mantığı. İçerik (LevelData) şu an
/// yalnız Dünya 0'da (3 level). İçeriği olmayan dünyalar kilitli kalır (Sprint 5'te açılır). Bir dünya
/// tamamlanınca (tüm leveller bitince) sonraki içerikli dünya açılır. Level ilerlemesi PlayerPrefs
/// "World{n}_Unlocked" (LevelManager). Patika tasarımı 5 duraklı (LevelsPerWorld).
/// </summary>
public static class WorldCatalog
{
    public const int Count = 18;
    public const int LevelsPerWorld = 5;   // (eski varsayılan) — patika artık PlayableLevels'e göre çizilir

    /// <summary>
    /// GÖRÜNTÜ/İLERLEME SIRASI (data-driven): slot → worldId. Dünya taşımak = SADECE bu diziyi değiştirmek.
    /// worldId'ler SABİT KİMLİK (LevelData asset'leri, PlayerPrefs unlock anahtarları, LevelManager.worlds,
    /// Icons/Names indeksleri) — asla yeniden numaralanmaz.
    /// 2026-07-22: Park (0) sonlara alındı; ilk dünya = Yiyecekler (1); final = Karma (17).
    /// 2026-07-23: Elektronik (8) yeterli model yok → içeriksiz; zincir ortasında sonraki dünyaların kilidini
    /// kırmasın diye SONA (sondan bir önceki) taşındı. Model bulunmazsa Order'dan çıkarılıp silinecek.
    /// </summary>
    // 2026-07-24: Elektronik(8) + Harikalar(16) KALDIRILDI (model yok). Park(0)=14. dünya, Karma(17)=15./final.
    // 15 dünya toplam: 13 tematik içerikli + Park + Karma(karışık).
    public static readonly int[] Order =
    {
        1, 2, 3, 4, 5,          // 1-5:  Yiyecekler, Arabalar, Binalar, Tatlılar, İçecekler
        6, 7, 9, 10, 11,        // 6-10: Hediyeler, Kitaplar, Kediler, Köpekler, Gemiler
        12, 13, 15,             // 11-13: Uçaklar, Hazine(13=Para+Altın), Mücevher(15)
        0, 17,                  // 14: Park, 15: Karma (final, ilk 14 dünyadan karışık)
    };

    /// <summary>Dünyanın sıradaki konumu (0-tabanlı slot); bilinmiyorsa -1.</summary>
    public static int OrderIndex(int world)
    {
        for (int i = 0; i < Order.Length; i++) if (Order[i] == world) return i;
        return -1;
    }

    /// <summary>Sırada bu dünyadan SONRAKİ dünyanın worldId'si; son dünyaysa/bilinmiyorsa -1.</summary>
    public static int NextWorld(int world)
    {
        int i = OrderIndex(world);
        return (i >= 0 && i + 1 < Order.Length) ? Order[i + 1] : -1;
    }

    /// <summary>Sıradaki İLK dünya (yeni oyuncunun başlangıcı).</summary>
    public static int FirstWorld => Order[0];

    // Icons/Names indeksi = worldId (görüntü sırası DEĞİL — sıra için Order kullan).
    public static readonly string[] Icons =
    {
        "world_trees", "world_foods", "world_cars", "world_buildings", "world_sweets", "world_drinks",
        "world_gifts", "world_books", "world_electronics", "world_cats", "world_dogs", "world_ships",
        "world_planes", "world_gold", "world_gold", "world_jewels", "world_wonders", "world_mixed",
        // 2026-07-24: index13 (Hazine) ikonu banknotes→gold (hazine/zenginlik hissi).
    };

    public static readonly string[] Names =
    {
        "Park", "Yiyecekler", "Arabalar", "Binalar", "Tatlılar", "İçecekler",
        "Hediyeler", "Kitaplar", "Elektronik", "Kediler", "Köpekler", "Gemiler",
        "Uçaklar", "Hazine", "Altın", "Mücevher", "Harikalar", "Karma",
        // 2026-07-24: index13 "Paralar"→"Hazine" (Para+Altın birleşik); index14 "Altın" ARTIK KULLANILMIYOR (Order'dan çıktı).
    };

    /// <summary>O dünyada oynanabilir (LevelData mevcut) level sayısı. Park=5; içerikli dünyalar 15; gerisi 0.</summary>
    public static int PlayableLevels(int world)
    {
        if (world == 8 || world == 14 || world == 16) return 0;     // Elektronik+Harikalar (model yok), Altın (Hazine'ye birleşti)
        if (world == 0 || (world >= 1 && world <= 7) || (world >= 9 && world <= 13) || world == 15 || world == 17) return 15;
        return 0;                                                    // (Park artık 15 level — generic deco)
    }

    public static bool HasContent(int world) => PlayableLevels(world) > 0;

    /// <summary>Zor (HARD) level mi? Her 5 levelda 1 (5., 10., 15.). LevelData.IsHard ile aynı kural. (2026-07-24: %10→%5, 15 level/dünya.)</summary>
    public static bool IsHard(int world, int levelIndex) => (levelIndex + 1) % 5 == 0;

    /// <summary>Dünyanın tüm (oynanabilir) levelleri bitti mi?</summary>
    public static bool WorldComplete(int world)
    {
        int p = PlayableLevels(world);
        return p > 0 && LevelManager.UnlockedIndex(world) >= p;
    }

    /// <summary>Dünya seçilebilir mi? (içeriği var VE sıradaki ilk dünya ya da SIRADA öncekini bitirdiyse)</summary>
    public static bool WorldUnlocked(int world)
    {
        if (!HasContent(world)) return false;
        int i = OrderIndex(world);
        if (i < 0) return false;
        if (i == 0) return true;
        return WorldComplete(Order[i - 1]);
    }
}
