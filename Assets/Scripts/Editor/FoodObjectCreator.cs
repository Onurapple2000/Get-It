using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Yiyecekler dünyası (Sprint 5) prefablarını Meshy GLB modellerinden üretir.
/// Her model: kök GO + ölçeklenip yere oturtulmuş mesh child + Rigidbody (deco profili) +
/// bounds BoxCollider + PhysicsSwallowable (objectType=temiz ad, boyuta göre grow/score).
/// Kaynak: Assets/Art/worlds/foods/*.glb  →  Assets/Prefabs/Foods/*.prefab
/// Menu: Tools/GET_IT/Create Food Objects
/// </summary>
public static class FoodObjectCreator
{
    const string ART_DIR    = "Assets/Art/worlds/foods";
    const string PREFAB_DIR = "Assets/Prefabs/Foods";

    struct Cfg
    {
        public string match, prefab;   // match = dosya adında geçen benzersiz parça
        public float target;           // en büyük boyut bu birime ölçeklenir
        public int score;
        public Cfg(string match, string prefab, float target, int score)
        { this.match = match; this.prefab = prefab; this.target = target; this.score = score; }
    }

    // Boyut/skor küçük→büyük (zorluk eğrisi + büyüme için). target = world birim cinsinden max boyut.
    static readonly Cfg[] Foods =
    {
        new Cfg("Tomato",         "Tomato",       0.5f,  8),
        new Cfg("Donut",          "Donut",        0.6f, 10),
        new Cfg("Broccoli",       "Broccoli",     0.7f, 14),
        new Cfg("Fried_Egg",      "FriedEgg",     0.8f, 18),
        new Cfg("Pizza_Slice",    "PizzaSlice",   0.85f,20),
        new Cfg("French_Fries",   "FrenchFries",  0.85f,22),
        new Cfg("Hot_Dog",        "HotDog",       0.9f, 24),
        new Cfg("Sushi",          "Sushi",        0.9f, 26),
        new Cfg("Kebab",          "Kebab",        1.0f, 28),
        new Cfg("Hamburger",      "Hamburger",    0.95f,30),
        new Cfg("Ramen",          "Ramen",        1.1f, 40),
        new Cfg("Chicken_Salad",  "ChickenSalad", 1.2f, 50),
        new Cfg("Spaghetti",      "Spaghetti",    1.2f, 50),
        new Cfg("Whole_Pizza",    "WholePizza",   1.4f, 40),   // orta boy → 40 (≤60: kamera titremesin, kullanıcı 2026-08-20)
        // Yeni nesneler (2026-06-29)
        new Cfg("cupcake",        "Cupcake",      0.55f,12),
        new Cfg("icecream",       "IceCream",     0.7f, 18),
        new Cfg("kruvasan",       "Croissant",    0.8f, 20),
        new Cfg("steak",          "Steak",        1.0f, 35),
        new Cfg("Taco",           "Taco",         0.85f,24),
        new Cfg("bentobox",       "BentoBox",     1.15f,48),
        new Cfg("Rectangular_Tray", "Tray",       1.4f, 16),
        // 5 renk macaron (heykel/eser için renkli)
        new Cfg("macaron_pink",   "MacaronPink",   0.45f,12),
        new Cfg("macaron_blue",   "MacaronBlue",   0.45f,12),
        new Cfg("macaron_green",  "MacaronGreen",  0.45f,12),
        new Cfg("macaron_yellow", "MacaronYellow", 0.45f,12),
        new Cfg("Macaron_Purple", "MacaronPurple", 0.45f,12),

        // ── Yeni yiyecekler (2026-07-06, extraFoods) ──────────────────────────
        // match = dosya adında SADECE o modelde geçen benzersiz parça (çakışma yok).
        // Meyve/sebze (küçük)
        new Cfg("apple1",              "Apple",        0.5f,  8),
        new Cfg("pear_0706",           "Pear",         0.55f,10),
        new Cfg("cherry_red",          "Cherry",       0.35f, 6),
        new Cfg("strawbe",             "Strawberry",   0.4f,  8),
        new Cfg("Cucumber",            "Cucumber",     0.7f, 12),
        new Cfg("Eggplant",            "Eggplant",     0.7f, 14),
        new Cfg("Palmyra",             "PalmFruit",    0.6f, 12),
        new Cfg("Jack_o_Lantern",      "Pumpkin",      1.0f, 30),
        // Tatlı/fırın
        new Cfg("Cookies_in_Red",      "Cookies",      0.55f,12),
        new Cfg("Heartfelt",           "HeartCookies", 0.55f,12),
        new Cfg("Cherry_Danish",       "CherryDanish", 0.6f, 14),
        new Cfg("Popcorn",             "Popcorn",      0.8f, 20),
        new Cfg("Golden_Twist",        "Pretzel",      0.7f, 18),
        new Cfg("goldbraun",           "Toast",        0.6f, 14),
        new Cfg("torta_de_peru",       "Pie",          0.9f, 40),
        // Ekmek
        new Cfg("Artisan_Bread",       "Bread",        0.85f,24),
        new Cfg("long_fren",           "Baguette",     0.9f, 20),
        // Et/protein
        new Cfg("Sliced_Sausage",      "Sausage",      0.7f, 16),
        new Cfg("glistening_skewer",   "Skewer",       0.9f, 26),
        new Cfg("Paneer_Tikka",        "PaneerTikka",  0.9f, 28),
        new Cfg("Kibbeh",              "Kibbeh",       0.6f, 16),
        new Cfg("Tamales",             "Tamales",      0.8f, 22),
        new Cfg("Juicy_steak",         "SteakPlate",   1.0f, 40),
        new Cfg("Double_Cheeseburger", "Cheeseburger", 0.95f,32),
        new Cfg("7liburger",           "TowerBurger",  1.1f, 45),
        // Deniz ürünü / suşi
        new Cfg("Ebi_Nigiri",          "EbiNigiri",    0.5f, 14),
        new Cfg("AI_Nigiri_Delight",   "Nigiri",       0.5f, 14),
        new Cfg("Salmon_Nigiri",       "SalmonNigiri", 0.5f, 14),
        new Cfg("Salmon_Delight",      "Salmon",       1.0f, 40),
        // Dünya mutfağı / tabak
        new Cfg("Khachapuri",          "Khachapuri",   0.9f, 30),
        new Cfg("Margherita",          "MargheritaPizza",1.2f,55),
        new Cfg("veggie_paradise",     "VeggiePlatter",1.3f, 60),
        new Cfg("marul_tabagi",        "Lettuce",      1.2f, 50),
    };

    [MenuItem("Tools/GET_IT/Create Food Objects")]
    public static void Run()
    {
        EnsureDir(PREFAB_DIR);
        AssetDatabase.Refresh();   // yeni/işlenmemiş GLB'ler (ör. Hamburger meta 155B) import olsun

        // Önce üst klasör (mevcut eşleşmeler oradan çözülsün), sonra extraFoods (yeni modeller).
        var baseDir = Path.Combine(Application.dataPath, "Art/worlds/foods");
        var glbList = new System.Collections.Generic.List<string>(Directory.GetFiles(baseDir, "*.glb"));
        var extraDir = Path.Combine(baseDir, "extraFoods");
        if (Directory.Exists(extraDir)) glbList.AddRange(Directory.GetFiles(extraDir, "*.glb"));
        var glbs = glbList.ToArray();

        int made = 0;
        foreach (var c in Foods)
        {
            string file = System.Array.Find(glbs, g => Path.GetFileName(g).Contains(c.match));
            if (file == null) { Debug.LogWarning($"[FoodCreator] GLB bulunamadı (match='{c.match}')"); continue; }
            // tam varlık yolu (üst klasör ya da extraFoods): dataPath'e göre türet.
            string glbPath = "Assets" + file.Substring(Application.dataPath.Length).Replace('\\', '/');
            if (CreatePrefab(glbPath, c)) made++;
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[FoodCreator] {made}/{Foods.Length} yiyecek prefabı üretildi → {PREFAB_DIR}");
    }

    static bool CreatePrefab(string glbPath, Cfg c)
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(glbPath);
        if (model == null) { Debug.LogWarning($"[FoodCreator] import edilmemiş: {glbPath}"); return false; }

        string prefabPath = $"{PREFAB_DIR}/{c.prefab}.prefab";
        var root = new GameObject(c.prefab);

        var mesh = Object.Instantiate(model);
        mesh.transform.SetParent(root.transform, false);
        mesh.transform.localPosition = Vector3.zero;
        mesh.transform.localRotation = Quaternion.identity;

        Bounds b = CombinedBounds(mesh);
        float maxDim = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
        float scale = maxDim > 0.0001f ? c.target / maxDim : 1f;
        mesh.transform.localScale = Vector3.one * scale;

        b = CombinedBounds(mesh);
        mesh.transform.localPosition = new Vector3(-b.center.x, -b.min.y, -b.center.z);  // ortala + taban y=0

        MeshDecimate.DecimateInstance(mesh, 5000, "Assets/Prefabs/Foods/Meshes", c.prefab);  // ağırsa düşür (3k'lar atlanır)
        MeshyImport.DownscaleTextures(mesh, "Assets/Prefabs/Foods/Tex", c.prefab, 1024);     // 4K → 1024

        var rb = root.AddComponent<Rigidbody>();
        rb.mass = Mathf.Max(1f, c.target * 4f);
        rb.linearDamping = 0.4f;
        rb.angularDamping = 0.05f;
        rb.useGravity = true;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        rb.maxDepenetrationVelocity = 1.5f;

        var col = root.AddComponent<BoxCollider>();
        Bounds wb = CombinedBounds(root);
        col.center = root.transform.InverseTransformPoint(wb.center);
        // Taban ayak izini görselden DAR yap (XZ ×0.8): kare collider köşeleri yuvarlak delik kenarına
        // takılmasın → "görsel olarak girer gibi görünen" nesne gerçekten rahat girer. Yükseklik tam.
        col.size = new Vector3(wb.size.x * 0.8f, wb.size.y, wb.size.z * 0.8f);
        col.sharedMaterial = GripMat.Get();   // statik yüksek/dinamik düşük (stabil + kolay ayrılma)

        var sw = root.AddComponent<PhysicsSwallowable>();
        sw.objectType = c.prefab;
        sw.scoreValue = c.score;
        sw.growAmount = Mathf.Clamp(c.target * 0.18f, 0.06f, 0.32f);

        PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        Object.DestroyImmediate(root);
        Debug.Log($"[FoodCreator] {prefabPath} (size~{c.target}, score {c.score})");
        return true;
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
