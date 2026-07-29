using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Güç-Up (Sprint 4) prefablarını Meshy GLB modellerinden üretir + World0 level'larının spawn
/// listesine az sayıda ekler. Her model: kök GO + ölçeklenip yere oturtulmuş mesh child +
/// Rigidbody (deco ayarlarıyla aynı) + bounds'a oturan BoxCollider + PhysicsSwallowable (powerUp set).
/// Menu: Tools/GET_IT/Create PowerUps
/// </summary>
public static class PowerUpCreator
{
    const string ART_DIR    = "Assets/Art/PowerUps";
    const string PREFAB_DIR = "Assets/Prefabs/PowerUps";

    struct Cfg
    {
        public string glb, prefab, objectType;
        public PowerUpType power;
        public float target;   // en büyük boyut bu birime ölçeklenir
        public Cfg(string glb, string prefab, string objectType, PowerUpType power, float target)
        { this.glb = glb; this.prefab = prefab; this.objectType = objectType; this.power = power; this.target = target; }
    }

    static readonly Cfg[] Items =
    {
        new Cfg("Meshy_AI_Grow_Bigger_Power_Up__0627153941_image-to-3d-texture.glb",
                "PowerGrow",   "PowerGrow",   PowerUpType.SizeBurst, 1.0f),
        new Cfg("Meshy_AI_Lightning_Bolt_Speed__0627153955_image-to-3d-texture.glb",
                "PowerSpeed",  "PowerSpeed",  PowerUpType.Speed,     1.0f),
        new Cfg("Meshy_AI_Horseshoe_Magnet_Powe_0627154003_image-to-3d-texture.glb",
                "PowerMagnet", "PowerMagnet", PowerUpType.Magnet,    1.0f),
    };

    static readonly string[] Levels =
    {
        "Assets/Levels/World0_Level1.asset",
        "Assets/Levels/World0_Level2.asset",
        "Assets/Levels/World0_Level3.asset",
    };

    [MenuItem("Tools/GET_IT/Create PowerUps")]
    public static void Run()
    {
        EnsureDir(PREFAB_DIR);
        CreatePrefabs();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        AddToLevels();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[PowerUpCreator] Güç-Up prefabları üretildi + level spawn'larına eklendi (grow×2, speed×2, magnet×1).");
    }

    static void CreatePrefabs()
    {
        foreach (var c in Items)
        {
            string glbPath    = $"{ART_DIR}/{c.glb}";
            string prefabPath = $"{PREFAB_DIR}/{c.prefab}.prefab";

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(glbPath);
            if (model == null) { Debug.LogWarning($"[PowerUpCreator] GLB yok/import edilmemiş: {glbPath}"); continue; }

            var root = new GameObject(c.prefab);

            // Mesh child — ölçek + yere oturtma (kök origin, identity → world bounds = local)
            var mesh = Object.Instantiate(model);
            mesh.transform.SetParent(root.transform, false);
            mesh.transform.localPosition = Vector3.zero;
            mesh.transform.localRotation = Quaternion.identity;

            Bounds b = CombinedBounds(mesh);
            float maxDim = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
            float scale = maxDim > 0.0001f ? c.target / maxDim : 1f;
            mesh.transform.localScale = Vector3.one * scale;

            b = CombinedBounds(mesh);                                  // ölçek sonrası yeniden
            mesh.transform.localPosition = new Vector3(-b.center.x, -b.min.y, -b.center.z);  // ortala + tabanı y=0

            MeshDecimate.DecimateInstance(mesh, 5000, "Assets/Prefabs/PowerUps/Meshes", c.prefab);  // ağırsa düşür
            MeshyImport.DownscaleTextures(mesh, "Assets/Prefabs/PowerUps/Tex", c.prefab, 1024);     // 4K → 1024

            // Rigidbody — deco nesnelerle aynı fizik profili (devrilebilir, sürtünmesiz zeminde sönümlü)
            var rb = root.AddComponent<Rigidbody>();
            rb.mass = 3f;
            rb.linearDamping = 0.4f;
            rb.angularDamping = 0.05f;
            rb.useGravity = true;
            rb.isKinematic = false;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            rb.maxDepenetrationVelocity = 1.5f;

            // BoxCollider — gerçek mesh bounds'una oturt
            var col = root.AddComponent<BoxCollider>();
            Bounds wb = CombinedBounds(root);
            col.center = root.transform.InverseTransformPoint(wb.center);
            col.size = new Vector3(wb.size.x * 0.8f, wb.size.y, wb.size.z * 0.8f);   // dar ayak izi (kolay yutulma)
            col.sharedMaterial = GripMat.Get();

            // PhysicsSwallowable — güç ataması (skor düşük, asıl etki güç-up)
            var sw = root.AddComponent<PhysicsSwallowable>();
            sw.growAmount = 0.04f;
            sw.scoreValue = 0;
            sw.objectType = c.objectType;
            sw.powerUp = c.power;

            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);
            Debug.Log($"[PowerUpCreator] Prefab: {prefabPath} (powerUp={c.power})");
        }
    }

    // Level spawn listelerine güç-up'ları ekle (varolan Power* girdilerini temizleyip yeniden ekler).
    static void AddToLevels()
    {
        var grow   = AssetDatabase.LoadAssetAtPath<GameObject>($"{PREFAB_DIR}/PowerGrow.prefab");
        var speed  = AssetDatabase.LoadAssetAtPath<GameObject>($"{PREFAB_DIR}/PowerSpeed.prefab");
        var magnet = AssetDatabase.LoadAssetAtPath<GameObject>($"{PREFAB_DIR}/PowerMagnet.prefab");
        if (grow == null || speed == null || magnet == null)
        { Debug.LogWarning("[PowerUpCreator] Güç-Up prefabları yüklenemedi; level eklemesi atlandı."); return; }

        foreach (var path in Levels)
        {
            var ld = AssetDatabase.LoadAssetAtPath<LevelData>(path);
            if (ld == null) { Debug.LogWarning($"[PowerUpCreator] LevelData yok: {path}"); continue; }

            ld.spawns.RemoveAll(e => e.prefab != null && e.prefab.name.StartsWith("Power"));
            ld.spawns.Add(new LevelData.SpawnEntry { prefab = grow,   count = 2 });
            ld.spawns.Add(new LevelData.SpawnEntry { prefab = speed,  count = 2 });
            ld.spawns.Add(new LevelData.SpawnEntry { prefab = magnet, count = 1 });
            EditorUtility.SetDirty(ld);
            Debug.Log($"[PowerUpCreator] Spawn eklendi: {path}");
        }
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
