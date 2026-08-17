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
/// ÇÖZÜM: VERTEX CLUSTERING (voxel-grid). Vertexler ızgara hücrelerine kümelenir, hücre temsilcisi = hücrede
/// MERKEZDEN EN UZAK (yüzeydeki) vertex (2026-08-03: eski ortalama YÜZEYİ İÇE gömüyordu → rim görseli kesiyordu).
/// Izgara çözünürlüğü binary search ile "≤ maxVerts benzersiz hücre" olacak şekilde seçilir →
/// MATEMATİKSEL GARANTİ: convex hull üçgen sayısı ≤ 2V-4 (V=100 → ≤196 < 256). MeshSimplifier gibi
/// takılmaz; sivri tepe (apex) kendi hücresinde yalnızdır → temsilci = kendisi → külah/kule sivri ucu korunur.
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
            // ⚠️ KÜMELEME TELAFİSİ (2026-08-03): hücre ORTALAMASI temsilcileri yüzeyin İÇİNE çeker → hull ayak izi
            // ~%10-15 küçülür (collider görselden küçük → nesneler yan yana İÇ İÇE + delikten büyük olsa da GEÇER,
            // kullanıcı). XZ'yi kümeleme-ÖNCESİ extent'e geri ölçekle → footprint = xzShrink×görsel (DÜRÜST).
            // Merkez c etrafında; Y'ye DOKUNMA (yükseklik/apex korunur). Sadece BÜYÜT (telafi), asla küçültme.
            float sMinX = 1e9f, sMaxX = -1e9f, sMinZ = 1e9f, sMaxZ = -1e9f;
            for (int i = 0; i < verts.Length; i++) { var p = verts[i]; if (p.x < sMinX) sMinX = p.x; if (p.x > sMaxX) sMaxX = p.x; if (p.z < sMinZ) sMinZ = p.z; if (p.z > sMaxZ) sMaxZ = p.z; }
            float oMinX = 1e9f, oMaxX = -1e9f, oMinZ = 1e9f, oMaxZ = -1e9f;
            for (int i = 0; i < outVerts.Length; i++) { var p = outVerts[i]; if (p.x < oMinX) oMinX = p.x; if (p.x > oMaxX) oMaxX = p.x; if (p.z < oMinZ) oMinZ = p.z; if (p.z > oMaxZ) oMaxZ = p.z; }
            float fx = (oMaxX - oMinX) > 1e-4f ? Mathf.Clamp((sMaxX - sMinX) / (oMaxX - oMinX), 1f, 1.6f) : 1f;
            float fz = (oMaxZ - oMinZ) > 1e-4f ? Mathf.Clamp((sMaxZ - sMinZ) / (oMaxZ - oMinZ), 1f, 1.6f) : 1f;
            for (int i = 0; i < outVerts.Length; i++) { outVerts[i].x = c.x + (outVerts[i].x - c.x) * fx; outVerts[i].z = c.z + (outVerts[i].z - c.z) * fz; }
        }

        // ⚠️ DÜZ TABAN (2026-07-31): clustering hücre ORTALAMASI hull dibini (a) gerçek tabandan yukarı kaçırıyor
        // (uyanınca GÖRSEL BATMA) (b) tek NOKTAYA indiriyor (bina kısmi oyulunca see-saw gibi eğik/batık KALIYOR,
        // düzelemiyor — kullanıcı). Çözüm: gerçek tabanda (trueMinY) AYAK-İZİ dörtgeni ekle → hull DÜZ TABANLI:
        // dip=görsel taban (batma yok), delik yeterince oyunca deliğe DEVRİLİR (kullanıcı seviyor), delik çekilince
        // kalan zeminde KENDİNİ DÜZLER (eğik/batık kalmaz).
        {
            float tMinY = float.PositiveInfinity, tMaxY = float.NegativeInfinity;
            for (int i = 0; i < verts.Length; i++) { if (verts[i].y < tMinY) tMinY = verts[i].y; if (verts[i].y > tMaxY) tMaxY = verts[i].y; }
            float band = tMinY + 0.12f * Mathf.Max(1e-4f, tMaxY - tMinY);
            float xMin = 1e9f, xMax = -1e9f, zMin = 1e9f, zMax = -1e9f;
            for (int i = 0; i < verts.Length; i++)
                if (verts[i].y <= band)
                { var p = verts[i]; if (p.x < xMin) xMin = p.x; if (p.x > xMax) xMax = p.x; if (p.z < zMin) zMin = p.z; if (p.z > zMax) zMax = p.z; }
            if (xMax > xMin && zMax > zMin)
            {
                int b0 = outVerts.Length;
                System.Array.Resize(ref outVerts, b0 + 4);
                outVerts[b0]     = new Vector3(xMin, tMinY, zMin);
                outVerts[b0 + 1] = new Vector3(xMax, tMinY, zMin);
                outVerts[b0 + 2] = new Vector3(xMax, tMinY, zMax);
                outVerts[b0 + 3] = new Vector3(xMin, tMinY, zMax);
                int t0 = outTris.Length;
                System.Array.Resize(ref outTris, t0 + 6);
                outTris[t0] = b0; outTris[t0 + 1] = b0 + 1; outTris[t0 + 2] = b0 + 2;
                outTris[t0 + 3] = b0; outTris[t0 + 4] = b0 + 2; outTris[t0 + 5] = b0 + 3;
            }
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
    /// Vertex clustering: bounds'u R×R×R ızgaraya böl, hücre temsilcisi = hücrede MERKEZDEN EN UZAK (yüzeydeki) vertex.
    /// ⚠️ 2026-08-03 (kullanıcı: bazı nesnelerde delik ağzı bıçak gibi kesiyor): eskiden temsilci = hücre ORTALAMASIYDI →
    /// ortalama noktalar YÜZEYİN İÇİNDE kalır → hull görsel meshin İÇİNE gömülür (her Y seviyesinde) → nesne yarı-delikte
    /// hızlı sürüklenince rim, hull'un DIŞINA taşan görsel meshi keser. EN UZAK vertex = yüzeyde → hull görseli SARAR
    /// (kesme yok). Hull yine ≤ maxVerts vertex (cook güvenli). En büyük R binary search ile ≤ maxVerts hücre.
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

        // best çözünürlükte temsilcileri üret: her hücrede merkezden EN UZAK vertex (yüzeyi sarar, içe gömmez).
        int r = best;
        Vector3 ctr = b.center;
        var cellIndex = new Dictionary<long, int>();          // hücre anahtarı → çıkış vertex index'i
        var reps = new List<Vector3>();                        // hücre temsilcisi (merkezden en uzak)
        var repD = new List<float>();                          // temsilcinin merkeze uzaklık² (daha uzağı gelirse değişir)
        var vertMap = new int[verts.Length];                  // eski vertex → çıkış index
        for (int i = 0; i < verts.Length; i++)
        {
            long key = CellKey(verts[i], min, size, r);
            float d = (verts[i] - ctr).sqrMagnitude;
            if (!cellIndex.TryGetValue(key, out int idx))
            {
                idx = reps.Count;
                cellIndex.Add(key, idx);
                reps.Add(verts[i]);
                repD.Add(d);
            }
            else if (d > repD[idx]) { reps[idx] = verts[i]; repD[idx] = d; }
            vertMap[i] = idx;
        }
        outVerts = reps.ToArray();

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

// (touch 1785504154 to force Unity asset re-scan)
