using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Tanı: prefablardaki mesh üçgen sayısı + texture çözünürlüklerini loglar (performans teşhisi).
/// Menu: Tools/GET_IT/Log Model Stats
/// </summary>
public static class ModelStats
{
    [MenuItem("Tools/GET_IT/Log Model Stats")]
    public static void Run()
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== MODEL STATS (perf teşhisi) ===");
        Report(sb, "Assets/Prefabs/Foods");
        Report(sb, "Assets/Prefabs/Cars");
        Report(sb, "Assets/Prefabs/PowerUps");
        Report(sb, "Assets/Prefabs/DecoObjects");
        Debug.Log(sb.ToString());
    }

    [MenuItem("Tools/GET_IT/Log GLB Stats")]
    public static void RunGlb()
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== GLB STATS (ham modeller) ===");
        ReportGlb(sb, "Assets/Art/PowerUps");
        ReportGlb(sb, "Assets/Art/worlds/foods");
        Debug.Log(sb.ToString());
    }

    static void ReportGlb(StringBuilder sb, string dir)
    {
        sb.AppendLine($"\n--- {dir} ---");
        var guids = AssetDatabase.FindAssets("t:GameObject", new[] { dir });
        foreach (var g in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(g);
            if (!path.EndsWith(".glb")) continue;
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go == null) continue;
            long tris = 0;
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>())
                if (mf.sharedMesh != null) tris += mf.sharedMesh.triangles.Length / 3;
            var texSizes = new HashSet<string>();
            foreach (var r in go.GetComponentsInChildren<Renderer>())
                foreach (var m in r.sharedMaterials)
                {
                    if (m == null) continue;
                    var t = m.GetTexture("_BaseMap") ?? m.mainTexture;
                    if (t != null) texSizes.Add($"{t.width}x{t.height}");
                }
            sb.AppendLine($"{System.IO.Path.GetFileName(path),-60} tris={tris,8:n0}  tex=[{string.Join(",", texSizes)}]");
        }
    }

    static void Report(StringBuilder sb, string dir)
    {
        sb.AppendLine($"\n--- {dir} ---");
        var guids = AssetDatabase.FindAssets("t:Prefab", new[] { dir });
        long grand = 0;
        foreach (var g in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(g);
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go == null) continue;

            long tris = 0;
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>())
                if (mf.sharedMesh != null) tris += mf.sharedMesh.triangles.Length / 3;

            var texSizes = new HashSet<string>();
            foreach (var r in go.GetComponentsInChildren<Renderer>())
                foreach (var m in r.sharedMaterials)
                {
                    if (m == null) continue;
                    var t = m.GetTexture("_BaseMap") ?? m.mainTexture;
                    if (t != null) texSizes.Add($"{t.width}x{t.height}");
                }

            grand += tris;
            sb.AppendLine($"{go.name,-16} tris={tris,8:n0}   tex=[{string.Join(",", texSizes)}]");
        }
        sb.AppendLine($"TOPLAM tris (bu klasör): {grand:n0}");
    }
}
