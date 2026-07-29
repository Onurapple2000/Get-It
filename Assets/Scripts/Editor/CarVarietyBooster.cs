using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// ARABALAR (World2) tüm levellarına daha fazla ARABA ÇEŞİDİ ekler (kullanıcı 2026-07-25: her levelda daha fazla
/// çeşit araç). Prefabs/Cars'ta 100+ araba var ama levellar ~15 kullanıyor. Bu araç her levele, o levelde OLMAYAN
/// arabalardan hedef sayıya kadar ekler (count 1, stack 1). Objektifler/süre/mevcut spawn'lar KORUNUR.
/// Level bazlı deterministik seçim → her level farklı ek araçlar alır. Menu: Tools/GET_IT/Boost Car Variety (World2)
/// </summary>
public static class CarVarietyBooster
{
    const int TARGET_TYPES = 42;   // level başına ~araba türü (kullanıcı 2026-07-28: daha çok çeşit; tür sayısı perf'i etkilemez)

    [MenuItem("Tools/GET_IT/Boost Car Variety (World2)")]
    public static void Run()
    {
        var allCars = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs/Cars" })
            .Select(g => AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(p => p != null && p.GetComponent<PhysicsSwallowable>() != null)
            .ToList();
        if (allCars.Count == 0) { Debug.LogWarning("[CarVariety] Prefabs/Cars'ta araba bulunamadı."); return; }

        int totalAdded = 0;
        for (int lvl = 1; lvl <= 15; lvl++)
        {
            string path = $"Assets/Levels/World2_Level{lvl}.asset";
            var ld = AssetDatabase.LoadAssetAtPath<LevelData>(path);
            if (ld == null) continue;

            var existing = new HashSet<string>(ld.spawns.Where(e => e.prefab != null).Select(e => e.prefab.name));
            int carTypes = existing.Count(n => !n.StartsWith("Power"));

            // Level bazlı deterministik sıralama → her level farklı ek araçlar.
            var pool = allCars.Where(p => !existing.Contains(p.name))
                              .OrderBy(p => (p.name.GetHashCode() ^ (lvl * 7919)) & 0x7fffffff)
                              .ToList();

            int added = 0, idx = 0;
            while (carTypes < TARGET_TYPES && idx < pool.Count)
            {
                ld.spawns.Add(new LevelData.SpawnEntry { prefab = pool[idx], count = 1, stack = 1, scale = 1f });
                carTypes++; added++; idx++;
            }
            if (added > 0) { EditorUtility.SetDirty(ld); totalAdded += added; Debug.Log($"[CarVariety] L{lvl}: +{added} tür (toplam {carTypes})"); }
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[CarVariety] Bitti — World2 levellarına toplam {totalAdded} araba türü eklendi (hedef {TARGET_TYPES}/level).");
    }
}
