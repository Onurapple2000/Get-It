using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// TEST: Horseshoe magnet'in ORİJİNAL (273k) hâli ile Unity-decimate edilmiş mobil hâlini yan yana sahneye
/// koyar (görsel A/B) + üçgen sayılarını loglar. Beğenilirse aynı pipeline tüm modellere uygulanır.
/// Menu: Tools/GET_IT/TEST - Mobile Magnet A-B
/// </summary>
public static class MobileMagnetTest
{
    const string ORIG_GLB = "Assets/Art/PowerUps/Meshy_AI_Horseshoe_Magnet_Powe_0627154003_image-to-3d-texture.glb";
    const string OUT_PREFAB = "Assets/Prefabs/PowerUps/_TestMagnetMobile.prefab";
    const int TARGET_TRIS = 4000;

    [MenuItem("Tools/GET_IT/TEST - Mobile Magnet A-B")]
    public static void Run()
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(ORIG_GLB);
        if (model == null) { Debug.LogError($"[Test] GLB yok: {ORIG_GLB}"); return; }

        // --- Mobil hâli üret (decimate + texture 1024) ---
        var root = new GameObject("Magnet_MOBILE");
        var mesh = Object.Instantiate(model);
        mesh.transform.SetParent(root.transform, false);

        long before = CountTris(mesh);
        MeshDecimate.DecimateInstance(mesh, TARGET_TRIS, "Assets/Prefabs/PowerUps/TestMeshes", "MagnetLOD");
        MeshyImport.DownscaleTextures(mesh, "Assets/Prefabs/PowerUps/Tex", "TestMagnet", 1024);
        long after = CountTris(mesh);

        var prefab = PrefabUtility.SaveAsPrefabAsset(root, OUT_PREFAB);
        Object.DestroyImmediate(root);

        // --- Sahneye yan yana koy: ORİJİNAL (sol) vs MOBİL (sağ) ---
        var orig = (GameObject)PrefabUtility.InstantiatePrefab(model);
        orig.name = "Magnet_ORIGINAL_273k";
        orig.transform.position = new Vector3(-2.5f, 1f, 0f);

        var mob = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        mob.name = $"Magnet_MOBILE_{after}tris";
        mob.transform.position = new Vector3(2.5f, 1f, 0f);

        Selection.objects = new Object[] { orig, mob };
        SceneView.FrameLastActiveSceneView();

        Debug.Log($"[Test] Magnet: ORİJİNAL {before:n0} tris (4096 tex)  →  MOBİL {after:n0} tris (1024 tex). " +
                  "Sol=orijinal, sağ=mobil. Görsel farkı incele.");
    }

    const string MESHY3K_GLB = "Assets/Art/PowerUps/Meshy_AI__0627201115_image-to-3d-texture.glb";
    const string MESHY3K_PREFAB = "Assets/Prefabs/PowerUps/_TestMagnetMeshy3k.prefab";

    [MenuItem("Tools/GET_IT/TEST - Place Meshy 3k Magnet")]
    public static void PlaceMeshy3k()
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(MESHY3K_GLB);
        if (model == null) { Debug.LogError($"[Test] GLB yok: {MESHY3K_GLB}"); return; }

        var root = new GameObject("Magnet_MESHY3K");
        var mesh = Object.Instantiate(model);
        mesh.transform.SetParent(root.transform, false);

        long tris = CountTris(mesh);
        MeshyImport.DownscaleTextures(mesh, "Assets/Prefabs/PowerUps/Tex", "TestMeshy3k", 1024);   // sadece texture 1024

        var prefab = PrefabUtility.SaveAsPrefabAsset(root, MESHY3K_PREFAB);
        Object.DestroyImmediate(root);

        var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        inst.name = $"Magnet_MESHY3K_{tris}tris";
        inst.transform.position = new Vector3(6f, 1f, 0f);
        Selection.activeObject = inst;
        SceneView.FrameLastActiveSceneView();

        Debug.Log($"[Test] Meshy 3k magnet: {tris:n0} tris, texture 1024'e indirildi, sahnede x=6 (en sağ).");
    }

    static long CountTris(GameObject go)
    {
        long t = 0;
        foreach (var mf in go.GetComponentsInChildren<MeshFilter>())
            if (mf.sharedMesh != null) t += mf.sharedMesh.triangles.Length / 3;
        return t;
    }
}
