using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Son build'in (Library/LastBuild.buildreport) boyut kırılımını loglar: tür bazlı toplam + en büyük assetler.
/// Shader mı texture mı mesh mi şişiriyor net görmek için. Menu: Tools/GET_IT/Dump Last Build Report
/// </summary>
public static class BuildReportDump
{
    [MenuItem("Tools/GET_IT/Dump Last Build Report")]
    public static void Dump()
    {
        const string src = "Library/LastBuild.buildreport";
        if (!File.Exists(src)) { Debug.LogError("[BuildReport] Library/LastBuild.buildreport yok (önce build al)."); return; }

        const string dst = "Assets/_LastBuild.buildreport";
        File.Copy(src, dst, true);
        AssetDatabase.ImportAsset(dst);
        var report = AssetDatabase.LoadAssetAtPath<BuildReport>(dst);
        if (report == null) { Debug.LogError("[BuildReport] yüklenemedi."); AssetDatabase.DeleteAsset(dst); return; }

        var byType = new Dictionary<string, ulong>();
        var byAsset = new List<(string path, string type, ulong size)>();
        ulong total = 0;

        foreach (var pa in report.packedAssets)
            foreach (var e in pa.contents)
            {
                string t = e.type != null ? e.type.Name : "?";
                byType.TryGetValue(t, out var v);
                byType[t] = v + e.packedSize;
                total += e.packedSize;
                byAsset.Add((e.sourceAssetPath, t, e.packedSize));
            }

        var sb = new StringBuilder();
        sb.AppendLine($"=== BUILD REPORT — packed toplam: {total / 1048576f:0.1} MB ===");
        var types = new List<KeyValuePair<string, ulong>>(byType);
        types.Sort((a, b) => b.Value.CompareTo(a.Value));
        sb.AppendLine("-- Tür bazlı --");
        foreach (var kv in types)
            if (kv.Value > 524288) sb.AppendLine($"{kv.Value / 1048576f,8:0.1} MB  {kv.Key}");

        byAsset.Sort((a, b) => b.size.CompareTo(a.size));
        sb.AppendLine("-- En büyük 20 asset --");
        for (int i = 0; i < byAsset.Count && i < 20; i++)
            sb.AppendLine($"{byAsset[i].size / 1048576f,8:0.1} MB  [{byAsset[i].type}] {byAsset[i].path}");

        Debug.Log(sb.ToString());
        AssetDatabase.DeleteAsset(dst);
    }
}
