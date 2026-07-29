using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// TANI ARACI — mesh'lerde GERÇEK DELİK/ÇATLAK var mı? Vertexleri POZİSYONA göre kaynakla (UV-seam kopyaları
/// birleşir), sonra tam 1 üçgene ait kenarları (boundary edge = açık kenar = delik sınırı) say. Watertight
/// mesh'te 0 olmalı. Decimation dikiş çatlağı açtıysa yüzlerce çıkar → cihazdaki "beyaz nokta + gölge deliği"
/// teorisini build almadan kanıtlar/çürütür. Menü: Tools/GET_IT/Analyze Mesh Holes.
/// </summary>
public static class MeshHoleAnalyzer
{
    static readonly string[] Paths =
    {
        "Assets/Prefabs/PowerUps/Meshes/PowerMagnet_0.asset",
        "Assets/Prefabs/PowerUps/Meshes/PowerSpeed_0.asset",
        "Assets/Prefabs/PowerUps/Meshes/PowerGrow_0.asset",
        "Assets/Prefabs/Drinks/Meshes/CoffeeCompanion_0.asset",
        "Assets/Prefabs/Drinks/Meshes/CoffeeCup_0.asset",       // temiz görünen kıyas
        "Assets/Prefabs/Foods/Meshes/Tomato_0.asset",           // eski dünya kıyası (varsa)
    };

    [MenuItem("Tools/GET_IT/Analyze Mesh Holes")]
    public static void Run()
    {
        // Kedi mesh'i prefab'dan (dosya adı bilinmiyor olabilir)
        var cat = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Cats/Cat.prefab");
        if (cat != null)
            foreach (var mf in cat.GetComponentsInChildren<MeshFilter>(true))
                if (mf.sharedMesh != null) Analyze(mf.sharedMesh, "Cat/" + mf.sharedMesh.name);
        if (cat != null)
            foreach (var smr in cat.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (smr.sharedMesh != null) Analyze(smr.sharedMesh, "Cat(skinned)/" + smr.sharedMesh.name);

        foreach (var p in Paths)
        {
            var m = AssetDatabase.LoadAssetAtPath<Mesh>(p);
            if (m == null) { Debug.Log($"[HoleAnalyzer] yok: {p}"); continue; }
            Analyze(m, System.IO.Path.GetFileNameWithoutExtension(p));
        }
    }

    static void Analyze(Mesh mesh, string label)
    {
        var verts = mesh.vertices;
        var tris = mesh.triangles;

        // Pozisyona göre kaynak (weld): quantize edilmiş pozisyon → kanonik indeks.
        float eps = Mathf.Max(1e-6f, mesh.bounds.size.magnitude * 1e-5f);
        var canon = new int[verts.Length];
        var map = new Dictionary<Vector3Int, int>(verts.Length);
        for (int i = 0; i < verts.Length; i++)
        {
            var k = new Vector3Int(Mathf.RoundToInt(verts[i].x / eps),
                                   Mathf.RoundToInt(verts[i].y / eps),
                                   Mathf.RoundToInt(verts[i].z / eps));
            if (!map.TryGetValue(k, out int c)) { map[k] = i; c = i; }
            canon[i] = c;
        }

        // Kenar → kaç üçgen kullanıyor
        var edges = new Dictionary<long, int>(tris.Length);
        void AddEdge(int a, int b)
        {
            if (a == b) return;                        // dejenere
            long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
            edges.TryGetValue(key, out int c);
            edges[key] = c + 1;
        }
        int degenerate = 0;
        for (int t = 0; t < tris.Length; t += 3)
        {
            int a = canon[tris[t]], b = canon[tris[t + 1]], c = canon[tris[t + 2]];
            if (a == b || b == c || a == c) { degenerate++; continue; }
            AddEdge(a, b); AddEdge(b, c); AddEdge(c, a);
        }

        int boundary = 0, overShared = 0;
        foreach (var kv in edges)
        {
            if (kv.Value == 1) boundary++;
            else if (kv.Value > 2) overShared++;
        }

        Debug.Log($"[HoleAnalyzer] {label}: tris={tris.Length / 3} verts={verts.Length} " +
                  $"AÇIK-KENAR={boundary} (delik sınırı; watertight=0) aşırı-paylaşım={overShared} dejenere={degenerate}");
    }
}
