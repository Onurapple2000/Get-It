using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Binalar dünyası (Sprint 5, dünya 3) prefablarını Meshy GLB'lerinden OTOMATİK üretir — CarObjectCreator kalıbı.
///   - Dosya adından temiz PascalCase isim (Meshy_AI_ öneki, digit/texture/ASCII-dışı ayıklanır); çakışma _2,_3…
///   - Boyut kademesi: ÇOĞUNLUK SMALL (kullanıcı: binalar hantal → az big). kulübe/kiosk=Tiny, gökdelen/kule/avm=Medium
///     (artık Large değil), aksi hash-ağırlıklı (Small %54 en fazla / Medium %29 / Tiny %12 / Large %5).
///   - ⚠️ Arabalardan FARKI: binalar YÜKSEK → "en uzun yatay kenar" yerine **en büyük BOYUTA (yükseklik dahil)**
///     ölçeklenir → gökdelen sahneyi/kamerayı aşacak kadar devleşmez, tutarlı görsel boyut.
///   - Mesh decimate 8000 (ağırsa) + texture 1024'e indir + Rigidbody + bounds BoxCollider (XZ ×0.8) + GripMat + PhysicsSwallowable.
/// IDEMPOTENT: prefabı olan modeli atlar. Kaynak: Assets/Art/worlds/buildings/*.glb → Assets/Prefabs/Buildings/*.prefab
/// Menü: Tools/GET_IT/Create Building Objects (+ Rebuild ... overwrite sizes).
/// </summary>
public static class BuildingObjectCreator
{
    const string ART_SUB    = "Art/worlds/buildings";
    const string PREFAB_DIR = "Assets/Prefabs/Buildings";
    const string MESH_DIR   = "Assets/Prefabs/Buildings/Meshes";
    const float  COL_XZ_SHRINK = 0.82f;   // collider ayak izini XZ'de daralt (eski box ×0.8 davranışı → yuvarlak deliğe girer)

    // Boyut kademeleri: en BÜYÜK boyut (x/y/z) bu birime ölçeklenir. Delik 1.5 çapında başlar → Tiny(1.2)/Small(1.8)
    // erken yutulur, gökdelen büyük delik ister. Binalar arabalardan büyük hissettirir ama hole maxSize (4.5) içinde.
    struct Tier { public float dim; public int score; public float grow; public Tier(float d, int s, float g){dim=d;score=s;grow=g;} }
    static readonly Tier Tiny   = new Tier(1.2f, 10, 0.12f);
    static readonly Tier Small  = new Tier(1.8f, 18, 0.17f);
    static readonly Tier Medium = new Tier(2.6f, 30, 0.24f);
    static readonly Tier Large  = new Tier(3.6f, 48, 0.32f);

    static readonly string[] TinyKeys  = { "hut", "cabin", "shack", "kiosk", "booth", "cottage", "shed", "tent", "outhouse", "small", "cart", "stall", "birdhouse", "doghouse", "toilet", "phone", "mailbox" };
    static readonly string[] LargeKeys = { "skyscraper", "sky_scraper", "tower", "highrise", "high_rise", "mall", "stadium", "cathedral", "castle", "factory", "warehouse", "hotel", "office", "apartment", "temple", "palace", "arena", "airport", "station", "hospital", "mosque", "church_big" };
    static readonly HashSet<string> Noise = new HashSet<string> { "texture", "image", "to", "3d", "the", "on", "a", "in", "of", "under", "view", "front", "model", "ultra", "realistic", "style", "it", "needs", "be", "bo", "s", "following", "make", "building", "generate", "detailed" };

    // Çöp/anlamsız dosya adları için elle güzel isim (dosya adındaki zaman damgasıyla eşleşir). Modeller gelince doldurulur.
    static readonly (string ts, string name)[] NameOverrides = { };

    static bool overwrite;

    [MenuItem("Tools/GET_IT/Create Building Objects")]
    public static void Run() { overwrite = false; Build(); }

    [MenuItem("Tools/GET_IT/Rebuild Building Objects (overwrite sizes)")]
    public static void Rebuild() { overwrite = true; Build(); }

    static void Build()
    {
        EnsureDir(PREFAB_DIR);
        AssetDatabase.Refresh();

        var dir = Path.Combine(Application.dataPath, "Art/worlds/buildings");
        if (!Directory.Exists(dir)) { Debug.LogError($"[BuildingCreator] klasör yok: {dir}"); return; }
        var files = Directory.GetFiles(dir, "*.glb");
        System.Array.Sort(files);
        if (files.Length == 0) { Debug.LogWarning($"[BuildingCreator] {dir} içinde .glb yok — modelleri koydun mu?"); return; }

        var used = new HashSet<string>();
        if (!overwrite)
        {
            string pdir = Path.Combine(Application.dataPath, "Prefabs/Buildings");
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
            catch (System.Exception e) { Debug.LogWarning($"[BuildingCreator] HATA {name}: {e.Message}"); failed++; }

            if ((made + skipped + failed) % 10 == 0)
                Debug.Log($"[BuildingCreator] ilerleme: {made + skipped + failed}/{files.Length}");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[BuildingCreator] BİTTİ. {files.Length} GLB → yeni {made}, atlanan {skipped}, hata {failed}. Prefablar: {PREFAB_DIR}");
    }

    static bool CreatePrefab(string glbPath, string name)
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(glbPath);
        if (model == null) { Debug.LogWarning($"[BuildingCreator] import edilmemiş: {glbPath}"); return false; }

        var root = new GameObject(name);
        var mesh = Object.Instantiate(model);
        mesh.transform.SetParent(root.transform, false);
        mesh.transform.localPosition = Vector3.zero;
        mesh.transform.localRotation = Quaternion.identity;

        Tier t = PickTier(glbPath);

        // En BÜYÜK boyutu (x/y/z — yükseklik dahil) tier.dim'e ölçekle → gökdelen devleşmez, tutarlı görsel boyut.
        Bounds b = CombinedBounds(mesh);
        float maxDim = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
        float scale = maxDim > 0.0001f ? t.dim / maxDim : 1f;
        mesh.transform.localScale = Vector3.one * scale;

        b = CombinedBounds(mesh);
        mesh.transform.localPosition = new Vector3(-b.center.x, -b.min.y, -b.center.z);  // ortala + taban y=0

        MeshDecimate.DecimateInstance(mesh, 8000, "Assets/Prefabs/Buildings/Meshes", name);
        MeshyImport.DownscaleTextures(mesh, "Assets/Prefabs/Buildings/Tex", name, 512);   // binalar: 512 (kullanıcı isteği, daha küçük)

        var rb = root.AddComponent<Rigidbody>();
        rb.mass = Mathf.Max(1f, t.dim * 5f);
        rb.linearDamping = 0.4f;
        rb.angularDamping = 0.05f;
        rb.useGravity = true;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        rb.maxDepenetrationVelocity = 1.5f;

        // CONVEX MESH COLLIDER: collider'ı GERÇEK mesh silüetinden (convex hull) türet → sivri/konik çatılı
        // bina ters düşünce tek noktada temas eder, dengesizdir → DEVRİLİR. (Eski tek BoxCollider'ın düz üst
        // yüzeyi, ters bina için düz zemin sağlıyordu → başaşağı dengede kalıyordu.) Düz çatılar hâlâ oturur.
        // Ayak izi XZ'de daraltılır (eski ×0.8) ki kare bina yuvarlak deliğe girsin; apex XZ-merkezde olduğundan
        // sivri tepe nokta olarak korunur (devrilme bozulmaz). Dinamik Rigidbody'de convex MeshCollider geçerlidir.
        // ⚠️ PERF FIX: ColliderHullUtil vertex-clustering ile ≤100 vertex GARANTİ eder (hull ≤196 tri < 256)
        // → "partial hull" uyarısı + cook hitch biter. (Eski yöntem render mesh kopyasıydı: 5000-8000 tri.)
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
            // Yedek (mesh bulunamazsa): eski bounds BoxCollider.
            var col = root.AddComponent<BoxCollider>();
            Bounds wb = CombinedBounds(root);
            col.center = root.transform.InverseTransformPoint(wb.center);
            col.size = new Vector3(wb.size.x * 0.8f, wb.size.y, wb.size.z * 0.8f);
            col.sharedMaterial = GripMat.Get();
            Debug.LogWarning($"[BuildingCreator] {name}: MeshFilter yok → BoxCollider yedeğe düşüldü.");
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
        // Kullanıcı isteği: binalar hantal → ÇOK AZ Large. Çoğunluk SMALL (en fazla), sonra Medium, az Tiny, çok az Large.
        foreach (var k in TinyKeys)  if (low.Contains(k)) return Tiny;    // kulübe/kiosk = küçük kalsın
        foreach (var k in LargeKeys) if (low.Contains(k)) return Medium;  // gökdelen/kule/avm artık Large DEĞİL → Medium
        // Anahtar yoksa isimden DETERMİNİSTİK hash → SMALL ağırlıklı dağılım.
        int h = 0; foreach (char c in low) h = h * 31 + c;
        int bkt = (h & 0x7fffffff) % 100;
        if (bkt < 12) return Tiny;     // %12
        if (bkt < 66) return Small;    // %54 — EN FAZLA
        if (bkt < 95) return Medium;   // %29
        return Large;                  // %5 — çok az big
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
        return string.IsNullOrEmpty(name) ? "Building" : name;
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
