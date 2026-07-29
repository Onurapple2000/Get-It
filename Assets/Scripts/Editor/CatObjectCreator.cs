using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Kediler dünyası (hareketli nesneler) — yürüyen kedi prefabını Meshy'nin animasyonlu (skinned) GLB'sinden üretir.
/// FoodObjectCreator kalıbı, farklarla:
///   - Kaynak GLB skinned + Mecanim Animator içerir (yürüme klibi otomatik oynar) → mesh DECIMATE EDİLMEZ
///     (rig/bone weight bozulmasın). Sadece texture 1024'e indirilir.
///   - Animator.applyRootMotion = false (yürüme yerinde oynar; ilerlemeyi PatrolWalker sağlar).
///   - PhysicsSwallowable (freezeUntilNear=true → kinematik donuk başlar) + PatrolWalker (sağa-sola yürür).
/// Kaynak: Assets/Art/Meshy_AI_model_Animation_Walking_withSkin.glb  →  Assets/Prefabs/Cats/Cat.prefab
/// Menu: Tools/GET_IT/Create Cat Object
/// </summary>
public static class CatObjectCreator
{
    const string GLB_PATH   = "Assets/Art/Meshy_AI_model_Animation_Walking_withSkin.glb";
    const string PREFAB_DIR = "Assets/Prefabs/Cats";
    const string PREFAB     = "Cat";
    const float  TARGET     = 5.2f;   // en büyük boyut bu birime ölçeklenir (büyük, sahnede belirgin)
    const int    SCORE      = 30;

    [MenuItem("Tools/GET_IT/Create Cat Object")]
    public static void Run()
    {
        EnsureDir(PREFAB_DIR);
        AssetDatabase.Refresh();

        var model = AssetDatabase.LoadAssetAtPath<GameObject>(GLB_PATH);
        if (model == null) { Debug.LogError($"[CatCreator] GLB import edilmemiş / bulunamadı: {GLB_PATH}"); return; }

        var root = new GameObject(PREFAB);

        var mesh = Object.Instantiate(model);
        mesh.transform.SetParent(root.transform, false);
        mesh.transform.localPosition = Vector3.zero;
        mesh.transform.localRotation = Quaternion.identity;

        // Ölçek: en büyük boyut TARGET'e. (Skinned renderer bounds'u mevcut poz için yeterli.)
        Bounds b = CombinedBounds(mesh);
        float maxDim = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
        float scale = maxDim > 0.0001f ? TARGET / maxDim : 1f;
        mesh.transform.localScale = Vector3.one * scale;

        // Ortala (XZ) + tabanı y=0'a otur.
        b = CombinedBounds(mesh);
        mesh.transform.localPosition = new Vector3(-b.center.x, -b.min.y, -b.center.z);

        // Yürüme YERİNDE oynasın (ilerlemeyi PatrolWalker verir); kök hareket birikmesin.
        var animator = mesh.GetComponentInChildren<Animator>();
        if (animator != null)
        {
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate; // uzaktayken de yürüsün

            // glTFast Mecanim modunda klipleri import eder ama AnimatorController ÜRETMEZ (controller null →
            // animasyon oynamaz). GLB alt-assetlerinden yürüme klibini bulup tek-state'li bir controller kur & ata.
            var clip = FindWalkClip(GLB_PATH);
            if (clip != null)
            {
                string ctrlPath = $"{PREFAB_DIR}/CatWalk.controller";
                var ctrl = AnimatorController.CreateAnimatorControllerAtPathWithClip(ctrlPath, clip);
                animator.runtimeAnimatorController = ctrl;  // varsayılan state klibi sürekli döngüler → yürüme
                Debug.Log($"[CatCreator] AnimatorController: {ctrlPath} (clip '{clip.name}')");
            }
            else Debug.LogWarning("[CatCreator] GLB'de AnimationClip bulunamadı — yürüme oynamaz (import animationMethod?).");
        }
        else Debug.LogWarning("[CatCreator] Animator bulunamadı — yürüme animasyonu oynamayabilir (GLB animationMethod?).");

        // Texture'ları 1024'e indir (mesh'e dokunma — rig korunsun).
        MeshyImport.DownscaleTextures(mesh, PREFAB_DIR + "/Tex", PREFAB, 1024);

        // Rigidbody — food profili (freezeUntilNear kinematik başlatacak).
        var rb = root.AddComponent<Rigidbody>();
        rb.mass = 6f;
        rb.linearDamping = 0.4f;
        rb.angularDamping = 0.05f;
        rb.useGravity = true;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        rb.maxDepenetrationVelocity = 1.5f;

        // Collider — bounds box, ayak izi XZ ×0.8 (yuvarlak delik kenarına takılmasın).
        var col = root.AddComponent<BoxCollider>();
        Bounds wb = CombinedBounds(root);
        col.center = root.transform.InverseTransformPoint(wb.center);
        col.size = new Vector3(wb.size.x * 0.8f, wb.size.y, wb.size.z * 0.8f);
        col.sharedMaterial = GripMat.Get();

        var sw = root.AddComponent<PhysicsSwallowable>();
        sw.objectType = PREFAB;
        sw.scoreValue = SCORE;
        sw.growAmount = 0.2f;
        // freezeUntilNear=true (default) → kinematik donuk başlar, PatrolWalker yürütür, delik yakınında uyanır.

        var walk = root.AddComponent<PatrolWalker>();
        walk.frameHalf = 15f;          // LevelManager.frameHalf ile aynı → çerçeveden döner, dışarı çıkmaz
        walk.wallInset = 0.6f;
        walk.speed = 2f;
        walk.startMovingLeft = true;   // sağa yerleştir, sola yürüsün

        string prefabPath = $"{PREFAB_DIR}/{PREFAB}.prefab";
        PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[CatCreator] {prefabPath} üretildi (size~{TARGET}, score {SCORE}). Sahneye ekleyip test edin.");
    }

    /// <summary>GLB alt-assetlerindeki AnimationClip'lerden yürüyüşü döndürür ("walk" içereni tercih, yoksa ilki).</summary>
    static AnimationClip FindWalkClip(string glbPath)
    {
        var clips = new List<AnimationClip>();
        foreach (var a in AssetDatabase.LoadAllAssetRepresentationsAtPath(glbPath))
            if (a is AnimationClip c && !c.name.StartsWith("__preview")) clips.Add(c);
        if (clips.Count == 0) return null;
        var walk = clips.Find(c => c.name.ToLower().Contains("walk"));
        return walk != null ? walk : clips[0];
    }

    static Bounds CombinedBounds(GameObject go)
    {
        var rs = go.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.one * 0.5f);
        var b = rs[0].bounds;
        for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
        return b;
    }

    static void EnsureDir(string assetPath)
    {
        string full = Path.Combine(Application.dataPath, assetPath.Substring("Assets/".Length));
        if (!Directory.Exists(full)) Directory.CreateDirectory(full);
    }
}
