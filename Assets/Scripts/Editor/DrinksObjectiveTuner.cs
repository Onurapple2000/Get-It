using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// İÇECEKLER (World5) hedef ayarı. Tip sayısı L1=2, L2..L10=3, L11..L15=4.
/// ⚠️ 2026-08-03 (kullanıcı):
///  • Hedef türleri artık KÜÇÜK-ORTA ile SINIRLI DEĞİL → footprint'e göre YAYILMIŞ seçilir (küçük→büyük). Erken
///    levellar küçük/orta, geç levellar BÜYÜK hedef içerir → oyuncu büyük hedefi yutmak için DELİĞİ BÜYÜTMEK zorunda.
///  • Büyük nesne hedefse required, arenaya SIĞACAK kadar düşürülür (maxPlaceable) → garanti korunur.
///  • Sahne fazlası RANK'a göre (ComposeCarCity uygular): rank0 = TAM req (fazla 0), rank1 +2, rank2 +3, rank3 +5.
///    Bu yüzden required, (maxPlaceable − rankFazlası)'nı aşmaz → sahnedeki toplam (req+fazla) sığar.
/// Rank sırası = footprint ARTAN (rank0 = en küçük = en yüksek req; son rank = en büyük = düşük req + en çok fazla).
/// Menü: Tools/GET_IT/Tune Drinks Objectives.
/// </summary>
public static class DrinksObjectiveTuner
{
    const string DIR = "Assets/Levels";
    // Sahne fazlası (required ÜSTÜNE), rank'a göre — ComposeCarCity da AYNI diziyi kullanır (drk).
    static readonly int[] SURPLUS = { 0, 2, 3, 5 };

    [MenuItem("Tools/GET_IT/Tune Drinks Objectives")]
    public static void Run() => TuneWorld(5);

    [MenuItem("Tools/GET_IT/Tune Gift Objectives")]
    public static void RunGifts() => TuneWorld(6);

    // İçecekler (5) + Hediyeler (6) ORTAK hedef ayarı (footprint-yayılım + rank fazlası).
    public static void TuneWorld(int world)
    {
        var fpCache = new Dictionary<string, float>();
        int changed = 0;
        var sb = new System.Text.StringBuilder($"[Obj] World{world} hedef ayarı (footprint-yayılım + rank fazlası):\n");
        for (int i = 0; i < 15; i++)
        {
            string path = $"{DIR}/World{world}_Level{i + 1}.asset";
            var lv = AssetDatabase.LoadAssetAtPath<LevelData>(path);
            if (lv == null) { sb.AppendLine($"  L{i + 1}: YOK"); continue; }

            int desired = (i == 0) ? 2 : (i <= 9) ? 3 : 4;

            var spawns = lv.spawns.Where(s => s.prefab != null && !s.prefab.name.StartsWith("Power"))
                                  .Select(s => s.prefab).GroupBy(p => p.name).Select(g => g.First()).ToList();
            foreach (var pf in spawns) if (!fpCache.ContainsKey(pf.name)) fpCache[pf.name] = MeasureFootprint(pf);
            float Fp(string name) => fpCache.TryGetValue(name, out var f) ? f : 1f;

            // Adaylar footprint ARTAN. Hedefleri footprint aralığına YAYARAK seç (küçük→büyük). Aralık level ile büyür:
            // erken levellar küçük/orta (maxPct ~0.55), geç levellar en büyüğe kadar (maxPct 1.0).
            var cand = spawns.Select(p => p.name).Distinct().OrderBy(Fp).ToList();
            if (cand.Count == 0) { sb.AppendLine($"  L{i + 1}: spawn yok"); continue; }
            float maxPct = Mathf.Lerp(0.55f, 1f, i / 14f);
            var picked = new List<string>();
            var usedIdx = new HashSet<int>();
            for (int k = 0; k < desired; k++)
            {
                float pct = (desired <= 1) ? 0f : (k / (float)(desired - 1)) * maxPct;
                int idx = Mathf.Clamp(Mathf.RoundToInt(pct * (cand.Count - 1)), 0, cand.Count - 1);
                int guard = 0;
                while (usedIdx.Contains(idx) && guard++ < cand.Count) idx = (idx + 1) % cand.Count;
                usedIdx.Add(idx); picked.Add(cand[idx]);
            }
            var objTypes = picked.Distinct().OrderBy(Fp).ToList();   // rank = footprint artan

            // Miktar: top level ile artar; rank deseni {1,0.72,0.55,0.45}; sahnedeki toplam (req+fazla) maxPlaceable'ı aşmasın.
            float tt = i / 14f;
            int top = Mathf.RoundToInt(Mathf.Lerp(18f, 40f, tt));
            float[] fr = { 1f, 0.72f, 0.55f, 0.45f };

            var newObjs = new List<LevelData.ObjectiveEntry>();
            for (int k = 0; k < objTypes.Count; k++)
            {
                float fp = Fp(objTypes[k]);
                int surplus = SURPLUS[Mathf.Min(k, SURPLUS.Length - 1)];
                int rankReq = Mathf.RoundToInt(top * fr[Mathf.Min(k, fr.Length - 1)]);
                int cap = Mathf.Max(3, MaxPlaceable(fp) - surplus);       // sahne(req+fazla) ≤ maxPlaceable
                int req = Mathf.Clamp(rankReq, 3, cap);
                newObjs.Add(new LevelData.ObjectiveEntry { objectType = objTypes[k], required = req });
            }
            lv.objectives = newObjs;
            EditorUtility.SetDirty(lv);
            changed++;
            sb.AppendLine($"  L{i + 1}: " + string.Join(", ", newObjs.Select((o, k) => $"{o.objectType}(fp{Fp(o.objectType):F1})=req{o.required}+{SURPLUS[Mathf.Min(k, 3)]}")));
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        sb.AppendLine($"Toplam {changed} level değişti.");
        Debug.Log(sb.ToString());
    }

    // Footprint'e göre arenaya çakışmasız yerleşebilecek YAKLAŞIK adet (KONSERVATİF → ComposeCarCity hedefi kesin sığdırır).
    static int MaxPlaceable(float fp) =>
        fp <= 0.8f ? 50 : fp <= 1.3f ? 42 : fp <= 2.0f ? 30 : fp <= 2.6f ? 18 : fp <= 3.2f ? 11 : 7;

    static float MeasureFootprint(GameObject pf)
    {
        var go = Object.Instantiate(pf);
        var rs = go.GetComponentsInChildren<Renderer>();
        float fp = 1f;
        if (rs.Length > 0)
        {
            Bounds b = rs[0].bounds; for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            fp = Mathf.Max(b.size.x, b.size.z);
        }
        Object.DestroyImmediate(go);
        return fp;
    }
}
