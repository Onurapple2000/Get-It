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
    const string DIR = "Assets/Prefabs/Buildings";
    const string CHILD = "_BaseCollider";
    const float SLAB = 0.12f;   // en-alt dilim yüksekliği (taban ayak izini örneklemek için) — prefab-yerel birim
    const float BOXH = 0.10f;   // eklenen kutunun yüksekliği (ince pad)

    [MenuItem("Tools/GET_IT/Add Base Colliders (Buildings)")]
    public static void AddBaseColliders()
    {
        var guids = AssetDatabase.FindAssets("t:Prefab", new[] { DIR });
        int done = 0, skip = 0, already = 0;
        foreach (var g in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(g);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null || prefab.GetComponent<PhysicsSwallowable>() == null) { skip++; continue; }

            var root = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            try
            {
                if (root.transform.Find(CHILD) != null) { already++; continue; }

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

                var pad = new GameObject(CHILD);
                pad.transform.SetParent(root.transform, false);
                pad.transform.localPosition = new Vector3(cx, minY + BOXH * 0.5f, cz);
                pad.transform.localRotation = Quaternion.identity;
                pad.transform.localScale = Vector3.one;
                var box = pad.AddComponent<BoxCollider>();
                box.size = new Vector3(sx, BOXH, sz);

                PrefabUtility.ApplyPrefabInstance(root, InteractionMode.AutomatedAction);
                done++;
            }
            finally { Object.DestroyImmediate(root); }
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[BaseCol] Bitti — {done} prefaba taban collider EKLENDİ, {already} zaten vardı, {skip} atlandı (toplam {guids.Length}).");
    }

    [MenuItem("Tools/GET_IT/Remove Base Colliders (Buildings)")]
    public static void RemoveBaseColliders()
    {
        var guids = AssetDatabase.FindAssets("t:Prefab", new[] { DIR });
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
