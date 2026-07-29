using System.IO;
using UnityEditor;
using UnityEngine;
using UnityMeshSimplifier;

/// <summary>
/// TEK-SEFERLİK DÜZELTME (2026-07-25): PowerUp mesh'leri decimate EDİLMEMİŞ olarak kalmış
/// (PowerGrow ~127k, PowerSpeed ~76k, PowerMagnet ~67k vert; .asset'ler 6/3.6/3.2MB). Bu 3 nesne
/// neredeyse HER levelda 3'er tane → sabit ağır yük (mobil donma teşhisine katkı). PowerUpCreator
/// yeni .asset üretip decimate ediyordu ama mevcut prefablar ham mesh'e bağlı kalmış.
///
/// Bu araç mevcut mesh .asset'lerini YERİNDE decimate eder (CopySerialized → GUID/fileID korunur,
/// prefab referansı bozulmaz). UV kenar korumaları MeshDecimate ile aynı (beyaz benek fix'i).
/// Menu: Tools/GET_IT/Fix - Decimate PowerUp Meshes
/// </summary>
public static class PowerUpMeshFix
{
    const string MESH_DIR = "Assets/Prefabs/PowerUps/Meshes";
    const int TARGET_TRIS = 2500;   // basit şekiller (şimşek/mıknatıs/ok) — 2.5k fazlasıyla yeter

    [MenuItem("Tools/GET_IT/Fix - Decimate PowerUp Meshes")]
    public static void Run()
    {
        var guids = AssetDatabase.FindAssets("t:Mesh", new[] { MESH_DIR });
        int fixedCount = 0;
        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null) continue;

            int tris = mesh.triangles.Length / 3;
            if (tris <= TARGET_TRIS) { Debug.Log($"[PowerUpMeshFix] atlandı (zaten hafif {tris} tri): {path}"); continue; }

            float quality = Mathf.Clamp01((float)TARGET_TRIS / tris);

            // PowerUp'lar KÜÇÜK toplama nesneleri → agresif decimate: UV/border korumaları KAPALI
            // (bu korumalar Meshy'nin çok-adalı UV'sinde tüm kenar çökertmesini engelliyor → decimate işe yaramıyordu).
            // SmartLink çakışık (unwelded) vertexleri birleştirir → gerçek indirgeme olur.
            var opt = SimplificationOptions.Default;
            opt.PreserveBorderEdges = false;
            opt.PreserveUVSeamEdges = false;
            opt.PreserveUVFoldoverEdges = false;
            opt.EnableSmartLink = true;

            var ms = new MeshSimplifier { SimplificationOptions = opt };
            ms.Initialize(mesh);
            ms.SimplifyMesh(quality);
            var nm = ms.ToMesh();
            nm.RecalculateNormals();
            nm.RecalculateBounds();

            int newTris = nm.triangles.Length / 3;
            EditorUtility.CopySerialized(nm, mesh);   // aynı asset'e yaz → GUID korunur, prefab bağı bozulmaz
            EditorUtility.SetDirty(mesh);
            fixedCount++;
            Debug.Log($"[PowerUpMeshFix] {Path.GetFileName(path)}: {tris} → {newTris} tri");
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[PowerUpMeshFix] Bitti — {fixedCount} mesh decimate edildi (hedef ≤{TARGET_TRIS} tri).");
    }
}
