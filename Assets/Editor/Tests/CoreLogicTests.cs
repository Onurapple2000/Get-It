using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

/// <summary>
/// Yayın öncesi güvenlik ağı — saf mantık testleri (sahne/cihaz gerekmez, saniyeler içinde çalışır).
/// Window → General → Test Runner → EditMode → Run All
///
/// Neden bu dördü: bozulduklarında sessizce ve GERİ DÖNÜLMEZ hasar veriyorlar —
/// eksik çeviri (kutu/anahtar metin), bulut kaydında ilerleme kaybı, satın almanın ürünü bulamaması.
/// </summary>
public class LocalizationTests
{
    // Loc.Table private → üretim kodunu değiştirmemek için reflection ile okunur.
    static Dictionary<string, string[]> Table =>
        (Dictionary<string, string[]>)typeof(Loc)
            .GetField("Table", BindingFlags.NonPublic | BindingFlags.Static)
            .GetValue(null);

    const int LangCount = 7;   // TR, EN, ES, DE, AR, KO, RU

    [Test]
    public void Dil_sayisi_enum_ile_tutarli()
    {
        Assert.AreEqual(LangCount, Enum.GetValues(typeof(Language)).Length,
            "Language enum'a dil eklendi/çıkarıldı ama sözlük dizileri güncellenmedi.");
    }

    [Test]
    public void Her_anahtar_tam_7_ceviri_icerir()
    {
        var eksik = Table.Where(kv => kv.Value.Length != LangCount)
                         .Select(kv => $"{kv.Key} ({kv.Value.Length} çeviri)").ToList();
        Assert.IsEmpty(eksik, "Eksik/fazla çevirisi olan anahtarlar: " + string.Join(", ", eksik));
    }

    [Test]
    public void Hicbir_ceviri_bos_degil()
    {
        var bos = new List<string>();
        foreach (var kv in Table)
            for (int i = 0; i < kv.Value.Length; i++)
                if (string.IsNullOrWhiteSpace(kv.Value[i])) bos.Add($"{kv.Key}[{(Language)i}]");
        Assert.IsEmpty(bos, "Boş çeviri: " + string.Join(", ", bos));
    }

    [Test]
    public void Sistem_dili_desteklenen_bir_dile_eslenir()
    {
        var d = Loc.SystemDefault();
        Assert.IsTrue(Enum.IsDefined(typeof(Language), d), "SystemDefault geçersiz dil döndürdü.");
    }
}

public class SaveStateMergeTests
{
    static SaveState S(int coins = 0, int stars = 0, int score = 0, bool noAds = false)
        => new SaveState { coins = coins, earnedStars = stars, totalScore = score, noAds = noAds };

    [Test]
    public void Merge_ilerlemeyi_kaybetmez_max_kazanir()
    {
        var m = SaveState.Merge(S(coins: 100, stars: 5, score: 900), S(coins: 40, stars: 9, score: 1200));
        Assert.AreEqual(100, m.coins, "coins: büyük olan kazanmalı");
        Assert.AreEqual(9, m.earnedStars, "yıldız: büyük olan kazanmalı");
        Assert.AreEqual(1200, m.totalScore, "skor: büyük olan kazanmalı");
    }

    [Test]
    public void Merge_noAds_bir_kez_alindiysa_hep_acik()
    {
        Assert.IsTrue(SaveState.Merge(S(noAds: false), S(noAds: true)).noAds);
        Assert.IsTrue(SaveState.Merge(S(noAds: true), S(noAds: false)).noAds);
    }

    [Test]
    public void Merge_bos_yerel_bulut_kaydini_ezmez()
    {
        // Yeniden kurulum senaryosu: yerel sıfır, bulut dolu → hiçbir şey kaybolmamalı.
        var bulut = S(coins: 5000, stars: 42, score: 99999, noAds: true);
        bulut.unlocks.Add(new SaveState.IntKV("W1", 7));
        bulut.stars.Add(new SaveState.IntKV("W1_L3", 3));
        bulut.powerups.Add(new SaveState.IntKV(PowerUpType.Super.ToString(), 6));

        var m = SaveState.Merge(S(), bulut);

        Assert.AreEqual(5000, m.coins);
        Assert.AreEqual(42, m.earnedStars);
        Assert.IsTrue(m.noAds);
        Assert.AreEqual(7, m.unlocks.Single(k => k.k == "W1").v, "açılmış level kaybolmuş");
        Assert.AreEqual(3, m.stars.Single(k => k.k == "W1_L3").v, "yıldız kaybolmuş");
        Assert.AreEqual(6, m.powerups.Single(k => k.k == "Super").v, "güç-up kaybolmuş");
    }

    [Test]
    public void Merge_null_bulut_yereli_dondurur()
    {
        var yerel = S(coins: 10);
        Assert.AreSame(yerel, SaveState.Merge(yerel, null));
    }

    [Test]
    public void Json_gidis_donus_veriyi_korur()
    {
        var s = S(coins: 1234, stars: 7, score: 555, noAds: true);
        s.langChosen = true; s.lang = (int)Language.German;
        s.unlocks.Add(new SaveState.IntKV("W2", 4));

        var geri = SaveState.FromJson(s.ToJson());

        Assert.AreEqual(1234, geri.coins);
        Assert.AreEqual((int)Language.German, geri.lang);
        Assert.IsTrue(geri.langChosen, "dil tercihi JSON'da kaybolmuş (reinstall'da dil ekranı tekrar çıkar)");
        Assert.AreEqual(4, geri.unlocks.Single(k => k.k == "W2").v);
    }

    [Test]
    public void Bozuk_json_cokmez_null_doner()
    {
        Assert.IsNull(SaveState.FromJson("{bozuk"));
        Assert.IsNull(SaveState.FromJson(""));
    }
}

public class IapCatalogTests
{
    static Dictionary<string, string[]> LocTable =>
        (Dictionary<string, string[]>)typeof(Loc)
            .GetField("Table", BindingFlags.NonPublic | BindingFlags.Static)
            .GetValue(null);

    [Test]
    public void Urun_kimlikleri_benzersiz_ve_bos_degil()
    {
        var ids = IapService.Packs.Select(p => p.id).ToList();
        Assert.IsFalse(ids.Any(string.IsNullOrWhiteSpace), "Boş ürün kimliği var.");
        Assert.AreEqual(ids.Count, ids.Distinct().Count(), "Yinelenen ürün kimliği var: " +
            string.Join(", ", ids.GroupBy(i => i).Where(g => g.Count() > 1).Select(g => g.Key)));
    }

    [Test]
    public void Tam_bir_tane_reklamsiz_urunu_var()
    {
        Assert.AreEqual(1, IapService.Packs.Count(p => p.noAds), "noAds ürünü tam olarak 1 olmalı.");
        Assert.AreEqual(IapService.NoAdsId, IapService.Packs.Single(p => p.noAds).id);
    }

    [Test]
    public void Her_paketin_loc_anahtari_sozlukte_var()
    {
        var eksik = IapService.Packs.Where(p => !LocTable.ContainsKey(p.locKey))
                                    .Select(p => $"{p.id} → {p.locKey}").ToList();
        Assert.IsEmpty(eksik, "Loc'ta karşılığı olmayan paket adı: " + string.Join(", ", eksik));
    }

    [Test]
    public void Her_paket_bir_sey_verir()
    {
        var bos = IapService.Packs
            .Where(p => !p.noAds && p.coins <= 0 && (p.powers == null || p.powers.Length == 0))
            .Select(p => p.id).ToList();
        Assert.IsEmpty(bos, "Hiçbir ödül vermeyen paket: " + string.Join(", ", bos));
    }

    [Test]
    public void Fiyat_kademeleri_gecerli_araliktan()
    {
        foreach (var p in IapService.Packs.Where(p => !p.noAds))
            Assert.That(p.priceTier, Is.InRange(0, 4), $"{p.id} geçersiz priceTier: {p.priceTier}");
    }
}

public class EconomyTests
{
    [Test]
    public void Tum_powerup_fiyatlari_pozitif()
    {
        foreach (PowerUpType t in Enum.GetValues(typeof(PowerUpType)))
        {
            if (t == PowerUpType.None) continue;
            Assert.Greater(Economy.PowerupCost(t), 0, $"{t} fiyatı 0 veya negatif.");
        }
    }

    [Test]
    public void Coin_harcama_esikleri_makul()
    {
        Assert.Greater(Economy.FreeCoinsPerAd, 0);
        Assert.Greater(Economy.ContinueCost, 0);
        Assert.Greater(Economy.RefillLifeCost, 0);
        Assert.Greater(Economy.SkipAdCost, 0);
        Assert.Greater(Economy.ContinueBonusSeconds, 0f);
    }
}
