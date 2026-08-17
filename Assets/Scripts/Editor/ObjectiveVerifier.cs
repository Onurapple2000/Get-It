using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// DOĞRULAMA (2026-08-06): Her levelda hedefler KARŞILANABİLİR mi? STATİK kontrol (BuildPreview değil — edit-mode'da
/// Awake/OnEnable çalışmadığından PhysicsSwallowable.All boş kalır, sayım yanıltıcı olur).
/// WINNABILITY = her objectType, o levelın SPAWN paletinde (prefab != null) VAR mı? Varsa LevelManager.
/// EnsureObjectiveCounts required adedi GARANTİLİ üretir (gerekirse çakışarak) → hedef kesin karşılanır. Palette'de
/// yoksa hiç doğmaz → karşılanamaz. Menü: Tools/GET_IT/Verify Objectives (static, all worlds).
/// </summary>
public static class ObjectiveVerifier
{
    [MenuItem("Tools/GET_IT/Verify Objectives (static, all worlds)")]
    public static void Run()
    {
        var sb = new StringBuilder("[ObjVerify] statik hedef karşılanabilirlik:\n");
        int bad = 0, levels = 0;
        for (int w = 0; w <= 17; w++)
            for (int i = 0; i < 15; i++)
            {
                var lv = AssetDatabase.LoadAssetAtPath<LevelData>($"Assets/Levels/World{w}_Level{i + 1}.asset");
                if (lv == null || lv.objectives == null || lv.objectives.Count == 0) continue;
                levels++;

                var palette = new HashSet<string>(
                    lv.spawns.Where(s => s.prefab != null).Select(s => s.prefab.name),
                    System.StringComparer.OrdinalIgnoreCase);
                foreach (var o in lv.objectives)
                    if (!palette.Contains(o.objectType))
                    {
                        sb.AppendLine($"  ❌ W{w} L{i + 1}: '{o.objectType}' PALETTE'de YOK → doğmaz (req {o.required})"); bad++;
                    }
            }
        sb.AppendLine(bad == 0
            ? $"✅ {levels} level tarandı — TÜM hedef türleri palette'de (EnsureObjectiveCounts garantisiyle required kesin karşılanır)."
            : $"❌ {bad} karşılanamaz hedef ({levels} level).");
        Debug.Log(sb.ToString());
    }
}
