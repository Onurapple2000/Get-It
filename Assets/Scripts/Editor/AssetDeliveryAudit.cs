using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

/// <summary>
/// ASSET DELIVERY FAZ 2 — doğrulama/denetim aracı.
/// Menü: Tools/GET_IT/Asset Delivery/Audit Level Refs
///  1. Tüm LevelData asset'lerini tarar: her SpawnEntry ref'i editörde bir prefab'a ÇÖZÜLÜYOR mu (migrasyon kaybı var mı)?
///  2. Her prefab'ın Addressables grubunu bulur: grupsuz (= base build'e de girmez, on-demand'den de gelmez → RUNTIME'DA
///     EKSİK) prefab'ları listeler. Bunlar Setup Addressable Groups'a eklenmeli.
///  3. Dünya başına benzersiz prefab sayısını ve hangi gruplardan beslendiğini raporlar (Karma dünyası çok gruplu olur).
/// Rapor: Console + Builds/asset-delivery-audit.txt
/// </summary>
public static class AssetDeliveryAudit
{
    [MenuItem("Tools/GET_IT/Asset Delivery/Audit Level Refs")]
    public static void Run()
    {
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        var guids = AssetDatabase.FindAssets("t:LevelData", new[] { "Assets/Levels" });
        int levels = 0, entries = 0, resolved = 0, missing = 0, noRef = 0;
        var ungrouped = new SortedDictionary<string, int>();          // prefab path → kaç level kullanıyor
        var worldPrefabs = new SortedDictionary<int, HashSet<string>>();
        var worldGroups = new SortedDictionary<int, HashSet<string>>();
        var missingList = new List<string>();

        foreach (var g in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(g);
            var ld = AssetDatabase.LoadAssetAtPath<LevelData>(path);
            if (ld == null) continue;
            levels++;
            if (!worldPrefabs.ContainsKey(ld.worldId)) { worldPrefabs[ld.worldId] = new HashSet<string>(); worldGroups[ld.worldId] = new HashSet<string>(); }
            for (int i = 0; i < ld.spawns.Count; i++)
            {
                var s = ld.spawns[i]; entries++;
                if (!s.HasRef) { noRef++; missingList.Add($"{Path.GetFileName(path)} #{i}: REF YOK"); continue; }
                var pf = s.prefab;   // editörde AssetDatabase fallback
                if (pf == null) { missing++; missingList.Add($"{Path.GetFileName(path)} #{i}: guid {s.PrefabGuid} ÇÖZÜLEMEDİ"); continue; }
                resolved++;
                string ppath = AssetDatabase.GetAssetPath(pf);
                worldPrefabs[ld.worldId].Add(ppath);
                var entry = settings != null ? settings.FindAssetEntry(s.PrefabGuid, true) : null;   // klasör girdisi dahil
                if (entry == null) { ungrouped.TryGetValue(ppath, out int c); ungrouped[ppath] = c + 1; }
                else worldGroups[ld.worldId].Add(entry.parentGroup.Name);
            }
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"[AssetDeliveryAudit] {levels} level, {entries} girdi → çözülen {resolved}, çözülemeyen {missing}, ref'siz {noRef}");
        foreach (var kv in worldPrefabs)
            sb.AppendLine($"  dünya {kv.Key,2}: {kv.Value.Count,3} benzersiz prefab | gruplar: {string.Join(", ", worldGroups[kv.Key])}");
        sb.AppendLine($"GRUPSUZ prefab (Addressables'ta değil → build'de EKSİK olur): {ungrouped.Count}");
        foreach (var kv in ungrouped) sb.AppendLine($"  {kv.Key}  (x{kv.Value})");
        if (missingList.Count > 0) { sb.AppendLine("SORUNLU GİRDİLER:"); foreach (var m in missingList) sb.AppendLine("  " + m); }

        Directory.CreateDirectory("Builds");
        File.WriteAllText("Builds/asset-delivery-audit.txt", sb.ToString());
        if (missing > 0 || noRef > 0) Debug.LogError(sb.ToString()); else Debug.Log(sb.ToString());
    }
}
