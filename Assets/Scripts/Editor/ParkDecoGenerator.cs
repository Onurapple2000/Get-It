using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Park dünyası (worldId 0) için PROSEDÜREL ek deco nesneleri üretir — Unity primitiflerinden (küp/silindir/küre)
/// tanınabilir park objeleri kurar (çalı, kaya, çöp kutusu, yangın musluğu, posta kutusu, piknik masası, çeşme,
/// çardak, heykel). FBX deco'ları (FlowerPot/Tree/Bench...) TAMAMLAR → çeşitlilik. Her nesne: primitiv mesh'ler +
/// düz-renk URP materyal (Clay_Red.mat KOPYALANIR → magenta tuzağı yok) + box collider (XZ×0.8) + Rigidbody +
/// PhysicsSwallowable. Assets/Prefabs/DecoObjects/*.prefab (BuildGenericWorld Park'ı buradan doldurur).
/// Menü: Tools/GET_IT/Generate Park Deco (procedural)
/// </summary>
public static class ParkDecoGenerator
{
    const string PREFAB_DIR = "Assets/Prefabs/DecoObjects";
    const string MAT_DIR    = "Assets/Prefabs/DecoObjects/Mats";
    const string TEMPLATE   = "Assets/Materials/DecoObjects/Clay_Red.mat";

    static readonly Color Green   = new(0.28f, 0.55f, 0.22f);
    static readonly Color Stone   = new(0.55f, 0.55f, 0.58f);
    static readonly Color DarkGry = new(0.28f, 0.28f, 0.30f);
    static readonly Color Red     = new(0.78f, 0.16f, 0.12f);
    static readonly Color Blue    = new(0.20f, 0.40f, 0.75f);
    static readonly Color Wood    = new(0.45f, 0.30f, 0.16f);
    static readonly Color Tan     = new(0.74f, 0.67f, 0.50f);
    static readonly Color White   = new(0.90f, 0.90f, 0.86f);
    static readonly Color Water   = new(0.35f, 0.60f, 0.82f);

    static Dictionary<string, Material> _mats;
    static GameObject _root;

    [MenuItem("Tools/GET_IT/Generate Park Deco")]
    public static void Run()
    {
        EnsureDir(PREFAB_DIR); EnsureDir(MAT_DIR);
        _mats = new Dictionary<string, Material>();

        Bush(); GardenRock(); TrashCan(); FireHydrant(); Mailbox();
        PicnicTable(); Fountain(); Gazebo(); Statue();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[ParkDeco] 9 prosedürel park nesnesi üretildi → " + PREFAB_DIR);
    }

    // ── NESNELER (taban y=0, yukarı doğru kurulur) ──
    static void Bush()
    {
        Begin();
        Part(PrimitiveType.Sphere, new(0f, 0.30f, 0f),     new(0.7f, 0.6f, 0.7f), Green);
        Part(PrimitiveType.Sphere, new(0.26f, 0.24f, 0.1f),new(0.5f, 0.45f, 0.5f), Green);
        Part(PrimitiveType.Sphere, new(-0.24f, 0.26f, -0.08f), new(0.5f, 0.45f, 0.5f), Green);
        End("Bush", 10, 0.11f);
    }

    static void GardenRock()
    {
        Begin();
        Part(PrimitiveType.Sphere, new(0f, 0.22f, 0f),  new(0.8f, 0.5f, 0.7f), Stone);
        Part(PrimitiveType.Sphere, new(0.3f, 0.14f, 0.12f), new(0.4f, 0.3f, 0.4f), Stone);
        End("GardenRock", 9, 0.10f);
    }

    static void TrashCan()
    {
        Begin();
        Part(PrimitiveType.Cylinder, new(0f, 0.34f, 0f), new(0.44f, 0.34f, 0.44f), DarkGry);
        Part(PrimitiveType.Cylinder, new(0f, 0.70f, 0f), new(0.50f, 0.04f, 0.50f), Stone);   // kapak
        End("TrashCan", 12, 0.12f);
    }

    static void FireHydrant()
    {
        Begin();
        Part(PrimitiveType.Cylinder, new(0f, 0.30f, 0f), new(0.30f, 0.30f, 0.30f), Red);      // gövde
        Part(PrimitiveType.Sphere,   new(0f, 0.66f, 0f), new(0.34f, 0.26f, 0.34f), Red);      // tepe
        Part(PrimitiveType.Cylinder, new(0.26f, 0.42f, 0f), new(0.10f, 0.12f, 0.10f), Stone, new(0f, 0f, 90f)); // yan ağız
        End("FireHydrant", 14, 0.13f);
    }

    static void Mailbox()
    {
        Begin();
        Part(PrimitiveType.Cylinder, new(0f, 0.45f, 0f), new(0.12f, 0.45f, 0.12f), Wood);      // direk
        Part(PrimitiveType.Cube,     new(0f, 1.02f, 0f), new(0.44f, 0.34f, 0.66f), Blue);      // kutu
        Part(PrimitiveType.Cube,     new(0.24f, 1.06f, 0f), new(0.06f, 0.14f, 0.14f), Red);    // bayrak
        End("Mailbox", 20, 0.16f);
    }

    static void PicnicTable()
    {
        Begin();
        Part(PrimitiveType.Cube, new(0f, 0.74f, 0f),   new(1.5f, 0.10f, 0.8f), Wood);          // masa üstü
        Part(PrimitiveType.Cube, new(0f, 0.40f, 0.62f), new(1.5f, 0.10f, 0.32f), Wood);        // sıra 1
        Part(PrimitiveType.Cube, new(0f, 0.40f, -0.62f), new(1.5f, 0.10f, 0.32f), Wood);       // sıra 2
        Part(PrimitiveType.Cube, new(-0.6f, 0.37f, 0f), new(0.12f, 0.74f, 1.4f), Wood);        // ayak sol
        Part(PrimitiveType.Cube, new(0.6f, 0.37f, 0f),  new(0.12f, 0.74f, 1.4f), Wood);        // ayak sağ
        End("PicnicTable", 34, 0.24f);
    }

    static void Fountain()
    {
        Begin();
        Part(PrimitiveType.Cylinder, new(0f, 0.18f, 0f), new(1.5f, 0.18f, 1.5f), Stone);       // havuz
        Part(PrimitiveType.Cylinder, new(0f, 0.20f, 0f), new(1.25f, 0.16f, 1.25f), Water);     // su
        Part(PrimitiveType.Cylinder, new(0f, 0.55f, 0f), new(0.4f, 0.4f, 0.4f), Stone);        // orta sütun
        Part(PrimitiveType.Cylinder, new(0f, 0.95f, 0f), new(0.85f, 0.10f, 0.85f), Stone);     // üst tabak
        Part(PrimitiveType.Cylinder, new(0f, 1.02f, 0f), new(0.65f, 0.08f, 0.65f), Water);
        End("Fountain", 55, 0.32f);
    }

    static void Gazebo()
    {
        Begin();
        for (int i = 0; i < 4; i++)
        {
            float sx = (i < 2 ? 1 : -1) * 0.7f, sz = (i % 2 == 0 ? 1 : -1) * 0.7f;
            Part(PrimitiveType.Cylinder, new(sx, 0.85f, sz), new(0.12f, 0.85f, 0.12f), Tan);   // 4 direk
        }
        Part(PrimitiveType.Cube, new(0f, 1.72f, 0f), new(1.9f, 0.10f, 1.9f), Tan);             // taban tavan
        Part(PrimitiveType.Cylinder, new(0f, 2.02f, 0f), new(1.5f, 0.30f, 1.5f), Red);         // çatı (koni benzeri kısa silindir)
        Part(PrimitiveType.Sphere, new(0f, 2.32f, 0f), new(0.3f, 0.3f, 0.3f), Red);            // tepe topuz
        End("Gazebo", 60, 0.34f);
    }

    static void Statue()
    {
        Begin();
        Part(PrimitiveType.Cube,     new(0f, 0.3f, 0f),  new(0.9f, 0.6f, 0.9f), Tan);           // kaide
        Part(PrimitiveType.Cube,     new(0f, 0.7f, 0f),  new(0.55f, 0.3f, 0.55f), Tan);         // üst kaide
        Part(PrimitiveType.Capsule,  new(0f, 1.5f, 0f),  new(0.5f, 0.55f, 0.5f), White);        // gövde
        Part(PrimitiveType.Sphere,   new(0f, 2.05f, 0f), new(0.38f, 0.4f, 0.38f), White);       // baş
        End("Statue", 58, 0.33f);
    }

    // ── PRIMITIF KURMA ──
    static void Begin() { _root = null; }

    static void Part(PrimitiveType p, Vector3 pos, Vector3 scale, Color c, Vector3? euler = null)
    {
        if (_root == null) _root = new GameObject("temp");
        var go = GameObject.CreatePrimitive(p);
        Object.DestroyImmediate(go.GetComponent<Collider>());   // kendi collider'ımızı ekleyeceğiz
        go.transform.SetParent(_root.transform, false);
        go.transform.localPosition = pos;
        go.transform.localScale = scale;
        if (euler.HasValue) go.transform.localEulerAngles = euler.Value;
        go.GetComponent<MeshRenderer>().sharedMaterial = Mat(c);
    }

    static void End(string name, int score, float grow)
    {
        _root.name = name;

        var rb = _root.AddComponent<Rigidbody>();
        rb.mass = Mathf.Max(1f, grow * 20f);
        rb.linearDamping = 0.4f; rb.angularDamping = 0.05f;
        rb.useGravity = true; rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        rb.maxDepenetrationVelocity = 1.5f;

        // Box collider — renderer bounds'una otur, ayak izi XZ×0.8 (yuvarlak deliğe girsin).
        var col = _root.AddComponent<BoxCollider>();
        var rends = _root.GetComponentsInChildren<Renderer>();
        Bounds wb = rends[0].bounds;
        for (int i = 1; i < rends.Length; i++) wb.Encapsulate(rends[i].bounds);
        col.center = _root.transform.InverseTransformPoint(wb.center);
        col.size = new Vector3(wb.size.x * 0.8f, wb.size.y, wb.size.z * 0.8f);
        col.sharedMaterial = GripMat.Get();

        var sw = _root.AddComponent<PhysicsSwallowable>();
        sw.objectType = name; sw.scoreValue = score; sw.growAmount = grow;

        PrefabUtility.SaveAsPrefabAsset(_root, $"{PREFAB_DIR}/{name}.prefab");
        Object.DestroyImmediate(_root);
        _root = null;
    }

    // Düz-renk URP materyal (Clay_Red template KOPYALANIR → runtime magenta olmaz; texture kaldırılır).
    static Material Mat(Color c)
    {
        string key = ColorUtility.ToHtmlStringRGB(c);
        if (_mats.TryGetValue(key, out var m)) return m;
        string path = $"{MAT_DIR}/Deco_{key}.mat";
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.CopyAsset(TEMPLATE, path);
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", null);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.15f);
        EditorUtility.SetDirty(mat);
        _mats[key] = mat;
        return mat;
    }

    static void EnsureDir(string assetPath)
    {
        string full = Path.Combine(Application.dataPath, assetPath.Substring("Assets/".Length));
        if (!Directory.Exists(full)) Directory.CreateDirectory(full);
    }
}
