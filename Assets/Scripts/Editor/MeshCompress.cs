using UnityEditor;
using UnityEngine;

/// <summary>
/// Model mesh'lerine Unity vertex QUANTIZATION (Mesh Compression) uygular → build/indirme boyutu düşer,
/// runtime GPU boyutu aynı kalır, görsel fark ~yok (pozisyon 16-bit vb.). glTFast alt-asset mesh'leri +
/// decimate .asset mesh'leri hedefler. Menu: Tools/GET_IT/Compress Model Meshes (High)
/// NOT: glTFast alt-asset mesh'lerinde kalıcılık garantisiz olabilir → etkiyi build report'ta ölçeriz.
/// </summary>
public static class MeshCompress
{
    static readonly string[] GlbDirs = { "Assets/Art/worlds/cars", "Assets/Art/worlds/foods",
                                         "Assets/Art/PowerUps", "Assets/Models/DecoObjects" };
    static readonly string[] MeshDirs = { "Assets/Prefabs/Cars/Meshes", "Assets/Prefabs/Foods/Meshes",
                                          "Assets/Prefabs/PowerUps/Meshes" };

    [MenuItem("Tools/GET_IT/Compress Model Meshes (High)")]
    public static void Run()
    {
        int n = 0;

        // GLB alt-asset mesh'leri
        foreach (var d in GlbDirs)
        {
            if (!AssetDatabase.IsValidFolder(d)) continue;
            foreach (var g in AssetDatabase.FindAssets("t:GameObject", new[] { d }))
            {
                string p = AssetDatabase.GUIDToAssetPath(g);
                if (!p.EndsWith(".glb")) continue;
                foreach (var o in AssetDatabase.LoadAllAssetsAtPath(p))
                    if (o is Mesh m) { MeshUtility.SetMeshCompression(m, ModelImporterMeshCompression.High); n++; }
            }
        }

        // Ayrı .asset mesh'ler (decimate çıktısı)
        foreach (var d in MeshDirs)
        {
            if (!AssetDatabase.IsValidFolder(d)) continue;
            foreach (var g in AssetDatabase.FindAssets("t:Mesh", new[] { d }))
            {
                var m = AssetDatabase.LoadAssetAtPath<Mesh>(AssetDatabase.GUIDToAssetPath(g));
                if (m != null) { MeshUtility.SetMeshCompression(m, ModelImporterMeshCompression.High); n++; }
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[MeshCompress] {n} mesh'e High quantization uygulandı. Etkiyi sonraki build report'ta göreceğiz.");
    }
}
