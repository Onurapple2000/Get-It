using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// GENERIC dünya nesne üreteci — her yeni temaya ayrı kopya creator yazmak yerine TEK parametreli sınıf
/// (DrinksObjectCreator kalıbının genelleştirilmişi). Her tema için bir menü öğesi + Config yeter.
///   - Assets/Art/worlds/{tema}/*.glb → Assets/Prefabs/{Tema}/*.prefab
///   - Dosya adından temiz PascalCase isim (Meshy_AI_ öneki + gürültü kelimeler ayıklanır).
///   - Boyut kademesi: anahtar kelime (küçük/büyük) yoksa isim-hash → ağırlıklı dağılım (çoğunluk küçük-orta).
///   - Mesh decimate + texture downscale (Meshy 4K → 512/1024).
///   - Convex MeshCollider: ColliderHullUtil vertex-clustering ≤100 vertex (hull ≤196 tri < 256 GARANTİ) +
///     XZ×0.82 daraltma (yuvarlak deliğe girer). [[project-worlds]] collider perf fix.
///   - Rigidbody + PhysicsSwallowable (freezeUntilNear kinematik donuk başlar).
/// IDEMPOTENT (var olan prefabı atlar). Rebuild ... = aynı isim/GUID üzerine yaz (level referansları bozulmaz).
/// </summary>
public static class WorldObjectCreator
{
    struct Tier { public float dim; public int score; public float grow; public Tier(float d, int s, float g){dim=d;score=s;grow=g;} }

    // Ortak (evrensel) kademe — nesne-boyutlu temalar (kutu/kitap/kedi). Tiny/Small başlangıç deliğine (1.5) sığar.
    static readonly Tier Tiny   = new Tier(0.8f,  14, 0.12f);
    static readonly Tier Small  = new Tier(1.2f,  20, 0.16f);
    static readonly Tier Medium = new Tier(1.7f,  32, 0.24f);
    static readonly Tier Large  = new Tier(2.4f,  50, 0.32f);

    class Config
    {
        public string artSub;         // "gifts"
        public string prefabDir;      // "Assets/Prefabs/Gifts"
        public string fallbackName;   // "Gift"
        public int decimate = 6000;
        public int texSize = 512;
        public string[] tinyKeys = { };
        public string[] largeKeys = { };
        // hash bucket eşikleri (kümülatif %): tiny, small, medium (kalan = large)
        public int pTiny = 20, pSmall = 60, pMedium = 88;
        // Tema-özel boyut kademeleri (null → evrensel Tiny/Small/Medium/Large). Sıra: [tiny,small,medium,large].
        public Tier[] tiers;
        // Collider XZ daraltma (footprint = colShrink×görsel). Varsayılan 0.82; İçecekler/Hediyeler DÜRÜST collider (~0.9)
        // → "delik yarı-batmış nesneyi bıçak gibi kesme" artefaktı azalır (bkz drinks çözümü).
        public float colShrink = COL_XZ_SHRINK;
    }

    static readonly HashSet<string> Noise = new HashSet<string> {
        "texture","image","to","3d","the","on","a","an","in","of","under","view","front","model","ultra",
        "realistic","style","stylized","hyper","it","needs","be","bo","s","following","make","create","generate",
        "detailed","prop","game","with","and","round","detail","low","poly","high","render","render","cute","cartoon"
    };

    const float COL_XZ_SHRINK = 0.82f;
    static bool overwrite;

    // ── TEMA MENÜLERİ ──
    static Config GiftsCfg => new Config {
        artSub = "gifts", prefabDir = "Assets/Prefabs/Gifts", fallbackName = "Gift",
        tinyKeys = new[]{ "small","mini","tiny","single" },
        largeKeys = new[]{ "big","large","huge","stack","pile","tower","giant" },
        // 2026-08-04 kullanıcı: Hediyeler İÇECEKLER gibi olsun → boyutlar drink kademeleri (~2.4×), collider DÜRÜST (0.9).
        tiers = new[]{ new Tier(1.3f, 20, 0.11f), new Tier(1.8f, 34, 0.15f), new Tier(2.3f, 52, 0.22f), new Tier(3.0f, 82, 0.30f) },
        colShrink = 0.9f,
    };
    static Config BooksCfg => new Config {
        artSub = "books", prefabDir = "Assets/Prefabs/Books", fallbackName = "Book",
        tinyKeys = new[]{ "single","small","pocket","mini" },
        largeKeys = new[]{ "stack","pile","shelf","stacked","tower","big","large","tome","encyclopedia" },
    };
    static Config CatsCfg => new Config {
        artSub = "cats", prefabDir = "Assets/Prefabs/Cats", fallbackName = "Cat",
        texSize = 512,
        // 2026-08-05 kullanıcı: TINY kedi OLMASIN, SMALL az olsun. tinyKeys BOŞ (isimle tiny zorlanmasın) + pTiny=0
        // (hash tiny üretmesin) + pSmall=12 (yalnız %12 small; kalan medium/large → kediler daha iri/net).
        tinyKeys = new string[0],
        largeKeys = new[]{ "big","large","battle","giant","fat","huge","king" },
        pTiny = 0, pSmall = 12, pMedium = 58,
        // Kademe boyutları: tiny(1.2) artık KULLANILMAZ (pTiny=0 + tinyKeys boş) — en küçük kedi Small(1.6).
        tiers = new[]{ new Tier(1.2f, 20, 0.16f), new Tier(1.6f, 30, 0.22f), new Tier(2.1f, 42, 0.28f), new Tier(2.8f, 58, 0.34f) },
    };

    [MenuItem("Tools/GET_IT/Create Gift Objects")]                      public static void Gifts()        { overwrite = false; Build(GiftsCfg); }
    [MenuItem("Tools/GET_IT/Rebuild Gift Objects (overwrite sizes)")]   public static void GiftsRebuild() { overwrite = true;  Build(GiftsCfg); }
    [MenuItem("Tools/GET_IT/Create Book Objects")]                      public static void Books()        { overwrite = false; Build(BooksCfg); }
    [MenuItem("Tools/GET_IT/Rebuild Book Objects (overwrite sizes)")]   public static void BooksRebuild() { overwrite = true;  Build(BooksCfg); }
    [MenuItem("Tools/GET_IT/Create Cat World Objects")]                 public static void Cats()         { overwrite = false; Build(CatsCfg); }
    [MenuItem("Tools/GET_IT/Rebuild Cat World Objects (overwrite sizes)")] public static void CatsRebuild() { overwrite = true; Build(CatsCfg); }

    static Config DogsCfg => new Config {
        artSub = "dogs", prefabDir = "Assets/Prefabs/Dogs", fallbackName = "Dog",
        texSize = 512,
        // 2026-08-06 kullanıcı: köpeklerde TINY ve SMALL boyut OLMASIN, büyüt. tinyKeys BOŞ + pTiny=0 + pSmall=0 →
        // hiç tiny/small yok; hepsi Medium(2.1)/Large(2.8). (Kedilerde small az idi; köpeklerde HİÇ small.)
        tinyKeys = new string[0],
        largeKeys = new[]{ "big","large","mastiff","great","dane","giant","husky","shepherd","st","bernard" },
        pTiny = 0, pSmall = 0, pMedium = 55,
        // tiny(1.2)/small(1.6) KULLANILMAZ; en küçük köpek Medium(2.1).
        tiers = new[]{ new Tier(1.2f, 20, 0.16f), new Tier(1.6f, 30, 0.22f), new Tier(2.1f, 42, 0.28f), new Tier(2.8f, 58, 0.34f) },
    };
    [MenuItem("Tools/GET_IT/Create Dog World Objects")]                 public static void Dogs()         { overwrite = false; Build(DogsCfg); }
    [MenuItem("Tools/GET_IT/Rebuild Dog World Objects (overwrite sizes)")] public static void DogsRebuild() { overwrite = true; Build(DogsCfg); }

    static Config ShipsCfg => new Config {
        artSub = "ships", prefabDir = "Assets/Prefabs/Ships", fallbackName = "Ship",
        texSize = 512,
        tinyKeys = new[]{ "boat","dinghy","canoe","raft","kayak","small","fishing","ferry" },
        largeKeys = new[]{ "carrier","cruiser","battleship","tanker","titanic","galleon","destroyer","cargo","container","liner","warship" },
        // 2026-08-06 kullanıcı: tiny/small gemiler ÇOK küçüktü → tiny/small kademeleri BÜYÜTÜLDÜ (medium/large ~aynı).
        // (Özel yapı/landmark gemileri ScaleTo ile kendi boyutuna ölçeklendiğinden tier'dan BAĞIMSIZ → değişmez.)
        tiers = new[]{ new Tier(2.0f, 22, 0.16f), new Tier(2.4f, 32, 0.22f), new Tier(2.6f, 46, 0.28f), new Tier(3.1f, 62, 0.34f) },
    };
    [MenuItem("Tools/GET_IT/Create Ship World Objects")]                 public static void Ships()        { overwrite = false; Build(ShipsCfg); }
    [MenuItem("Tools/GET_IT/Rebuild Ship World Objects (overwrite sizes)")] public static void ShipsRebuild() { overwrite = true; Build(ShipsCfg); }

    static Config PlanesCfg => new Config {
        artSub = "planes", prefabDir = "Assets/Prefabs/Planes", fallbackName = "Plane",
        texSize = 512,
        tinyKeys = new[]{ "drone","small","glider","propeller","cessna","biplane","paper" },
        largeKeys = new[]{ "boeing","airbus","cargo","bomber","747","jumbo","airliner","transport","b52","b2" },
        tiers = new[]{ new Tier(1.2f, 20, 0.16f), new Tier(1.6f, 30, 0.22f), new Tier(2.1f, 42, 0.28f), new Tier(2.8f, 58, 0.34f) },
    };
    [MenuItem("Tools/GET_IT/Create Plane World Objects")]                 public static void Planes()        { overwrite = false; Build(PlanesCfg); }
    [MenuItem("Tools/GET_IT/Rebuild Plane World Objects (overwrite sizes)")] public static void PlanesRebuild() { overwrite = true; Build(PlanesCfg); }

    // HAZİNE dünyası (worldId 13) — Para + Altın BİRLEŞİK (banknot/sikke/külçe/sandık). Klasör: moneys.
    static Config MoneyCfg => new Config {
        artSub = "moneys", prefabDir = "Assets/Prefabs/Money", fallbackName = "Money",
        texSize = 512,
        tinyKeys = new[]{ "coin","single","small","bill","note" },
        largeKeys = new[]{ "stack","pile","bar","ingot","chest","vault","safe","bundle","brick","bag","treasure" },
        tiers = new[]{ new Tier(1.0f, 18, 0.14f), new Tier(1.4f, 28, 0.20f), new Tier(1.9f, 40, 0.26f), new Tier(2.6f, 56, 0.32f) },
    };
    [MenuItem("Tools/GET_IT/Create Money World Objects")]                 public static void Money()        { overwrite = false; Build(MoneyCfg); }
    [MenuItem("Tools/GET_IT/Rebuild Money World Objects (overwrite sizes)")] public static void MoneyRebuild() { overwrite = true; Build(MoneyCfg); }

    // MÜCEVHER dünyası (worldId 15). Klasör adı kullanıcının bıraktığı gibi 'jevelary'.
    static Config JewelryCfg => new Config {
        artSub = "jevelary", prefabDir = "Assets/Prefabs/Jewelry", fallbackName = "Jewel",
        texSize = 512,
        tinyKeys = new[]{ "ring","gem","stone","earring","small","stud","bead" },
        largeKeys = new[]{ "crown","necklace","tiara","chest","box","set","collier","big","large" },
        tiers = new[]{ new Tier(1.0f, 18, 0.14f), new Tier(1.35f, 28, 0.20f), new Tier(1.8f, 40, 0.26f), new Tier(2.4f, 54, 0.32f) },
    };
    [MenuItem("Tools/GET_IT/Create Jewelry World Objects")]                 public static void Jewelry()        { overwrite = false; Build(JewelryCfg); }
    [MenuItem("Tools/GET_IT/Rebuild Jewelry World Objects (overwrite sizes)")] public static void JewelryRebuild() { overwrite = true; Build(JewelryCfg); }

    static void Build(Config cfg)
    {
        EnsureDir(cfg.prefabDir);
        AssetDatabase.Refresh();

        var dir = Path.Combine(Application.dataPath, "Art/worlds/" + cfg.artSub);
        if (!Directory.Exists(dir)) { Debug.LogError($"[WorldObj:{cfg.artSub}] klasör yok: {dir}"); return; }
        var files = Directory.GetFiles(dir, "*.glb");
        System.Array.Sort(files);
        if (files.Length == 0) { Debug.LogWarning($"[WorldObj:{cfg.artSub}] .glb yok."); return; }

        var used = new HashSet<string>();
        if (!overwrite)
        {
            string pdir = Path.Combine(Application.dataPath, cfg.prefabDir.Substring("Assets/".Length));
            if (Directory.Exists(pdir))
                foreach (var p in Directory.GetFiles(pdir, "*.prefab"))
                    used.Add(Path.GetFileNameWithoutExtension(p));
        }

        int made = 0, skipped = 0, failed = 0;
        for (int i = 0; i < files.Length; i++)
        {
            string glbPath = "Assets" + files[i].Substring(Application.dataPath.Length).Replace('\\', '/');
            string baseName = CleanName(Path.GetFileNameWithoutExtension(files[i]), cfg.fallbackName);
            string name = Unique(baseName, used);

            string prefabPath = $"{cfg.prefabDir}/{name}.prefab";
            if (!overwrite && File.Exists(Path.Combine(Application.dataPath, prefabPath.Substring("Assets/".Length))))
            { used.Add(name); skipped++; continue; }

            try { if (CreatePrefab(cfg, glbPath, name)) { used.Add(name); made++; } else failed++; }
            catch (System.Exception e) { Debug.LogWarning($"[WorldObj:{cfg.artSub}] HATA {name}: {e.Message}"); failed++; }

            if ((made + skipped + failed) % 10 == 0)
                Debug.Log($"[WorldObj:{cfg.artSub}] ilerleme: {made + skipped + failed}/{files.Length}");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[WorldObj:{cfg.artSub}] BİTTİ. {files.Length} GLB → yeni {made}, atlanan {skipped}, hata {failed}. Prefablar: {cfg.prefabDir}");
    }

    static bool CreatePrefab(Config cfg, string glbPath, string name)
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(glbPath);
        if (model == null) { Debug.LogWarning($"[WorldObj:{cfg.artSub}] import edilmemiş: {glbPath}"); return false; }

        var root = new GameObject(name);
        var mesh = Object.Instantiate(model);
        mesh.transform.SetParent(root.transform, false);
        mesh.transform.localPosition = Vector3.zero;
        mesh.transform.localRotation = Quaternion.identity;

        Tier t = PickTier(cfg, glbPath);

        Bounds b = CombinedBounds(mesh);
        float maxDim = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
        float scale = maxDim > 0.0001f ? t.dim / maxDim : 1f;
        mesh.transform.localScale = Vector3.one * scale;

        b = CombinedBounds(mesh);
        mesh.transform.localPosition = new Vector3(-b.center.x, -b.min.y, -b.center.z);   // ortala + taban y=0

        string meshDir = cfg.prefabDir + "/Meshes";
        string texDir  = cfg.prefabDir + "/Tex";
        MeshDecimate.DecimateInstance(mesh, cfg.decimate, meshDir, name);
        MeshyImport.DownscaleTextures(mesh, texDir, name, cfg.texSize);

        var rb = root.AddComponent<Rigidbody>();
        rb.mass = Mathf.Max(1f, t.dim * 4f);
        rb.linearDamping = 0.4f;
        rb.angularDamping = 0.05f;
        rb.useGravity = true;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        rb.maxDepenetrationVelocity = 1.5f;

        EnsureDir(meshDir);
        Mesh colMesh = ColliderHullUtil.BuildConvexColliderMesh(mesh, cfg.colShrink, name);
        if (colMesh != null)
        {
            string colPath = $"{meshDir}/{name}_col.asset";
            AssetDatabase.DeleteAsset(colPath);
            AssetDatabase.CreateAsset(colMesh, colPath);
            var mc = mesh.AddComponent<MeshCollider>();
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
            Debug.LogWarning($"[WorldObj:{cfg.artSub}] {name}: mesh yok → BoxCollider yedeğe düşüldü.");
        }

        var sw = root.AddComponent<PhysicsSwallowable>();
        sw.objectType = name;
        sw.scoreValue = t.score;
        sw.growAmount = t.grow;

        PrefabUtility.SaveAsPrefabAsset(root, $"{cfg.prefabDir}/{name}.prefab");
        Object.DestroyImmediate(root);
        return true;
    }

    static Tier PickTier(Config cfg, string glbPath)
    {
        Tier tiny = cfg.tiers != null ? cfg.tiers[0] : Tiny;
        Tier small = cfg.tiers != null ? cfg.tiers[1] : Small;
        Tier medium = cfg.tiers != null ? cfg.tiers[2] : Medium;
        Tier large = cfg.tiers != null ? cfg.tiers[3] : Large;

        string low = Path.GetFileName(glbPath).ToLowerInvariant();
        foreach (var k in cfg.largeKeys) if (low.Contains(k)) return large;
        foreach (var k in cfg.tinyKeys)  if (low.Contains(k)) return tiny;
        int h = 0; foreach (char ch in low) h = h * 31 + ch;
        int bkt = (h & 0x7fffffff) % 100;
        if (bkt < cfg.pTiny) return tiny;
        if (bkt < cfg.pSmall) return small;
        if (bkt < cfg.pMedium) return medium;
        return large;
    }

    static string CleanName(string raw, string fallback)
    {
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
        return string.IsNullOrEmpty(name) ? fallback : name;
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
