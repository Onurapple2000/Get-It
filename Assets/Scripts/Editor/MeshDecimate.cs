using System.IO;
using UnityEditor;
using UnityEngine;
using UnityMeshSimplifier;

/// <summary>
/// GLB (Meshy) modellerini Unity içinde mobil-dostu hâle getirme: mesh decimation (UnityMeshSimplifier)
/// + texture küçültme (MeshyImport). Meshy'de Remesh çalışmadığından geometri burada azaltılır.
/// </summary>
public static class MeshDecimate
{
    /// <summary>instance içindeki tüm mesh'leri hedef TOPLAM üçgen sayısına indirger; baked mesh'leri kaydeder.</summary>
    public static void DecimateInstance(GameObject instance, int targetTotalTris, string meshDir, string prefix)
    {
        var mfs = instance.GetComponentsInChildren<MeshFilter>();
        long total = 0;
        foreach (var mf in mfs) if (mf.sharedMesh != null) total += mf.sharedMesh.triangles.Length / 3;
        if (total <= targetTotalTris || total == 0) return;

        float quality = Mathf.Clamp01((float)targetTotalTris / total);
        EnsureDir(meshDir);

        int idx = 0;
        foreach (var mf in mfs)
        {
            var src = mf.sharedMesh;
            if (src == null) continue;

            var ms = new MeshSimplifier();
            // ⚠️ UV KORUMASI (beyaz benek fix'i): varsayılan ayarlarla kenar çökertme, üçgen UV'lerini atlas
            // parça sınırlarının DIŞINA (beyaz arka plana) taşırıyordu → yüzeyde mesh'e sabit BEYAZ lekeler/noktalar.
            // Border/UV-seam/foldover kenarlarını koru → UV'ler parça içinde kalır, benek oluşmaz.
            var opt = SimplificationOptions.Default;
            opt.PreserveBorderEdges = true;
            opt.PreserveUVSeamEdges = true;
            opt.PreserveUVFoldoverEdges = true;
            ms.SimplificationOptions = opt;
            ms.Initialize(src);
            ms.SimplifyMesh(quality);
            var nm = ms.ToMesh();
            nm.name = src.name + "_lod";
            nm.RecalculateNormals();
            nm.RecalculateBounds();

            string p = $"{meshDir}/{prefix}_{idx++}.asset";
            AssetDatabase.CreateAsset(nm, p);
            mf.sharedMesh = nm;
        }
    }

    static void EnsureDir(string assetPath)
    {
        string full = Path.Combine(Application.dataPath, assetPath.Substring("Assets/".Length));
        if (!Directory.Exists(full)) { Directory.CreateDirectory(full); AssetDatabase.Refresh(); }
    }
}
