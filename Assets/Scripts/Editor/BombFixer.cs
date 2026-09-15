using UnityEditor;
using UnityEngine;

/// <summary>
/// Kural: L1-L2 bombasız (öğrenme), **L3'ten itibaren HER level bombalı**.
/// 2026-08-22 tespiti: bu kural bazı dünyalarda bozulmuştu — W1/W2'de L6-L7 boştu,
/// W3 (Binalar, 2026-07-31'de sıfırdan kuruldu) yalnız HARD levellarda (L5/L10/L15) bomba içeriyordu.
///
/// Bu araç YALNIZCA eksikleri doldurur; mevcut bombCount değerlerine DOKUNMAZ
/// (hard levellardaki özel yüksek değerler korunur). Referans rampa W0'dan (Park) alındı.
/// </summary>
public static class BombFixer
{
    const string BombPath = "Assets/Prefabs/Bomb.prefab";
    const int MaxWorld = 17, MaxLevel = 15;

    // Level no (1-tabanlı) → bomba sayısı. W0'ın mevcut, dengeli rampası: levella birlikte artar.
    static int RampCount(int level) => level switch
    {
        3 or 4 => 3,
        5 or 6 => 4,
        7 or 8 => 5,
        9 or 10 => 6,
        11 or 12 => 7,
        13 or 14 => 8,
        _ => 9,
    };

    [MenuItem("Tools/GET_IT/Fix Missing Bombs")]
    public static void FixMissingBombs()
    {
        var bomb = AssetDatabase.LoadAssetAtPath<GameObject>(BombPath);
        if (bomb == null) { Debug.LogError($"[BombFixer] Bomba prefab'ı bulunamadı: {BombPath}"); return; }

        int fixedCount = 0;
        for (int w = 0; w <= MaxWorld; w++)
        {
            for (int n = 3; n <= MaxLevel; n++)
            {
                string path = $"Assets/Levels/World{w}_Level{n}.asset";
                var data = AssetDatabase.LoadAssetAtPath<LevelData>(path);
                if (data == null) continue;
                if (data.bombsEnabled && data.bombPrefab != null && data.bombCount > 0) continue;   // zaten tamam

                data.bombsEnabled = true;
                data.bombPrefab = bomb;
                if (data.bombCount <= 0) data.bombCount = RampCount(n);
                EditorUtility.SetDirty(data);
                fixedCount++;
                Debug.Log($"[BombFixer] Düzeltildi: World{w}_Level{n} → bombCount={data.bombCount}");
            }
        }

        if (fixedCount > 0) { AssetDatabase.SaveAssets(); AssetDatabase.Refresh(); }
        Debug.Log($"[BombFixer] TAMAM — {fixedCount} level düzeltildi.");
    }

    /// <summary>Sadece raporlar, hiçbir şeyi değiştirmez — düzeltme öncesi/sonrası doğrulama için.</summary>
    [MenuItem("Tools/GET_IT/Report Bomb Coverage")]
    public static void ReportBombCoverage()
    {
        var sb = new System.Text.StringBuilder("[BombFixer] L3+ bombasız levellar:\n");
        int missing = 0;
        for (int w = 0; w <= MaxWorld; w++)
        {
            string miss = "";
            for (int n = 3; n <= MaxLevel; n++)
            {
                var data = AssetDatabase.LoadAssetAtPath<LevelData>($"Assets/Levels/World{w}_Level{n}.asset");
                if (data == null) continue;
                if (!data.bombsEnabled || data.bombPrefab == null || data.bombCount <= 0) { miss += $" L{n}"; missing++; }
            }
            if (miss.Length > 0) sb.AppendLine($"  World{w}:{miss}");
        }
        sb.AppendLine(missing == 0 ? "  (yok — tüm L3+ levellar bombalı ✓)" : $"  TOPLAM: {missing}");
        Debug.Log(sb.ToString());
    }
}
