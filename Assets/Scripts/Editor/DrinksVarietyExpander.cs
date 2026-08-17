using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// İÇECEKLER (World5) SPAWN PALETİ genişletme (2026-08-04 kullanıcı): sahne ~2× büyüyünce nesne sayısı 2× arttı ama
/// AYNI türlerin kopyası çoğaldı. İstek: sayısal çoklamayı ÇEŞİT artırarak yap → her levelda bulunabilecek FARKLI
/// tür sayısını ~2.5× büyüt (16→~36). Kompozisyon (ComposeCarCity) zaten paletteki TÜM türleri kullanır → palet büyüyünce
/// aynı toplam sayı DAHA ÇOK türe dağılır (her tür daha az tekrar = daha çeşitli sahne). Toplam sayı ~aynı kalır
/// (arena boyutu + G/fg yoğunluğu belirler). Güç-up spawn'ları korunur; bomba ayarına dokunulmaz.
/// Levellar arası pencere kaydırılır → her level farklı alt-küme. ÇALIŞTIRDIKTAN SONRA hedef tuner'ı yeniden çalıştır
/// (palet değişti). Menü: Tools/GET_IT/Expand Drinks Variety (world 5).
/// </summary>
public static class DrinksVarietyExpander
{
    const string DIR = "Assets/Levels";
    const int TYPES_PER_LEVEL = 36;

    [MenuItem("Tools/GET_IT/Expand Drinks Variety (world 5)")]
    public static void Run() => Expand(5, "Assets/Prefabs/Drinks");

    [MenuItem("Tools/GET_IT/Expand Gift Variety (world 6)")]
    public static void RunGifts() => Expand(6, "Assets/Prefabs/Gifts");

    // Büyük-nesne dünyaları 7-15: her içerikli dünya için palet genişletme + hedef ayarı (tek menüde). worldId → prefab dir
    // (+ hariç tutulacak özel ad). Contentless 8/14 yok. Cats(9): animasyonlu "Cat" prefabı HARİÇ (5.2 birim, düzeni bozar).
    static readonly (int world, string dir, string exclude)[] BigWorlds =
    {
        (7,  "Assets/Prefabs/Books",   null),
        (9,  "Assets/Prefabs/Cats",    "Cat"),
        (10, "Assets/Prefabs/Dogs",    null),
        (11, "Assets/Prefabs/Ships",   null),
        (12, "Assets/Prefabs/Planes",  null),
        (13, "Assets/Prefabs/Money",   null),
        (15, "Assets/Prefabs/Jewelry", null),
    };

    [MenuItem("Tools/GET_IT/Apply Big-World Content (7-15)")]
    public static void ApplyBigWorlds7to15()
    {
        foreach (var (w, dir, ex) in BigWorlds)
        {
            Expand(w, dir, ex);
            DrinksObjectiveTuner.TuneWorld(w);
        }
        Debug.Log("[Variety] 7-15 içerikli dünyalar: palet genişletildi + hedefler ayarlandı.");
    }

    // PARK (0) + KARMA (17): Park = tek klasör (DecoObjects) → palet genişlet + hedef. Karma = KARIŞIK palet (çok
    // dünyadan) → palet SIFIRDAN kurulmamalı (çeşitliliği zaten karışık); sadece hedef ayarı. Engel/kompozisyon/fizik
    // LevelManager gate'i (BigObjWorld) ile otomatik.
    [MenuItem("Tools/GET_IT/Apply Park+Karma Content (0,17)")]
    public static void ApplyParkKarma()
    {
        Expand(0, "Assets/Prefabs/DecoObjects");   // Park: deco paleti (güç-up'lar korunur)
        DrinksObjectiveTuner.TuneWorld(0);
        DrinksObjectiveTuner.TuneWorld(17);         // Karma: mevcut KARIŞIK paletten footprint-hedef (expander YOK)
        Debug.Log("[Variety] Park(0) palet+hedef, Karma(17) hedef ayarlandı.");
    }

    // Bir dünyanın spawn PALETİNİ verilen prefab klasöründen genişletir (çeşit ↑). NOT: paleti SIFIRDAN kurar →
    // klasör dışı (ör. yanlışlıkla karışan bina) spawn'lar OTOMATİK temizlenir. Güç-up spawn'ları korunur.
    public static void Expand(int world, string prefabDir, string excludeExact = null)
    {
        var guids = AssetDatabase.FindAssets("t:Prefab", new[] { prefabDir });
        var pool = guids.Select(g => AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(g)))
                          .Where(p => p != null && !p.name.StartsWith("Power") && (excludeExact == null || p.name != excludeExact))
                          .OrderBy(p => p.name).ToList();
        if (pool.Count == 0) { Debug.LogError($"[Variety] {prefabDir} içinde prefab yok."); return; }
        int n = Mathf.Min(TYPES_PER_LEVEL, pool.Count);
        var drinks = pool;   // (isim uyumu için)

        var sb = new StringBuilder($"[Variety] World{world} palet ({pool.Count} havuz, level başına {n} tür):\n");
        int changed = 0;
        for (int i = 0; i < 15; i++)
        {
            string path = $"{DIR}/World{world}_Level{i + 1}.asset";
            var lv = AssetDatabase.LoadAssetAtPath<LevelData>(path);
            if (lv == null) { sb.AppendLine($"  L{i + 1}: YOK"); continue; }

            // Güç-up spawn'larını KORU (sistem onları tanır/yerleştirir).
            var keep = lv.spawns.Where(s => s.prefab != null && s.prefab.name.StartsWith("Power")).ToList();

            // Pencereyi level'e göre kaydır → her level farklı çeşit alt-kümesi (havuzda tur atar).
            int off = (i * 9) % drinks.Count;
            var newSpawns = new List<LevelData.SpawnEntry>();
            for (int k = 0; k < n; k++)
            {
                var pf = drinks[(off + k) % drinks.Count];
                newSpawns.Add(new LevelData.SpawnEntry { prefab = pf, count = 1, stack = 1, scale = 1f });
            }
            newSpawns.AddRange(keep);   // güç-up'lar sona
            lv.spawns = newSpawns;
            EditorUtility.SetDirty(lv);
            changed++;
            sb.AppendLine($"  L{i + 1}: {n} drink tür + {keep.Count} güç-up");
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        sb.AppendLine($"Toplam {changed} level. ⚠️ Şimdi 'Tune Drinks Objectives'i yeniden çalıştır (palet değişti).");
        Debug.Log(sb.ToString());
    }
}
