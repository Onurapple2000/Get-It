using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Tatlılar dünyası (Sprint 5, dünya 4) prefablarını Meshy GLB'lerinden OTOMATİK üretir — BuildingObjectCreator kalıbı.
///   - Dosya adından temiz PascalCase isim (Meshy_AI_ öneki, digit/texture/gürültü ayıklanır); çakışma _2,_3…
///   - Boyut kademesi dosya adı anahtar kelimesinden (makaron/kurabiye/şeker=küçük, pasta/turta/külah=büyük, aksi orta).
///   - En BÜYÜK boyuta (yükseklik dahil) ölçeklenir → tutarlı görsel boyut (foods gibi küçük tatlılar).
///   - Mesh decimate 5000 + texture 512 (kullanıcı isteği) + Rigidbody + **convex MeshCollider** (gerçek silüet →
///     dondurma külahı gibi sivri tatlı ters düşünce devrilir; düz tatlı oturur) + GripMat + PhysicsSwallowable.
/// IDEMPOTENT: prefabı olan modeli atlar. Kaynak: Assets/Art/worlds/sweets/*.glb → Assets/Prefabs/Sweets/*.prefab
/// (Makaronlar foods'tan buraya kopyalandı — makaron da tatlı sayılır.)
/// Menü: Tools/GET_IT/Create Sweets Objects (+ Rebuild ... overwrite sizes).
/// </summary>
public static class SweetsObjectCreator
{
    const string ART_DIR    = "Assets/Art/worlds/sweets";
    const string PREFAB_DIR = "Assets/Prefabs/Sweets";
    const string MESH_DIR   = "Assets/Prefabs/Sweets/Meshes";
    const string TEX_DIR    = "Assets/Prefabs/Sweets/Tex";
    const float  COL_XZ_SHRINK = 0.82f;   // collider ayak izini XZ'de daralt (kare collider yuvarlak deliğe girsin)

    // Boyut kademeleri: en BÜYÜK boyut (x/y/z) bu birime ölçeklenir. Tatlılar KÜÇÜK (foods ölçeği: 0.5–1.3).
    struct Tier { public float dim; public int score; public float grow; public Tier(float d, int s, float g){dim=d;score=s;grow=g;} }
    static readonly Tier Tiny   = new Tier(0.5f,  10, 0.10f);
    static readonly Tier Small  = new Tier(0.7f,  16, 0.14f);
    static readonly Tier Medium = new Tier(0.95f, 26, 0.20f);
    static readonly Tier Large  = new Tier(1.3f,  42, 0.28f);

    // ELLE KÜRATÖRLÜK (kullanıcı isteği): detaylı nesne BÜYÜK (küçükken tanınmaz), basit nesne KÜÇÜK olabilir.
    // Değer = en büyük boyutun ölçekleneceği dünya-birimi. İsim/şekil/GLB-boyutu yargısıyla atandı; L1'de görülüp
    // düzeltilecek. Haritada OLMAYAN yeni model, keyword/hash tier'ına düşer (fallback).
    static readonly Dictionary<string, float> SizeOverride = new Dictionary<string, float>
    {
        // Detaylı / büyük tatlılar
        { "BerryNutTart", 1.45f }, { "CherryChocolateRoll", 1.35f }, { "FruitcakeF", 1.35f },
        { "StackPancakesF", 1.30f }, { "CandyCa", 1.25f }, { "DecadentFo", 1.25f }, { "DecadentFo2", 1.25f },
        { "Poppy", 1.20f }, { "Berried", 1.20f }, { "Molten", 1.20f }, { "Lava", 1.20f }, { "RaspberryF", 1.15f },
        // Orta
        { "IceCrea", 1.05f }, { "IceCreamC", 1.05f }, { "IceCreamC2", 1.05f }, { "RainbowSwirlCupcake", 1.05f },
        { "AntwerpenBelgische", 1.05f }, { "Eclair", 1.00f }, { "LuminousFlan", 1.00f }, { "SliceCaramelDr", 1.00f },
        { "SliceCaramelDr2", 1.00f }, { "Heartfelt", 1.00f }, { "FlorenzGelatoAr", 1.00f }, { "Lat", 1.00f },
        { "Lat2", 1.00f }, { "Sweet", 1.00f }, { "Sprinkle", 0.95f },
        // Basit / küçük (yine de okunur, çünkü basit şekil)
        { "SweetSwirl", 0.85f }, { "Jelly", 0.75f }, { "Cookie", 0.72f }, { "PralineSchokoM", 0.68f },
        { "MacaronBlue", 0.72f }, { "MacaronGreen", 0.72f }, { "MacaronPink", 0.72f }, { "MacaronPurple", 0.72f },
        { "MacaronYellow", 0.72f }, { "Strawberry", 0.65f }, { "Strawberry2", 0.65f },
    };

    static readonly string[] TinyKeys  = { "macaron", "cookie", "candy", "praline", "truffle", "bonbon", "mint", "gummy", "lollipop", "marshmallow", "biscuit", "schoko", "chocolate", "wafer", "mochi" };
    static readonly string[] LargeKeys = { "cake", "gateau", "gâteau", "cheesecake", "sundae", "wedding", "layer", "tart", "pie", "fruitcake", "roll", "pavlova", "trifle" };
    static readonly HashSet<string> Noise = new HashSet<string> { "texture", "image", "to", "3d", "the", "on", "a", "an", "in", "of", "under", "view", "front", "model", "ultra", "realistic", "style", "it", "needs", "be", "bo", "s", "following", "make", "generate", "detailed", "delight", "delightful", "magical", "exotic", "food", "christmas", "holiday", "chr", "eine", "der", "die", "das", "und", "with" };

    static readonly (string ts, string name)[] NameOverrides = { };

    static bool overwrite;

    [MenuItem("Tools/GET_IT/Create Sweets Objects")]
    public static void Run() { overwrite = false; Build(); }

    [MenuItem("Tools/GET_IT/Rebuild Sweets Objects (overwrite sizes)")]
    public static void Rebuild() { overwrite = true; Build(); }

    static void Build()
    {
        EnsureDir(PREFAB_DIR);
        AssetDatabase.Refresh();   // yeni kopyalanan GLB'ler (makaronlar) import olsun

        var dir = Path.Combine(Application.dataPath, "Art/worlds/sweets");
        if (!Directory.Exists(dir)) { Debug.LogError($"[SweetsCreator] klasör yok: {dir}"); return; }
        var files = Directory.GetFiles(dir, "*.glb");
        System.Array.Sort(files);
        if (files.Length == 0) { Debug.LogWarning($"[SweetsCreator] {dir} içinde .glb yok — modelleri koydun mu?"); return; }

        var used = new HashSet<string>();
        if (!overwrite)
        {
            string pdir = Path.Combine(Application.dataPath, "Prefabs/Sweets");
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
            catch (System.Exception e) { Debug.LogWarning($"[SweetsCreator] HATA {name}: {e.Message}"); failed++; }

            if ((made + skipped + failed) % 10 == 0)
                Debug.Log($"[SweetsCreator] ilerleme: {made + skipped + failed}/{files.Length}");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[SweetsCreator] BİTTİ. {files.Length} GLB → yeni {made}, atlanan {skipped}, hata {failed}. Prefablar: {PREFAB_DIR}");
    }

    static bool CreatePrefab(string glbPath, string name)
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(glbPath);
        if (model == null) { Debug.LogWarning($"[SweetsCreator] import edilmemiş: {glbPath}"); return false; }

        var root = new GameObject(name);
        var mesh = Object.Instantiate(model);
        mesh.transform.SetParent(root.transform, false);
        mesh.transform.localPosition = Vector3.zero;
        mesh.transform.localRotation = Quaternion.identity;

        Tier t = PickTier(glbPath);
        if (SizeOverride.TryGetValue(name, out float od))   // elle küratörlük öncelikli
            t = new Tier(od, Mathf.RoundToInt(od * 28f), Mathf.Clamp(od * 0.20f, 0.08f, 0.32f));

        // En BÜYÜK boyutu (x/y/z) tier.dim'e ölçekle → tutarlı görsel boyut.
        Bounds b = CombinedBounds(mesh);
        float maxDim = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
        float scale = maxDim > 0.0001f ? t.dim / maxDim : 1f;
        mesh.transform.localScale = Vector3.one * scale;

        b = CombinedBounds(mesh);
        mesh.transform.localPosition = new Vector3(-b.center.x, -b.min.y, -b.center.z);  // ortala + taban y=0

        MeshDecimate.DecimateInstance(mesh, 5000, MESH_DIR, name);
        MeshyImport.DownscaleTextures(mesh, TEX_DIR, name, 512);   // tatlılar: 512 (kullanıcı isteği)

        var rb = root.AddComponent<Rigidbody>();
        rb.mass = Mathf.Max(1f, t.dim * 4f);
        rb.linearDamping = 0.4f;
        rb.angularDamping = 0.05f;
        rb.useGravity = true;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        rb.maxDepenetrationVelocity = 1.5f;

        // CONVEX MESH COLLIDER: gerçek silüetten (convex hull) → sivri tatlı (külah) ters düşünce devrilir.
        // Ayak izi XZ×0.82 daraltılır (yuvarlak deliğe girsin), apex merkezde kaldığı için sivri tepe korunur.
        // ⚠️ PERF FIX: ColliderHullUtil vertex-clustering ile ≤100 vertex GARANTİ eder (hull ≤196 tri < 256)
        // → "partial hull" uyarısı + cook hitch biter. (Eski yöntem render mesh kopyasıydı: 5000+ tri.)
        Mesh colMesh = ColliderHullUtil.BuildConvexColliderMesh(mesh, COL_XZ_SHRINK, name);
        if (colMesh != null)
        {
            EnsureDir(MESH_DIR);
            string colPath = $"{MESH_DIR}/{name}_col.asset";
            AssetDatabase.DeleteAsset(colPath);   // rebuild'de eskisini değiştir
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
            Debug.LogWarning($"[SweetsCreator] {name}: MeshFilter yok → BoxCollider yedeğe düşüldü.");
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
        // Anahtar yoksa isimden DETERMİNİSTİK hash → ağırlıklı dağılım (çoğunluk küçük-orta).
        int h = 0; foreach (char c in low) h = h * 31 + c;
        int bkt = (h & 0x7fffffff) % 100;
        if (bkt < 30) return Tiny;
        if (bkt < 65) return Small;
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
            foreach (char c in tok) if (c < 128 && char.IsLetterOrDigit(c)) sb.Append(c);
            string cl = sb.ToString();
            if (cl.Length == 0) continue;
            bool hasLetter = false; foreach (char c in cl) if (char.IsLetter(c)) { hasLetter = true; break; }
            if (!hasLetter) continue;
            if (Noise.Contains(cl.ToLowerInvariant())) continue;
            kept.Add(char.ToUpperInvariant(cl[0]) + (cl.Length > 1 ? cl.Substring(1) : ""));
            if (kept.Count >= 3) break;
        }
        string name = string.Join("", kept);
        return string.IsNullOrEmpty(name) ? "Sweet" : name;
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
