using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// ASSET DELIVERY FAZ 2 güvenlik ağı (EditMode, Addressables "Use Asset Database" modu):
///  • LevelCatalog tüm levelları içeriyor mu?
///  • WorldContentLoader.Prepare bir level'ın TÜM prefab'larını GUID anahtarıyla Addressables'tan yükleyebiliyor mu?
///    (Klasör girdilerinin içindeki prefab'lar için GUID anahtarı katalogda olmalı — bu kırılırsa cihazda boş level açılır.)
/// </summary>
public class AssetDeliveryTests
{
    [Test]
    public void LevelCatalog_tum_levellari_iceriyor()
    {
        var cat = Resources.Load<LevelCatalog>(LevelCatalog.ResourceName);
        Assert.IsNotNull(cat, "Resources/LevelCatalog.asset yok — Tools/GET_IT/Asset Delivery/Rebuild Level Catalog çalıştır");
        Assert.AreEqual(225, cat.all.Count, "15 dünya × 15 level bekleniyor");
        Assert.IsNotNull(LevelCatalog.Get(1, 0), "Yiyecekler L1 katalogda yok");
        Assert.IsNotNull(LevelCatalog.Get(17, 14), "Karma L15 katalogda yok");
    }

    [Test]
    public void WorldPacks_paket_adlari_google_kuralina_uyar()
    {
        foreach (var t in WorldPacks.Table)
        {
            Assert.IsTrue(char.IsLetter(t.pack[0]), t.pack);
            foreach (var c in t.pack) Assert.IsTrue(char.IsLetterOrDigit(c) || c == '_', $"{t.pack}: '{c}' yasak");
        }
        Assert.IsFalse(WorldPacks.PacksFor(WorldCatalog.FirstWorld).Count > 0, "ilk dünya install-time → indirilecek paket olmamalı");
        Assert.AreEqual(WorldPacks.Table.Length - 1, WorldPacks.PacksFor(17).Count, "Karma = tüm on-demand paketler");
    }

    [UnityTest]
    public IEnumerator Prepare_Yiyecekler_L1_prefablari_yukler()
    {
        var ld = LevelCatalog.Get(1, 0);
        Assert.IsNotNull(ld);
        foreach (var s in ld.spawns) s.SetResolved(null);   // editör fallback'ini devre dışı bırakmak için cache'i boşalt

        bool done = false, ok = false;
        // EditMode'da MonoBehaviour coroutine'i ilerlemez → rutini elle sür
        var it = WorldContentLoader.PrepareRoutine(1, 0, null, r => { ok = r; done = true; });
        float t0 = Time.realtimeSinceStartup;
        while (!done && it.MoveNext() && Time.realtimeSinceStartup - t0 < 60f) yield return null;

        Assert.IsTrue(done, "Prepare 60 sn içinde bitmedi");
        Assert.IsTrue(ok, "Prepare başarısız: " + WorldContentLoader.LastError);
        int n = 0;
        foreach (var s in ld.spawns)
        {
            if (!s.HasRef) continue;
            var go = WorldContentLoader.Resolve(s.PrefabGuid);   // AssetDatabase fallback DEĞİL, gerçek Addressables sonucu
            Assert.IsNotNull(go, "Addressables'tan yüklenemedi guid=" + s.PrefabGuid);
            n++;
        }
        Assert.Greater(n, 0);
    }
}
