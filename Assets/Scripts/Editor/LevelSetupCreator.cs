using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Sprint 1 ikinci faz kurulumu: Bomba prefabı + örnek LevelData asset'leri üretir ve sahneyi bağlar
/// (LevelManager ekle + levels ata + statik _DecoGroup'u sil → artık nesneler runtime spawn edilir).
/// Menu: Tools/GET_IT/Create Sample Levels
/// </summary>
public static class LevelSetupCreator
{
    const string DECO_DIR   = "Assets/Prefabs/DecoObjects";
    const string LEVELS_DIR = "Assets/Levels";
    const string BOMB_PATH  = "Assets/Prefabs/Bomb.prefab";
    const string MATS_DIR   = "Assets/Materials/Bomb";

    [MenuItem("Tools/GET_IT/Create Sample Levels")]
    public static void Run()
    {
        EnsureDir(LEVELS_DIR);

        var bomb = CreateBombPrefab();

        // Örnek 3 level (artan zorluk; 3.'de bomba)
        var l1 = CreateLevel("World0_Level1", 0, 0, 75f,
            spawns: new() { ("FlowerPot", 6), ("SmallBarrel", 4), ("SmallCrate", 4), ("Bench", 2), ("StreetLamp", 2), ("Tree", 1), ("LargePlanter", 2) },
            objectives: new() { ("FlowerPot", 3), ("SmallBarrel", 2) },
            bomb: null, bombCount: 0);   // Level 1 bombasız (bombalar Level 3'te)

        var l2 = CreateLevel("World0_Level2", 0, 1, 65f,
            spawns: new() { ("FlowerPot", 5), ("SmallBarrel", 5), ("SmallCrate", 5), ("Bench", 3), ("StreetLamp", 3), ("Tree", 2), ("LargePlanter", 2) },
            objectives: new() { ("SmallCrate", 3), ("StreetLamp", 2), ("Tree", 1) },
            bomb: null, bombCount: 0);

        var l3 = CreateLevel("World0_Level3", 0, 2, 60f,
            spawns: new() { ("FlowerPot", 6), ("SmallBarrel", 4), ("SmallCrate", 4), ("Bench", 3), ("StreetLamp", 4), ("Tree", 2), ("LargePlanter", 3) },
            objectives: new() { ("FlowerPot", 4), ("LargePlanter", 2), ("Bench", 2) },
            bomb: bomb, bombCount: 3);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        WireScene(new[] { l1, l2, l3 });

        Debug.Log("[LevelSetup] Bomba + 3 level üretildi, sahne bağlandı (LevelManager + _DecoGroup silindi).");
    }

    // ── BOMBA PREFABI (karikatür 3B: siyah parlak küre + boyun + fitil + kıvılcım) ──
    static GameObject CreateBombPrefab()
    {
        EnsureDir(MATS_DIR);   // materyaller GERÇEK .mat asset'i olsun (gömülü inline magenta veriyor)

        // Gövde = küre (root), primitive sphere collider ile gelir.
        var root = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        root.name = "Bomb";
        root.GetComponent<SphereCollider>().radius = 0.5f;
        root.GetComponent<MeshRenderer>().sharedMaterial = BombBodyMat();

        // Görsel parçalar (collider YOK — küre yuvarlanırken takılmasın)
        AddVisual(root, "Neck", PrimitiveType.Cylinder,
            new Vector3(0f, 0.52f, 0f), Vector3.zero, new Vector3(0.22f, 0.12f, 0.22f),
            Lit("BombNeck", new Color(0.14f, 0.14f, 0.16f), 0.5f, 0.6f));
        AddVisual(root, "Fuse", PrimitiveType.Cylinder,
            new Vector3(0.07f, 0.74f, 0f), new Vector3(0f, 0f, -22f), new Vector3(0.06f, 0.18f, 0.06f),
            Lit("BombFuse", new Color(0.55f, 0.42f, 0.25f), 0.2f, 0f));
        AddVisual(root, "Spark", PrimitiveType.Sphere,
            new Vector3(0.16f, 0.94f, 0f), Vector3.zero, new Vector3(0.13f, 0.13f, 0.13f),
            Emissive("BombSpark", new Color(1f, 0.92f, 0.45f), new Color(2.4f, 1.6f, 0.2f)));

        var rb = root.AddComponent<Rigidbody>();
        rb.mass = 3f;
        rb.linearDamping = 0.1f;        // hafif (gezinmesin ama hareket edebilsin)
        rb.angularDamping = 0.05f;      // DÜŞÜK → küre serbestçe YUVARLANIR
        rb.useGravity = true;
        rb.constraints = RigidbodyConstraints.None;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        rb.maxDepenetrationVelocity = 1.5f;

        var sw = root.AddComponent<PhysicsSwallowable>();
        sw.objectType = "Bomb";
        sw.isBomb = true;
        sw.scoreValue = 0;
        sw.growAmount = 0.05f;

        // Materyal/texture asset'lerini prefab onları REFERANS almadan ÖNCE diske commit et
        // (yoksa prefab kopuk referans tutar → runtime magenta).
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        var prefab = PrefabUtility.SaveAsPrefabAsset(root, BOMB_PATH);
        Object.DestroyImmediate(root);
        return prefab;
    }

    static void AddVisual(GameObject root, string name, PrimitiveType type,
        Vector3 localPos, Vector3 localEuler, Vector3 localScale, Material mat)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        var col = go.GetComponent<Collider>();
        if (col != null) Object.DestroyImmediate(col);   // görsel-only (yuvarlanmayı bozmasın)
        go.transform.SetParent(root.transform, false);
        go.transform.localPosition = localPos;
        go.transform.localEulerAngles = localEuler;
        go.transform.localScale = localScale;
        go.GetComponent<MeshRenderer>().sharedMaterial = mat;
    }

    static Material Lit(string name, Color baseColor, float smoothness, float metallic)
    {
        var m = LoadOrCreateMat(name);
        m.SetColor("_BaseColor", baseColor);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
        m.SetTexture("_BaseMap", null); m.mainTexture = null;
        EditorUtility.SetDirty(m);
        return m;
    }

    static Material Emissive(string name, Color baseColor, Color emission)
    {
        var m = LoadOrCreateMat(name);
        m.SetColor("_BaseColor", baseColor);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.6f);
        m.EnableKeyword("_EMISSION");
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", emission);
        EditorUtility.SetDirty(m);
        return m;
    }

    // ÇALIŞAN bir URP materyalini KOPYALAYARAK yarat (kod ile `new Material(Shader.Find)` materyali
    // runtime'da magenta verebiliyor — URP'nin beklediği iç kurulum eksik kalıyor). Var olanı yeniden
    // kullan (GUID sabit, referans kopmaz). Template: bir DecoObject .mat'i (URP/Lit, sorunsuz render).
    const string MAT_TEMPLATE = "Assets/Materials/DecoObjects/Clay_Red.mat";
    static Material LoadOrCreateMat(string name)
    {
        string path = $"{MATS_DIR}/{name}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            AssetDatabase.CopyAsset(MAT_TEMPLATE, path);
            m = AssetDatabase.LoadAssetAtPath<Material>(path);
            m.name = name;
        }
        return m;
    }

    // Gövde: "BOMB" yazısı DOKUYA basılmış (ekvator bandında 180° arayla 2 kez → ön+arka).
    // Küre UV'si: U=boylam (çevre), V=enlem. Yatay yazı bandı = ekvator halkası → kola etiketi gibi,
    // küreyle birlikte yuvarlanır.
    static Material BombBodyMat()
    {
        var m = LoadOrCreateMat("BombBody");
        m.SetColor("_BaseColor", Color.white);   // doku renkleri olduğu gibi gözüksün
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.78f);
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0.35f);

        var tex = BakeBombTexturePNG("BombBodyTex",
            new Color(0.07f, 0.07f, 0.08f), new Color(0.95f, 0.92f, 0.85f));
        m.SetTexture("_BaseMap", tex);
        m.mainTexture = tex;
        EditorUtility.SetDirty(m);
        return m;
    }

    // Texture'ı NORMAL PNG olarak import et (ham Texture2D .asset değil) → sağlam referans, magenta yok.
    static Texture2D BakeBombTexturePNG(string name, Color bg, Color textCol)
    {
        var t = BakeBombTexture(bg, textCol);
        string assetPath = $"{MATS_DIR}/{name}.png";
        string full = System.IO.Path.Combine(Application.dataPath, assetPath.Substring("Assets/".Length));
        System.IO.File.WriteAllBytes(full, t.EncodeToPNG());
        Object.DestroyImmediate(t);
        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);

        var imp = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (imp != null)
        {
            imp.textureType = TextureImporterType.Default;
            imp.sRGBTexture = true;
            imp.mipmapEnabled = true;
            imp.wrapMode = TextureWrapMode.Repeat;
            imp.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
    }

    // 5×7 bitmap glyph'lerle "BOMB" çiz (font bağımlılığı yok). İki kopya: U≈0.25 ve U≈0.75.
    static readonly System.Collections.Generic.Dictionary<char, string[]> Glyphs = new()
    {
        { 'B', new[]{ "11110","10001","10001","11110","10001","10001","11110" } },
        { 'O', new[]{ "01110","10001","10001","10001","10001","10001","01110" } },
        { 'M', new[]{ "10001","11011","10101","10101","10001","10001","10001" } },
    };

    static Texture2D BakeBombTexture(Color bg, Color textCol)
    {
        const int W = 256, H = 128, scale = 3;
        var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat };
        var fill = new Color[W * H];
        for (int i = 0; i < fill.Length; i++) fill[i] = bg;
        tex.SetPixels(fill);

        DrawCentered(tex, "BOMB", W / 4, H / 2, scale, textCol);        // U≈0.25 (ön)
        DrawCentered(tex, "BOMB", W * 3 / 4, H / 2, scale, textCol);    // U≈0.75 (arka)

        tex.Apply();
        return tex;
    }

    static void DrawCentered(Texture2D tex, string s, int cx, int cy, int scale, Color col)
    {
        int w = (s.Length * 6 - 1) * scale;   // her harf 5 + 1 boşluk
        int h = 7 * scale;
        DrawString(tex, s, cx - w / 2, cy - h / 2, scale, col);
    }

    static void DrawString(Texture2D tex, string s, int startX, int startY, int scale, Color col)
    {
        int x = startX;
        foreach (char ch in s)
        {
            if (Glyphs.TryGetValue(ch, out var g))
            {
                for (int r = 0; r < 7; r++)
                    for (int c = 0; c < 5; c++)
                        if (g[r][c] == '1')
                            FillBlock(tex, x + c * scale, startY + (6 - r) * scale, scale, col);
                x += 6 * scale;
            }
            else x += 4 * scale;
        }
    }

    static void FillBlock(Texture2D tex, int x0, int y0, int size, Color col)
    {
        for (int y = y0; y < y0 + size; y++)
            for (int x = x0; x < x0 + size; x++)
                if (x >= 0 && x < tex.width && y >= 0 && y < tex.height)
                    tex.SetPixel(x, y, col);
    }

    // ── LEVEL ASSET ───────────────────────────────────────────────────────────
    static LevelData CreateLevel(string name, int world, int index, float time,
        List<(string type, int count)> spawns,
        List<(string type, int required)> objectives,
        GameObject bomb, int bombCount)
    {
        string path = $"{LEVELS_DIR}/{name}.asset";
        var data = AssetDatabase.LoadAssetAtPath<LevelData>(path);
        bool isNew = data == null;
        if (isNew) data = ScriptableObject.CreateInstance<LevelData>();

        data.worldId = world;
        data.levelIndex = index;
        data.levelTime = time;

        data.spawns = new List<LevelData.SpawnEntry>();
        foreach (var (type, count) in spawns)
        {
            var pf = AssetDatabase.LoadAssetAtPath<GameObject>($"{DECO_DIR}/{type}.prefab");
            if (pf == null) { Debug.LogWarning($"[LevelSetup] prefab yok: {type}"); continue; }
            data.spawns.Add(new LevelData.SpawnEntry { prefab = pf, count = count });
        }

        data.objectives = new List<LevelData.ObjectiveEntry>();
        foreach (var (type, required) in objectives)
            data.objectives.Add(new LevelData.ObjectiveEntry { objectType = type, required = required });

        data.bombsEnabled = bomb != null;
        data.bombPrefab = bomb;
        data.bombCount = bombCount;

        if (isNew) AssetDatabase.CreateAsset(data, path);
        else EditorUtility.SetDirty(data);
        return data;
    }

    // ── SAHNE BAĞLAMA ─────────────────────────────────────────────────────────
    static void WireScene(LevelData[] levels)
    {
        // Statik dekoru kaldır (artık runtime spawn)
        var deco = GameObject.Find("_DecoGroup");
        if (deco != null) Object.DestroyImmediate(deco);

        // LevelManager bul/oluştur
        var lmGo = GameObject.Find("LevelManager");
        if (lmGo == null) lmGo = new GameObject("LevelManager");
        var lm = lmGo.GetComponent<LevelManager>();
        if (lm == null) lm = lmGo.AddComponent<LevelManager>();
        lm.levels = levels;
        EditorUtility.SetDirty(lm);

        EditorSceneManager.MarkSceneDirty(lmGo.scene);
    }

    static void EnsureDir(string assetPath)
    {
        string full = Path.Combine(Application.dataPath, assetPath.Substring("Assets/".Length));
        if (!Directory.Exists(full)) { Directory.CreateDirectory(full); AssetDatabase.Refresh(); }
    }
}
