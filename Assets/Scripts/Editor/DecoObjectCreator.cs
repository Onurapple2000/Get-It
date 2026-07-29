using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Imports Blender FBX files, creates proper prefabs with Rigidbody + Collider + Swallowable,
/// and places them symmetrically in the scene.
/// Menu: Tools/GET_IT/Place Deco Objects
/// </summary>
public static class DecoObjectCreator
{
    const string MODEL_DIR  = "Assets/Models/DecoObjects";
    const string PREFAB_DIR = "Assets/Prefabs/DecoObjects";

    // Per-object physics + game config
    struct ObjCfg
    {
        public float objectSize, growAmount, mass;
        public int   scoreValue;
        public Vector3 colCenter, colSize;
        public ObjCfg(float sz, float grow, int score, float mass,
                       Vector3 cc, Vector3 cs)
        {
            objectSize  = sz; growAmount = grow;
            scoreValue  = score; this.mass = mass;
            colCenter   = cc;   colSize   = cs;
        }
    }

    static readonly Dictionary<string, ObjCfg> Cfg = new()
    {
        //                    size  grow  score  mass   collider-center              collider-size
        { "FlowerPot",    new(0.45f, 0.10f,  8, 2f,  new(0, 0.37f, 0), new(0.56f, 0.74f, 0.56f)) },
        { "SmallBarrel",  new(0.45f, 0.10f,  8, 3f,  new(0, 0.21f, 0), new(0.52f, 0.42f, 0.52f)) },
        { "SmallCrate",   new(0.40f, 0.10f,  8, 4f,  new(0, 0.20f, 0), new(0.42f, 0.40f, 0.42f)) },
        { "Bench",        new(0.90f, 0.20f, 25, 8f,  new(0, 0.46f, 0), new(1.24f, 0.90f, 0.42f)) },
        { "StreetLamp",   new(1.00f, 0.22f, 30, 6f,  new(0, 1.72f, 0), new(0.38f, 3.44f, 0.38f)) },
        { "Tree",         new(1.60f, 0.35f, 60, 10f, new(0, 2.00f, 0), new(1.85f, 4.00f, 1.85f)) },
        { "LargePlanter", new(1.20f, 0.28f, 45, 12f, new(0, 1.02f, 0), new(1.75f, 2.04f, 1.75f)) },
    };

    // Sadece prefab'ları yeniden üretir (collider/config güncellemesi sonrası). Sahneye DOKUNMAZ —
    // runtime spawn (LevelManager) kullanıldığından _DecoGroup oluşturmaz. LevelData referansları
    // aynı prefab path/GUID'i koruduğundan geçerli kalır.
    [MenuItem("Tools/GET_IT/Rebuild Deco Prefabs (no placement)")]
    public static void RebuildPrefabsOnly()
    {
        EnsureDir(PREFAB_DIR);
        CreatePrefabs();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[DecoCreator] Prefab'lar yeniden üretildi (sahne yerleşimi yok).");
    }

    [MenuItem("Tools/GET_IT/Place Deco Objects")]
    public static void Run()
    {
        EnsureDir(PREFAB_DIR);

        // 1. Fix FBX import settings (bake axis conversion → objects stand upright in Unity)
        FixImports();
        AssetDatabase.Refresh();

        // 2. Create prefab wrappers with Rigidbody + BoxCollider + Swallowable
        CreatePrefabs();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // 3. Remove old group, place new objects
        var old = GameObject.Find("_DecoGroup");
        if (old != null) Object.DestroyImmediate(old);
        PlaceInScene();

        Debug.Log("[DecoCreator] Done.");
    }

    // ── FBX IMPORT FIX ────────────────────────────────────────────────────────
    static void FixImports()
    {
        foreach (var name in Cfg.Keys)
        {
            string path = $"{MODEL_DIR}/{name}.fbx";
            var imp = AssetImporter.GetAtPath(path) as ModelImporter;
            if (imp == null) { Debug.LogWarning($"FBX not found: {path}"); continue; }

            imp.globalScale              = 1.0f;
            imp.bakeAxisConversion       = false;
            imp.importNormals            = ModelImporterNormals.Calculate;
            imp.normalSmoothingAngle     = 60f;
            imp.isReadable               = false;
            // Extract materials as external .mat files so we can fix their shaders
            imp.materialLocation         = ModelImporterMaterialLocation.External;
            imp.SaveAndReimport();
            Debug.Log($"[DecoCreator] Reimported: {path}");
        }

        AssetDatabase.Refresh();

        // Fix extracted materials: Standard → URP/Lit
        var urpShader = Shader.Find("Universal Render Pipeline/Lit");
        if (urpShader == null)
        {
            Debug.LogError("[DecoCreator] URP shader not found! Make sure URP is installed.");
            return;
        }

        var matGuids = AssetDatabase.FindAssets("t:Material", new[] { MODEL_DIR });
        foreach (var guid in matGuids)
        {
            var matPath = AssetDatabase.GUIDToAssetPath(guid);
            var mat     = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null || mat.shader == urpShader) continue;

            // Preserve base color before switching shader
            Color col = Color.white;
            if (mat.HasProperty("_BaseColor"))       col = mat.GetColor("_BaseColor");
            else if (mat.HasProperty("_Color"))      col = mat.color;

            mat.shader = urpShader;
            mat.SetColor("_BaseColor", col);
            mat.SetFloat("_Smoothness", 0.2f);
            EditorUtility.SetDirty(mat);
            Debug.Log($"[DecoCreator] Fixed material: {matPath}");
        }

        AssetDatabase.SaveAssets();
    }

    // ── PREFAB CREATION ───────────────────────────────────────────────────────
    static void CreatePrefabs()
    {
        foreach (var (name, cfg) in Cfg)
        {
            string fbxPath    = $"{MODEL_DIR}/{name}.fbx";
            string prefabPath = $"{PREFAB_DIR}/{name}.prefab";

            var fbxModel = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (fbxModel == null) { Debug.LogWarning($"FBX missing: {fbxPath}"); continue; }

            // Root wrapper
            var root = new GameObject(name);

            // Rigidbody — FİZİK MEKANİĞİ: rotasyon SERBEST (nesne devrilebilsin), düşük drag.
            // NOT: FreezeRotation KULLANMA — yoksa nesne devrilemez, delik gidince gömülü kalır.
            var rb                = root.AddComponent<Rigidbody>();
            rb.mass               = cfg.mass;
            // Sürtünmesiz zemin (hareketli HoleFloor) üzerinde rezidüel kayma sönsün → nesneler "gezinmez".
            // linearDamping yalnız LİNEER hızı söndürür; devrilme AÇISAL olduğundan etkilenmez.
            rb.linearDamping      = 0.4f;
            rb.angularDamping     = 0.05f;
            rb.useGravity         = true;
            rb.isKinematic        = false;
            rb.constraints        = RigidbodyConstraints.None;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative; // tünelleme önler
            rb.maxDepenetrationVelocity = 1.5f;  // spawn'da üst üste binenler YUMUŞAK ayrışsın (fırlama yok)

            // Mesh child ÖNCE (collider'ı gerçek mesh bounds'una oturtabilmek için).
            // Plain Instantiate (PrefabUtility değil) → tüm mesh hiyerarşisi prefab'a gömülür.
            // localRotation'a DOKUNMA — FBX import Y-up için rotasyon uygulamış olabilir.
            var meshChild = Object.Instantiate(fbxModel);
            meshChild.transform.SetParent(root.transform, false);
            meshChild.transform.localPosition = Vector3.zero;

            // Collider(ler): concave nesnelere compound (primitive'ler), diğerlerine mesh'e oturan tek box
            BuildColliders(root, name, cfg);

            // Sürtünme: paylaşılan grip malzemesi (durağanken stabil, hareket halinde kolay ayrılma)
            foreach (var c in root.GetComponentsInChildren<Collider>()) c.sharedMaterial = GripMat.Get();

            // PhysicsSwallowable (fizik-temelli; eski script'li Swallowable yerine)
            var sw       = root.AddComponent<PhysicsSwallowable>();
            sw.growAmount = cfg.growAmount;
            sw.scoreValue = cfg.scoreValue;
            sw.objectType = name;   // Kota/hedef eşleştirme türü (FlowerPot, Tree, ...)

            // Save prefab
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);
            Debug.Log($"[DecoCreator] Prefab saved: {prefabPath}");
        }
    }

    // ── COLLIDER BUILD ────────────────────────────────────────────────────────
    // Concave nesneler (ince gövde + geniş tepe) tek box ile doğru davranmaz; bileşik
    // (compound) primitive collider gerekir. Diğerleri için tek box yeterli.
    static void BuildColliders(GameObject root, string name, ObjCfg cfg)
    {
        // Değerler PhysicsTest sahnesinde elle kurulup doğrulandı (2026-06-26).
        switch (name)
        {
            case "Tree": // ince gövde girer, geniş tepe rim'e takılır
                AddCapsule(root, "Trunk",  new Vector3(0f, 1.3f, 0f), 0.3f, 2.6f);
                AddSphere (root, "Canopy", new Vector3(0f, 2.7f, 0f), 1.4f);
                // Düz tabanlı base: sürtünmesiz zeminde yüksek-COM ağacın kararsız (nokta-temas) kapsül
                // ucundan devrilmesini önler. Yeterince dar → büyüyen deliğe yine girer.
                AddBox(root, "Base", new Vector3(0f, 0.18f, 0f), new Vector3(0.55f, 0.36f, 0.55f));
                break;

            case "StreetLamp": // direk + tepe lamba kolu (-X) + taban
                AddCapsule(root, "Pole", new Vector3(0f, 1.6f, 0f), 0.1f, 3.2f);
                AddBox(root, "Head", new Vector3(-0.35f, 3.3f, 0f), new Vector3(0.55f, 0.4f, 0.35f));
                AddBox(root, "Base", new Vector3(0f, 0.12f, 0f),    new Vector3(0.35f, 0.25f, 0.35f));
                break;

            case "Bench": // oturak+ayak gövde + arkalık (-Z)
                AddBox(root, "Body", new Vector3(0f, 0.22f, 0.05f),  new Vector3(1.25f, 0.46f, 0.46f));
                AddBox(root, "Back", new Vector3(0f, 0.6f, -0.15f),  new Vector3(1.25f, 0.42f, 0.1f));
                break;

            default: // basit/convex nesneler — box'ı GERÇEK mesh bounds'una oturt (pivot uyumsuzluğunu önler)
                var col = root.AddComponent<BoxCollider>();
                var rends = root.GetComponentsInChildren<Renderer>();
                if (rends.Length > 0)
                {
                    Bounds wb = rends[0].bounds;
                    for (int i = 1; i < rends.Length; i++) wb.Encapsulate(rends[i].bounds);
                    // root origin'de, scale 1, rotasyonsuz → world bounds = local bounds
                    col.center = root.transform.InverseTransformPoint(wb.center);
                    // Taban ayak izini DAR yap (XZ ×0.8) → büyük nesneler (saksı) yuvarlak deliğe rahat girsin
                    col.size   = new Vector3(wb.size.x * 0.8f, wb.size.y, wb.size.z * 0.8f);
                }
                else { col.center = cfg.colCenter; col.size = cfg.colSize; }
                break;
        }
    }

    // Compound collider yardımcıları (rotasyonsuz child + tek primitive)
    static GameObject AddChild(GameObject root, string name, Vector3 localPos)
    {
        var go = new GameObject(name);
        go.transform.SetParent(root.transform, false);
        go.transform.localPosition = localPos;
        return go;
    }
    static void AddCapsule(GameObject root, string name, Vector3 pos, float radius, float height)
    {
        var c = AddChild(root, name, pos).AddComponent<CapsuleCollider>();
        c.radius = radius; c.height = height; c.direction = 1; // Y ekseni
    }
    static void AddSphere(GameObject root, string name, Vector3 pos, float radius)
    {
        AddChild(root, name, pos).AddComponent<SphereCollider>().radius = radius;
    }
    static void AddBox(GameObject root, string name, Vector3 pos, Vector3 size)
    {
        AddChild(root, name, pos).AddComponent<BoxCollider>().size = size;
    }

    // ── SCENE PLACEMENT ───────────────────────────────────────────────────────
    static void PlaceInScene()
    {
        // Park-style symmetric layout — place near scene origin where ground exists
        var group = new GameObject("_DecoGroup");
        group.transform.position = new Vector3(0, 0, 0);

        // Large objects — tree at center, planters on sides
        Place("Tree",         group, new Vector3(   0, 0,    0));
        Place("LargePlanter", group, new Vector3(-6.5f, 0,  0));
        Place("LargePlanter", group, new Vector3( 6.5f, 0,  0));

        // Medium — 4 lamps at corners, 2 benches facing tree
        Place("StreetLamp", group, new Vector3(-4.5f, 0, -4.5f));
        Place("StreetLamp", group, new Vector3( 4.5f, 0, -4.5f));
        Place("StreetLamp", group, new Vector3(-4.5f, 0,  4.5f));
        Place("StreetLamp", group, new Vector3( 4.5f, 0,  4.5f));
        Place("Bench", group, new Vector3( 0, 0, -3.8f), Quaternion.identity);
        Place("Bench", group, new Vector3( 0, 0,  3.8f), Quaternion.Euler(0, 180, 0));

        // Small stacked — near lamps
        Stack("FlowerPot",   group, new Vector3(-4.5f, 0, -4.5f) + new Vector3( 0.9f, 0,  0.9f), 0.60f, 2);
        Stack("FlowerPot",   group, new Vector3( 4.5f, 0, -4.5f) + new Vector3(-0.9f, 0,  0.9f), 0.60f, 2);
        Stack("FlowerPot",   group, new Vector3(-4.5f, 0,  4.5f) + new Vector3( 0.9f, 0, -0.9f), 0.60f, 2);
        Stack("FlowerPot",   group, new Vector3( 4.5f, 0,  4.5f) + new Vector3(-0.9f, 0, -0.9f), 0.60f, 2);

        Stack("SmallBarrel", group, new Vector3(-2.8f, 0, 0), 0.44f, 3);
        Stack("SmallBarrel", group, new Vector3( 2.8f, 0, 0), 0.44f, 3);

        Stack("SmallCrate",  group, new Vector3(0, 0, -2.0f), 0.42f, 2);
        Stack("SmallCrate",  group, new Vector3(0, 0,  2.0f), 0.42f, 2);
    }

    static void Place(string name, GameObject parent, Vector3 local, Quaternion? rot = null)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PREFAB_DIR}/{name}.prefab");
        if (prefab == null) return;
        var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        inst.transform.SetParent(parent.transform);
        inst.transform.localPosition = local;
        inst.transform.localRotation = rot ?? Quaternion.identity;
    }

    static void Stack(string name, GameObject parent, Vector3 baseLocal, float step, int count)
    {
        for (int i = 0; i < count; i++)
            Place(name, parent, baseLocal + Vector3.up * step * i);
    }

    static void EnsureDir(string assetPath)
    {
        string full = Path.Combine(Application.dataPath, assetPath.Substring("Assets/".Length));
        if (!Directory.Exists(full)) Directory.CreateDirectory(full);
    }
}
