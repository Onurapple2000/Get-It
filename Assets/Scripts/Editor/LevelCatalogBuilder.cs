using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Resources/LevelCatalog.asset'i tüm LevelData asset'lerinden üretir/yeniler.
/// Menü: Tools/GET_IT/Asset Delivery/Rebuild Level Catalog. Player build öncesi OTOMATİK çalışır (bayat kalmaz).
/// </summary>
public class LevelCatalogBuilder : IPreprocessBuildWithReport
{
    const string AssetPath = "Assets/Resources/" + LevelCatalog.ResourceName + ".asset";

    public int callbackOrder => -50;
    public void OnPreprocessBuild(BuildReport report) { Rebuild(); }

    [MenuItem("Tools/GET_IT/Asset Delivery/Rebuild Level Catalog")]
    public static void Rebuild()
    {
        var cat = AssetDatabase.LoadAssetAtPath<LevelCatalog>(AssetPath);
        if (cat == null)
        {
            Directory.CreateDirectory("Assets/Resources");
            cat = ScriptableObject.CreateInstance<LevelCatalog>();
            AssetDatabase.CreateAsset(cat, AssetPath);
        }
        cat.all.Clear();
        foreach (var g in AssetDatabase.FindAssets("t:LevelData", new[] { "Assets/Levels" }))
        {
            var ld = AssetDatabase.LoadAssetAtPath<LevelData>(AssetDatabase.GUIDToAssetPath(g));
            if (ld != null) cat.all.Add(ld);
        }
        cat.all.Sort((a, b) => a.worldId != b.worldId ? a.worldId.CompareTo(b.worldId) : a.levelIndex.CompareTo(b.levelIndex));
        EditorUtility.SetDirty(cat);
        AssetDatabase.SaveAssets();
        Debug.Log($"[LevelCatalog] {cat.all.Count} level kataloglandı → {AssetPath}");
    }
}
