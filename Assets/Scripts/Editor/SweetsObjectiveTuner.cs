using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// TATLILAR (World4) hedef ayarı (2026-07-31 kullanıcı): tip sayısı L1=2, L2..L10=3, L11..L15=4 (eksikse o levelın
/// SPAWN türlerinden ekle). İLK 5 LEVEL'da hedef MİKTARLARINI ~%35 artır (rank'a göre desen). 2026-08-01 kullanıcı:
/// L6..L15'te de hedef miktarlarını artır → mevcut required'ları ×1.25 (dağılımı koru), 60'ta tavanla. Reachability
/// otomatik: LevelManager.EnsureObjectiveCounts runtime'da her hedef türünden required+3 adet garantiler (world4'te
/// _occ boş → hep yerleşir). Süreye/spawn'lara dokunmaz. Menü: Tools/GET_IT/Tune Sweets Objectives.
/// </summary>
public static class SweetsObjectiveTuner
{
    const string DIR = "Assets/Levels";

    [MenuItem("Tools/GET_IT/Tune Sweets Objectives")]
    public static void Run()
    {
        int changed = 0;
        var sb = new System.Text.StringBuilder("[SweetsObj] Tatlılar hedef ayarı:\n");
        for (int i = 0; i < 15; i++)
        {
            string path = $"{DIR}/World4_Level{i + 1}.asset";
            var lv = AssetDatabase.LoadAssetAtPath<LevelData>(path);
            if (lv == null) { sb.AppendLine($"  L{i + 1}: YOK"); continue; }

            int desired = (i == 0) ? 2 : (i <= 9) ? 3 : 4;
            bool firstFive = i <= 4;

            var objTypes = lv.objectives.Select(o => o.objectType).ToList();
            var spawnTypes = lv.spawns.Where(s => s.prefab != null && !s.prefab.name.StartsWith("Power"))
                                      .Select(s => s.prefab.name).Distinct().ToList();

            // eksik tip: o levelın spawn'larından, hedefte olmayanı ekle
            foreach (var t in spawnTypes)
            {
                if (objTypes.Count >= desired) break;
                if (!objTypes.Contains(t)) objTypes.Add(t);
            }
            objTypes = objTypes.Take(desired).ToList();

            // İlk-5 hep işlenir; L6..L15 ARTIK HER ZAMAN işlenir (miktar boost'u için — eskiden tip değişmiyorsa atlanıyordu).
            const int CAP = 60;         // absürt sayıya kaçmasın (storm + oynanabilirlik)
            const float BOOST = 1.25f;  // L6..L15 mevcut required çarpanı

            // miktarlar: ilk-5 → mevcut TOP × 1.35, rank deseni; L6..L15 → mevcut required ×1.25 (dağılım korunur)
            int curTop = lv.objectives.Count > 0 ? lv.objectives.Max(o => o.required) : 18;
            var oldReq = lv.objectives.ToDictionary(o => o.objectType, o => o.required);
            float[] fr = { 1f, 0.72f, 0.55f, 0.45f };
            int nb = Mathf.RoundToInt(curTop * 1.35f);

            var newObjs = new List<LevelData.ObjectiveEntry>();
            for (int k = 0; k < objTypes.Count; k++)
            {
                int req;
                if (firstFive) req = Mathf.Max(6, Mathf.RoundToInt(nb * fr[Mathf.Min(k, fr.Length - 1)]));
                else if (oldReq.TryGetValue(objTypes[k], out int r)) req = Mathf.RoundToInt(r * BOOST);   // mevcut ×1.25
                else req = Mathf.Max(6, Mathf.RoundToInt(curTop * BOOST * fr[Mathf.Min(k, fr.Length - 1)]));  // eklenen tipe değer
                newObjs.Add(new LevelData.ObjectiveEntry { objectType = objTypes[k], required = Mathf.Min(CAP, req) });
            }
            lv.objectives = newObjs;
            EditorUtility.SetDirty(lv);
            changed++;
            sb.AppendLine($"  L{i + 1}: {objTypes.Count} tip | " + string.Join(", ", newObjs.Select(o => $"{o.objectType}={o.required}")));
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        sb.AppendLine($"Toplam {changed} level değişti.");
        Debug.Log(sb.ToString());
    }
}
