using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// İçecekler dünyası (Sprint 5, dünya 5) prefablarını Meshy GLB'lerinden OTOMATİK üretir — SweetsObjectCreator kalıbı.
///   - Dosya adından temiz PascalCase isim; boyut kademesi anahtar kelime/hash (bardak=küçük, set/makine=büyük).
///   - Mesh decimate 6000 + texture 512 + Rigidbody + PhysicsSwallowable.
///   - ⚠️ COLLIDER (perf fix): convex MeshCollider mesh'i ColliderHullUtil ile üretilir — TÜM parçalar BİRLEŞTİRİLİP
///     (çay seti gibi çok-parçalı) vertex-clustering ile ≤100 vertex'e indirilir (hull ≤196 tri < 256 GARANTİ,
///     "partial hull" uyarısı olmaz), cook hızlı. XZ×0.82 daraltma (yuvarlak deliğe girer). Şişe ters düşünce devrilir.
/// IDEMPOTENT. Kaynak: Assets/Art/worlds/drinks/*.glb → Assets/Prefabs/Drinks/*.prefab
/// Menü: Tools/GET_IT/Create Drinks Objects (+ Rebuild ... overwrite sizes).
/// </summary>
public static class DrinksObjectCreator
{
    const string ART_DIR    = "Assets/Art/worlds/drinks";
    const string PREFAB_DIR = "Assets/Prefabs/Drinks";
    const string MESH_DIR   = "Assets/Prefabs/Drinks/Meshes";
    const string TEX_DIR    = "Assets/Prefabs/Drinks/Tex";
    // ⚠️ 2026-08-02 kullanıcı: İçecek nesneleri ÇOK KÜÇÜKTÜ → TÜM tier'lar ~3× büyütüldü.
    // ⚠️ 2026-08-03 DÜZELTME (kullanıcı): shrink 0.75 iken collider görselin %75'iydi; nesneler 3× büyüyünce bu %25
    // inset MUTLAK olarak büyüdü → (a) yan yana meshler GÖRÜNÜR iç içe, (b) footprint görselden küçük olduğundan
    // "delikten büyük olsa da geçiyor". FIX: shrink 0.92 (DÜRÜST footprint; yalnız köşeli setlere hafif pay, yuvarlak
    // içecekler zaten dertsiz). Soft-lock riski (en küçük tier 1.5 deliğe sığmaz) → dünya-5 başlangıç deliği 2.0'a
    // çıkarıldı (LevelManager.ResetHole; 3× nesneli dünyaya orantılı, Tiny footprint ~1.66 < 2.0 → sığar).
    const float  COL_XZ_SHRINK = 0.97f;   // 2026-08-03: 0.95→0.97 (bıçak-kesme fix; EN-UZAK-vertex hull + ~dürüst footprint)

    // 2026-08-03 kullanıcı: 3× biraz büyüktü → ~%20 küçült (≈2.4× orijinal). Collider+mesh rebuild'de otomatik ölçeklenir
    // (mesh tier'a göre; collider ColliderHullUtil telafisiyle görselin ~0.95'i). Start hole 1.7 (ResetHole) ile uyumlu.
    struct Tier { public float dim; public int score; public float grow; public Tier(float d, int s, float g){dim=d;score=s;grow=g;} }
    // 2026-08-03: "çok az daha küçült" → tier'lar ~%10 küçüldü (daha çok nesne sığsın, çeşit artsın).
    static readonly Tier Tiny   = new Tier(1.3f,  20, 0.11f);
    static readonly Tier Small  = new Tier(1.8f,  34, 0.15f);
    static readonly Tier Medium = new Tier(2.3f,  52, 0.22f);
    static readonly Tier Large  = new Tier(3.0f,  82, 0.30f);   // footprint ~3.0×0.95 → < maxSize 5.5 → yutulur

    static readonly string[] TinyKeys  = { "cup", "mug", "glass", "espresso", "shot", "saucer", "egg", "companion" };
    static readonly string[] LargeKeys = { "set", "service", "maker", "machine", "grinder", "barista", "station", "pitcher", "tray", "carton", "lemonade", "moment", "harmony", "serenity", "elegance", "trove", "infusion" };
    static readonly HashSet<string> Noise = new HashSet<string> { "texture", "image", "to", "3d", "the", "on", "a", "an", "in", "of", "under", "view", "front", "model", "ultra", "realistic", "style", "stylized", "hyper", "it", "needs", "be", "bo", "s", "following", "make", "create", "generate", "detailed", "prop", "game", "delight", "delightful", "japan", "japanese", "mount", "fu", "fuji", "imperial", "traditional", "with", "and", "round", "detail" };

    static readonly (string ts, string name)[] NameOverrides = { };

    static bool overwrite;

    [MenuItem("Tools/GET_IT/Create Drinks Objects")]
    public static void Run() { overwrite = false; Build(); }

    [MenuItem("Tools/GET_IT/Rebuild Drinks Objects (overwrite sizes)")]
    public static void Rebuild() { overwrite = true; Build(); }

    static void Build()
    {
        EnsureDir(PREFAB_DIR);
        AssetDatabase.Refresh();

        var dir = Path.Combine(Application.dataPath, "Art/worlds/drinks");
        if (!Directory.Exists(dir)) { Debug.LogError($"[DrinksCreator] klasör yok: {dir}"); return; }
        var files = Directory.GetFiles(dir, "*.glb");
        System.Array.Sort(files);
        if (files.Length == 0) { Debug.LogWarning($"[DrinksCreator] {dir} içinde .glb yok."); return; }

        var used = new HashSet<string>();
        if (!overwrite)
        {
            string pdir = Path.Combine(Application.dataPath, "Prefabs/Drinks");
            if (Directory.Exists(pdir))
                foreach (var p in Directory.GetFiles(pdir, "*.prefab"))
                    used.Add(Path.GetFileNameWithoutExtension(p));
        }

        int made = 0, skipped = 0, failed = 0;
        for (int i = 0; i < files.Length; i++)
        {
            string glbPath = "Assets" + files[i].Substring(Application.dataPath.Length).Replace('\\', '/');
            string baseName = CleanName(Path.GetFileNameWithoutExtension(files[i]));
            string name = Unique(baseName, used);

            string prefabPath = $"{PREFAB_DIR}/{name}.prefab";
            if (!overwrite && File.Exists(Path.Combine(Application.dataPath, prefabPath.Substring("Assets/".Length))))
            { used.Add(name); skipped++; continue; }

            try
            {
                if (CreatePrefab(glbPath, name)) { used.Add(name); made++; }
                else failed++;
            }
            catch (System.Exception e) { Debug.LogWarning($"[DrinksCreator] HATA {name}: {e.Message}"); failed++; }

            if ((made + skipped + failed) % 10 == 0)
                Debug.Log($"[DrinksCreator] ilerleme: {made + skipped + failed}/{files.Length}");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[DrinksCreator] BİTTİ. {files.Length} GLB → yeni {made}, atlanan {skipped}, hata {failed}. Prefablar: {PREFAB_DIR}");
    }

    static bool CreatePrefab(string glbPath, string name)
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(glbPath);
        if (model == null) { Debug.LogWarning($"[DrinksCreator] import edilmemiş: {glbPath}"); return false; }

        var root = new GameObject(name);
        var mesh = Object.Instantiate(model);
        mesh.transform.SetParent(root.transform, false);
        mesh.transform.localPosition = Vector3.zero;
        mesh.transform.localRotation = Quaternion.identity;

        Tier t = PickTier(glbPath);

        Bounds b = CombinedBounds(mesh);
        float maxDim = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
        float scale = maxDim > 0.0001f ? t.dim / maxDim : 1f;
        mesh.transform.localScale = Vector3.one * scale;

        b = CombinedBounds(mesh);
        mesh.transform.localPosition = new Vector3(-b.center.x, -b.min.y, -b.center.z);   // ortala + taban y=0

        MeshDecimate.DecimateInstance(mesh, 6000, MESH_DIR, name);
        MeshyImport.DownscaleTextures(mesh, TEX_DIR, name, 512);

        var rb = root.AddComponent<Rigidbody>();
        rb.mass = Mathf.Max(1f, t.dim * 4f);
        rb.linearDamping = 0.4f;
        rb.angularDamping = 0.05f;
        rb.useGravity = true;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        rb.maxDepenetrationVelocity = 1.5f;

        // DÜŞÜK-POLİ CONVEX COLLIDER: tüm parçalar birleştir → XZ daralt → vertex-clustering ≤100 vertex GARANTİ
        // (hull ≤196 tri < 256). Eski MeshSimplifier yöntemi çok-parçalı setlerde ~1000 tri'de takılabiliyordu.
        EnsureDir(MESH_DIR);
        Mesh colMesh = ColliderHullUtil.BuildConvexColliderMesh(mesh, COL_XZ_SHRINK, name);
        if (colMesh != null)
        {
            string colPath = $"{MESH_DIR}/{name}_col.asset";
            AssetDatabase.DeleteAsset(colPath);
            AssetDatabase.CreateAsset(colMesh, colPath);
            var mc = mesh.AddComponent<MeshCollider>();   // birleşik mesh, mesh-kökünde
            mc.sharedMesh = colMesh;
            mc.convex = true;
            mc.sharedMaterial = GripMat.Get();
        }
        else
        {
            var col = root.AddComponent<BoxCollider>();
            Bounds wb = CombinedBounds(root);
            col.center = root.transform.InverseTransformPoint(wb.center);
            col.size = new Vector3(wb.size.x * 0.8f, wb.size.y, wb.size.z * 0.8f);
            col.sharedMaterial = GripMat.Get();
            Debug.LogWarning($"[DrinksCreator] {name}: mesh yok → BoxCollider yedeğe düşüldü.");
        }

        var sw = root.AddComponent<PhysicsSwallowable>();
        sw.objectType = name;
        sw.scoreValue = t.score;
        sw.growAmount = t.grow;

        PrefabUtility.SaveAsPrefabAsset(root, $"{PREFAB_DIR}/{name}.prefab");
        Object.DestroyImmediate(root);
        return true;
    }

    static Tier PickTier(string glbPath)
    {
        string low = Path.GetFileName(glbPath).ToLowerInvariant();
        foreach (var k in LargeKeys) if (low.Contains(k)) return Large;
        foreach (var k in TinyKeys)  if (low.Contains(k)) return Tiny;
        int h = 0; foreach (char ch in low) h = h * 31 + ch;
        int bkt = (h & 0x7fffffff) % 100;
        if (bkt < 22) return Tiny;
        if (bkt < 60) return Small;
        if (bkt < 88) return Medium;
        return Large;
    }

    static string CleanName(string raw)
    {
        foreach (var o in NameOverrides) if (raw.Contains(o.ts)) return o.name;

        string n = raw;
        if (n.StartsWith("Meshy_AI_")) n = n.Substring("Meshy_AI_".Length);

        var tokens = n.Split(new[] { '_', ' ', '-', '(', ')', '.' }, System.StringSplitOptions.RemoveEmptyEntries);
        var kept = new List<string>();
        foreach (var tok in tokens)
        {
            var sb = new StringBuilder();
            foreach (char ch in tok) if (ch < 128 && char.IsLetterOrDigit(ch)) sb.Append(ch);
            string cl = sb.ToString();
            if (cl.Length == 0) continue;
            bool hasLetter = false; foreach (char ch in cl) if (char.IsLetter(ch)) { hasLetter = true; break; }
            if (!hasLetter) continue;
            if (Noise.Contains(cl.ToLowerInvariant())) continue;
            kept.Add(char.ToUpperInvariant(cl[0]) + (cl.Length > 1 ? cl.Substring(1) : ""));
            if (kept.Count >= 3) break;
        }
        string name = string.Join("", kept);
        return string.IsNullOrEmpty(name) ? "Drink" : name;
    }

    static string Unique(string baseName, HashSet<string> used)
    {
        if (!used.Contains(baseName)) return baseName;
        for (int i = 2; ; i++) { string cand = baseName + i; if (!used.Contains(cand)) return cand; }
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
