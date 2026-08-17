using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Kullanıcının istediği 10 bina modelini TÜM levellardan çıkarır (2026-07-31). Her çıkarılan spawn'ın yerine
/// AYNI tier'da (PhysicsSwallowable.scoreValue) VE o levelda OLMAYAN başka bir bina koyar (spawn SLOTU korunur →
/// yerleşim/Voronoi düzeni aynı kalır; aynı boyut → footprint aralığı aynı). Çıkarılan bina HEDEF ise, hedef
/// tipini o levelda seçilen yeni binaya çevirir (required korunur). Ayrıca BuildBuildingWorld yeniden üretirse
/// geri gelmesinler diye exclude listesi RemovedNames'ten okunmalı. Menü: Tools/GET_IT/Remove Listed Building Models.
/// </summary>
public static class RemoveBuildingModels
{
    const string DIR = "Assets/Prefabs/Buildings";

    public static readonly HashSet<string> RemovedNames = new HashSet<string>
    {
        "Art", "CyberpunkCityStore", "NeonCityCorner", "ExtravagantAnimeSty4", "Ideal",
        "Golden", "MediterraneanUrbanR", "SnowyVillageChristm", "TownHall", "WesternSaloonBuildi",
        "Cyberpunk",       // 2026-07-31 ek: Meshy_AI_cyberpunk_building_9_0709213651
        "HeartShapedGift", // 2026-07-31: bina DEĞİL (kalp hediye) — binalardan çıkar; GLB gifts klasörüne taşındı
    };

    [MenuItem("Tools/GET_IT/Remove Listed Building Models")]
    public static void Run()
    {
        // 1) tier (scoreValue) → çıkarılmayan bina prefabları (isimce sıralı, deterministik)
        var byTier = new Dictionary<int, List<GameObject>>();
        var tierOf = new Dictionary<string, int>();
        foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { DIR }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.StartsWith(DIR + "/") || !path.EndsWith(".prefab")) continue;
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var sw = go != null ? go.GetComponent<PhysicsSwallowable>() : null;
            if (sw == null) continue;
            tierOf[go.name] = sw.scoreValue;
            if (RemovedNames.Contains(go.name)) continue;
            if (!byTier.TryGetValue(sw.scoreValue, out var lst)) { lst = new List<GameObject>(); byTier[sw.scoreValue] = lst; }
            lst.Add(go);
        }
        foreach (var kv in byTier) kv.Value.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        var tiersAsc = byTier.Keys.OrderBy(x => x).ToList();

        GameObject Pick(int tier, HashSet<string> inLevel, HashSet<string> used, int rot)
        {
            // önce tam tier, sonra en yakın tier'lar (boyut benzeri)
            foreach (int t in tiersAsc.OrderBy(t => Mathf.Abs(t - tier)))
            {
                var lst = byTier[t];
                for (int i = 0; i < lst.Count; i++)
                {
                    var go = lst[(i + rot) % lst.Count];
                    if (!inLevel.Contains(go.name) && !used.Contains(go.name)) return go;
                }
            }
            return null;
        }

        // 2) tüm levelları işle
        int changedLevels = 0, swapped = 0, objSwapped = 0, failed = 0;
        foreach (var guid in AssetDatabase.FindAssets("t:LevelData"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var lv = AssetDatabase.LoadAssetAtPath<LevelData>(path);
            if (lv == null || lv.spawns == null) continue;

            var inLevel = new HashSet<string>();
            foreach (var s in lv.spawns) if (s.prefab != null && !RemovedNames.Contains(s.prefab.name)) inLevel.Add(s.prefab.name);

            int rot = Mathf.Abs(lv.name.GetHashCode()) % 97;   // levellar arası çeşit için döndür
            var mapping = new Dictionary<string, string>();
            var used = new HashSet<string>();
            bool dirty = false;

            foreach (var s in lv.spawns)
            {
                if (s.prefab == null || !RemovedNames.Contains(s.prefab.name)) continue;
                int tier = tierOf.TryGetValue(s.prefab.name, out int tv) ? tv : 15;
                var rep = Pick(tier, inLevel, used, rot);
                if (rep == null) { Debug.LogWarning($"[RemoveBld] {lv.name}: {s.prefab.name} için yedek bulunamadı (tier {tier})"); failed++; continue; }
                mapping[s.prefab.name] = rep.name;
                used.Add(rep.name); inLevel.Add(rep.name);
                s.prefab = rep; dirty = true; swapped++;
            }

            if (lv.objectives != null)
                foreach (var o in lv.objectives)
                {
                    if (!RemovedNames.Contains(o.objectType)) continue;
                    if (mapping.TryGetValue(o.objectType, out var rep)) { o.objectType = rep; dirty = true; objSwapped++; }
                    else
                    {
                        int tier = tierOf.TryGetValue(o.objectType, out int tv) ? tv : 15;
                        var r = Pick(tier, inLevel, used, rot);
                        if (r != null) { o.objectType = r.name; used.Add(r.name); inLevel.Add(r.name);
                            lv.spawns.Add(new LevelData.SpawnEntry { prefab = r, count = 1, stack = 1, scale = 1f });
                            dirty = true; objSwapped++; }
                        else { Debug.LogWarning($"[RemoveBld] {lv.name}: hedef {o.objectType} için yedek yok"); failed++; }
                    }
                }

            if (dirty) { EditorUtility.SetDirty(lv); changedLevels++; }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[RemoveBld] BİTTİ — {changedLevels} level değişti, {swapped} spawn + {objSwapped} hedef değiştirildi, {failed} başarısız. Çıkarılan: {string.Join(", ", RemovedNames)}");
    }
}
