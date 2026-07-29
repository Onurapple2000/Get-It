using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Çekirdek fizik: katı zemin + deliğin olduğu yerde boşluk.
/// Delik-merkezli, ortasında dairesel boşluk olan kapalı KATI bir disk (washer) mesh'i üretir.
/// Mesh YEREL uzayda (origin = delik merkezi) kurulur ve obje **kinematik Rigidbody** ile deliğin
/// peşinden YUMUŞAKÇA HAREKET eder (MovePosition) — geometri ışınlanmaz. Böylece çukur duvarı
/// hareket ederken nesneleri normal kuvvetle İTER: nesne kapanan zeminin içine hapsolmaz / zeminden
/// fırlamaz, delik boşluğuna doğru sürülür ve takla deliğin İÇİNDEN tamamlanır.
/// Mesh yalnızca delik BÜYÜYÜNCE (yarıçap değişince) yeniden inşa edilir.
///
/// Üst yüzey SÜRTÜNMESİZ (frictionless): zemin her yeri kapladığından, üzerinde duran dekor
/// nesnelerinin delik hareketiyle sürüklenmemesi için. Durağan nesne yatay kuvvet almaz (yerinde
/// kalır); sadece çukur duvarının doğrudan bastığı nesne dik yüzeyin normaliyle itilir.
///
/// Kurulum: boş bir GameObject'e ekle. `hole` boşsa sahnedeki HoleController otomatik bulunur.
/// </summary>
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
public class HoleFloor : MonoBehaviour
{
    [Tooltip("Delik kaynağı (pozisyon + boyut). Boşsa otomatik bulunur.")]
    public HoleController hole;

    [Header("Manuel test (HoleController yokken)")]
    [Tooltip("HoleController yoksa elle test: merkez bu objenin pozisyonu, yarıçap aşağıdaki.")]
    public bool manualMode = false;
    public float manualRadius = 1.5f;

    [Header("Zemin")]
    [Tooltip("Dünya-sabit dış sınır yarıçapı (daire). Oyun alanından büyük olmalı.")]
    public float boundaryRadius = 30f;
    [Tooltip("Zemin texture'ının kaç dünya-biriminde bir tekrarlanacağı (tile boyutu).")]
    public float groundTile = 6f;
    [Tooltip("Çukur duvar derinliği. Uzun nesnelerin (sokak lambası vb.) tepesi zemin altına inip yutulabilmesi için yeterli olmalı.")]
    public float pitDepth = 9f;
    [Range(16, 256)] public int segments = 80;

    [Header("Güncelleme eşiği (performans)")]
    [Tooltip("Mesh yalnızca delik yarıçapı bu kadar değişince yeniden inşa edilir (pozisyon transform ile taşınır).")]
    public float radiusThreshold = 0.01f;

    [Header("Sarım (collision yüzü ters ise aç)")]
    public bool flipTopWinding = false;
    public bool flipWallWinding = false;

    [Header("Delik içi (çukur) görünümü")]
    [Tooltip("Çukur dibindeki en koyu renk.")]
    public Color pitDeepColor = new Color(0.02f, 0.02f, 0.03f);
    [Tooltip("Çukur ağzındaki (rim) renk — toprak/derinlik hissi için hafif kahve-gri.")]
    public Color pitRimColor  = new Color(0.10f, 0.08f, 0.07f);

    Mesh mesh;
    MeshCollider mc;
    Rigidbody rb;
    Material darkMat;
    Material groundMat;          // üst zemin (runtime instance — asset bozulmaz)
    float lastRadius = -1f;
    readonly List<int> topTris  = new List<int>(80 * 6);
    readonly List<int> wallTris = new List<int>(80 * 9);

    void Awake()
    {
        mesh = new Mesh { name = "HoleFloorMesh" };
        mesh.MarkDynamic();
        GetComponent<MeshFilter>().sharedMesh = mesh;
        mc = GetComponent<MeshCollider>();

        // Kinematik Rigidbody: zemin deliğin peşinden MovePosition ile YUMUŞAKÇA hareket etsin
        // (geometri ışınlanmasın → çukur duvarı nesneleri itsin, hapsetmesin).
        rb = GetComponent<Rigidbody>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
        rb.interpolation = RigidbodyInterpolation.None;   // artık LateUpdate'te transform ile kare-başı sürülüyor → interpolate lag yaratır
        // Non-convex MeshCollider yalnızca kinematik Rigidbody'de geçerli (Unity kuralı).
        mc.convex = false;

        // ⚠️ SÜRTÜNMESİZ üst yüzey (ZORUNLU): zemin deliği takip ederek MovePosition ile HAREKET ettiğinden,
        // sürtünme olursa hareketli zemin üstündeki DİNAMİK nesneleri sürükler → "delik onları peşinden çeker"
        // hatası olur. Durağan dekor delik gelene kadar KİNEMATİK donuk (freezeUntilNear) → zaten kuvvet almaz.
        // Uyanmış/düşen nesnelerin FAZLA kaymasını zeminden DEĞİL, nesnenin kendi damping'inden çözüyoruz
        // (PhysicsSwallowable.WakeNow → linearDamping artışı) → zemine kilitlenmeden yavaşlar.
        var pm = new PhysicsMaterial("HoleFloorFrictionless")
        {
            dynamicFriction = 0f,
            staticFriction = 0f,
            frictionCombine = PhysicsMaterialCombine.Minimum,
            bounceCombine = PhysicsMaterialCombine.Minimum,
            bounciness = 0f
        };
        mc.sharedMaterial = pm;

        var mr = GetComponent<MeshRenderer>();
        // Submesh 0 = üst zemin halkası (mevcut/atanmış malzeme), Submesh 1 = koyu çukur içi.
        var baseGround = mr.sharedMaterial;
        if (baseGround == null)
        {
            var sh = SafeShader("Universal Render Pipeline/Lit");
            baseGround = new Material(sh) { color = new Color(0.45f, 0.5f, 0.4f) };
        }
        // Runtime instance: tema texture/offset'i asset'i bozmadan değişebilsin.
        groundMat = new Material(baseGround) { name = "GroundMat (Instance)" };

        // Koyu iç: Unlit (sahne ışığından etkilenmez → her zaman koyu), dikey gradient, çift yüz.
        // GÜVENLİ arama: shader stripping'de null olup new Material(null) ile ÇÖKMESİN (asıl cihaz bug'ı buydu).
        var unlit = SafeShader("Universal Render Pipeline/Unlit");
        darkMat = new Material(unlit) { name = "HolePitMat" };
        if (darkMat.HasProperty("_BaseColor")) darkMat.SetColor("_BaseColor", Color.white);
        if (darkMat.HasProperty("_Cull")) darkMat.SetFloat("_Cull", 0f); // CullMode.Off — iki yüzden de görünür
        darkMat.SetTexture("_BaseMap", BuildPitGradientTexture());
        darkMat.mainTexture = darkMat.GetTexture("_BaseMap");

        mr.sharedMaterials = new[] { groundMat, darkMat };
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    /// <summary>Dünya teması: üst zemin texture'ı + tint + tile boyutu (LevelManager çağırır).</summary>
    public void SetGroundTheme(Texture2D tex, Color tint, float tile)
    {
        if (tile > 0.01f) groundTile = tile;
        if (groundMat != null)
        {
            groundMat.color = tint;
            if (groundMat.HasProperty("_BaseColor")) groundMat.SetColor("_BaseColor", tint);
            groundMat.mainTexture = tex;
            if (groundMat.HasProperty("_BaseMap")) groundMat.SetTexture("_BaseMap", tex);
        }
        if (lastRadius > 0f) Rebuild(lastRadius);   // UV tile yeniden bake
    }

    // Dikey gradient: alt (v=0) en koyu → rim (v=1) hafif toprak rengi.
    Texture2D BuildPitGradientTexture()
    {
        const int H = 16;
        var tex = new Texture2D(1, H, TextureFormat.RGB24, false)
        { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        for (int p = 0; p < H; p++)
        {
            float t = p / (float)(H - 1);          // 0 = dip, 1 = rim
            // Karesel: çoğunlukla koyu kalsın, sadece rim'e doğru biraz açılsın
            Color c = Color.Lerp(pitDeepColor, pitRimColor, t * t);
            tex.SetPixel(0, p, c);
        }
        tex.Apply();
        return tex;
    }

    void Start()
    {
        if (hole == null && !manualMode) hole = FindFirstObjectByType<HoleController>();
        // Başlangıç pozisyonunu deliğe oturt + mesh'i ilk kez kur
        if (TryGetCenterRadius(out Vector3 c0, out float r0))
        {
            transform.position = new Vector3(c0.x, 0f, c0.z);
            Rebuild(r0);
        }
    }

    bool TryGetCenterRadius(out Vector3 center, out float radius)
    {
        if (!manualMode && hole != null)
        {
            center = hole.transform.position; center.y = 0f;
            radius = Mathf.Max(0.1f, hole.currentSize * 0.5f);
            return true;
        }
        if (manualMode)
        {
            center = transform.position; center.y = 0f;
            radius = Mathf.Max(0.1f, manualRadius);
            return true;
        }
        center = default; radius = 0f;
        return false;
    }

    // ⚠️ 2026-07-25 (hız powerup YALPALAMA fix): Delik Update'te (kare-başı) hareket eder; zemin ise EskiDEN
    // FixedUpdate'te (50Hz) MovePosition+Interpolate ile takip ediyordu → render ~1 fizik-adımı geride kalıp
    // delikten geri kalıyordu (yüksek hızda belirgin titreşim = "sekme"). Artık LateUpdate'te (delik Update'inden
    // SONRA) transform.position ile BİREBİR takip → her karede delikle tam senkron, zemin dokusu düzgün akar.
    // Zemin üstü SÜRTÜNMESİZ olduğundan nesneleri sürüklemez; çukur duvarı kinematik collider olarak nesneleri
    // yine iter (kinematik body transform ile taşınsa da dinamik nesnelerle çarpışır).
    void LateUpdate()
    {
        if (manualMode || hole == null)
        {
            if (hole == null && !manualMode) hole = FindFirstObjectByType<HoleController>();
            return;
        }
        Vector3 hp = hole.transform.position;
        transform.position = new Vector3(hp.x, 0f, hp.z);   // delikle BİREBİR (kare-başı, gecikmesiz)

        // Zemin texture'ı DÜNYA-SABİT görünsün: mesh hareket ettikçe UV offset'i konumla telafi et.
        if (groundMat != null)
            groundMat.mainTextureOffset = new Vector2(hp.x / groundTile, hp.z / groundTile);

        // Mesh yalnızca delik BÜYÜYÜNCE yeniden inşa edilir (pozisyon değişimi mesh'i etkilemez).
        float r = Mathf.Max(0.1f, hole.currentSize * 0.5f);
        if (Mathf.Abs(r - lastRadius) >= radiusThreshold) Rebuild(r);
    }

    [ContextMenu("Force Rebuild")]
    public void ForceRebuild()
    {
        if (hole == null && !manualMode) hole = FindFirstObjectByType<HoleController>();
        if (TryGetCenterRadius(out Vector3 c, out float r))
        {
            transform.position = new Vector3(c.x, 0f, c.z);
            Rebuild(r);
        }
    }

    // Mesh YEREL uzayda kurulur (origin = delik merkezi). Pozisyonu transform taşır.
    void Rebuild(float radius)
    {
        lastRadius = radius;

        int n = segments;
        // KAPALI KATI ZEMİN (washer): içi dolu disk. Her segment 6 vert:
        //  0 innerTop(rim), 1 outer(sınır), 2 innerBot(rim,-pit), 3 outerBot(sınır,-pit)  → ÇUKUR (uv.y gradient)
        //  4 topInner, 5 topOuter (innerTop/outer ile aynı konum ama PLANAR UV)            → ÜST HALKA (tile)
        // Üst halka ayrı vert'lerde olmalı: çukur duvarları uv.y'yi gradient için kullanırken üst zemin
        // texture'ı (picnic vb.) dünya-XZ'ye göre tile'lanabilsin (UV çakışması olmasın).
        var verts = new Vector3[n * 6 + 1];
        var uvs   = new Vector2[n * 6 + 1];
        float inv = 1f / Mathf.Max(0.01f, groundTile);
        for (int i = 0; i < n; i++)
        {
            float a = (float)i / n * Mathf.PI * 2f;
            Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            Vector3 innerTop = dir * radius;
            Vector3 outer    = dir * boundaryRadius;
            Vector3 innerBot = innerTop + Vector3.down * pitDepth;
            Vector3 outerBot = outer + Vector3.down * pitDepth;

            int b = i * 6;
            verts[b + 0] = innerTop;  verts[b + 1] = outer;
            verts[b + 2] = innerBot;  verts[b + 3] = outerBot;
            verts[b + 4] = innerTop;  verts[b + 5] = outer;     // üst halka kopyaları (planar UV)

            // Çukur: uv.y gradient (rim=1, dip=0)
            uvs[b + 0] = new Vector2(0.5f, 1f);  uvs[b + 1] = new Vector2(0.5f, 1f);
            uvs[b + 2] = new Vector2(0.5f, 0f);  uvs[b + 3] = new Vector2(0.5f, 0f);
            // Üst halka: yerel XZ → UV (offset FixedUpdate'te konumla telafi → dünya-sabit)
            uvs[b + 4] = new Vector2(innerTop.x * inv, innerTop.z * inv);
            uvs[b + 5] = new Vector2(outer.x   * inv, outer.z   * inv);
        }
        int cap = n * 6;
        verts[cap] = new Vector3(0f, -pitDepth, 0f);
        uvs[cap]   = new Vector2(0.5f, 0f); // dip = en koyu

        topTris.Clear();
        wallTris.Clear();
        for (int i = 0; i < n; i++)
        {
            int a0 = i * 6, a1 = ((i + 1) % n) * 6;
            int aT = a0 + 0, aO = a0 + 1, aB = a0 + 2, aOB = a0 + 3, aTI = a0 + 4, aTO = a0 + 5;
            int bT = a1 + 0, bO = a1 + 1, bB = a1 + 2, bOB = a1 + 3, bTI = a1 + 4, bTO = a1 + 5;

            // Üst halka (submesh 0, zemin malzemesi): planar-UV kopyaları (tile'lı texture)
            AddQuad(topTris, aTI, bTI, bTO, aTO, flipTopWinding);
            // Çukur (iç) duvarı (submesh 1, koyu malzeme — çift yüz)
            AddQuad(wallTris, aT, aB, bB, bT, flipWallWinding);
            // Dip kapağı (submesh 1)
            wallTris.Add(cap); wallTris.Add(bB); wallTris.Add(aB);
            // KAPATMA — alt halka (submesh 1)
            AddQuad(wallTris, aB, bB, bOB, aOB, false);
            // KAPATMA — dış duvar (submesh 1)
            AddQuad(wallTris, aO, aOB, bOB, bO, false);
        }

        mesh.Clear();
        mesh.SetVertices(verts);
        mesh.SetUVs(0, uvs);
        mesh.subMeshCount = 2;
        mesh.SetTriangles(topTris, 0);
        mesh.SetTriangles(wallTris, 1);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        mc.sharedMesh = null;   // cook'u zorla
        mc.sharedMesh = mesh;
    }

    // Shader stripping'e karşı güvenli arama: build'de shader atılmışsa fallback → asla null dönmez.
    static Shader SafeShader(string name)
    {
        return Shader.Find(name)
            ?? Shader.Find("Universal Render Pipeline/Lit")
            ?? Shader.Find("Sprites/Default")
            ?? Shader.Find("Hidden/InternalErrorShader");
    }

    void AddQuad(List<int> tris, int a, int b, int c, int d, bool flip)
    {
        if (!flip)
        {
            tris.Add(a); tris.Add(b); tris.Add(c);
            tris.Add(a); tris.Add(c); tris.Add(d);
        }
        else
        {
            tris.Add(a); tris.Add(c); tris.Add(b);
            tris.Add(a); tris.Add(d); tris.Add(c);
        }
    }
}
