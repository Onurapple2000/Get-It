using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Convex MeshCollider için GARANTİLİ düşük-vertex collider mesh üretir (Sweets/Buildings/Drinks ortak).
///
/// SORUN: Unity/PhysX convex cook sınırı 256 poly. Eski yöntemler bunu garanti edemiyordu:
///   - Sweets/Buildings: render mesh kopyası (5000-8000 tri) → "Couldn't create a Convex Mesh ... partial hull"
///     uyarısı + yükleme hitch + hafif yanlış collider.
///   - Drinks: MeshSimplifier hedef 180 tri — ama çok-parçalı setlerde (çay seti) component koruyup ~1000
///     tri'de KALABİLİYOR → yine >256.
///
/// ÇÖZÜM: VERTEX CLUSTERING (voxel-grid). Vertexler ızgara hücrelerine kümelenir, hücre temsilcisi =
/// ortalama. Izgara çözünürlüğü binary search ile "≤ maxVerts benzersiz hücre" olacak şekilde seçilir →
/// MATEMATİKSEL GARANTİ: convex hull üçgen sayısı ≤ 2V-4 (V=100 → ≤196 < 256). MeshSimplifier gibi
/// takılmaz; tek başına kalan sivri tepe (apex) kendi hücresinde yalnızdır → ortalama = kendisi → külah/
/// kule sivri ucu korunur (ters düşünce devrilme davranışı bozulmaz).
///
/// Akış: tüm alt mesh'ler meshRoot-yerel uzayda BİRLEŞTİRİLİR (çok-parçalı setler kapsanır) → XZ daraltma
/// (yuvarlak deliğe girsin; apex merkezde kalır) → clustering. Dönen mesh'i meshRoot'a MeshCollider
/// (convex=true) olarak tak. Orijinal render mesh'lerine DOKUNMAZ.
/// </summary>
public static class ColliderHullUtil
{
    /// <summary>Hull ≤ 2V-4 = 196 üçgen → 256 sınırına güvenli pay.</summary>
    public const int MAX_VERTS = 100;

    /// <summary>
    /// meshRoot altındaki TÜM mesh'lerden meshRoot-yerel uzayda düşük-vertex convex collider mesh'i üretir.
    /// Mesh yoksa null döner (çağıran BoxCollider yedeğine düşer).
    /// </summary>
    public static Mesh BuildConvexColliderMesh(GameObject meshRoot, float xzShrink, string name, int maxVerts = MAX_VERTS)
    {
        // 1) Birleştir (meshRoot-yerel uzay; çocuk transform/scale'ler pişirilir).
        var mfs = meshRoot.GetComponentsInChildren<MeshFilter>();
        var combines = new List<CombineInstance>();
        Matrix4x4 rootInv = meshRoot.transform.worldToLocalMatrix;
        foreach (var mf in mfs)
        {
            if (mf.sharedMesh == null) continue;
            combines.Add(new CombineInstance { mesh = mf.sharedMesh, transform = rootInv * mf.transform.localToWorldMatrix });
        }
        if (combines.Count == 0) return null;

        var combined = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        combined.CombineMeshes(combines.ToArray(), true, true);

        // 2) XZ daraltma (ayak izi küçülür → yuvarlak deliğe girer; Y ve XZ-merkezdeki apex korunur).
        var verts = combined.vertices;
        var tris = combined.triangles;
        Vector3 c = combined.bounds.center;
        for (int i = 0; i < verts.Length; i++)
        {
            verts[i].x = c.x + (verts[i].x - c.x) * xzShrink;
            verts[i].z = c.z + (verts[i].z - c.z) * xzShrink;
        }

        int srcVertCount = verts.Length;
        Vector3[] outVerts;
        int[] outTris;

        if (srcVertCount <= maxVerts)
        {
            outVerts = verts;
            outTris = tris;
        }
        else
        {
            Cluster(verts, tris, maxVerts, out outVerts, out outTris);
        }

        Object.DestroyImmediate(combined);

        var m = new Mesh { name = name + "_col", indexFormat = UnityEngine.Rendering.IndexFormat.UInt16 };
        m.vertices = outVerts;
        m.triangles = outTris;
        m.RecalculateBounds();

        if (srcVertCount > maxVerts)
            Debug.Log($"[ColliderHull] {name}: {srcVertCount}v → {outVerts.Length}v (hull ≤{2 * outVerts.Length - 4} tri, sınır 256)");
        return m;
    }

    /// <summary>
    /// Vertex clustering: bounds'u R×R×R ızgaraya böl, hücre temsilcisi = hücredeki vertexlerin ortalaması.
    /// En büyük R (en çok detay) öyle seçilir ki benzersiz hücre sayısı ≤ maxVerts (binary search 1..64).
    /// </summary>
    static void Cluster(Vector3[] verts, int[] tris, int maxVerts, out Vector3[] outVerts, out int[] outTris)
    {
        Bounds b = new Bounds(verts[0], Vector3.zero);
        for (int i = 1; i < verts.Length; i++) b.Encapsulate(verts[i]);
        Vector3 min = b.min;
        // Dejenere eksen (düz model) bölme hatası vermesin.
        Vector3 size = Vector3.Max(b.size, Vector3.one * 1e-5f);

        int lo = 1, hi = 64, best = 1;
        while (lo <= hi)
        {
            int mid = (lo + hi) / 2;
            if (CountCells(verts, min, size, mid) <= maxVerts) { best = mid; lo = mid + 1; }
            else hi = mid - 1;
        }

        // best çözünürlükte temsilcileri üret.
        int r = best;
        var cellIndex = new Dictionary<long, int>();          // hücre anahtarı → çıkış vertex index'i
        var sums = new List<Vector3>();
        var counts = new List<int>();
        var vertMap = new int[verts.Length];                  // eski vertex → çıkış index
        for (int i = 0; i < verts.Length; i++)
        {
            long key = CellKey(verts[i], min, size, r);
            if (!cellIndex.TryGetValue(key, out int idx))
            {
                idx = sums.Count;
                cellIndex.Add(key, idx);
                sums.Add(Vector3.zero);
                counts.Add(0);
            }
            sums[idx] += verts[i];
            counts[idx]++;
            vertMap[i] = idx;
        }
        outVerts = new Vector3[sums.Count];
        for (int i = 0; i < sums.Count; i++) outVerts[i] = sums[i] / counts[i];

        // Üçgenleri yeniden eşle; dejenere olanları at. (Convex cook vertexlerden hull üretir —
        // üçgenler yalnız mesh geçerliliği için; azalmaları sorun değil.)
        var kept = new List<int>(tris.Length);
        for (int i = 0; i < tris.Length; i += 3)
        {
            int a = vertMap[tris[i]], b2 = vertMap[tris[i + 1]], c2 = vertMap[tris[i + 2]];
            if (a == b2 || b2 == c2 || a == c2) continue;
            kept.Add(a); kept.Add(b2); kept.Add(c2);
        }
        if (kept.Count == 0)
        {
            // Teorik yedek: tüm üçgenler dejenere → basit fan (mesh geçerli kalsın).
            for (int i = 0; i + 2 < outVerts.Length; i++) { kept.Add(0); kept.Add(i + 1); kept.Add(i + 2); }
        }
        outTris = kept.ToArray();
    }

    static int CountCells(Vector3[] verts, Vector3 min, Vector3 size, int r)
    {
        var cells = new HashSet<long>();
        for (int i = 0; i < verts.Length; i++) cells.Add(CellKey(verts[i], min, size, r));
        return cells.Count;
    }

    static long CellKey(Vector3 v, Vector3 min, Vector3 size, int r)
    {
        long x = (long)Mathf.Clamp((int)((v.x - min.x) / size.x * r), 0, r - 1);
        long y = (long)Mathf.Clamp((int)((v.y - min.y) / size.y * r), 0, r - 1);
        long z = (long)Mathf.Clamp((int)((v.z - min.z) / size.z * r), 0, r - 1);
        return (x << 42) | (y << 21) | z;
    }
}
