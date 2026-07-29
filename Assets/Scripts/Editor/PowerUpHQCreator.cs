using UnityEditor;
using UnityEngine;

/// <summary>
/// SADECE Foods L1 tanıtımı için YÜKSEK KALİTE (HQ) güç-up prefabları üretir. Normal power-up'lar performans için
/// decimate + 1024 texture'a düşürüldü; L1'de kameranın zoom yaptığı yakın çekimde bunlar kalitesiz görünüyor.
/// Bu araç GLB kaynağından TAM MESH (decimate YOK) + 2048 texture ile ayrı prefablar (PowerGrowHQ/…) üretir.
/// Diğer levellar normal (optimize) prefabları kullanmaya devam eder; sadece World1_Level1 spawn'ları HQ'ya geçer.
/// Menu: Tools/GET_IT/Create HQ PowerUps (Foods L1)
/// </summary>
public static class PowerUpHQCreator
{
    const string ART_DIR    = "Assets/Art/PowerUps";
    const string PREFAB_DIR = "Assets/Prefabs/PowerUps";
    const string HQ_TEX_DIR = "Assets/Prefabs/PowerUps/HQ_Tex";
    const int    HQ_TEX     = 2048;

    struct Cfg
    {
        public string glb, prefab, objectType; public PowerUpType power; public float target;
        public Cfg(string g, string p, string o, PowerUpType pw, float t) { glb = g; prefab = p; objectType = o; power = pw; target = t; }
    }

    static readonly Cfg[] Items =
    {
        new Cfg("Meshy_AI_Grow_Bigger_Power_Up__0627153941_image-to-3d-texture.glb", "PowerGrowHQ",   "PowerGrow",   PowerUpType.SizeBurst, 1.0f),
        new Cfg("Meshy_AI_Lightning_Bolt_Speed__0627153955_image-to-3d-texture.glb", "PowerSpeedHQ",  "PowerSpeed",  PowerUpType.Speed,     1.0f),
        new Cfg("Meshy_AI_Horseshoe_Magnet_Powe_0627154003_image-to-3d-texture.glb", "PowerMagnetHQ", "PowerMagnet", PowerUpType.Magnet,    1.0f),
    };

    [MenuItem("Tools/GET_IT/Create HQ PowerUps (Foods L1)")]
    public static void Run()
    {
        foreach (var c in Items)
        {
            string glbPath    = $"{ART_DIR}/{c.glb}";
            string prefabPath = $"{PREFAB_DIR}/{c.prefab}.prefab";
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(glbPath);
            if (model == null) { Debug.LogWarning($"[PowerUpHQ] GLB yok/import edilmemiş: {glbPath}"); continue; }

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
            mesh.transform.localPosition = new Vector3(-b.center.x, -b.min.y, -b.center.z);

            // DECIMATE YOK (tam mesh — yakın çekimde temiz silüet/UV). Texture 2048 HQ.
            MeshyImport.DownscaleTextures(mesh, HQ_TEX_DIR, c.prefab, HQ_TEX);

            var rb = root.AddComponent<Rigidbody>();
            rb.mass = 3f; rb.linearDamping = 0.4f; rb.angularDamping = 0.05f;
            rb.useGravity = true; rb.isKinematic = false;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            rb.maxDepenetrationVelocity = 1.5f;

            var col = root.AddComponent<BoxCollider>();
            Bounds wb = CombinedBounds(root);
            col.center = root.transform.InverseTransformPoint(wb.center);
            col.size = new Vector3(wb.size.x * 0.8f, wb.size.y, wb.size.z * 0.8f);
            col.sharedMaterial = GripMat.Get();

            var sw = root.AddComponent<PhysicsSwallowable>();
            sw.growAmount = 0.04f; sw.scoreValue = 0; sw.objectType = c.objectType; sw.powerUp = c.power;

            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);
            Debug.Log($"[PowerUpHQ] Prefab: {prefabPath} (tam mesh + {HQ_TEX} tex, powerUp={c.power})");
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        SwapFoodsL1();
        Debug.Log("[PowerUpHQ] Bitti — HQ power-up prefabları üretildi + Foods L1 spawn'larına bağlandı.");
    }

    // World1_Level1 (Foods L1) power-up spawn'larını HQ prefablarla değiştir. Diğer levellar dokunulmaz.
    static void SwapFoodsL1()
    {
        var grow   = AssetDatabase.LoadAssetAtPath<GameObject>($"{PREFAB_DIR}/PowerGrowHQ.prefab");
        var speed  = AssetDatabase.LoadAssetAtPath<GameObject>($"{PREFAB_DIR}/PowerSpeedHQ.prefab");
        var magnet = AssetDatabase.LoadAssetAtPath<GameObject>($"{PREFAB_DIR}/PowerMagnetHQ.prefab");
        var ld = AssetDatabase.LoadAssetAtPath<LevelData>("Assets/Levels/World1_Level1.asset");
        if (ld == null || grow == null || speed == null || magnet == null) { Debug.LogWarning("[PowerUpHQ] L1 swap atlandı (asset yok)."); return; }
        int n = 0;
        for (int i = 0; i < ld.spawns.Count; i++)
        {
            var e = ld.spawns[i];
            if (e.prefab == null) continue;
            string nm = e.prefab.name;
            if      (nm == "PowerGrow")   { e.prefab = grow;   ld.spawns[i] = e; n++; }
            else if (nm == "PowerSpeed")  { e.prefab = speed;  ld.spawns[i] = e; n++; }
            else if (nm == "PowerMagnet") { e.prefab = magnet; ld.spawns[i] = e; n++; }
        }
        EditorUtility.SetDirty(ld);
        AssetDatabase.SaveAssets();
        Debug.Log($"[PowerUpHQ] World1_Level1: {n} power-up spawn'ı HQ'ya çevrildi.");
    }

    static Bounds CombinedBounds(GameObject go)
    {
        var rs = go.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.one * 0.5f);
        var b = rs[0].bounds;
        for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
        return b;
    }
}
