using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// BİNALAR (World3): her bina prefabına GÖRSEL TABANA oturan ince bir BoxCollider ("_BaseCollider") ekler.
/// Neden: bina prefabları convex MeshCollider (ayrı/basit mesh) kullanıyor; onun tabanı GÖRSEL tabandan yukarıda →
/// bina uyanıp yere oturunca görsel taban altta kalıp "batık" görünüyor + "delik üstten giriyor". Taban plakasına
/// (mesh'in en alt dilimindeki gerçek ayak izi) oturan ince bir kutu → binanın en alçak çarpışma noktası = görsel
/// taban → görsel taban zeminde durur. Mevcut MeshCollider KORUNUR. Idempotent (varsa atlar). Menu: Tools/GET_IT.
/// Geri almak için: "Remove Base Colliders (Buildings)".
/// </summary>
public static class BuildingColliderFixer
{
    const string CHILD = "_BaseCollider";
    const float SLAB = 0.12f;   // en-alt dilim yüksekliği (taban ayak izini örneklemek için) — prefab-yerel birim
    // 2026-07-30: 0.10 → 0.60. İnce pad ağır/ölçekli binalarda TÜNELLEME yapıyordu (bir frame'de zemini geçip
    // batıyordu). Kalın kutu (görsel tabandan YUKARI doğru uzar; alt yüz hâlâ minY'de → görsel taban zeminde flush,
    // batma yok) sağlam çarpışma verir. Kutu YUKARI uzadığı için havada durma/float OLMAZ.
    const float BOXH = 0.60f;   // eklenen kutunun yüksekliği (KALIN pad, tünellemeyi önler)

    // MeshCollider kullanan (görsel tabandan yukarıda → uyanınca batan) dünyalar. Foods/Cars/DecoObjects/PowerUps
    // BoxCollider kullanır (gap=0, batmaz) → HARİÇ. Buildings zaten yapıldı → ayrı menü.
    static readonly string[] MESH_WORLDS = {
        "Assets/Prefabs/Books", "Assets/Prefabs/Cats", "Assets/Prefabs/Dogs", "Assets/Prefabs/Drinks",
        "Assets/Prefabs/Gifts", "Assets/Prefabs/Jewelry", "Assets/Prefabs/Money", "Assets/Prefabs/Planes",
        "Assets/Prefabs/Ships", "Assets/Prefabs/Sweets",
    };

    [MenuItem("Tools/GET_IT/Add Base Colliders (Mesh Worlds - Foods hariç)")]
    public static void AddBaseCollidersMeshWorlds() => AddBaseColliders(MESH_WORLDS);

    [MenuItem("Tools/GET_IT/Remove Base Colliders (Mesh Worlds)")]
    public static void RemoveBaseCollidersMeshWorlds() => RemoveBaseColliders(MESH_WORLDS);

    [MenuItem("Tools/GET_IT/Add Base Colliders (Buildings)")]
    public static void AddBaseCollidersBuildings() => AddBaseColliders(new[] { "Assets/Prefabs/Buildings" });

    public static void AddBaseColliders(string[] dirs)
    {
        var guids = AssetDatabase.FindAssets("t:Prefab", dirs);
        int done = 0, skip = 0, updated = 0;
        foreach (var g in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(g);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null || prefab.GetComponent<PhysicsSwallowable>() == null) { skip++; continue; }

            var root = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            try
            {
                // Mevcut pad'i güncelle (varsa) — YENİ kalınlık/ölçüler yeniden hesaplanıp uygulansın (idempotent değil,
                // her çalıştırmada değerleri tazeler). Yoksa yenisini oluştur.
                bool exists = root.transform.Find(CHILD) != null;

                // Tüm mesh vertex'lerini ROOT-YEREL uzayda topla (root'un kendi transform'u hariç → child'ları içerir).
                var mfs = root.GetComponentsInChildren<MeshFilter>();
                var pts = new List<Vector3>();
                float minY = float.PositiveInfinity;
                foreach (var mf in mfs)
                {
                    if (mf.sharedMesh == null) continue;
                    var m = root.transform.worldToLocalMatrix * mf.transform.localToWorldMatrix;
                    var verts = mf.sharedMesh.vertices;
                    for (int i = 0; i < verts.Length; i++)
                    {
                        var p = m.MultiplyPoint3x4(verts[i]);
                        pts.Add(p);
                        if (p.y < minY) minY = p.y;
                    }
                }
                if (pts.Count == 0) { skip++; continue; }

                // En-alt dilim (minY .. minY+SLAB) içindeki vertex'lerin XZ ayak izi = taban.
                float minX = 1e9f, maxX = -1e9f, minZ = 1e9f, maxZ = -1e9f;
                foreach (var p in pts)
                    if (p.y <= minY + SLAB)
                    {
                        if (p.x < minX) minX = p.x; if (p.x > maxX) maxX = p.x;
                        if (p.z < minZ) minZ = p.z; if (p.z > maxZ) maxZ = p.z;
                    }
                float cx = (minX + maxX) * 0.5f, cz = (minZ + maxZ) * 0.5f;
                float sx = Mathf.Max(0.05f, maxX - minX), sz = Mathf.Max(0.05f, maxZ - minZ);

                var padTf = root.transform.Find(CHILD);
                GameObject pad = padTf != null ? padTf.gameObject : new GameObject(CHILD);
                pad.transform.SetParent(root.transform, false);
                pad.transform.localPosition = new Vector3(cx, minY + BOXH * 0.5f, cz);
                pad.transform.localRotation = Quaternion.identity;
                pad.transform.localScale = Vector3.one;
                var box = pad.GetComponent<BoxCollider>() ?? pad.AddComponent<BoxCollider>();
                box.center = Vector3.zero;
                box.size = new Vector3(sx, BOXH, sz);

                PrefabUtility.ApplyPrefabInstance(root, InteractionMode.AutomatedAction);
                if (exists) updated++; else done++;
            }
            finally { Object.DestroyImmediate(root); }
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[BaseCol] Bitti — {done} prefaba taban collider EKLENDİ, {updated} GÜNCELLENDİ (kalınlık {BOXH}), {skip} atlandı (toplam {guids.Length}).");
    }

    public static void RemoveBaseColliders(string[] dirs)
    {
        var guids = AssetDatabase.FindAssets("t:Prefab", dirs);
        int removed = 0;
        foreach (var g in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(g);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) continue;
            var root = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            try
            {
                var child = root.transform.Find(CHILD);
                if (child != null) { Object.DestroyImmediate(child.gameObject); PrefabUtility.ApplyPrefabInstance(root, InteractionMode.AutomatedAction); removed++; }
            }
            finally { Object.DestroyImmediate(root); }
        }
        AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        Debug.Log($"[BaseCol] {removed} prefabdan taban collider KALDIRILDI.");
    }
}
