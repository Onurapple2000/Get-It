using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Arabalar dünyası (Sprint 5, dünya 2) prefablarını Meshy GLB'lerinden OTOMATİK üretir.
/// FoodObjectCreator kalıbı ama elle tablo YOK — klasördeki TÜM *.glb'yi tarar:
///   - Dosya adından temiz PascalCase isim türetir (Meshy_AI_ öneki, sondaki digit/texture parçaları,
///     ASCII-dışı karakterler ayıklanır). Çakışan isimler _2, _3… ile ayrılır.
///   - Boyut KADEMESİ dosya adındaki anahtar kelimeden (mini/kompakt=küçük, truck/van/limo=büyük, aksi=orta)
///     → oynanış çeşitliliği + zorluk (küçük araba erken yutulur, büyük araba büyük delik ister).
///   - Mesh decimate (≤hedef ise atlar) + texture 1024'e indir (kullanıcı Meshy'de texture ayarlayamıyor).
///   - Rigidbody + bounds BoxCollider (XZ ×0.8) + GripMat + PhysicsSwallowable.
/// IDEMPOTENT: prefabı zaten olan modeli atlar → 103 modeli parça parça / tekrar çalıştırarak bitirebilirsin.
/// Kaynak: Assets/Art/worlds/cars/*.glb  →  Assets/Prefabs/Cars/*.prefab
/// Menu: Tools/GET_IT/Create Car Objects
/// </summary>
public static class CarObjectCreator
{
    const string ART_DIR    = "Assets/Art/worlds/cars";
    const string PREFAB_DIR = "Assets/Prefabs/Cars";

    // Boyut kademeleri (en uzun yatay kenar bu birime ölçeklenir) + skor + grow.
    // Delik 1.5 çapında başlar → boyu < delik olunca düşer. Tiny(0.8) & Small(1.2) başlangıç deliğine SIĞAR
    // → oyuncu erkenden bol küçük araba yiyip deliği büyütür. Dağılımın ~%65'i tiny+small (çok sayıda küçük).
    struct Tier { public float len; public int score; public float grow; public Tier(float l, int s, float g){len=l;score=s;grow=g;} }
    static readonly Tier Tiny   = new Tier(0.8f,  8, 0.10f);
    static readonly Tier Small  = new Tier(1.2f, 14, 0.15f);
    static readonly Tier Medium = new Tier(1.7f, 24, 0.20f);
    static readonly Tier Large  = new Tier(2.4f, 40, 0.28f);

    // Anahtar kelime override'ı (aksi halde isim-hash'li ağırlıklı dağılım). mini/kompakt=tiny, truck/van/limo=large.
    static readonly string[] TinyKeys  = { "mini", "smart", "compact", "baby", "low_poly", "lowpoly", "cartoon", "kart", "individual", "corolla", "fiat" };
    static readonly string[] LargeKeys = { "truck", "van", "hauler", "limo", "limou", "cruiser", "land_cruiser", "off_road", "offroad", "bus", "trx", "macan", "suv", "warrior", "gto", "muscle", "stretch", "pickup" };
    // İsim türetiminde atılacak gürültü token'ları (pure-digit token'lar zaten atılır).
    static readonly HashSet<string> Noise = new HashSet<string> { "texture", "image", "to", "3d", "the", "on", "a", "in", "of", "under", "view", "front", "model", "ultra", "realistic", "style", "it", "needs", "be", "bo", "s", "following", "make" };

    static bool overwrite;

    // İdempotent: sadece yeni GLB'ler işlenir (prefabı olan atlanır).
    [MenuItem("Tools/GET_IT/Create Car Objects")]
    public static void Run() { overwrite = false; Build(); }

    // Yerinde yeniden üret: mevcut prefabları AYNI isimle (aynı GUID → level referansları bozulmaz) üzerine yazar
    // → boyut/skor tablosu değişince tüm arabaları güncellemek için.
    [MenuItem("Tools/GET_IT/Rebuild Car Objects (overwrite sizes)")]
    public static void Rebuild() { overwrite = true; Build(); }

    static void Build()
    {
        EnsureDir(PREFAB_DIR);
        AssetDatabase.Refresh();

        var dir = Path.Combine(Application.dataPath, "Art/worlds/cars");
        if (!Directory.Exists(dir)) { Debug.LogError($"[CarCreator] klasör yok: {dir}"); return; }
        var files = Directory.GetFiles(dir, "*.glb");
        System.Array.Sort(files);

        // Overwrite modunda 'used' BOŞ başlar → aynı sıralı+deterministik isimlendirme aynı isimleri üretip
        // mevcutları üzerine yazar (GUID korunur). İdempotent modda mevcut isimlerle doldur (atla + çakışmayı önle).
        var used = new HashSet<string>();
        if (!overwrite)
            foreach (var p in Directory.GetFiles(Path.Combine(Application.dataPath, "Prefabs/Cars"), "*.prefab"))
                used.Add(Path.GetFileNameWithoutExtension(p));

        int made = 0, skipped = 0, failed = 0;
        for (int i = 0; i < files.Length; i++)
        {
            string glbPath = "Assets" + files[i].Substring(Application.dataPath.Length).Replace('\\', '/');
            string baseName = CleanName(Path.GetFileNameWithoutExtension(files[i]));
            string name = Unique(baseName, used);

            string prefabPath = $"{PREFAB_DIR}/{name}.prefab";
            // İdempotent modda: prefab zaten varsa atla. Overwrite modda atlama yok → üzerine yazılır.
            if (!overwrite && File.Exists(Path.Combine(Application.dataPath, prefabPath.Substring("Assets/".Length))))
            { used.Add(name); skipped++; continue; }

            try
            {
                if (CreatePrefab(glbPath, name)) { used.Add(name); made++; }
                else failed++;
            }
            catch (System.Exception e) { Debug.LogWarning($"[CarCreator] HATA {name}: {e.Message}"); failed++; }

            if ((made + skipped + failed) % 10 == 0)
                Debug.Log($"[CarCreator] ilerleme: {made + skipped + failed}/{files.Length} (yeni {made}, atlanan {skipped}, hata {failed})");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[CarCreator] BİTTİ. {files.Length} GLB → yeni {made}, atlanan {skipped}, hata {failed}. Prefablar: {PREFAB_DIR}");
    }

    static bool CreatePrefab(string glbPath, string name)
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(glbPath);
        if (model == null) { Debug.LogWarning($"[CarCreator] import edilmemiş: {glbPath}"); return false; }

        var root = new GameObject(name);
        var mesh = Object.Instantiate(model);
        mesh.transform.SetParent(root.transform, false);
        mesh.transform.localPosition = Vector3.zero;
        mesh.transform.localRotation = Quaternion.identity;

        Tier t = PickTier(glbPath);

        // En uzun YATAY kenarı (araç boyu) tier.len'e ölçekle → araçlar tutarlı oyun boyutunda.
        Bounds b = CombinedBounds(mesh);
        float horiz = Mathf.Max(b.size.x, b.size.z);
        float scale = horiz > 0.0001f ? t.len / horiz : 1f;
        mesh.transform.localScale = Vector3.one * scale;

        b = CombinedBounds(mesh);
        mesh.transform.localPosition = new Vector3(-b.center.x, -b.min.y, -b.center.z);  // ortala + taban y=0

        MeshDecimate.DecimateInstance(mesh, 8000, "Assets/Prefabs/Cars/Meshes", name);   // ağırsa düşür
        MeshyImport.DownscaleTextures(mesh, "Assets/Prefabs/Cars/Tex", name, 1024);      // 4K → 1024

        var rb = root.AddComponent<Rigidbody>();
        rb.mass = Mathf.Max(1f, t.len * 4f);
        rb.linearDamping = 0.4f;
        rb.angularDamping = 0.05f;
        rb.useGravity = true;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        rb.maxDepenetrationVelocity = 1.5f;

        var col = root.AddComponent<BoxCollider>();
        Bounds wb = CombinedBounds(root);
        col.center = root.transform.InverseTransformPoint(wb.center);
        col.size = new Vector3(wb.size.x * 0.8f, wb.size.y, wb.size.z * 0.8f);   // ayak izi dar → yuvarlak deliğe girer
        col.sharedMaterial = GripMat.Get();

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
        // Anahtar kelime yoksa: isimden DETERMİNİSTİK hash → ağırlıklı dağılım (çoğunluk küçük).
        // %30 tiny, %35 small, %25 medium, %10 large. (Stabil: aynı isim hep aynı boyut.)
        int h = 0; foreach (char c in low) h = h * 31 + c;
        int b = (h & 0x7fffffff) % 100;
        if (b < 30) return Tiny;
        if (b < 65) return Small;
        if (b < 90) return Medium;
        return Large;
    }

    // Çöp/anlamsız dosya adlarına elle güzel isim (dosya adındaki BENZERSİZ zaman damgasıyla eşleşir → güvenilir).
    // Kullanıcı: "isimleri çöp olanlara kendin güzel farklı isim ver." Model içeriğiyle birebir örtüşmeyebilir; sorun değil.
    static readonly (string ts, string name)[] NameOverrides =
    {
        ("0707110932", "FutureConcept"),    // 1未来风格车辆3d
        ("0707105823", "RetroRacer"),       // 16 bit 2D top down
        ("0707163918", "PhantomGT"),        // Ultra realistic 3D mo
        ("0706225653", "StreetCruiser"),    // car
        ("0706215424", "UrbanRunner"),      // (boş ad)
        ("0706232047", "CityCoupe"),        // (boş ad)
        ("0706232520", "AsphaltKing"),      // (boş ad)
        ("0707125544", "NightRider"),       // (boş ad)
        ("0706221902", "SteelHorse"),       // Изящный чёр (rusça)
        ("0706225340", "IronBullet"),       // Сгенерируй (rusça)
        ("0707110248", "BoulevardCruiser"), // It needs to be the bo
        ("0706214729", "ClassicRunabout"),  // individual vehicle p
        ("0706221647", "VintageRoadster"),  // individual vehicle p (2)
        ("0706215902", "ApexRacer"),        // extreme detail perfec
        ("0708120541", "FormulaRed"),       // make the following f1 (2. dosya; 1.si "F1" olarak kalıyor)
    };

    /// <summary>"Meshy_AI_Azure_Classic_Sedan_0707131132_texture" → "AzureClassicSedan" (max 3 anlamlı token).
    /// Çöp adlar NameOverrides ile güzel isme çevrilir.</summary>
    static string CleanName(string raw)
    {
        foreach (var o in NameOverrides) if (raw.Contains(o.ts)) return o.name;

        string n = raw;
        if (n.StartsWith("Meshy_AI_")) n = n.Substring("Meshy_AI_".Length);

        var tokens = n.Split(new[] { '_', ' ', '-', '(', ')', '.' }, System.StringSplitOptions.RemoveEmptyEntries);
        var kept = new List<string>();
        foreach (var tok in tokens)
        {
            // ASCII alfanümerik süz
            var sb = new StringBuilder();
            foreach (char c in tok) if (c < 128 && char.IsLetterOrDigit(c)) sb.Append(c);
            string cl = sb.ToString();
            if (cl.Length == 0) continue;
            bool hasLetter = false; foreach (char c in cl) if (char.IsLetter(c)) { hasLetter = true; break; }
            if (!hasLetter) continue;                       // pure-digit token at
            if (Noise.Contains(cl.ToLowerInvariant())) continue;
            // PascalCase (ilk harf büyük)
            kept.Add(char.ToUpperInvariant(cl[0]) + (cl.Length > 1 ? cl.Substring(1) : ""));
            if (kept.Count >= 3) break;
        }
        string name = string.Join("", kept);
        return string.IsNullOrEmpty(name) ? "Car" : name;
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
