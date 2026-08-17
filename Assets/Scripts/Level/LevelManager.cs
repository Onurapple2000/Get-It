using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Aktif level'ı kurar (Sprint 1 ikinci faz): LevelData'dan nesneleri runtime SPAWN eder, deliği
/// taze boyuta resetler, hedefleri/süreyi diğer sistemlere "pull" ile sunar.
/// [DefaultExecutionOrder(-100)] → Awake herkesten önce çalışır; GameManager (süre) ve
/// ObjectiveTracker (hedefler) kendi başlangıçlarında buradan okur.
///
/// Hangi level? <see cref="CurrentIndex"/> (static; NextLevel sahne reload'ları arası taşır).
/// </summary>
[DefaultExecutionOrder(-100)]
public partial class LevelManager : MonoBehaviour   // partial: LandmarkBuilder.cs (dünya harikaları) ile bölüşür
{
    public static LevelManager Instance;
    [Tooltip("Hangi level yüklensin (bu dünyanın levels dizisindeki index). NextLevel artırır.")]
    public static int CurrentIndex = 0;
    [Tooltip("Hangi dünya yüklensin (worlds dizisindeki worldId). Ana menü Play'de set eder.")]
    public static int CurrentWorld = WorldCatalog.FirstWorld;   // soğuk açılışta SIRADAKİ İLK dünya (Park artık sonlarda)

    [System.Serializable]
    public class WorldLevelSet
    {
        public int worldId;
        public LevelData[] levels;

        [Header("Tema (zemin)")]
        public Texture2D groundTexture;          // null = düz renk
        public Color groundTint = Color.white;
        public float groundTile = 6f;
    }

    [Tooltip("Dünya-bazlı level setleri (worldId + sıralı levelları). Sprint 5: çoklu dünya.")]
    public WorldLevelSet[] worlds;

    [Tooltip("Geriye-dönük: worlds boşsa kullanılan tek-dünya level dizisi (eski World0).")]
    public LevelData[] levels;

    [Header("Kameraya-yüz dünyaları (kediler vb.)")]
    [Tooltip("Bu worldId'lerdeki nesneler rastgele yaw yerine kameraya YÜZ döner (oyuncu yüzü görsün). Boş = kapalı.")]
    public int[] faceCameraWorlds = { 9, 10 };   // Kediler + Köpekler
    [Tooltip("Kameraya-yüz taban yaw'ı. Nesneler ters/yana bakıyorsa 0/90/180/270 dene (recompile gerekmez).")]
    public float faceCameraYaw = 180f;       // kamera +Z'ye bakar (izleyici -Z) → model önü +Z varsayımıyla 180 = yüz -Z (kameraya)
    [Tooltip("Yüz yönüne küçük rastgele sapma (robotik görünmesin).")]
    public float faceCameraJitter = 14f;

    /// <summary>Aktif dünyanın level dizisi (worlds'te worldId eşleşmesi; yoksa eski 'levels').</summary>
    LevelData[] CurrentLevels()
    {
        if (worlds != null)
            for (int i = 0; i < worlds.Length; i++)
                if (worlds[i] != null && worlds[i].worldId == CurrentWorld && worlds[i].levels != null && worlds[i].levels.Length > 0)
                    return worlds[i].levels;
        return levels;
    }

    [Header("Dizilim (simetrik eşmerkezli halkalar)")]
    [Tooltip("En dış halka yarıçapı (oyun alanı doluluk sınırı).")]
    public float playHalf = 11f;
    [Tooltip("İlk (iç) halka yarıçapı — delik başlangıç boşluğunun hemen dışı.")]
    public float ringInner = 3.4f;
    [Tooltip("Halkalar arası yarıçap farkı (görsel: altın-oran benzeri sıkışık bantlar).")]
    public float ringGap = 1.9f;
    [Tooltip("Halka üzerinde nesneler arası yaklaşık yay mesafesi. Küçük = daha çok nesne.")]
    public float ringSpacing = 2.5f;
    public float centerClearance = 3.2f;

    [Header("Sınır çerçevesi")]
    [Tooltip("Yükseltili kenar çerçevesinin yarı-genişliği (oyun alanını çevreler).")]
    public float frameHalf = 13.8f;
    [Tooltip("Çerçeve duvar yüksekliği.")]
    public float frameHeight = 1.0f;
    [Tooltip("Çerçeve dışındaki alanı karartan soluk örtünün dış yarı-genişliği.")]
    public float outerFadeHalf = 60f;

    [Header("Delik")]
    public float holeStartSize = 1.5f;

    public LevelData Active { get; private set; }

    void Awake()
    {
        Instance = this;
        var lv = CurrentLevels();
        if (lv == null || lv.Length == 0)
        {
            Debug.LogWarning($"[LevelManager] Dünya {CurrentWorld} için level yok — kurulamadı.");
            return;
        }
        Active = lv[Mathf.Clamp(CurrentIndex, 0, lv.Length - 1)];

        SetupArena();   // şekilli arena (world-5+): boyut + dışlama kutuları — ResetHole/SpawnObjects'ten ÖNCE
        ResetHole();
        SpawnObjects();
    }

    void Start()
    {
        ApplyGroundTheme();
        WorldColorGrade.Apply(CurrentWorld);   // dünya-bazlı renk canlılığı (İçecekler soluk → canlı)
        ApplyShadowStrength();                  // gölgeleri AÇIK yap (artefaktlar göze az batsın)
        BuildArena();
        AudioManager.Instance?.PlayGameplayMusic();   // rastgele oyun loop'u (level bitene kadar döngü)
        AudioManager.Instance?.ResetSwallowRange();    // yutma ses eşikleri bu levelin boyut dağılımından yeniden ölçülsün
        FoodsL1Tutorial.CheckAndStart();               // Yiyecekler L1 ön-tanıtımı (build'de kesin çalışsın diye buradan)
    }

    /// <summary>EDİTÖR ÖNİZLEME: SADECE Eyfel kulesini merkeze kurar (başka hiçbir şey yok), incelemek için.</summary>
    public void BuildEiffelPreview(GameObject kebabPrefab, GameObject[] macs, Vector2 center)
    {
        var root = new GameObject("_LevelObjects").transform;
        BuildEiffel((kebabPrefab, 1, 1f), macs, center, root);
    }

    /// <summary>EDİTÖR ÖNİZLEME: belirtilen level'ı Edit modunda sahneye kurar (oynamadan incelemek için).</summary>
    public void BuildPreview(int worldId, int levelIndex)
    {
        CurrentWorld = worldId; CurrentIndex = levelIndex;
        var lv = CurrentLevels();
        if (lv == null || lv.Length == 0) { Debug.LogWarning("[Preview] level yok"); return; }
        Active = lv[Mathf.Clamp(levelIndex, 0, lv.Length - 1)];
        SetupArena();   // şekilli arena (boyut+kutular) preview'da da kurulsun (Awake'siz çağrıldığı için)
        SpawnObjects();
        BuildArena();
    }

    // Oyun alanını çevreleyen YÜKSELTİLİ kare çerçeve + dışını karartan soluk örtü (dünya-sabit).
    void BuildArena()
    {
        if (GameObject.Find("_PlayArena") != null) return;
        var parent = new GameObject("_PlayArena").transform;

        var frameMat = MakeLitMat("ArenaFrameMat", new Color(0.30f, 0.22f, 0.15f), 0.25f);  // sıcak ahşap/taş
        float h = frameHalf, t = 0.6f, wy = frameHeight;
        Bar(parent, frameMat, new Vector3(0, wy * 0.5f,  h), new Vector3(2 * h + t, wy, t));   // Kuzey
        Bar(parent, frameMat, new Vector3(0, wy * 0.5f, -h), new Vector3(2 * h + t, wy, t));   // Güney
        Bar(parent, frameMat, new Vector3( h, wy * 0.5f, 0), new Vector3(t, wy, 2 * h + t));   // Doğu
        Bar(parent, frameMat, new Vector3(-h, wy * 0.5f, 0), new Vector3(t, wy, 2 * h + t));   // Batı

        // GÖRÜNMEZ yüksek tutma duvarları: hiçbir nesne dışarı fırlayamasın. Konumları görünür çerçeveden İÇERİDE,
        // deliğin AĞZININ ulaşabildiği sınıra (HoleController.boundaryLimit) hizalanır → nesneler hep delik
        // erişiminde kalır; çerçeveye kadar olan dış şeritte (delik ağzının ulaşamadığı, rim tuğlalarının olduğu
        // bölgede) takılıp yutulamaz kalmazlar. Görünür çerçeve (Bar) frameHalf'ta; rim tuğlaları duvar↔çerçeve arasında.
        float wh = 14f, t2 = 0.5f;
        var hcForWall = FindFirstObjectByType<HoleController>();
        float wallH = hcForWall != null ? hcForWall.boundaryLimit : (h - 0.6f);
        Wall(parent, new Vector3(0,  wh * 0.5f,  wallH), new Vector3(2 * wallH + t2, wh, t2));
        Wall(parent, new Vector3(0,  wh * 0.5f, -wallH), new Vector3(2 * wallH + t2, wh, t2));
        Wall(parent, new Vector3( wallH, wh * 0.5f, 0), new Vector3(t2, wh, 2 * wallH + t2));
        Wall(parent, new Vector3(-wallH, wh * 0.5f, 0), new Vector3(t2, wh, 2 * wallH + t2));

        BuildOuterFade(parent, h, outerFadeHalf);

        // ŞEKİLLİ ARENA (world-5+): dışlama kutularını YÜKSELTİLMİŞ BLOK olarak kur (görünür + BoxCollider → nesneler
        // çarpar; delik analitik olarak dışında tutulur). İç ada / kenar çıkıntısı — ikisi de aynı blok.
        if (arenaShaped)
        {
            const float blockH = 3.0f, blockWallH = 6.0f;
            foreach (var b in arenaBoxes)
            {
                // 1) GÖRÜNÜR blok — ZEMİN dokusuyla kaplı (kullanıcı: yükseltiler zemin resmiyle kaplansın). Collider YOK
                //    (çarpışmayı aşağıdaki görünmez keep-out duvarı sağlar; delik rim'i analitik olarak zaten dışarıda).
                var vis = GameObject.CreatePrimitive(PrimitiveType.Cube);
                vis.name = "ArenaBlock";
                var vc = vis.GetComponent<Collider>();
                if (vc != null) { if (Application.isPlaying) Destroy(vc); else DestroyImmediate(vc); }
                vis.transform.SetParent(parent, false);
                vis.transform.localPosition = new Vector3(b.center.x, blockH * 0.5f, b.center.y);
                vis.transform.localScale = new Vector3(b.half.x * 2f, blockH, b.half.y * 2f);
                vis.GetComponent<MeshRenderer>().sharedMaterial = MakeGroundBlockMat(b);

                // 2) GÖRÜNMEZ KEEP-OUT duvarı — blok + BOX_KEEPOUT (kullanıcı: kutuların çevresine görünmez duvar).
                //    Nesneler rim mesafesi kadar uzakta kalır; blok yüzüne rim'den yakın saçılan/dinlenen nesne olmaz.
                var wall = new GameObject("ArenaBlockWall", typeof(BoxCollider));
                wall.transform.SetParent(parent, false);
                wall.transform.localPosition = new Vector3(b.center.x, blockWallH * 0.5f, b.center.y);
                wall.GetComponent<BoxCollider>().size = new Vector3((b.half.x + BOX_KEEPOUT) * 2f, blockWallH, (b.half.y + BOX_KEEPOUT) * 2f);
            }
        }
    }

    // Şekilli arena bloğu için ZEMİN dokulu Lit materyal — sahneyle bütünleşsin (blok boyutuna göre tile'lanır).
    Material MakeGroundBlockMat(ArenaBox b)
    {
        var sh = SafeShader("Universal Render Pipeline/Lit");
        var m = new Material(sh) { name = "ArenaBlockMat" };
        Color tint = (_groundTex != null) ? _groundTint : new Color(0.34f, 0.26f, 0.20f);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", tint); else m.color = tint;
        if (_groundTex != null)
        {
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", _groundTex);
            m.mainTexture = _groundTex;
            float tile = Mathf.Max(0.01f, _groundTile);
            Vector2 sc = new Vector2((b.half.x * 2f) / tile, (b.half.y * 2f) / tile);
            m.mainTextureScale = sc;
            if (m.HasProperty("_BaseMap")) m.SetTextureScale("_BaseMap", sc);
        }
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.1f);
        return m;
    }

    // Görünmez tutma duvarı (sadece collider).
    void Wall(Transform parent, Vector3 pos, Vector3 size)
    {
        var go = new GameObject("Wall", typeof(BoxCollider));
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        var bc = go.GetComponent<BoxCollider>();
        bc.size = size;
    }

    void Bar(Transform parent, Material mat, Vector3 pos, Vector3 size)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "Frame";
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = size;
        go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        // collider kalır → stray nesneler alanda tutulur
    }

    // Çerçeve dışını karartan yarı-saydam kare-halka (annulus) örtü, zeminin hemen üstünde.
    void BuildOuterFade(Transform parent, float inner, float outer)
    {
        var go = new GameObject("OuterFade", typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(0f, 0.06f, 0f);

        // 8 vert: dış kare (0-3) + iç kare (4-7), aradaki bant = halka
        var v = new Vector3[]
        {
            new(-outer,0,-outer), new(outer,0,-outer), new(outer,0,outer), new(-outer,0,outer),
            new(-inner,0,-inner), new(inner,0,-inner), new(inner,0,inner), new(-inner,0,inner),
        };
        var tris = new int[]
        {
            0,4,5, 0,5,1,   // güney bant
            1,5,6, 1,6,2,   // doğu bant
            2,6,7, 2,7,3,   // kuzey bant
            3,7,4, 3,4,0,   // batı bant
        };
        var m = new Mesh { name = "OuterFadeMesh" };
        m.SetVertices(v); m.SetTriangles(tris, 0); m.RecalculateNormals(); m.RecalculateBounds();
        go.GetComponent<MeshFilter>().sharedMesh = m;
        go.GetComponent<MeshRenderer>().sharedMaterial = MakeTransparentMat("OuterFadeMat", new Color(0f, 0f, 0f, 0.5f));
    }

    // Shader stripping'e karşı GÜVENLİ arama: build'de shader atılmışsa fallback → asla null dönmez
    // (null → new Material(null) ArgumentNullException → kurulum çöker). Bkz Always-Included Shaders fix.
    static Shader SafeShader(string name)
    {
        return Shader.Find(name)
            ?? Shader.Find("Universal Render Pipeline/Lit")
            ?? Shader.Find("Sprites/Default")
            ?? Shader.Find("Hidden/InternalErrorShader");
    }

    static Material MakeLitMat(string name, Color color, float smooth)
    {
        var sh = SafeShader("Universal Render Pipeline/Lit");
        var m = new Material(sh) { name = name };
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color); else m.color = color;
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
        return m;
    }

    static Material MakeTransparentMat(string name, Color color)
    {
        var sh = SafeShader("Universal Render Pipeline/Unlit");
        var m = new Material(sh) { name = name };
        // URP Unlit → Transparent surface
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_Blend", 0f);
        m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.SetInt("_ZWrite", 0);
        m.DisableKeyword("_SURFACE_TYPE_OPAQUE");
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color); else m.color = color;
        return m;
    }

    [Header("Gölge")]
    [Tooltip("Yönlü ışığın gölge gücü (0=görünmez, 1=tam koyu). Düşük = AÇIK gölge → gölgedeki küçük artefaktlar göze az batar.")]
    [Range(0f, 1f)] public float shadowStrength = 0.5f;

    // Gölgeleri açık yap: gölgedeki küçük leak/acne noktaları koyu gölgede belli oluyordu → strength düşünce silikleşir.
    void ApplyShadowStrength()
    {
        var lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
        foreach (var l in lights)
            if (l.type == LightType.Directional && l.shadows != LightShadows.None)
                l.shadowStrength = shadowStrength;
    }

    // Aktif dünyanın zemin temasını HoleFloor'a uygula (HoleFloor.Start mesh'i bundan sonra kurar).
    void ApplyGroundTheme()
    {
        if (worlds == null) return;
        WorldLevelSet set = null;
        for (int i = 0; i < worlds.Length; i++)
            if (worlds[i] != null && worlds[i].worldId == CurrentWorld) { set = worlds[i]; break; }
        if (set == null) return;
        _groundTex = set.groundTexture; _groundTint = set.groundTint; _groundTile = set.groundTile;   // BuildArena blok materyali için sakla
        var floor = FindAnyObjectByType<HoleFloor>();
        if (floor != null)
        {
            // ŞEKİLLİ ARENA: alan büyüdü → zemin diski köşeleri kaplasın (kare ~24 → köşe ~34; radius'u büyüt).
            if (arenaShaped) floor.boundaryRadius = Mathf.Max(floor.boundaryRadius, 42f);
            floor.SetGroundTheme(set.groundTexture, set.groundTint, set.groundTile);
        }
    }

    void ResetHole()
    {
        var hole = FindAnyObjectByType<HoleController>();
        // İÇECEKLER (dünya 5): nesneler ~2.4× büyük + DÜRÜST collider → küçük tier baştan sığsın diye delik biraz büyük
        // başlar. 2026-08-03 nesneler %20 küçülünce 2.0→1.7 (Tiny footprint ~1.38 < 1.7). Diğer dünyalar holeStartSize (1.5).
        float start = BigObjWorld(CurrentWorld) ? 1.7f : holeStartSize;
        if (hole != null)
        {
            hole.currentSize = start;
            // İÇECEKLER: en büyük (geniş) hedef footprint ~3.2 → deliğin ona ULAŞABİLMESİ + üstüne pay için maxSize'ı
            // genişlet (kullanıcı 2026-08-03: "max büyüme size'ını biraz daha geniş yap"). 4.5→5.5.
            if (BigObjWorld(CurrentWorld)) hole.maxSize = 5.5f;
        }
    }

    // Görünmez arena duvarı (HoleController.boundaryLimit) — tüm spawn'lar bunun İÇİNDE kalır (ayak izi dahil).
    float _arenaLimit = 999f;
    bool _carLayout = false;   // araç düzeni aktifken kenar-clamp araçlarını üst üste bindirmek yerine ATLA
    readonly List<(Vector2 p, float r)> _occ = new();   // araç düzeninde dolu konumlar (hedef-tamamlama üst üste binmesin)
    readonly Dictionary<string, int> _typeCount = new(); // araç düzeninde tür başına yerleştirilen adet (hedef garantisi)
    readonly List<(Vector2 p, float r)> _objOcc = new();  // HEDEF park lotları (sokak/blok/dolgu bunların üstüne gelmesin)
    bool _thinBuildings = false;   // BİNALAR (dünya 3): ızgara formasyonlarında binaların ~%25'ini atla (seyrek şehir)

    // ── ŞEKİLLİ ARENA (2026-08-03, world-5+ pilotu) ─────────────────────────────────────────────────────────────
    // Sahne artık dünya-5'te DİKDÖRTGEN + eksen-hizalı "dışlama kutuları" (iç ada VEYA kenar çıkıntısı). Kutu = engel:
    // ne oyuncu (delik) ne nesne içine girer → oyuncu etrafından DOLANIR (daha çok gezinti). Delik collider'sız
    // (transform ile hareket) → duvarlarla durdurulamaz, bu yüzden ANALİTİK sınırlama (ConfineHoleXZ). Nesneler için
    // gerçek box-collider bloklar (BuildArena) + spawn'da InBox filtresi. ŞEKİLSİZ dünyalar (0-4,6+): arenaShaped=false
    // → tüm bu yollar KAPALI, eski kare davranış birebir korunur.
    public struct ArenaBox { public Vector2 center; public Vector2 half; }   // eksen-hizalı (xz merkez + yarı-boyut)
    bool arenaShaped = false;
    float arenaHalfX, arenaHalfZ;
    readonly List<ArenaBox> arenaBoxes = new();
    public bool ArenaShaped => arenaShaped;
    const float RIM_MARGIN = 0.7f;    // delik RİM'i (HoleRim.ringWidth ~0.6) blokların ALTINA girmesin → confine bu kadar fazla dışarıda tutar
    const float BOX_KEEPOUT = 0.7f;   // nesneler blok yüzünden bu kadar UZAK kalsın (görünmez keep-out duvarı + spawn payı; rim erişimiyle hizalı)
    // Zemin teması (BuildArena blok materyali için ApplyGroundTheme'de saklanır).
    Texture2D _groundTex; Color _groundTint = Color.white; float _groundTile = 6f;

    // BÜYÜK-NESNE dünyaları (İçecekler 5 … Mücevher 15): drink-tarzı kompozisyon + fizik (growMultiplier, maxSize,
    // knife-cut fix, seyrek/ground-shape dolgu, rank fazlası, footprint-hedef). Cars(2)/Buildings(3) ayrı; Sweets(4) hariç.
    static bool BigObjWorld(int w) => w == 0 || (w >= 5 && w <= 15) || w == 17;   // + Park(0) + Karma(17) (2026-08-05)

    // Dünya+level bazlı şekil kataloğu. null = şekilsiz (eski kare). Büyük-nesne dünyaları ~2× alan; boyut dünyaya göre değişir.
    // İçecekler (5): TÜM levellar engelli. Diğer büyük dünyalar (6-15): ÇİFT levellar engelli, TEK levellar düz (kutu yok).
    static (float hx, float hz, ArenaBox[] boxes)? ArenaShapeFor(int world, int levelIndex)
    {
        if (world == 5) return (24f, 24f, DrinkBoxes());              // İçecekler: TÜM levellar engelli
        if (BigObjWorld(world))                                       // diğer büyük dünyalar (6-15, Park 0, Karma 17): ÇİFT level engelli
        {
            float half = ArenaHalfForWorld(world);                    // dünyaya göre farklı boyut (22-26)
            bool even = ((levelIndex + 1) % 2) == 0;                  // level NUMARASI çift → engelli; tek → düz büyük kare
            return (half, half, even ? BoxesFor(world, levelIndex, half) : new ArenaBox[0]);
        }
        return null;
    }

    // Dünyaya göre kare arena yarı-boyutu (farklı BOYUT versiyonları). Kare tutulur (dairesel dolgu köşeleri iyi kaplar).
    static float ArenaHalfForWorld(int world)
    {
        float[] sizes = { 24f, 26f, 22f, 25f, 23f };
        return sizes[((world % sizes.Length) + sizes.Length) % sizes.Length];
    }

    static ArenaBox[] DrinkBoxes() => new[]
    {
        new ArenaBox { center = new Vector2(-17f,  5f), half = new Vector2(7f, 5f) },   // SOL kenardan çıkıntı
        new ArenaBox { center = new Vector2(  8f, -9f), half = new Vector2(5f, 5f) },   // İÇ ada
        new ArenaBox { center = new Vector2( 18f,  8f), half = new Vector2(6f, 5f) },   // SAĞ kenardan çıkıntı
    };

    // Büyük dünyalar (6-15) ÇİFT levelları: 5 şablonluk kütüphaneden (world,level) ile seçilen FARKLI yerleşim,
    // arena boyutuna (half/24) ölçeklenir. Şablonlar half=24 referansına göre tanımlı.
    static ArenaBox[] BoxesFor(int world, int levelIndex, float half)
    {
        int idx = (((world * 3 + levelIndex) % 5) + 5) % 5;
        ArenaBox[] t;
        switch (idx)
        {
            case 0: t = DrinkBoxes(); break;                                                      // 3 karışık
            case 1: t = new[] { Box(-9f, 8f, 6f, 4f), Box(11f, -7f, 5f, 5f) }; break;             // 2 ada
            case 2: t = new[] { Box(16f, 6f, 7f, 4f), Box(-10f, -8f, 5f, 5f) }; break;            // sağ çıkıntı + ada
            case 3: t = new[] { Box(-3f, 18f, 6f, 5f), Box(5f, -18f, 6f, 5f) }; break;            // üst + alt kenar çıkıntısı
            default: t = new[] { Box(-12f, 10f, 4f, 4f), Box(13f, 4f, 4f, 4f), Box(2f, -12f, 4f, 4f) }; break;  // 3 küçük dağınık ada
        }
        float s = half / 24f;
        if (Mathf.Abs(s - 1f) > 0.001f)
            for (int i = 0; i < t.Length; i++) { t[i].center *= s; t[i].half *= s; }
        return t;
    }

    static ArenaBox Box(float cx, float cz, float hx, float hz) => new ArenaBox { center = new Vector2(cx, cz), half = new Vector2(hx, hz) };

    // Awake'te (spawn'dan ÖNCE) çağrılır: aktif dünyanın arena şeklini kur + boyut alanlarını (playHalf/frameHalf/
    // boundaryLimit) ona göre ayarla. Şekilsizse eski değerleri korur.
    void SetupArena()
    {
        arenaBoxes.Clear();
        var shape = ArenaShapeFor(CurrentWorld, Active != null ? Active.levelIndex : CurrentIndex);
        if (shape.HasValue)
        {
            arenaShaped = true;
            arenaHalfX = shape.Value.hx; arenaHalfZ = shape.Value.hz;
            arenaBoxes.AddRange(shape.Value.boxes);
            playHalf = Mathf.Min(arenaHalfX, arenaHalfZ);              // dairesel dolgu yarıçapı (kare pilotta = halfX)
            frameHalf = Mathf.Max(arenaHalfX, arenaHalfZ) + 1.5f;
            var hc = FindFirstObjectByType<HoleController>();
            if (hc != null) hc.boundaryLimit = Mathf.Max(arenaHalfX, arenaHalfZ) + 0.5f;

            // LANDMARK LEVELLARI (L3/6/9/12 = idx 2/5/8/11; foods hariç): dünya harikası ARKA-ORTADA (0, playHalf*0.62,
            // yarıçap ≤9.5). O bölgeye DENK GELEN engel kutularını ELE → yapı engelin içinde kalmasın (kullanıcı: gemiler L6).
            int li = Active != null ? Active.levelIndex : CurrentIndex;
            if (CurrentWorld != 1 && (li == 2 || li == 5 || li == 8 || li == 11) && arenaBoxes.Count > 0)
            {
                Vector2 lm = new Vector2(0f, playHalf * 0.62f);
                const float lmClear = 11f;   // en geniş landmark (TowerBridge 9.5) + pay
                arenaBoxes.RemoveAll(b =>
                {
                    float cx = Mathf.Clamp(lm.x, b.center.x - b.half.x, b.center.x + b.half.x);
                    float cz = Mathf.Clamp(lm.y, b.center.y - b.half.y, b.center.y + b.half.y);
                    return (new Vector2(cx, cz) - lm).sqrMagnitude < lmClear * lmClear;   // kutu, landmark diskine değiyor
                });
            }
        }
        else { arenaShaped = false; arenaHalfX = arenaHalfZ = playHalf; }
    }

    // Nokta (yarıçap r ile) herhangi bir dışlama kutusunun İÇİNDE mi? (spawn/yerleşim bunlardan kaçınır.)
    // Şekilsiz dünyalarda arenaBoxes boş → daima false → hiçbir şeyi etkilemez.
    bool InBox(Vector2 p, float r)
    {
        for (int i = 0; i < arenaBoxes.Count; i++)
        {
            var b = arenaBoxes[i];
            // + BOX_KEEPOUT: nesne blok yüzüne rim mesafesinden yakın DOĞMASIN/saçılmasın (delik oraya rim ile ulaşamaz).
            if (Mathf.Abs(p.x - b.center.x) < b.half.x + r + BOX_KEEPOUT && Mathf.Abs(p.y - b.center.y) < b.half.y + r + BOX_KEEPOUT) return true;
        }
        return false;
    }

    // Deliğin XZ konumunu şekilli arenada tut: dış dikdörtgene clamp + her kutunun (rim payı ile) DIŞINA it.
    // HoleController (transform ile hareket, collider'sız) her kare bunu çağırır (yalnız arenaShaped iken).
    public Vector2 ConfineHoleXZ(Vector2 pos, float holeHalf)
    {
        float lx = Mathf.Max(0.5f, arenaHalfX - holeHalf), lz = Mathf.Max(0.5f, arenaHalfZ - holeHalf);
        pos.x = Mathf.Clamp(pos.x, -lx, lx);
        pos.y = Mathf.Clamp(pos.y, -lz, lz);
        // KÖŞE YUVARLAMA (2026-08-05 kullanıcı, pilot world 7): kare bloğun keskin köşesinde yuvarlak delik takılıyordu.
        // Kutuyu KESKİN dikdörtgen yerine YUVARLAK-köşeli kabul et → delik köşe etrafında m yarıçaplı YAY çizer, akıcı döner.
        // (Blok görseli/collider'ı kare kalır; sadece deliğin izlediği sınır köşede yuvarlanır.)
        // 2026-08-05: world-7 pilotu onaylandı → TÜM engelli dünyalara açıldı (ConfineHoleXZ zaten yalnız şekilli arenada çağrılır).
        bool rounded = true;
        for (int i = 0; i < arenaBoxes.Count; i++)
        {
            var b = arenaBoxes[i];
            float m = holeHalf + RIM_MARGIN;
            float qx = pos.x - b.center.x, qz = pos.y - b.center.y;
            float sx = qx < 0f ? -1f : 1f, sz = qz < 0f ? -1f : 1f;
            float ax = Mathf.Abs(qx) - b.half.x;   // >0 → x ekseninde kutu DIŞINDA
            float az = Mathf.Abs(qz) - b.half.y;
            if (ax >= m || az >= m) continue;      // keep-out bandının tamamen dışında

            if (rounded && ax > 0f && az > 0f)     // KÖŞE bölgesi → köşe noktasından radyal it (yuvarlak dönüş)
            {
                Vector2 corner = new Vector2(b.center.x + sx * b.half.x, b.center.y + sz * b.half.y);
                Vector2 d = pos - corner;
                float dist = d.magnitude;
                if (dist < m)
                {
                    d = dist > 1e-4f ? d / dist : new Vector2(sx, sz).normalized;
                    pos = corner + d * m;
                }
            }
            else                                    // YÜZ (veya kutu içi) → en yakın yüze eksen-it
            {
                float px = (b.half.x + m) - Mathf.Abs(qx), pz = (b.half.y + m) - Mathf.Abs(qz);
                if (px <= pz) pos.x = b.center.x + sx * (b.half.x + m);
                else          pos.y = b.center.y + sz * (b.half.y + m);
            }
        }
        return pos;
    }

    // Konum bir HEDEF lotuna değiyor mu? (sokak/blok yerleşimi hedef lotlarından kaçınsın)
    bool NearObj(Vector2 p, float r)
    {
        for (int k = 0; k < _objOcc.Count; k++)
        {
            float rr = _objOcc[k].r + r;
            if ((_objOcc[k].p - p).sqrMagnitude < rr * rr) return true;
        }
        return false;
    }

    // Verilen konum (yaklaşık daire yarıçapı r) mevcut araçlardan uzak mı? (hedef-tamamlama serpiştirmesi için)
    bool OccFree(Vector2 p, float r)
    {
        for (int k = 0; k < _occ.Count; k++)
        {
            float rr = _occ[k].r + r;
            if ((_occ[k].p - p).sqrMagnitude < rr * rr * 1.1f) return false;   // sınır-dairesi + %5 pay → çakışma YOK
        }
        return true;
    }

    void SpawnObjects()
    {
        // Level'e ÖZGÜ sabit dizilim: aynı level her girişte/yeniden aynı yerleşim.
        var prevState = Random.state;
        Random.InitState(1000 * Active.worldId + Active.levelIndex + 7919);

        var root = new GameObject("_LevelObjects").transform;

        // Görünmez duvarı bul → spawn clamp sınırı. Nesneler bu duvara (rim tuğlaları kadar) yaklaşabilir, aşamaz.
        var hcLimit = FindFirstObjectByType<HoleController>();
        _arenaLimit = hcLimit != null ? hcLimit.boundaryLimit : (frameHalf - 0.6f);
        // ⭐ DELİK BÜYÜMESİ = TÜM DÜNYALARDA ARABALAR/BİNALAR GİBİ (kullanıcı 2026-07-29): growMultiplier=1 + growAmount
        // SpawnObjects SONUNDA nesnenin BOYUTUNA göre yazılır (post-pass §aşağıda) → prefabın tutarsız growAmount'u EZİLİR,
        // büyüme küçük ~0.002 / orta ~0.004 / büyük ~0.006 / dev ~0.01-0.02 olur (yavaş, boyutla orantılı). Post-pass
        // root'taki TÜM PhysicsSwallowable'ı gezer → L3/6/9/12 LANDMARK yapıları da AYNI kurala girer. growMultiplier=1
        // → büyüme DOĞRUDAN growAmount.
        // ⚠️ İÇECEKLER (dünya 5, 2026-08-03): nesneler ~2.4× BÜYÜK ama SEYREK (az nesne) → size-tabanlı küçük growAmount
        // (0.0015-0.007) ile delik en büyük hedefe (footprint ~3.2) ULAŞAMIYOR / çok yavaş (kullanıcı). Bu dünyada
        // büyüme çarpanını 4× yap → yutulan her nesne deliği belirgin büyütür, büyük hedefe makul sürede ulaşır.
        if (hcLimit != null) hcLimit.growMultiplier = ((Active != null && BigObjWorld(Active.worldId)) ? 3.0f : 1.0f) * DifficultySettings.GrowthMultiplier;   // 2026-08-03: 4→3; 2026-08-06: KOLAY zorlukta ×3 (yutulan başına daha hızlı büyüme)

        SetupLandmarkArea();   // L3/6/9/12: dünya harikası konum + temiz alan (LandmarkBuilder.cs)

        // Spawn'ları ayır: HALKA türleri (normal yiyecek/deco) vs SEYREK yerleştirilenler (güç-up'lar).
        // Güç-up'lar bir halka doldurmaz; bombalar gibi az sayıda simetrik serpilir.
        var types = new List<(GameObject prefab, int stack, float scale)>();
        var sparse = new List<GameObject>();   // güç-up'lar (count kadar)
        foreach (var s in Active.spawns)
        {
            if (s.prefab == null) continue;
            var psw = s.prefab.GetComponent<PhysicsSwallowable>();
            if (psw != null && psw.powerUp != PowerUpType.None)
            {
                // ZOR zorlukta sahnede güç-up DOĞMAZ (oyuncu yalnız kendi envanterini kullanır — 2026-08-06 kullanıcı).
                if (DifficultySettings.PowerUpsSpawnInScene)
                    for (int i = 0; i < Mathf.Max(1, s.count); i++) sparse.Add(s.prefab);
                continue;
            }
            types.Add((s.prefab, Mathf.Max(1, s.stack), s.scale > 0.01f ? s.scale : 1f));
        }
        if (types.Count == 0) { Random.state = prevState; return; }

        // Hedef türlerini ÖNE al → garanti spawn (iç halkalarda, bol miktar).
        var objSet = new HashSet<string>();
        foreach (var o in Active.objectives) objSet.Add(o.objectType);
        types.Sort((a, b) =>
        {
            bool ao = objSet.Contains(a.prefab.name), bo = objSet.Contains(b.prefab.name);
            return ao == bo ? 0 : (ao ? -1 : 1);
        });

        // ÇEŞİTLİ SİMETRİK ÖBEKLER (kompozisyon): merkez boş (delik başlangıcı); etrafta anchor halkaları,
        // her halka tek FORMASYON stili (dönerek rotasyonel simetri) + tek tür → halkalar arası çeşitlilik.
        ComposeFormations(types, root);

        // DÜNYA HARİKASI (L3/6/9/12): arka-ortada, temizlenmiş alana ikonik yapı (LandmarkBuilder.cs).
        BuildWorldLandmark(types, root);

        // Güç-up'lar: bir ara yarıçapta eşit açıyla simetrik serpilir.
        PlaceSparse(sparse, ringInner + ringGap * 0.5f, root);
        // Bombalar: farklı bir ara yarıçapta simetrik.
        int bombs = (Active.bombsEnabled && Active.bombPrefab != null) ? Active.bombCount : 0;
        var bombList = new List<GameObject>();
        for (int j = 0; j < bombs; j++) bombList.Add(Active.bombPrefab);
        // Normal level: bombalar kameranın İLK görüş açısında (ön/yakın) — mutlaka görünür.
        // HARD level: bombalar gizli — dışta/arkada (kameranın ilk başta göremeyeceği yerde), öbekler arasında.
        PlaceBombs(bombList, Active.IsHard, root);

        // ZORUNLU: her hedef türünden, gerekli sayıdan (+ pay) AZ varsa eksiği tamamla → level kazanılabilir olsun.
        EnsureObjectiveCounts(root);

        // ⭐ TÜM DÜNYALAR: TÜM yutulabilir nesnelerin (kompozisyon + LANDMARK L3/6/9/12 yapıları dahil) delik-büyümesini
        // BOYUTLA orantılı TEK YERDEN ayarla → prefab'ın tutarsız growAmount'unu EZER, landmark dahil hepsi AYNI kural
        // (kullanıcı 2026-07-29: "bütün dünyalar arabalar/binalar gibi olsun, L3/6/9/12 özel yapılar dahil"). Güç-up hariç.
        // md = BoxCollider varsa bc.size·lossyScale, yoksa collider/renderer dünya-bounds. Değerler: küçük ~0.002 / orta
        // ~0.004 / büyük ~0.006 / dev ~0.01-0.02 → yavaş, boyutla orantılı büyüme.
        // İÇECEKLER (dünya 5, 2026-08-03): "delik ağzı yarı-delikteki nesneyi bıçak gibi kesiyor (hızlı sürüklerken)".
        // Zemin deliği BİREBİR takip eder; nesne ise gecikir → deliğin GERİ kenarına düşer → katı zemin halkası
        // gövdeyi keser. Çözüm: yutulan (delik üstünde+giren) nesneyi MERKEZE + AŞAĞI daha sert çek → rim'de oyalanmaz,
        // hızlı sürüklemede bile açıklığın ortasında/dibinde kalır. Yalnız dünya-5 (büyük nesneler bu artefaktı gösteriyor).
        bool drk5 = Active != null && BigObjWorld(Active.worldId);
        {
            foreach (var psw in root.GetComponentsInChildren<PhysicsSwallowable>())
            {
                if (psw.powerUp != PowerUpType.None) continue;   // güç-up'a dokunma
                if (drk5)
                {
                    psw.holeCentering = 11f; psw.holeSuction = 40f;   // 4→11 merkeze, 28→40 aşağı (rim'de oyalanmasın)
                    psw.wakeDepenetration = 2.5f;                     // 0.35→2.5: hareketli zemin nesneyi HIZLI geri itsin (bıçak-kesme gitsin)
                    if (psw.Body != null) psw.Body.mass = 2f;         // büyük içeceğin dim*4 kütlesi (≤13) → ATALET/gecikme; 2 sabit → zemin açıklığını daha iyi takip eder
                }
                float md = 1f;
                var bc = psw.GetComponent<BoxCollider>();
                if (bc != null) { var ws = Vector3.Scale(bc.size, psw.transform.lossyScale); md = Mathf.Max(ws.x, Mathf.Max(ws.y, ws.z)); }
                else
                {
                    // BoxCollider YOK (MeshCollider / convex) → dünya-uzayı bounds'tan boyut (collider, yoksa renderer).
                    var cs = psw.GetComponentsInChildren<Collider>();
                    if (cs.Length > 0) { Bounds b = cs[0].bounds; for (int i = 1; i < cs.Length; i++) b.Encapsulate(cs[i].bounds); md = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z)); }
                    else { var rs = psw.GetComponentsInChildren<Renderer>(); if (rs.Length > 0) { Bounds b = rs[0].bounds; for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds); md = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z)); } }
                }
                psw.growAmount = Mathf.Clamp(0.002f + 0.002f * (md - 0.8f), 0.0015f, 0.02f);
            }
        }

        Random.state = prevState;
    }

    // Her hedef türünden sahnede en az (required + pay) adet olduğunu GARANTİLER; eksikse ekler.
    void EnsureObjectiveCounts(Transform root)
    {
        if (Active.objectives == null) return;

        // tür adı → prefab (Active.spawns'tan)
        var map = new Dictionary<string, GameObject>();
        foreach (var s in Active.spawns)
            if (s.prefab != null && !map.ContainsKey(s.prefab.name)) map[s.prefab.name] = s.prefab;

        // İÇECEKLER (2026-08-03): ComposeCarCity hedefi RANK'a göre req+[0,2,3,5] koyar (kullanıcının fazla şeması).
        // EnsureObjectiveCounts +3 pay eklerse bu şemayı EZER (hepsi ≥req+3 olur). Bu yüzden dünya-5'te pay=0 →
        // yalnız GARANTİ: eksikse en az req'e tamamla (kazanılabilirlik), fazlaya karışma.
        bool drk = Active != null && BigObjWorld(Active.worldId);
        int spot = 0;
        foreach (var o in Active.objectives)
        {
            int have = 0;
            var all = PhysicsSwallowable.All;
            for (int i = 0; i < all.Count; i++)
                if (all[i] != null && !all[i].IsSwallowed && all[i].ResolvedType == o.objectType) have++;

            int need = (o.required + (drk ? 0 : 3)) - have;   // diğer dünyalar +3 pay; İçecekler yalnız req (fazla ComposeCarCity'de)
            if (need <= 0 || !map.TryGetValue(o.objectType, out var pf)) continue;

            float cr = Footprint(pf) * 0.5f;
            // Dış yarıçapı ARENA SINIRI içinde tut → SpawnStack'in clamp'i (özellikle İçecekler dünya-5 clamp-atla)
            // bu zorunlu hedef nesnesini ELEMESİN (aksi halde required+3 garantisi bozulur). arenaLimit-cr-0.6 < clamp lim.
            float maxR = Mathf.Max(centerClearance + 2f, Mathf.Min(playHalf - 1f, _arenaLimit - cr - 0.6f));
            for (int i = 0; i < need; i++)
            {
                // golden-angle saçılım — mevcut araçların ÜSTÜNE BİNMESİN: BOŞ (OccFree) nokta bulunca yerleştir;
                // bulunamazsa ATLA (çakışma yaratma). Araç düzeninde hedefler zaten grid'de garanti (yukarıda) →
                // burası genelde no-op; diğer dünyalarda _occ boş → ilk nokta hep serbest.
                bool ok = false; Vector2 p = Vector2.zero;
                // 1) TERCİH: kutu dışı + BOŞ (çakışmasız) nokta.
                for (int a = 0; a < 140; a++)
                {
                    float ang = spot * 2.39996323f;
                    float r = Mathf.Lerp(centerClearance + 1f, maxR, ((spot * 0.61803f) % 1f));
                    spot++;
                    p = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * r;
                    if (!InBox(p, cr) && OccFree(p, cr)) { ok = true; break; }   // şekilli arena: kutuya koyma
                }
                // 2) YEDEK (KAZANILABİLİRLİK GARANTİSİ): boş yer yoksa çakışmayı GÖZ ARDI et, yeter ki kutu DIŞI + arena
                // İÇİ bir yer olsun → hedef nesnesi KESİN doğar (aksi halde 1 eksik kalıp level kazanılamıyordu — kullanıcı: uçaklar L13).
                if (!ok)
                    for (int a = 0; a < 140; a++)
                    {
                        float ang = spot * 2.39996323f;
                        float r = Mathf.Lerp(centerClearance + 1f, maxR, ((spot * 0.61803f) % 1f));
                        spot++;
                        p = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * r;
                        if (!InBox(p, cr)) { ok = true; break; }
                    }
                if (ok) SpawnStack(pf, 1, 1f, p, Random.value * 360f, root);
            }
        }
    }

    // ── KOMPOZİSYON: çeşitli simetrik öbekler ──────────────────────────────────
    void ComposeFormations(List<(GameObject prefab, int stack, float scale)> types, Transform root)
    {
        _carLayout = false;

        // ARABALAR (dünya 2): ŞEHİR kompozisyonu — sürekli araç CADDELERİ (ızgara) + blok içi GARAJ KULELERİ (dikey);
        // merkezde küçük araç, kenara doğru büyük (boyut gradyanı). Oyuncu caddeleri takip ederek kesintisiz gezer/yutar.
        // Arabalar (2), Binalar (3) VE İÇECEKLER (5): Cars-tarzı şehir kompozisyonu (Voronoi çeşit, anlamlı şekil öbekleri,
        // yatay zemin desenleri + dikey kule/istif, hedef garantisi, boyut-orantılı büyüme). Binalar 2026-07-31 sıfırdan.
        // ⚠️ İÇECEKLER 2026-08-03: nesneler ~3× büyüyünce GENEL ızgara/formasyon (Tatlılar yolu) çok SIKIŞIK oldu → nesneler
        // İÇ İÇE doğuyordu (kullanıcı). ComposeCarCity TÜM yerleşimi OccFree ile ÇAKIŞMASIZ kurar + yoğunluğu kontrol eder →
        // büyük içeceklere doğru ev. Dünya-5 için SEYREK "drk" profili (bkz ComposeCarCity: nGiant 0, geniş grid, kısa istif).
        if (Active != null && (Active.worldId == 2 || Active.worldId == 3 || BigObjWorld(Active.worldId))) { ComposeCarCity(types, root); return; }

        if (IsBigObjectLevel(types)) { ComposeBigObjects(types, root); return; }

        // KARE IZGARA makro yerleşim (eşmerkezli halka DEĞİL → dairesel silüet yok). 4-katlı ayna simetrisi:
        // stil/tür abs(ix,iz)'e göre seçilir → (±ix,±iz) hücreleri eşleşir. Formasyonlar eksen-hizalı (facing 0).
        // Hedef türleri ağırlıklı (daha çok hücre) → yüksek hedef sayıları yetişsin.
        var objSet = new HashSet<string>();
        foreach (var o in Active.objectives) objSet.Add(o.objectType);
        var bag = new List<(GameObject prefab, int stack, float scale)>();
        foreach (var t in types) { if (t.prefab.name == "Tray") continue; int w = objSet.Contains(t.prefab.name) ? 4 : 1; for (int i = 0; i < w; i++) bag.Add(t); }
        Shuffle(bag);

        // Merkeze yakın hücreler için KISA türler (uzun ağaç/lamba/saksı deliğin önünü kapatmasın). Tray hariç.
        var shortTypes = types.FindAll(t => t.prefab.name != "Tray" && ObjHeight(t.prefab) * t.scale <= 1.3f);
        if (shortTypes.Count == 0) shortTypes = types;

        // Atomium (foods L2): sağ-arka köşede, çevresi BOŞ kalsın (net görünsün) → o bölgede başka nesne yok.
        bool hasAtom = (Active != null && Active.worldId == 1 && Active.levelIndex == 1);
        Vector2 atomPos = new Vector2(0f, playHalf * 0.62f);   // orta-arka
        float atomClear = 7.5f;

        var trayT = FindType(types, "Tray");   // varsa: tepside servis formasyonu (stil 7)

        // Chichen Itza piramitleri (foods L1): arka-sol & arka-sağ; çevreleri boş kalsın.
        bool hasPyr = (Active != null && Active.worldId == 1 && Active.levelIndex == 0);
        Vector2 pyrR = new Vector2(playHalf * 0.55f, playHalf * 0.55f);
        Vector2 pyrL = new Vector2(-playHalf * 0.55f, playHalf * 0.55f);
        float pyrClear = 6f;

        int[] styleSet = { 0, 1, 2, 3, 4, 5, 6, 7 };   // 7 = tepside servis (tray varsa)
        // KÜÇÜK NESNE YOĞUN-KÜME: küçük nesneler sıkı öbek olur → delik dalınca hızlı büyür. TÜM levellarda AÇIK.
        // İSTİSNA: Foods'un ÖZEL YAPI levelları (Eyfel/Atomium/TajMahal/ChichenItza) korunur — kullanıcı "dokunma" dedi.
        // Foods L1(idx0)/L2(1)/L3(2)/L5(4) = özel. (MaxDim eşiği zaten yalnız KÜÇÜK nesnelere uygular → büyük araba/bina etkilenmez.)
        bool foodsSpecial = (Active != null && Active.worldId == 1 &&
            (Active.levelIndex == 0 || Active.levelIndex == 1 || Active.levelIndex == 2 || Active.levelIndex == 4));
        bool denseSmall = (Active != null) && !foodsSpecial;
        // BÜYÜK-NESNE dünyaları (araba): formasyonlar araç ayak izine (uzun) göre yayılır → 4.5 gap'te KOMŞU hücrelerin
        // araçları BİRBİRİNE GİRER (collider'lar çakışır → delik uyandırınca fırlar). gap'i en büyük araca göre büyüt,
        // araç formasyonu (BuildCarFormation) COLLIDER boyuyla sıkı ama çakışmasız kur.
        bool bigObj = Active != null && Active.worldId == 2;
        _carLayout = bigObj;   // SpawnStack: kenarda clamp'lenecek aracı BİNDİRME, ATLA (yapışık ikiz olmasın)
        float maxFoot = 1f;
        if (bigObj) foreach (var t in types) if (t.prefab != null && t.prefab.name != "Tray") maxFoot = Mathf.Max(maxFoot, Footprint(t.prefab) * t.scale);
        float gap = bigObj ? Mathf.Clamp(maxFoot * 1.5f, 4.1f, 6.5f) : 4.5f;
        int half = Mathf.Max(1, Mathf.FloorToInt(playHalf / gap));
        // Öbek TÜR ızgarası: AYNI türden öbekler HİÇBİR YÖNDEN komşu olmasın (kullanıcı 2026-07-25). Her hücre,
        // zaten atanmış 4 komşusundan (sol/alt/sol-alt/sol-üst) FARKLI tür alır → sağ/üst sonra bu hücreden kaçınır.
        var cellType = new Dictionary<(int, int), (GameObject prefab, int stack, float scale)>();
        var nbUsed = new HashSet<GameObject>();
        // BİNALAR (dünya 3): bina sayısını ~%25 azalt (kullanıcı 2026-07-28). Izgara DÜZENİNİ bozmadan, SpawnStack
        // her HEDEF-DIŞI bina kolonunun ~%25'ini pozisyon-hash ile deterministik atlar → şehir seyrekleşir, hedefler
        // korunur (kazanılabilirlik). Yalnız bu ızgara döngüsünde aktif; landmark/hedef-tamamlama ETKİLENMEZ.
        _thinBuildings = (Active != null && Active.worldId == 3);
        void AddNb(int nx, int nz) { if (cellType.TryGetValue((nx, nz), out var v) && v.prefab != null) nbUsed.Add(v.prefab); }
        for (int ix = -half; ix <= half; ix++)
            for (int iz = -half; iz <= half; iz++)
            {
                Vector2 anchor = new Vector2(ix * gap, iz * gap);
                if (anchor.magnitude < centerClearance + 0.5f) continue;   // sadece delik başlangıç boşluğu
                if (hasAtom && (anchor - atomPos).magnitude < atomClear) continue;   // Atomium çevresi boş
                if (hasPyr && ((anchor - pyrR).magnitude < pyrClear || (anchor - pyrL).magnitude < pyrClear)) continue;  // piramit çevresi boş
                if (InLandmarkArea(anchor)) continue;   // dünya harikası bölgesi BOŞ (altında nesne olmasın)
                int ai = Mathf.Abs(ix), aj = Mathf.Abs(iz);
                bool nearC = anchor.magnitude < 8f;
                int style = styleSet[(ai * 3 + aj * 7) % styleSet.Length];
                if (nearC)   // Merkeze yakın: KISA formasyon (kamp/çember) → delik başta görünür, önü kapanmaz
                {
                    int[] shortS = { 2, 3 };
                    style = shortS[(ai + aj) % 2];
                }
                // TÜR: komşulardan farklı seç. Havuz merkeze yakın KISA türler, dışta ağırlıklı torba (bag).
                var pool = nearC ? shortTypes : bag;
                int baseIdx = (ai * 5 + aj * 11) % pool.Count;
                nbUsed.Clear();
                AddNb(ix - 1, iz); AddNb(ix, iz - 1); AddNb(ix - 1, iz - 1); AddNb(ix - 1, iz + 1);
                var type = pool[baseIdx];
                for (int tI = 0; tI < pool.Count; tI++)
                {
                    var cand = pool[(baseIdx + tI) % pool.Count];
                    if (!nbUsed.Contains(cand.prefab)) { type = cand; break; }
                }
                cellType[(ix, iz)] = type;
                // BÜYÜK-NESNE (araba): araç-uygun COLLIDER-hizalı formasyon (çakışmasız, zemin) → varsayılan düzenin
                // canlı görünümü korunur ama araçlar birbirine girmez.
                if (bigObj) { BuildCarFormation(style, type, anchor, root, ai * 31 + aj); continue; }
                // Stil 7: tepside servis (tray + üstünde simetrik küçük nesneler). Tray yoksa kampa düş.
                if (style == 7)
                {
                    if (trayT.prefab != null)
                    {
                        var item = shortTypes[(ai * 7 + aj * 3) % shortTypes.Count];
                        BuildTrayPlatter(trayT, item, anchor, root);
                        continue;
                    }
                    style = 2;
                }
                // Küçük nesne + dense → yoğun küme. Şekil hücreye göre ÇEŞİTLENİR (daire/helezon/beşgen/yıldız/
                // kule); DOLU/BOŞ hücreye göre ~yarı yarıya değişir (ayrı hash → şekille korelasyonsuz;
                // kullanıcı 2026-07-22: "4 yıldızın 2'si dolu 2'si boş olsun").
                if (denseSmall && MaxDim(type.prefab) * type.scale <= DENSE_SMALL_MAXDIM)
                {
                    var shape = (DenseShape)(((ai * 7 + aj * 13) & 0x7fffffff) % 5);
                    bool filled = (((ai * 11 + aj * 17) & 0x7fffffff) % 2) == 0;
                    var dtype = type;
                    // Foods İÇİ DOLU öbeklerde nesneleri BÜYÜT → step büyür, dolu-dairedeki nesne sayısı azalır
                    // (sayı ∝ 1/step²). Boş konturlara dokunma. (kullanıcı 2026-07-25)
                    if (filled && Active != null && Active.worldId == 1)
                    {
                        float dm = 1f;
                        if (Active.levelIndex == 6) dm = 1.42f;        // L7: sayı ~yarıya
                        else if (Active.levelIndex == 7) dm = 1.30f;   // L8: biraz büyüt (~%40 az)
                        if (dm > 1f) dtype.scale *= dm;
                    }
                    BuildDenseCluster(dtype, anchor, gap * 0.5f, root, shape, filled);
                    continue;
                }
                BuildFormation(style, type, anchor, 0f, root, ai * 31 + aj);
            }
        _thinBuildings = false;   // BİNALAR thinning YALNIZ ızgara döngüsünde; sonraki yapılar (masa/köprü/landmark) tam kalsın

        // SHOWCASE MASA: şiş kebap KOLONLARI (4 ayak) + üstte dev tabak yiyecek. Uygun prefablar varsa, simetrik.
        var legT = FindType(types, "Kebab");
        var topT = FindType(types, "ChickenSalad");
        if (topT.prefab == null) topT = FindType(types, "WholePizza");
        if (topT.prefab == null) topT = FindType(types, "FriedEgg");
        if (legT.prefab != null && topT.prefab != null)
        {
            float tr = 0.5f * playHalf;
            for (int k = 0; k < 4; k++)
            {
                float ang = (k + 0.5f) * (Mathf.PI * 0.5f);
                Vector2 anchor = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * tr;
                if (hasAtom && (anchor - atomPos).magnitude < atomClear) continue;   // Atomium bölgesine masa koyma
                if (hasPyr && ((anchor - pyrR).magnitude < pyrClear || (anchor - pyrL).magnitude < pyrClear)) continue;
                BuildTable(legT, topT, anchor, root);
            }
        }

        // MACARON HEYKELİ (renkli roket): 5 renk macaron varsa, 2 simetrik konumda kurulur.
        // Foods L12 (idx11, kullanıcı 2026-07-25): arka-ortadaki macaron kulesi istenmedi → L12'de kurma.
        bool noMacRocket = Active != null && Active.worldId == 1 && Active.levelIndex == 11;
        var mp = FindType(types, "MacaronPurple"); var mb = FindType(types, "MacaronBlue");
        var mg = FindType(types, "MacaronGreen");  var my = FindType(types, "MacaronYellow");
        var mpk = FindType(types, "MacaronPink");
        if (!noMacRocket && mp.prefab && mb.prefab && mg.prefab && my.prefab && mpk.prefab)
        {
            var cols = new[] { mp, mb, mg, my, mpk };   // alttan üste renk bandı
            float rr = 0.62f * playHalf;
            var rk1 = new Vector2(0f, rr); var rk2 = new Vector2(0f, -rr);
            if (!(hasAtom && (rk1 - atomPos).magnitude < atomClear)) BuildMacaronRocket(rk1, cols, mpk, root);
            if (!(hasAtom && (rk2 - atomPos).magnitude < atomClear)) BuildMacaronRocket(rk2, cols, mpk, root);
        }

        // TACO KÖPRÜSÜ: 2 simetrik, KENARDA (merkez-sağdaki Atomium/Eyfel ile çakışmasın diye dışta).
        var taco = FindType(types, "Taco");
        if (taco.prefab != null)
        {
            float tb = 0.85f * playHalf;
            BuildTacoBridge(taco, new Vector2( tb, 0f), Mathf.PI * 0.5f, root);
            BuildTacoBridge(taco, new Vector2(-tb, 0f), Mathf.PI * 0.5f, root);
        }

        // KEBAP EYFEL KULESİ: yalnız Yiyecekler Level 5'te, merkezin hafif sağında (delik altında başlamasın).
        if (Active != null && Active.worldId == 1 && Active.levelIndex == 4)
        {
            var keb = FindType(types, "Kebab");
            if (keb.prefab != null)
            {
                var macs = new[] { mp.prefab, mb.prefab, mg.prefab, my.prefab, mpk.prefab };
                bool allMacs = mp.prefab && mb.prefab && mg.prefab && my.prefab && mpk.prefab;
                BuildEiffel(keb, allMacs ? macs : null, new Vector2(4f, 0f), root);
            }
        }

        // CHICHEN ITZA: foods L1 — 2 kademeli piramit (arka-sol & arka-sağ): hamburger teraslar + kebap merdiven + tepsi tapınak.
        if (hasPyr)
        {
            var bg = FindType(types, "Hamburger"); var kbp = FindType(types, "Kebab"); var trp = FindType(types, "Tray");
            if (bg.prefab && kbp.prefab && trp.prefab)
            {
                BuildChichenItza(bg, kbp, trp, mg.prefab, pyrL, root);
                BuildChichenItza(bg, kbp, trp, mg.prefab, pyrR, root);
            }
        }

        // ATOMIUM: yalnız Yiyecekler Level 2'de — brokoli toplar + kebap çubuklar, merkez sağında (en büyük yapı).
        if (Active != null && Active.worldId == 1 && Active.levelIndex == 1)
        {
            var br = FindType(types, "Broccoli"); var kb = FindType(types, "Kebab");
            if (br.prefab != null && kb.prefab != null) BuildAtomium(br, kb, atomPos, root);   // sağ-arka köşe
        }

        // TAJ MAHAL: yalnız Yiyecekler Level 3'te — sadece macaronlardan (krem=sarı gövde + pembe/mavi/mor aksan).
        if (Active != null && Active.worldId == 1 && Active.levelIndex == 2)
        {
            var cr = FindType(types, "MacaronYellow"); var pk = FindType(types, "MacaronPink");
            var bl = FindType(types, "MacaronBlue");   var pu = FindType(types, "MacaronPurple");
            if (cr.prefab && pk.prefab && bl.prefab && pu.prefab)
                BuildTajMahal(cr.prefab, pk.prefab, bl.prefab, pu.prefab, new Vector2(5f, 0f), root);
        }
    }

    // ── BÜYÜK-NESNE DÜZ PARK-YERİ DÜZENİ (araba/bina/gemi/uçak) ──────────────────
    // Sorun (kullanıcı 2026-07-25): büyük nesneler kule/kolon formasyonlarında İSTİFLENİYORDU → pancake yığın,
    // iç içe geçme, birbiri altında kaybolma. Çözüm: TEK KATMAN uniform ızgara; hücre boyutu en büyük nesnenin
    // ayak izine göre (yan yana çakışmaz); komşu hücreler farklı tür; satır bazlı hizalı yön (park aisle görünümü).
    bool IsBigObjectLevel(List<(GameObject prefab, int stack, float scale)> types)
    {
        // KAPALI (2026-07-26): Arabalar için özel kompozisyonlar (park-yeri / şekil-kümesi / mandala) kullanıcı
        // tarafından reddedildi ("atlıkarınca gibi"). Arabalar dünyası artık EN BAŞTAKİ varsayılan ızgara
        // kompozisyonunu kullanır (ComposeBigObjects devre dışı). Diğer büyük dünyalar da varsayılanı kullanır.
        return false;
    }

    // (len, wid, baseYaw) — araç GÖRSEL boyu/eni (collider/0.8) ve uzun-ekseni +Z'ye hizalayan yaw.
    static (float len, float wid, float yaw) CarDims(GameObject pf, float sc)
    {
        float len, wid;
        var bc = pf.GetComponent<BoxCollider>();
        if (bc != null) { len = Mathf.Max(0.6f, Mathf.Max(bc.size.x, bc.size.z) / 0.8f * sc); wid = Mathf.Max(0.4f, Mathf.Min(bc.size.x, bc.size.z) / 0.8f * sc); }
        else { var rs = RendererSize(pf); len = Mathf.Max(0.6f, Mathf.Max(rs.x, rs.z) * sc); wid = Mathf.Max(0.4f, Mathf.Min(rs.x, rs.z) * sc); }
        float yaw = (Mathf.Abs(LongAxis(pf).x) > 0.5f) ? 90f : 0f;
        return (len, wid, yaw);
    }

    static readonly Vector2[] SquareUnit = { new(-0.92f, -0.92f), new(0.92f, -0.92f), new(0.92f, 0.92f), new(-0.92f, 0.92f) };

    // ARAÇLARLA YERDE ŞEKİL çizen öbek (Yiyecekler tarzı): dolu daire / halka / yıldız / kare / beşgen / spiral
    // (araçlar şekle TEĞET → deseni izler) veya KULE öbeği (dikey istif). Her araç OccFree+NearObj+sınır ile yerleşir →
    // ÇAKIŞMA YOK (köşelerde atlar). maxCount>0 ise en çok o kadar (hedef sayısı için; dolu daire ona ulaşana dek halka
    // ekler). Döner: yerleştirilen adet.
    int BuildCarShapeCluster(int shape, GameObject pf, float sc, Vector2 center, float radius, float extent, Transform root, int maxCount = 0)
    {
        var d = CarDims(pf, sc);
        float cr = Footprint(pf) * sc * 0.5f;
        float step = d.len * 1.14f;
        int placed = 0;
        bool Tan(Vector2 p, Vector2 dir, int stack)
        {
            if (maxCount > 0 && placed >= maxCount) return false;
            if (p.magnitude > extent - 0.6f || p.magnitude < centerClearance + 0.8f) return false;
            if (InBox(p, cr)) return false;   // şekilli arena: dışlama kutusuna (engel/çıkıntı) girme
            if (InLandmarkArea(p) || NearObj(p, cr) || !OccFree(p, cr)) return false;
            float head = Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg;
            SpawnStack(pf, stack, sc, p, d.yaw + head, root);
            placed++; return true;
        }
        void Ring(float rr, int stack)
        {
            int nn = Mathf.Max(3, Mathf.FloorToInt(2f * Mathf.PI * rr / step));
            float ph = Random.value * 6.283f;
            for (int i = 0; i < nn; i++)
            {
                float a = ph + i * (2f * Mathf.PI / nn), cs = Mathf.Cos(a), sn = Mathf.Sin(a);
                Tan(center + new Vector2(cs, sn) * rr, new Vector2(-sn, cs), stack);
            }
        }
        void Poly(Vector2[] unit, float scl)
        {
            int m = unit.Length;
            for (int e = 0; e < m; e++)
            {
                Vector2 A = center + unit[e] * scl, B = center + unit[(e + 1) % m] * scl;
                Vector2 dir = (B - A).sqrMagnitude > 0.001f ? (B - A).normalized : Vector2.up;
                int segs = Mathf.Max(1, Mathf.FloorToInt((B - A).magnitude / step));
                for (int s = 0; s < segs; s++) Tan(Vector2.Lerp(A, B, (s + 0.5f) / segs), dir, 1);
            }
        }
        void PolyFilled(Vector2[] unit)   // DOLU çokgen: eşmerkezli konturlar (içe doğru) → şekil dolu, yoğun
        {
            Tan(center, new Vector2(0, 1), 1);
            float unitR = 0f; foreach (var u in unit) unitR = Mathf.Max(unitR, u.magnitude);
            for (float scl = step / Mathf.Max(0.3f, unitR); scl * unitR <= radius + 0.01f; scl += step / Mathf.Max(0.3f, unitR))
                Poly(unit, scl);
        }
        switch (shape)
        {
            case 0:   // DOLU DAİRE (maxCount'a ulaşana dek halka ekle → hedefler için güvenilir sayı)
                Tan(center, new Vector2(0, 1), 1);
                for (float rr = step; (maxCount <= 0 ? rr <= radius : placed < maxCount) && rr < extent * 0.55f; rr += step) Ring(rr, 1);
                break;
            case 1: PolyFilled(StarUnit); break;                    // DOLU YILDIZ
            case 2: PolyFilled(SquareUnit); break;                  // DOLU KARE
            case 3: PolyFilled(PentaUnit); break;                   // DOLU BEŞGEN
            case 4: Ring(radius, 1); break;                         // HALKA (tek kontur — çeşit için seyrek)
            case 5:                                                  // SPİRAL (phyllotaxis, teğet)
            {
                float c = step * 0.95f;
                int nn = Mathf.Clamp(Mathf.RoundToInt((radius / c) * (radius / c)), 6, 28);
                for (int i = 0; i < nn; i++)
                {
                    float rr = c * Mathf.Sqrt(i + 0.5f); if (rr > radius) break;
                    float a = i * 2.399963f, cs = Mathf.Cos(a), sn = Mathf.Sin(a);
                    Tan(center + new Vector2(cs, sn) * rr, new Vector2(-sn, cs), 1);
                }
                break;
            }
            default:                                                 // KULE ÖBEĞİ — DİKEY↔YATAY TERS ORANTILI (kullanıcı):
            {                                                        // ya DAR+YÜKSEK (2-3 kule, 5-7 kat) ya GENİŞ+ALÇAK (çok, 1-2 kat).
                float sx = d.wid * 1.14f, sz = d.len * 1.06f;
                bool okp(Vector2 p) => !(p.magnitude > extent - 0.6f || p.magnitude < centerClearance + 0.6f || InLandmarkArea(p) || NearObj(p, cr));
                if (Random.value < 0.55f)
                {   // DAR + YÜKSEK: 2-3 kule yan yana, 5-7 kat (küçük ayak izi)
                    int nt = Random.Range(2, 4);
                    for (int t = 0; t < nt; t++)
                    {
                        Vector2 p = center + new Vector2((t - (nt - 1) * 0.5f) * sx, 0f);
                        if (okp(p)) { SpawnStack(pf, Random.Range(5, 8), sc, p, d.yaw, root); placed++; }
                    }
                }
                else
                {   // GENİŞ + ALÇAK: geniş ızgara, 1-2 kat (çok ayak izi ama kısa)
                    int cols = Mathf.Clamp(Mathf.CeilToInt(2.2f * radius / sx), 3, 5);
                    int rows = Mathf.Clamp(Mathf.CeilToInt(2.0f * radius / sz), 2, 3);
                    for (int gy = 0; gy < rows; gy++)
                        for (int gx = 0; gx < cols; gx++)
                        {
                            Vector2 p = center + new Vector2((gx - (cols - 1) * 0.5f) * sx, (gy - (rows - 1) * 0.5f) * sz);
                            if (okp(p)) { SpawnStack(pf, Random.Range(1, 3), sc, p, d.yaw, root); placed++; }
                        }
                }
                break;
            }
        }
        return placed;
    }

    // ── ARABALAR (dünya 2): YERDE ŞEKİL çizen araç öbekleri (Yiyecekler tarzı: daire/halka/yıldız/kare/beşgen/spiral)
    // + KULE öbekleri (dikey). Tür bölgeleri (Voronoi) → aynı tür 2-3 uzak alanda. Hedefler garantili (dolu daire).
    void ComposeCarCity(List<(GameObject prefab, int stack, float scale)> types, Transform root)
    {
        _carLayout = true;
        // BİNALAR (dünya 3): daha SEYREK yoğunluk (hedef 500-600, arabalar ~800) + kısa istif. Grid'ler büyür,
        // dolgu istifi 2-4 (dikey mimariyi korur ama sayıyı patlatmaz). (2026-07-31 kullanıcı: yoğunluk 500-600.)
        bool bld = Active != null && Active.worldId == 3;
        // İÇECEKLER (dünya 5, 2026-08-03): nesneler ~3× → EN SEYREK profil (kullanıcı: "sahneler çok kalabalık, sayıyı
        // azalt"). Geniş grid (az öbek), kısa istif, DEV içecek YOK (×3-4 dev = maxSize'ı aşar), şekil öbekleri ZEMİN
        // (tall kule yok → büyük bardak kuleleri kamerayı kapatmasın). Boyut zaten 3× → ComposeCarCity büyütmesi KAPALI.
        bool drk = Active != null && BigObjWorld(Active.worldId);
        _occ.Clear();
        _typeCount.Clear();
        // Araç boyutunu biraz büyüt (kullanıcı 2026-07-28) + çok küçükleri daha çok → görsel dolgunluk.
        var cars = new List<(GameObject prefab, int stack, float scale)>();
        foreach (var t in types)
        {
            if (t.prefab == null || t.prefab.name == "Tray") continue;
            var d = t; float md = MaxDim(d.prefab) * d.scale;
            if (!drk) d.scale *= (md < 1.0f) ? 1.4f : (md < 1.9f) ? 1.18f : 1.0f;   // küçük/orta büyür; BÜYÜK DOKUNULMAZ. İÇECEK: zaten 3× → dokunma.
            cars.Add(d);
        }
        if (cars.Count == 0) return;
        var objSet = new HashSet<string>();
        foreach (var o in Active.objectives) objSet.Add(o.objectType);

        // İÇECEKLER (2026-08-03 kullanıcı): hedeflerden biri GENİŞ/BÜYÜK bir nesne mi (footprint ≥ 2.8 → deliğin ~3.3'e
        // büyümesi gerekir)? Öyleyse o levelda KÜÇÜK+ORTA nesne sayısını artır (grid'i sıklaştır) → oyuncu deliği büyütecek
        // bol "yakıt" bulur (büyük hedefi yutacak boyuta ulaşır). Sadece dünya-5.
        bool hasBigObjective = false;
        if (drk)
            foreach (var t in cars)
                if (objSet.Contains(t.prefab.name) && Footprint(t.prefab) * t.scale >= 2.8f) { hasBigObjective = true; break; }

        float wall = (_arenaLimit < 900f) ? _arenaLimit : (frameHalf - 0.6f);
        float extent = Mathf.Min(playHalf, wall - 1.0f);

        // ── TÜR BÖLGELERİ (SEED/Voronoi): her tür 2-3 AYRI ve BİRBİRİNDEN UZAK bölge. Her türe K tohum, K-katlı
        // simetrik açıyla (aynı türün tohumları arena'nın karşı taraflarına düşer → uzak). Hedef türler 3 bölge
        // (daha çok arama-bulma), diğerleri 2. Her nokta EN YAKIN tohumun türünü alır → tür 2-3 uzak kümede toplanır.
        var seeds = new List<(Vector2 pos, GameObject pf, float sc)>();
        float rInner = centerClearance + 3f, rOuter = extent - 2f;
        const float gA = 2.399963f;   // altın açı → tür temel açıları yayılır
        for (int ti = 0; ti < cars.Count; ti++)
        {
            var t = cars[ti];
            // İÇECEKLER (2026-08-03 kullanıcı: "çok kolay; hedef her yerde"): HEDEF türlerini Voronoi dolgusundan ÇIKAR →
            // dolgu (şekil/kule/fill) YALNIZ non-hedef olur. Sahnede hedef nesnesi SADECE aşağıdaki kontrollü kümelerde
            // (= req+5) bulunur → oyuncu tek köşeden yığınla toplayıp bitiremez. Non-hedef K=3 (çeşit + yayılım).
            if (drk && objSet.Contains(t.prefab.name)) continue;
            int K = drk ? 3 : (objSet.Contains(t.prefab.name) ? 3 : 2);
            float A = ti * gA;
            float R = Mathf.Lerp(rInner, rOuter, (ti * 0.61803f) % 1f);
            for (int k = 0; k < K; k++)
            {
                float a = A + k * (2f * Mathf.PI / K);
                seeds.Add((new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * R, t.prefab, t.scale));
            }
        }
        (GameObject pf, float sc) Pick(Vector2 pos)
        {
            float best = float.MaxValue; int bi = 0;
            for (int s = 0; s < seeds.Count; s++)
            {
                float dd = (seeds[s].pos - pos).sqrMagnitude;
                if (dd < best) { best = dd; bi = s; }
            }
            return (seeds[bi].pf, seeds[bi].sc);
        }

        // HEDEF KÜMELERİ (EN ÖNCE → garantili yer): her hedef K=2 AYRI, UZAK, YOĞUN park lotu (ortak halkada eşit
        // açıyla, karşılıklı → 2 uzak bölge). Sokak/blok/dolgu sonra gelip lotlardan KAÇINIR (NearObj) → hedefler
        // asla ezilmez, altın-açı spiraline gerek kalmaz (ÇAKIŞMA YOK), oyuncu 2 uzak lotta ihtiyacını toplar.
        _objOcc.Clear();

        var vObj = new List<(GameObject pf, float sc, int req)>();
        foreach (var o in Active.objectives)
        {
            GameObject opf0 = null; float osc0 = 1f;
            for (int s = 0; s < cars.Count; s++) if (cars[s].prefab.name == o.objectType) { opf0 = cars[s].prefab; osc0 = cars[s].scale; break; }   // seeds değil cars (hedef seeds'ten çıkarıldı)
            if (opf0 != null) vObj.Add((opf0, osc0, o.required));
        }
        // İÇECEKLER: hedef sahnede EN FAZLA req+5; kobj=3 AYRI bölgeye böl → her disk ~(req+5)/3 < required → tek köşeden
        // tamamlanamaz, oyuncu gezmek zorunda. Diğer dünyalar: eski davranış (req+3, 2 lot).
        int kobj = drk ? 3 : 2;
        int totalC = Mathf.Max(1, vObj.Count * kobj);
        for (int oi = 0; oi < vObj.Count; oi++)
        {
            var (opf, osc, req) = vObj[oi];
            float ocr = Footprint(opf) * osc * 0.5f;
            // İÇECEK: sahnedeki FAZLA rank'a göre (kullanıcı): rank0 = TAM req (fazla 0), rank1 +2, rank2 +3, rank3 +5.
            // (DrinksObjectiveTuner AYNI diziyle required'ı maxPlaceable-fazla ile tavanlar → toplam sığar.)
            int surplus = drk ? (oi == 0 ? 0 : oi == 1 ? 2 : oi == 2 ? 3 : 5) : 3;
            int target = req + surplus, done = 0;
            for (int kk = 0; kk < kobj && done < target; kk++)
            {
                // Her hedefin kobj diski FARKLI açı+yarıçapta → arenanın farklı köşe/bölgeleri; iki disk asla aynı açıda değil.
                float ang = oi * (2f * Mathf.PI / vObj.Count) + kk * (2f * Mathf.PI / kobj) + 0.3f;
                float rr = extent * (kobj > 1 ? Mathf.Lerp(0.45f, 0.85f, (float)kk / (kobj - 1)) : 0.5f);
                Vector2 center = new(Mathf.Cos(ang) * rr, Mathf.Sin(ang) * rr);
                int need = Mathf.CeilToInt((target - done) / (float)(kobj - kk));
                int before = _occ.Count;
                done += BuildCarShapeCluster(0, opf, osc, center, extent * 0.28f, extent, root, need);   // DOLU DAİRE (kompakt küme), need adete kadar
                for (int z = before; z < _occ.Count; z++) _objOcc.Add(_occ[z]);                          // sokak/şekil bunlardan kaçınsın
            }
            // ⚠️ KAZANILABİLİRLİK GARANTİSİ: disk kenara taşıp target'a ulaşamazsa KALAN eksiği arena ızgarasında
            // boş yerlere koy (aksi halde o hedef eksik = kazanılamaz). İki KAYDIRILMIŞ geçiş (gap'leri yakalar).
            var od = CarDims(opf, osc);
            float gstep = Mathf.Max(0.6f, ocr * 0.72f);
            for (int pass = 0; pass < 4 && done < target; pass++)
            {
                float off = pass * gstep * 0.25f;
                int gnn = Mathf.FloorToInt((extent + off) / gstep) + 1;
                for (int gy = -gnn; gy <= gnn && done < target; gy++)
                    for (int gx = -gnn; gx <= gnn && done < target; gx++)
                    {
                        Vector2 p = new(gx * gstep + off, gy * gstep + off);
                        if (p.magnitude > extent - 0.6f || p.magnitude < centerClearance + 0.8f || InLandmarkArea(p) || InBox(p, ocr) || !OccFree(p, ocr)) continue;   // şekilli arena: kutuya koyma (yoksa done sayar ama SpawnStack atlar → eksik hedef)
                        SpawnStack(opf, 1, osc, p, od.yaw, root);
                        _objOcc.Add((p, ocr));
                        done++;
                    }
            }
        }

        // DEV ARAÇLAR (3-4 landmark, ×3-4 ölçek) — HEDEFLERDEN SONRA (hedefler öncelik alsın, dev alanı çalmasın).
        // Hedef-olmayan türlerden; OccFree ile BOŞ yere konur (hedeflerin üstüne gelmez); _objOcc'a eklenir → şekil/kule/
        // dolgu etrafından geçer. Bulamazsa açıyı döndürerek dener.
        int nGiant = (bld || drk) ? 0 : Random.Range(3, 5);   // BİNALAR/İÇECEKLER: dev YOK (×3-4 → hole maxSize'ı aşar, yutulamaz/takılı kalır).
        for (int gi = 0; gi < nGiant; gi++)
        {
            var gt = cars[(gi * 13 + 5) % cars.Count];
            for (int tries = 0; tries < cars.Count && objSet.Contains(gt.prefab.name); tries++) gt = cars[(gi * 13 + 5 + tries + 1) % cars.Count];
            float gscale = gt.scale * Random.Range(3.0f, 4.2f);
            var gd = CarDims(gt.prefab, gscale);
            float gcr = Footprint(gt.prefab) * gscale * 0.5f;
            for (int a = 0; a < 30; a++)   // boş yer bulana kadar açı/yarıçap dene
            {
                float gang = (gi + a * 0.37f) * (2f * Mathf.PI / nGiant) + 0.5f;
                float grad = extent * (0.4f + (a % 5) * 0.1f);
                Vector2 gc = new(Mathf.Cos(gang) * grad, Mathf.Sin(gang) * grad);
                if (gc.magnitude + gcr > extent - 0.5f || gc.magnitude < centerClearance + gcr || InLandmarkArea(gc) || InBox(gc, gcr) || !OccFree(gc, gcr)) continue;
                SpawnStack(gt.prefab, 1, gscale, gc, gd.yaw, root);
                _objOcc.Add((gc, gcr));
                break;
            }
        }

        // ŞEKİL ÖBEKLERİ (Yiyecekler tarzı): jitter'lı ızgarada her hücre bir ŞEKİL çizen araç öbeği — daire/halka/
        // yıldız/kare/beşgen/spiral/kule (Voronoi türü). Regimente sokak/park YOK; araçlar yerde desen çizer, kuleler
        // dikey verir. Her araç OccFree → öbekler birbirine girmez. HEDEF dolu-dairelerinden KAÇINIR (NearObj).
        // İÇECEK: hedef türleri artık dolguda YOK → boşluğu non-hedef ÇEŞİTLE doldur (kullanıcı: çeşit+diğer sayı artsın). Grid sıklaştı.
        float G = drk ? (hasBigObjective ? 3.0f : 3.3f) : (bld ? 4.0f : 3.5f);
        int gn = Mathf.FloorToInt(extent / G) + 1;
        for (int j = -gn; j <= gn; j++)
            for (int i = -gn; i <= gn; i++)
            {
                Vector2 c = new(j * G + Random.Range(-1.0f, 1.0f), i * G + Random.Range(-1.0f, 1.0f));
                if (c.magnitude < centerClearance - 0.3f || c.magnitude > extent - 0.6f) continue;
                if (InLandmarkArea(c) || InBox(c, G * 0.5f) || NearObj(c, G * 0.1f)) continue;   // şekilli arena: kutu hücresini atla
                var (pf, sc) = Pick(c);
                int h = Mathf.Abs(j) * 5 + Mathf.Abs(i) * 11 + (j + i < 0 ? 2 : 0);
                // İÇECEK: yalnız ZEMİN şekilleri (0-5) → tall bardak kulesi yok (kamerayı kapatmaz); dikeylik kısa dolgu
                // istifinden gelir. Arabalar/Binalar: ~%60 kule öbeği (dikey silüet).
                int shp = drk ? (h % 6) : ((h % 5 < 3) ? 6 : (h % 6));
                float radius = G * Random.Range(0.44f, 0.58f);
                BuildCarShapeCluster(shp, pf, sc, c, radius, extent, root);
            }

        // BOŞLUK DOLDURMA + DİKEY: ince ızgarada BOŞ kalan yerlere o bölgenin türünden GARAJ KULESİ koy. Sadece
        // OccFree (mevcut araçtan uzak) yerlere → ÇAKIŞMA YOK. Boş alanları kapatır + dikey dizilimi artırır.
        float fg = drk ? (hasBigObjective ? 2.2f : 2.4f) : (bld ? 2.8f : 2.2f);   // İÇECEK: daha sık dolgu (hedef dışı boşluğu doldur)
        int fn = Mathf.FloorToInt(extent / fg);
        for (int i = -fn; i <= fn; i++)
            for (int j = -fn; j <= fn; j++)
            {
                Vector2 c = new(j * fg + (i & 1) * fg * 0.5f, i * fg);   // satır bazlı kaydırma → daha sık kapama
                if (c.magnitude < centerClearance - 0.3f) continue;
                if (c.magnitude > extent - 0.5f) continue;
                if (InLandmarkArea(c)) continue;
                var (pf, sc) = Pick(c);
                float rr = Footprint(pf) * sc * 0.5f;
                if (InBox(c, rr)) continue;                               // şekilli arena: kutuya girme
                if (!OccFree(c, rr * 1.02f)) continue;                    // sadece BOŞ yerlere
                SpawnStack(pf, (bld || drk) ? Random.Range(2, 4) : Random.Range(5, 8), sc, c, CarDims(pf, sc).yaw, root);   // tall kule (bina/içecek: kısa 2-3 istif)
            }
    }

    // Bir caddeyi (2 şerit) tampon-tampona doldurur. skipInter=true ise diğer eksendeki S-katı kavşaklarda kesilir.
    void WalkAvenue(float fixedCoord, bool vertical, bool skipInter, float S, float interHalf, float extent,
                    System.Func<Vector2, (GameObject, float)> pick, Transform root)
    {
        float t = -extent; int guard = 0;
        while (t <= extent && guard++ < 700)
        {
            Vector2 p = vertical ? new Vector2(fixedCoord, t) : new Vector2(t, fixedCoord);
            // TÜR SEGMENT bazlı (kavşaklar arası tek tür) → segment içinde tek boyut → boyut-değişimi çakışması YOK.
            float segAlong = (Mathf.Floor(t / S) + 0.5f) * S;
            Vector2 segC = vertical ? new Vector2(fixedCoord, segAlong) : new Vector2(segAlong, fixedCoord);
            var (pf, sc) = pick(segC);
            var d = CarDims(pf, sc);
            float step = d.len * 1.05f;
            float alongCoord = vertical ? p.y : p.x;
            bool atInter = skipInter && Mathf.Abs(alongCoord - Mathf.Round(alongCoord / S) * S) < interHalf;
            if (p.magnitude < centerClearance + 1.0f || p.magnitude > extent || atInter || InLandmarkArea(p)) { t += step; continue; }
            float lane = d.wid * 0.62f;               // 2 şerit ofseti
            float yaw = vertical ? d.yaw : d.yaw + 90f;
            Vector2 perp = vertical ? new Vector2(1f, 0f) : new Vector2(0f, 1f);
            float cr = d.len * 0.5f;                   // HEDEF lotuna değen şeriti ATLA (lot ezilmesin)
            Vector2 p1 = p - perp * lane, p2 = p + perp * lane;
            if (!NearObj(p1, cr)) SpawnStack(pf, 1, sc, p1, yaw, root);
            if (!NearObj(p2, cr)) SpawnStack(pf, 1, sc, p2, yaw, root);
            t += step;
        }
    }

    void ComposeBigObjects(List<(GameObject prefab, int stack, float scale)> types, Transform root)
    {
        var objSet = new HashSet<string>();
        foreach (var o in Active.objectives) objSet.Add(o.objectType);

        // Tür torbası: hedefler biraz daha sık (arz yetsin), her tür en az bir kez. Karıştır → bantlar çeşitli sıralanır.
        // Çok küçük araçları BÜYÜT (kullanıcı: çok küçük araba olmasın) → min görsel boyut ~1.2.
        var bag = new List<(GameObject prefab, int stack, float scale)>();
        foreach (var t in types)
        {
            if (t.prefab == null || t.prefab.name == "Tray") continue;
            var d = t;
            float md0 = MaxDim(d.prefab) * d.scale;
            if (md0 < 1.2f) d.scale *= Mathf.Min(1.7f, 1.2f / Mathf.Max(0.3f, md0));
            int w = objSet.Contains(d.prefab.name) ? 2 : 1;
            for (int i = 0; i < w; i++) bag.Add(d);
        }
        if (bag.Count == 0) return;
        Shuffle(bag);

        // ── KONSANTRİK BANT MANDALA ────────────────────────────────────────────
        // Bütün arenayı iç içe halkalarla doldur. Her halka = BİR araç türü; araçlar TEĞET (uzun eksen çember
        // boyunca). İki çakışma-garantisi:
        //   • yay aralığı = 2πr/n, n=FLOOR(2πr / (boy·1.12)) → yay ≥ görsel boy → YATAY çakışma imkansız (her tür).
        //   • halkalar arası boşluk = (en_k + en_k+1)/2 · 1.12 → teğet araç radyal olarak ENİNİ gösterir → RADYAL
        //     çakışma imkansız; en'e bağlı olduğu için DAR → yoğun.
        // Küçük araç = sık dolu bant, büyük araç = ferah bant → irili ufaklı bol çeşit, ahenkli, KARIŞMADAN.
        // Yükseklik terasları (bazı bantlar kule) → skyline kabartması + "üst üste kule" isteği. Vertikal çakışmayı
        // SpawnStack/RestOn (×1.04 net boşluk) zaten önler.
        float extent = Mathf.Min(playHalf, Mathf.Min(_arenaLimit - 0.6f, frameHalf - 0.6f));

        // Bir aracın görsel boy/en/yaw'ı (COLLIDER'dan; RendererSize bazı prefablarda mesh-child scale'i atlıyor).
        (float len, float wid, float yaw) Dims((GameObject prefab, int stack, float scale) t)
        {
            var p = t.prefab; float s = t.scale; float L, W;
            var bc = p.GetComponent<BoxCollider>();
            if (bc != null) { L = Mathf.Max(0.6f, Mathf.Max(bc.size.x, bc.size.z) / 0.8f * s); W = Mathf.Max(0.4f, Mathf.Min(bc.size.x, bc.size.z) / 0.8f * s); }
            else { var rs = RendererSize(p); L = Mathf.Max(0.6f, Mathf.Max(rs.x, rs.z) * s); W = Mathf.Max(0.4f, Mathf.Min(rs.x, rs.z) * s); }
            float yaw = (Mathf.Abs(LongAxis(p).x) > 0.5f) ? 90f : 0f;
            return (L, W, yaw);
        }

        // Her halka ARC'lara bölünür; her arc SIRADAKİ tür (round-robin cursor tüm halkalar boyunca sürer) → tüm
        // türler eşit dağılır (tek tür bir halkayı domine etmez), çeşit MAKSİMUM. Her aracın açısal yuvası = boyu·1.3
        // → yay ≥ görsel boy → çakışma yok; artan boşluk yuvalara eşit dağıtılır (araçlar çemberi tam kapatır).
        int cursor = 0, ringIdx = 0;
        float r = centerClearance + 1.0f;
        while (r <= extent)
        {
            float circ = 2f * Mathf.PI * r;
            var ring = new List<(GameObject pf, float sc, float len, float yaw)>();
            float need = 0f, maxWid = 0.4f;
            while (ring.Count < 240)
            {
                var t = bag[cursor % bag.Count];
                var d = Dims(t);
                float slot = d.len * 1.16f;
                if (need + slot > circ) { if (ring.Count == 0) maxWid = d.wid; break; }   // halka doldu (veya araç sığmaz)
                if (ring.Count > 0 && ring[ring.Count - 1].pf == t.prefab && bag.Count > 1) { cursor++; continue; }  // ardışık aynı tür değil
                ring.Add((t.prefab, t.scale, d.len, d.yaw));
                need += slot; maxWid = Mathf.Max(maxWid, d.wid); cursor++;
            }

            if (ring.Count >= 3)
            {
                // Konsantrik yükseklik: KULELER İÇTE (odak + sayıyı şişirmez), dış bantlar TEK KAT (zemin deseni okunur).
                bool inner = r < extent * 0.6f;
                int stackH = inner ? (ringIdx % 2 == 0 ? Random.Range(3, 5) : Random.Range(1, 3)) : 1;
                float gap = (circ - need) / ring.Count;   // artan boşluğu yuvalara eşit dağıt → araçlar çemberi kapatır
                float a = (ringIdx % 2) * 0.35f;           // ardışık halkaları açıca kaydır → tuğla örgü, daha canlı
                foreach (var e in ring)
                {
                    float slot = e.len * 1.16f + gap;
                    float mid = a + slot * 0.5f;
                    a += slot;
                    float cs = Mathf.Cos(mid), sn = Mathf.Sin(mid);
                    Vector2 pos = new(cs * r, sn * r);
                    if (InLandmarkArea(pos)) continue;                     // dünya harikası bölgesi boş
                    float head = Mathf.Atan2(-sn, cs) * Mathf.Rad2Deg;     // teğet yön (uzun eksen çember boyunca)
                    SpawnStack(e.pf, stackH, e.sc, pos, e.yaw + head, root);
                }
            }

            r += maxWid * 1.28f;   // halkalar arası ≥ en → teğet araç radyal enini gösterir → radyal çakışma yok
            ringIdx++;
        }
    }

    // ── İÇECEKLER KOMPOZİSYONU (dünya 5) ─────────────────────────────────────
    // Kullanıcı geri bildirimi: (1) öbekler çok küçük nesneli + çok sayıda → oyun kolay + delik takılıyor.
    //   Çözüm: SADECE 4 yoğun öbek (simetrik iç köşe hücrelerinde), öbek nesneleri DAHA BÜYÜK + DAHA AZ sayıda.
    // (2) diğer hücrelerde tek-tip küçük yığın yerine TANINMIŞ 3B GEOMETRİK yapılar: piramit / silindir / kubbe
    //   (küre) / kule. Hepsi KISA tutulur (≤ ~3.2 birim) → kamera/deliğin önünü kapatmaz.
    void ComposeDrinks(List<(GameObject prefab, int stack, float scale)> types, Transform root)
    {
        var objSet = new HashSet<string>();
        foreach (var o in Active.objectives) objSet.Add(o.objectType);

        // Hedef türleri ağırlıklı torba (yüksek hedef sayıları yetişsin).
        var bag = new List<(GameObject prefab, int stack, float scale)>();
        foreach (var t in types)
        {
            if (t.prefab.name == "Tray") continue;
            int w = objSet.Contains(t.prefab.name) ? 3 : 1;
            for (int i = 0; i < w; i++) bag.Add(t);
        }
        if (bag.Count == 0) return;
        Shuffle(bag);

        // Merkeze yakın / kısa yapılar için ALÇAK türler (uzun şişe deliğin önünü kapatmasın).
        var shortTypes = types.FindAll(t => t.prefab.name != "Tray" && ObjHeight(t.prefab) * t.scale <= 1.2f);
        if (shortTypes.Count == 0) shortTypes = types;
        // Öbek türleri: KÜÇÜK ayak izli içecekler (öbekte büyütülünce bile deliğe sığar).
        var clusterTypes = types.FindAll(t => t.prefab.name != "Tray" && MaxDim(t.prefab) * t.scale <= 0.95f);
        if (clusterTypes.Count == 0) clusterTypes = shortTypes;

        float gap = 4.4f;   // yapılar birbirine YAKIN → yüzeyi kaplar, bütünlük (çok ayrık ada olmasın)
        int half = Mathf.Max(1, Mathf.FloorToInt(playHalf / gap));
        for (int ix = -half; ix <= half; ix++)
            for (int iz = -half; iz <= half; iz++)
            {
                Vector2 anchor = new Vector2(ix * gap, iz * gap);
                if (anchor.magnitude < centerClearance + 0.5f) continue;   // delik başlangıç boşluğu
                if (InLandmarkArea(anchor)) continue;   // dünya harikası bölgesi BOŞ
                int ai = Mathf.Abs(ix), aj = Mathf.Abs(iz);

                // 4 YOĞUN ÖBEK: yalnız iç köşe hücreleri (|ix|=|iz|=1) → tam 4 simetrik öbek.
                if (ai == 1 && aj == 1)
                {
                    var ct = clusterTypes[(ai * 3 + aj * 7) % clusterTypes.Count];
                    BuildDrinkCluster(ct, anchor, root);
                    continue;
                }

                bool near = anchor.magnitude < 9f;   // merkeze yakın → KISA yapı + kısa tür
                var type = near ? shortTypes[(ai * 5 + aj * 11) % shortTypes.Count]
                                : bag[(ai * 5 + aj * 11) % bag.Count];

                int shape = (ai * 3 + aj * 7) % 4;    // 0 piramit / 1 silindir / 2 kubbe / 3 kule
                if (near) shape = ((ai + aj) % 2 == 0) ? 0 : 3;   // yakın: sadece alçak piramit / kule
                switch (shape)
                {
                    case 0: BuildPyramid(type, anchor, root); break;
                    case 1: BuildCylinder(type, anchor, root); break;
                    case 2: BuildDome(type, anchor, root); break;
                    default: BuildDrinkTower(type, anchor, root); break;
                }
            }
    }

    // YOĞUN ÖBEK (içecekler): nesneleri ~1.5× büyüterek DAHA AZ sayıda, dolu disk (2 halka ≈ 12-16 nesne)
    // yerleştirir. Büyük nesne = deliğin boyut-geçidi devreye girer (bir anda hepsi yutulmaz) → oyun kolay olmaz.
    void BuildDrinkCluster((GameObject prefab, int stack, float scale) type, Vector2 center, Transform root)
    {
        float sc = type.scale * 1.5f;                       // öbek nesneleri daha büyük
        float u = Mathf.Max(0.3f, Footprint(type.prefab) * sc);
        float step = u * 1.2f;                              // geniş adım → daha az nesne
        float radius = gapClusterRadius;                    // öbek çapı (komşulara taşmasın)
        SpawnStack(type.prefab, 1, sc, center, Random.value * 360f, root);
        int rings = Mathf.Clamp(Mathf.FloorToInt(radius / step), 1, 2);
        for (int r = 1; r <= rings; r++)
        {
            float rr = r * step;
            int n = Mathf.Max(6, Mathf.RoundToInt(2f * Mathf.PI * rr / step));
            float phase = Random.value * Mathf.PI * 2f;
            for (int i = 0; i < n; i++)
            {
                float a = phase + i * (2f * Mathf.PI / n);
                SpawnStack(type.prefab, 1, sc, center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rr, Random.value * 360f, root);
            }
        }
    }
    const float gapClusterRadius = 2.6f;

    // Yapılar KISA kalsın (kamerayı/deliğin önünü kapatmasın): toplam yükseklik bu sınırı aşmaz.
    const float DRINK_STRUCT_MAXH = 3.4f;

    // PİRAMİT: kare katmanlar yukarı çıktıkça daralır (3×3 → 2×2 → 1). Yükseklik sınırına göre kat sayısı.
    void BuildPyramid((GameObject prefab, int stack, float scale) type, Vector2 anchor, Transform root)
    {
        float u = Footprint(type.prefab) * type.scale;
        float h = Mathf.Max(0.2f, ObjHeight(type.prefab) * type.scale * 0.95f);
        int layers = Mathf.Clamp(Mathf.FloorToInt(DRINK_STRUCT_MAXH / h), 2, 3);
        for (int layer = 0; layer < layers; layer++)
            ElevatedGrid(type, anchor, layers - layer, u, layer * h, root);
    }

    // SİLİNDİR: aynı yarıçapta dikey istiflenen halkalar (içi boş tambur). Yükseklik sınırlı.
    void BuildCylinder((GameObject prefab, int stack, float scale) type, Vector2 anchor, Transform root)
    {
        float u = Footprint(type.prefab) * type.scale;
        float h = Mathf.Max(0.2f, ObjHeight(type.prefab) * type.scale * 0.95f);
        float rad = u * 1.7f;   // geniş taban → yüzeyi daha çok kaplar
        int n = Mathf.Clamp(Mathf.RoundToInt(2f * Mathf.PI * rad / u), 6, 14);
        int levels = Mathf.Clamp(Mathf.FloorToInt(DRINK_STRUCT_MAXH / h), 2, 3);
        for (int lvl = 0; lvl < levels; lvl++)
            ElevatedRing(type, anchor, n, rad, lvl * h, root);
    }

    // KUBBE / KÜRE: taban geniş, yükseldikçe daralan halkalar (hemisfer profili) + tepede tek. Yükseklik sınırlı.
    void BuildDome((GameObject prefab, int stack, float scale) type, Vector2 anchor, Transform root)
    {
        float u = Footprint(type.prefab) * type.scale;
        float h = Mathf.Max(0.2f, ObjHeight(type.prefab) * type.scale * 0.95f);
        float baseR = u * 2.4f;   // geniş taban → yüzeyi daha çok kaplar
        int layers = Mathf.Clamp(Mathf.FloorToInt(DRINK_STRUCT_MAXH / h), 2, 3);
        for (int i = 0; i < layers; i++)
        {
            float t = i / (float)(layers - 1);                 // 0 taban → 1 tepe
            float rad = baseR * Mathf.Cos(t * Mathf.PI * 0.5f);
            float y = i * h;
            if (rad < u * 0.5f) { SpawnElevated(type.prefab, anchor, y, type.scale, Random.value * 360f, root); break; }
            int n = Mathf.Max(4, Mathf.RoundToInt(2f * Mathf.PI * rad / u));
            ElevatedRing(type, anchor, n, rad, y, root);
        }
    }

    // KULE: 2×2 kolon (dolu kule kümesi). Kolon yüksekliği yükseklik sınırına göre.
    void BuildDrinkTower((GameObject prefab, int stack, float scale) type, Vector2 anchor, Transform root)
    {
        float s = 0.55f * Footprint(type.prefab) * type.scale;
        float h = Mathf.Max(0.2f, ObjHeight(type.prefab) * type.scale);
        int st = Mathf.Clamp(Mathf.FloorToInt(DRINK_STRUCT_MAXH / h), 2, 4);
        Vector2[] o = { new(-s, -s), new(s, -s), new(-s, s), new(s, s) };
        foreach (var off in o) SpawnStack(type.prefab, st, type.scale, anchor + off, Random.value * 360f, root);
    }

    // ATOMIUM: BCC küp (8 köşe + 1 merkez) brokoli toplar; küp body-diagonali DİKEY (elmas duruş);
    // bağlantılar kebap çubuk (merkez→8 köşe + 12 küp kenarı). Kebap çubuk = 3-4 kebap uç uca.
    void BuildAtomium((GameObject prefab, int stack, float scale) broc, (GameObject prefab, int stack, float scale) keb,
                      Vector2 centerXZ, Transform root)
    {
        float a = 2.6f;            // küp yarı-kenarı
        float ball = 2.4f;         // brokoli top ölçeği
        float rod = 1.0f;          // kebap çubuk ölçeği
        Quaternion R = Quaternion.FromToRotation(new Vector3(1, 1, 1).normalized, Vector3.up);  // köşe yukarı

        var signs = new Vector3[8];
        var c = new Vector3[8];
        int idx = 0;
        for (int sx = -1; sx <= 1; sx += 2)
            for (int sy = -1; sy <= 1; sy += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                {
                    signs[idx] = new Vector3(sx, sy, sz);
                    c[idx] = R * (signs[idx] * a);
                    idx++;
                }

        float minY = 0f;
        for (int i = 0; i < 8; i++) minY = Mathf.Min(minY, c[i].y);
        float ballR = MaxDim(broc.prefab) * ball * 0.5f;
        Vector3 origin = new Vector3(centerXZ.x, -minY + ballR + 0.1f, centerXZ.y);

        // Toplar: 8 köşe + merkez
        for (int i = 0; i < 8; i++) SpawnExactRot(broc.prefab, origin + c[i], new Vector3(0f, Random.value * 360f, 0f), ball, root);
        SpawnExactRot(broc.prefab, origin, new Vector3(0f, Random.value * 360f, 0f), ball, root);

        // Çubuklar: merkez → 8 köşe
        for (int i = 0; i < 8; i++) BuildRod(keb.prefab, origin, origin + c[i], rod, root);
        // Çubuklar: 12 küp kenarı (sign farkı tek eksende = manhattan 2)
        for (int i = 0; i < 8; i++)
            for (int j = i + 1; j < 8; j++)
            {
                float md = Mathf.Abs(signs[i].x - signs[j].x) + Mathf.Abs(signs[i].y - signs[j].y) + Mathf.Abs(signs[i].z - signs[j].z);
                if (md == 2f) BuildRod(keb.prefab, origin + c[i], origin + c[j], rod, root);
            }
    }

    // İki nokta arası kebap ÇUBUK: yön boyunca uç uca kebaplar (uzun eksen yöne döndürülür).
    void BuildRod(GameObject prefab, Vector3 A, Vector3 B, float scale, Transform root)
    {
        Vector3 d = B - A; float L = d.magnitude;
        if (L < 0.05f) return; d /= L;
        Quaternion rot = Quaternion.FromToRotation(LongAxis(prefab), d);
        float kl = Mathf.Max(0.25f, MaxDim(prefab) * scale);
        int n = Mathf.Max(2, Mathf.RoundToInt(L / kl));
        for (int i = 0; i < n; i++)
        {
            Vector3 pos = A + d * ((i + 0.5f) / n * L);
            SpawnExactRot(prefab, pos, rot.eulerAngles, scale, root);
        }
    }

    static Vector3 LongAxis(GameObject prefab)
    {
        var bc = prefab.GetComponent<BoxCollider>();
        if (bc == null) return Vector3.right;
        var s = bc.size;
        if (s.x >= s.y && s.x >= s.z) return Vector3.right;
        if (s.z >= s.x && s.z >= s.y) return Vector3.forward;
        return Vector3.up;
    }

    // ── MACARON TAJ MAHAL (foods L3) ────────────────────────────────────────────
    // Yatık macaron disklerinden: platform + küp gövde + soğan kubbe + 4 küçük kubbe + 4 minare.
    void MacRing(GameObject pf, Vector2 c, float radius, float y, float msc, Transform root)
    {
        if (radius < 0.05f) { SpawnExactRot(pf, new Vector3(c.x, y, c.y), new Vector3(0f, Random.value * 360f, 0f), msc, root); return; }
        float fp = Footprint(pf) * msc;
        int n = Mathf.Max(6, Mathf.RoundToInt(2f * Mathf.PI * radius / fp));
        for (int i = 0; i < n; i++)
        {
            float a = i * (2f * Mathf.PI / n);
            SpawnExactRot(pf, new Vector3(c.x + Mathf.Cos(a) * radius, y, c.y + Mathf.Sin(a) * radius), new Vector3(0f, -a * Mathf.Rad2Deg, 0f), msc, root);
        }
    }
    void MacDisc(GameObject pf, Vector2 c, float radius, float y, float msc, Transform root)
    {
        float fp = Footprint(pf) * msc;
        for (float r = 0f; r <= radius + 0.01f; r += fp) MacRing(pf, c, r, y, msc, root);
    }
    void MacColumn(GameObject pf, Vector2 c, float y0, float height, float msc, Transform root)
    {
        float th = Mathf.Max(0.1f, ObjHeight(pf) * msc);
        int n = Mathf.Max(1, Mathf.RoundToInt(height / th));
        for (int i = 0; i < n; i++)
            SpawnExactRot(pf, new Vector3(c.x, y0 + (i + 0.5f) * th, c.y), new Vector3(0f, Random.value * 360f, 0f), msc, root);
    }
    // Soğan kubbe: kademeli halkalar (tabanda geniş, hafif şişkin, tepede sivri) + finial.
    void MacOnion(GameObject body, GameObject finial, Vector2 c, float R, float y0, float msc, Transform root)
    {
        int rings = 7; float domeH = R * 1.7f;
        for (int i = 0; i <= rings; i++)
        {
            float t = i / (float)rings;
            float r = R * Mathf.Pow(1f - t, 0.7f) * (1f + 0.22f * Mathf.Sin(Mathf.PI * t));
            MacRing(body, c, r, y0 + t * domeH, msc, root);
        }
        MacColumn(finial, c, y0 + domeH, R * 0.6f, msc, root);   // tepe finial
    }
    void BuildMinaret(GameObject shaft, GameObject top, GameObject finial, Vector2 c, float y0, float height, float msc, Transform root)
    {
        MacColumn(shaft, c, y0, height, msc, root);                                  // ince gövde (disk kolon)
        MacOnion(top, finial, c, Footprint(shaft) * msc * 0.95f, y0 + height, msc, root);  // küçük kubbe + finial
    }

    void BuildTajMahal(GameObject cream, GameObject pink, GameObject blue, GameObject purple, Vector2 ctr, Transform root)
    {
        float msc = 1.6f;   // daha KÜÇÜK macaronlar (daha ince/detaylı)
        float th = Mathf.Max(0.1f, ObjHeight(cream) * msc);

        // Platform (2 kare kafes kat)
        SquareFrameFlat(cream, ctr, 3.6f, 0f,  msc, root);
        SquareFrameFlat(cream, ctr, 3.6f, th,  msc, root);
        float baseY = 2f * th;

        // Küp gövde (kare kafes duvarlar)
        int bld = 5;
        for (int L = 0; L < bld; L++) SquareFrameFlat(cream, ctr, 2.1f, baseY + L * th, msc, root);
        float roofY = baseY + bld * th;
        MacDisc(cream, ctr, 2.1f, roofY, msc, root);   // çatı kapağı

        // Merkez kubbe: davul (drum) + soğan kubbe
        MacRing(cream, ctr, 1.5f, roofY + th, msc, root);
        MacRing(cream, ctr, 1.5f, roofY + 2f * th, msc, root);
        MacOnion(cream, purple, ctr, 1.5f, roofY + 3f * th, msc, root);

        // 4 köşede küçük kubbe (çatı köşeleri)
        Vector2[] cc = { new(1.5f, 1.5f), new(-1.5f, 1.5f), new(1.5f, -1.5f), new(-1.5f, -1.5f) };
        foreach (var k in cc)
        {
            MacColumn(cream, ctr + k, roofY, 1.5f * th, msc, root);
            MacOnion(pink, purple, ctr + k, 0.55f, roofY + 1.5f * th, msc, root);
        }

        // 4 minare (platform köşeleri), gövdeden uzun
        float mh = baseY + bld * th + 2.0f;
        Vector2[] mc = { new(3.1f, 3.1f), new(-3.1f, 3.1f), new(3.1f, -3.1f), new(-3.1f, -3.1f) };
        foreach (var k in mc) BuildMinaret(cream, blue, purple, ctr + k, 0f, mh, msc, root);
    }

    // Renkli MACARON ROKETİ: 2x2 gövde, katmanlar renk renk (alttan üste rainbow) + pembe burun + 4 fin.
    void BuildMacaronRocket(Vector2 center, (GameObject prefab, int stack, float scale)[] cols,
                            (GameObject prefab, int stack, float scale) nose, Transform root)
    {
        var pf0 = cols[0].prefab;
        float sc = 1.2f;
        float mh = Mathf.Max(0.15f, ObjHeight(pf0)) * sc;          // katman yüksekliği
        float s = Mathf.Max(0.22f, Footprint(pf0) * sc * 0.42f);   // 2x2 yarı-aralık
        int layers = 12;
        for (int L = 0; L < layers; L++)
        {
            var c = cols[L % cols.Length];
            float y = L * mh * 0.95f;
            SpawnElevated(c.prefab, center + new Vector2(-s, -s), y, sc, 0f, root);
            SpawnElevated(c.prefab, center + new Vector2( s, -s), y, sc, 0f, root);
            SpawnElevated(c.prefab, center + new Vector2(-s,  s), y, sc, 0f, root);
            SpawnElevated(c.prefab, center + new Vector2( s,  s), y, sc, 0f, root);
        }
        SpawnElevated(nose.prefab, center, layers * mh * 0.95f, sc * 1.7f, 0f, root);   // burun (pembe, büyük)
        float fs = s * 2.6f;   // 4 fin (mor, tabanda dışta)
        SpawnElevated(pf0, center + new Vector2( fs, 0f), 0f, sc, 0f, root);
        SpawnElevated(pf0, center + new Vector2(-fs, 0f), 0f, sc, 0f, root);
        SpawnElevated(pf0, center + new Vector2(0f,  fs), 0f, sc, 0f, root);
        SpawnElevated(pf0, center + new Vector2(0f, -fs), 0f, sc, 0f, root);

        // UPGRADE: 2 yan booster kolonu (renkli, kısa) + gövde ortasında 2 kanat
        float bx = s * 2.4f;
        for (int b = 0; b < 6; b++)
        {
            var c = cols[b % cols.Length];
            SpawnElevated(c.prefab, center + new Vector2( bx, 0f), b * mh * 0.95f, sc, 0f, root);
            SpawnElevated(c.prefab, center + new Vector2(-bx, 0f), b * mh * 0.95f, sc, 0f, root);
        }
        float wy = layers * 0.45f * mh * 0.95f;
        SpawnElevated(cols[2].prefab, center + new Vector2(0f,  bx), wy, sc, 0f, root);
        SpawnElevated(cols[2].prefab, center + new Vector2(0f, -bx), wy, sc, 0f, root);
    }

    (GameObject prefab, int stack, float scale) FindType(List<(GameObject prefab, int stack, float scale)> list, string name)
    {
        foreach (var t in list) if (t.prefab != null && t.prefab.name == name) return t;
        return (null, 0, 0f);
    }

    // TEPSİDE SERVİS: zemine tepsi + üstünde simetrik 6 küçük nesne. Tepsi uyanınca üsttekiler birlikte uyanır
    // (önce tepsi hareketlenir → üstündekiler donuk kalıp tek-tek zıplamaz).
    void BuildTrayPlatter((GameObject prefab, int stack, float scale) tray, (GameObject prefab, int stack, float scale) item,
                          Vector2 anchor, Transform root)
    {
        float tsc = 1.7f;
        float trayYaw = (LongAxis(tray.prefab) == Vector3.forward) ? 90f : 0f;   // uzun ekseni X'e hizala
        var trayGo = Instantiate(tray.prefab, new Vector3(anchor.x, 0f, anchor.y), Quaternion.Euler(0f, trayYaw, 0f), root);
        trayGo.name = tray.prefab.name;
        if (Mathf.Abs(tsc - 1f) > 0.001f) trayGo.transform.localScale *= tsc;
        ScaleEffect(trayGo, tsc);
        RestOn(trayGo, 0f);
        var grp = trayGo.AddComponent<TrayGroup>();
        var traySw = trayGo.GetComponent<PhysicsSwallowable>();
        if (traySw != null) traySw.group = grp;

        float topY = ObjHeight(tray.prefab) * tsc * 0.35f;   // tepsi tabanına yakın → nesneler tepsiye değer
        float tw = Footprint(tray.prefab) * tsc;
        float sx = tw * 0.24f, sz = tw * 0.14f;   // 3 sütun (uzun) × 2 sıra (kısa)
        for (int cx = -1; cx <= 1; cx++)
            for (int rz = -1; rz <= 1; rz += 2)
            {
                var it = Instantiate(item.prefab, new Vector3(anchor.x + cx * sx, topY, anchor.y + rz * sz),
                                     Quaternion.Euler(0f, Random.value * 360f, 0f), root);
                it.name = item.prefab.name;
                if (item.scale > 0.01f && Mathf.Abs(item.scale - 1f) > 0.001f) it.transform.localScale *= item.scale;
                ScaleEffect(it, item.scale);
                RestOn(it, topY);
                var sw = it.GetComponent<PhysicsSwallowable>();
                if (sw != null) { sw.externalWake = true; grp.items.Add(sw); }   // tepsiyle birlikte uyanır
            }
    }

    // CHICHEN ITZA piramidi: hamburgerlerden kademeli teraslar (daralan kare kafesler) + ön yüzde yatay
    // kebaplardan SIK merdiven + tepede tepsilerden tapınak.
    void BuildChichenItza((GameObject prefab, int stack, float scale) burger, (GameObject prefab, int stack, float scale) kebab,
                          (GameObject prefab, int stack, float scale) tray, GameObject topMacaron, Vector2 ctr, Transform root)
    {
        float hsc = 1.6f;
        float bh = Mathf.Max(0.2f, ObjHeight(burger.prefab) * hsc);
        float baseHalf = 3.4f, topHalf = 0.9f;
        int T = 7;

        // 7 KAT: her kat TEK sıra hamburger (dikey temas), her kat bir öncekinden belirgin daralır.
        float y = 0f;
        for (int i = 0; i < T; i++)
        {
            float half = Mathf.Lerp(baseHalf, topHalf, i / (float)(T - 1));
            SquareFrameFlat(burger.prefab, ctr, half, y, hsc, root);
            y += bh;
        }
        float topY = y;

        // MERDİVEN: piramidin 4 YÜZÜNDE (her basamak ORTADA tek kebap), basamaklar dışa doğru inen eğimle,
        // kenarlarda tepsilerden açılı korkuluk kolonları.
        Vector2[] faces = { new(0f, -1f), new(0f, 1f), new(1f, 0f), new(-1f, 0f) };
        foreach (var d in faces) BuildStairOnFace(burger, kebab, tray, ctr, baseHalf, topHalf, bh, T, hsc, d, root);

        // Tepe tapınağı: ÜST kattan daha DAR + detaylı (küçük tepsiler, kapı/pencere)
        BuildTemple(tray.prefab, ctr, topY, topHalf, root);

        // TEPEDE DEV YEŞİL MACARON (tapınağı taçlandırır)
        if (topMacaron != null)
        {
            float th = ObjHeight(tray.prefab) * 0.3f;
            float templeTop = topY + 6f * th;                       // 5 sıra + çatı + korniş
            float msc = 2.4f / Mathf.Max(0.1f, MaxDim(topMacaron));  // dev: çap ~2.4
            SpawnElevatedRot(topMacaron, ctr, templeTop, msc, Vector3.zero, root);
        }
    }

    // Bir yüzde merdiven: kebap basamaklar GERÇEK hamburger kademe yüzeyine dayalı (kat boyunca aynı derinlik,
    // kat sınırında dışa adımlar) → hamburgerle TEMAS, kopukluk/gömülme yok. Kenarlarda yüzeye DİK duran
    // (normal = dış yön) tepsi blok kolonları, aynı kademe yüzeyine dayalı.
    void BuildStairOnFace((GameObject prefab, int stack, float scale) burger, (GameObject prefab, int stack, float scale) kebab,
                          (GameObject prefab, int stack, float scale) tray, Vector2 ctr, float baseHalf, float topHalf,
                          float bh, int T, float hsc, Vector2 dir, Transform root)
    {
        Vector2 perp = new Vector2(-dir.y, dir.x);
        Vector3 dir3 = new Vector3(dir.x, 0f, dir.y), perp3 = new Vector3(perp.x, 0f, perp.y);

        float burgerR = MaxDim(burger.prefab) * hsc * 0.5f;            // hamburgerin dış yarıçapı (merkez halkasından dışa)
        float kebabH = Mathf.Max(0.12f, ObjHeight(kebab.prefab) * kebab.scale);   // basamak adım yüksekliği (sık → smooth)
        float kebabDepth = ObjHeight(kebab.prefab) * kebab.scale;      // dir yönü yarı-derinlik (~kalınlık)
        float topY = T * bh;

        Quaternion treadRot = Quaternion.FromToRotation(LongAxis(kebab.prefab), perp3);   // tread uzun ekseni yüze paralel
        Quaternion balRot = Quaternion.LookRotation(perp3, dir3);                          // tepsi paneli yüzeye DİK (normal=dış yön), dikey

        float ks = kebab.scale; Vector3 la = LongAxis(kebab.prefab);
        Vector3 treadScale = (la == Vector3.right) ? new Vector3(ks, 2f * ks, 2f * ks)
                           : (la == Vector3.up) ? new Vector3(2f * ks, ks, 2f * ks)
                           : new Vector3(2f * ks, 2f * ks, ks);
        float edge = MaxDim(kebab.prefab) * kebab.scale * 0.5f + 0.1f;   // tread ucu (korkuluk yan ofseti)

        int steps = Mathf.Max(8, Mathf.CeilToInt(topY / kebabH));
        for (int s = 0; s < steps; s++)
        {
            float sy = (s + 0.5f) * kebabH;
            float t = Mathf.Clamp01(sy / topY);                          // SMOOTH (sürekli) iniş
            // smooth eğri hamburger DIŞ yüzeyini takip eder: katların yarıçapı lerp + burgerR; kebap kalınlığı kadar dayalı
            float faceR = Mathf.Lerp(baseHalf, topHalf, t) + burgerR;
            float radial = faceR + kebabDepth - 0.12f;                   // kebap dış yüzeye değer, smooth iner
            Vector2 p = ctr + dir * radial;

            // tek tread — yatay (uzun) boyut SABİT, dikey+derinlik 2×
            var tg = Instantiate(kebab.prefab, new Vector3(p.x, sy, p.y), treadRot, root);
            tg.name = kebab.prefab.name;
            tg.transform.localScale = treadScale;

            if (s % 2 == 0)   // korkuluk blokları: kenarlarda, aynı kademe derinliği → hamburgere temas, kat sınırında dışa
            {
                Vector2 pl = p + perp * edge, pr = p - perp * edge;
                SpawnExactRot(tray.prefab, new Vector3(pl.x, sy, pl.y), balRot.eulerAngles, tray.scale * 0.6f, root);
                SpawnExactRot(tray.prefab, new Vector3(pr.x, sy, pr.y), balRot.eulerAngles, tray.scale * 0.6f, root);
            }
        }
    }

    // Tapınak: KÜÇÜK tepsilerden, üst kattan DAR; ön yüzde KAPI, yan yüzlerde PENCERE + düz çatı + korniş.
    void BuildTemple(GameObject tray, Vector2 ctr, float baseY, float maxHalf, Transform root)
    {
        float tsc = 0.3f;                                   // küçük tepsiler (detay için)
        float tw = Mathf.Max(0.2f, Footprint(tray) * tsc);
        float th = Mathf.Max(0.1f, ObjHeight(tray) * tsc);
        float templeHalf = maxHalf * 0.78f;                 // üst kattan dar
        int nx = Mathf.Clamp(Mathf.RoundToInt(2f * templeHalf / tw) + 1, 4, 7);
        int nz = Mathf.Max(3, nx - 1);
        int courses = 5;
        float hw = (nx - 1) * 0.5f * tw, hz = (nz - 1) * 0.5f * tw;

        for (int c = 0; c < courses; c++)
        {
            float yy = baseY + c * th + th * 0.5f;
            for (int ix = 0; ix < nx; ix++)
                for (int iz = 0; iz < nz; iz++)
                {
                    bool perimeter = (ix == 0 || ix == nx - 1 || iz == 0 || iz == nz - 1);
                    if (!perimeter) continue;                       // içi boş (oda)
                    bool frontWall = (iz == 0);                     // -z ön yüz
                    bool sideWall = (ix == 0 || ix == nx - 1);
                    if (frontWall && (ix == nx / 2) && c < 2) continue;            // KAPI (ön orta, alt 2 sıra)
                    if (sideWall && c == 2 && (iz == 1 || iz == nz - 2)) continue;  // PENCERE (yan, orta sıra)
                    float wy = sideWall ? 90f : 0f;
                    SpawnExactRot(tray, new Vector3(ctr.x + ix * tw - hw, yy, ctr.y + iz * tw - hz), new Vector3(0f, wy, 0f), tsc, root);
                }
        }

        // Çatı (düz, dolu) + üstte hafif geniş korniş sırası
        float roofY = baseY + courses * th;
        for (int ix = 0; ix < nx; ix++)
            for (int iz = 0; iz < nz; iz++)
                SpawnExactRot(tray, new Vector3(ctr.x + ix * tw - hw, roofY, ctr.y + iz * tw - hz), Vector3.zero, tsc, root);
        for (int ix = -1; ix <= nx; ix++)   // korniş: çatı kenarından biraz taşan tek sıra
        {
            SpawnExactRot(tray, new Vector3(ctr.x + ix * tw - hw, roofY + th, ctr.y - hz - tw * 0.5f), Vector3.zero, tsc, root);
            SpawnExactRot(tray, new Vector3(ctr.x + ix * tw - hw, roofY + th, ctr.y + hz + tw * 0.5f), Vector3.zero, tsc, root);
        }
    }

    // Masa: 4 köşede "#" şeklinde dizilmiş şiş kebap KOLONU (her kat = çaprazlanmış 4 kebap) + üstte DEV tabak.
    void BuildTable((GameObject prefab, int stack, float scale) leg, (GameObject prefab, int stack, float scale) top,
                    Vector2 center, Transform root)
    {
        float u = Footprint(leg.prefab) * leg.scale;
        float s = 1.0f * u;
        int layers = 4;
        float legTop = HashColumn(leg.prefab, leg.scale, center + new Vector2(-s, -s), layers, root);
        HashColumn(leg.prefab, leg.scale, center + new Vector2( s, -s), layers, root);
        HashColumn(leg.prefab, leg.scale, center + new Vector2(-s,  s), layers, root);
        HashColumn(leg.prefab, leg.scale, center + new Vector2( s,  s), layers, root);
        SpawnElevated(top.prefab, center, legTop, top.scale * 2.8f, 0f, root);   // dev tabak masanın üstünde
    }

    // "#" kolon: her kat 4 kebap (2'si bir yöne, 2'si dik) çaprazlanır; katlar üst üste → kolon. Tepe Y döner.
    float HashColumn(GameObject prefab, float scale, Vector2 center, int layers, Transform root)
    {
        float len = Footprint(prefab) * scale;
        float d = len * 0.22f;                                   // paralel çubukların merkeze uzaklığı
        float th = Mathf.Max(0.12f, ObjHeight(prefab) * scale);  // bir kat yüksekliği
        float y = 0f;
        for (int L = 0; L < layers; L++)
        {
            SpawnElevated(prefab, center + new Vector2(0f,  d), y,            scale, 0f,  root);
            SpawnElevated(prefab, center + new Vector2(0f, -d), y,            scale, 0f,  root);
            SpawnElevated(prefab, center + new Vector2( d, 0f), y + th * 0.5f, scale, 90f, root);
            SpawnElevated(prefab, center + new Vector2(-d, 0f), y + th * 0.5f, scale, 90f, root);
            y += th * 1.6f;
        }
        return y;
    }

    // Taco KÖPRÜSÜ: taco'lar (kavisli kabuk) bir kemer (parabol) boyunca dizilir → köprü/tünel.
    void BuildTacoBridge((GameObject prefab, int stack, float scale) taco, Vector2 center, float dirRad, Transform root)
    {
        int n = 7;
        float u = Footprint(taco.prefab) * taco.scale;
        float step = u * 0.8f, arch = u * 2.2f;
        float c = Mathf.Cos(dirRad), s = Mathf.Sin(dirRad);
        for (int i = 0; i < n; i++)
        {
            float t = i - (n - 1) * 0.5f;
            float frac = Mathf.Abs(t) / ((n - 1) * 0.5f);
            float y = arch * (1f - frac * frac);                 // parabolik kemer
            Vector2 p = center + new Vector2(c * t * step, s * t * step);
            SpawnElevated(taco.prefab, p, y, taco.scale, dirRad * Mathf.Rad2Deg, root);
        }
    }

    // KEBAP EYFEL KULESİ — gerçek silüet: dışa açılan AYAKLAR + taban KEMERLERİ + 2 PLATFORM + uzun ince üst
    // + tepe anteni. Yatay kafes katları DİKEY kebaplarla bağlanır. Kemer/balkon/anten topuzunda macaron.
    void BuildEiffel((GameObject prefab, int stack, float scale) kebab, GameObject[] macs, Vector2 center, Transform root)
    {
        var pf = kebab.prefab; float bs = kebab.scale;
        float baseHalf = 2.7f, half1 = 1.05f, half2 = 0.55f, topHalf = 0.12f, totalH = 15f;
        float p1 = 0.26f, p2 = 0.56f;
        float dy = 0.5f;
        int frames = Mathf.CeilToInt(totalH / dy);

        // Yatay kafes katları (rungs)
        for (int f = 0; f <= frames; f++)
        {
            float y = f * dy; float t = Mathf.Clamp01(y / totalH);
            float half = EiffelHalf(t, baseHalf, half1, half2, topHalf, p1, p2);
            float sc = bs * Mathf.Lerp(1.0f, 0.5f, t);
            if (t < p1) CornerLegs(pf, center, half, y, sc, root);
            else SquareFrameFlat(pf, center, half, y, sc, root);
        }

        // DİKEY köşe direkleri (kebap, ayakta) — katlar arası bağlantı; içe doğru daralarak çıkar.
        Vector3 up = UprightEuler(pf);
        float vLen = MaxDim(pf) / 0.8f * bs;
        for (float vy = 0f; vy < totalH; vy += vLen * 0.9f)
        {
            float t = Mathf.Clamp01(vy / totalH);
            float half = EiffelHalf(t, baseHalf, half1, half2, topHalf, p1, p2);
            int[] sg = { -1, 1 };
            foreach (int sx in sg) foreach (int sz in sg)
                SpawnElevatedRot(pf, center + new Vector2(sx * half, sz * half), vy, bs, up, root);
        }

        // 2 PLATFORM decki + üstünde renkli macaron balkon korkuluğu
        for (int d = 0; d < 2; d++)
        {
            float pt = d == 0 ? p1 : p2;
            float ph = EiffelHalf(pt, baseHalf, half1, half2, topHalf, p1, p2) * 1.35f;
            float py = pt * totalH;
            SquareFrameFlat(pf, center, ph, py,           bs * 0.85f, root);
            SquareFrameFlat(pf, center, ph, py + dy * 0.6f, bs * 0.85f, root);
            if (macs != null) MacaronRing(macs, center, ph, py + dy * 1.1f, bs * 0.9f, root);
        }

        // TABAN KEMERLERİ: 4 kenarda renkli MACARON kemeri (varsa), yoksa kebap
        float archPeak = p1 * totalH * 0.82f;
        float archInner = half1 * 0.9f;   // tepede merkeze çekilecek iç yarıçap
        ColorArch(pf, macs, center, true,  baseHalf, archInner, archPeak, bs, root);
        ColorArch(pf, macs, center, true, -baseHalf, archInner, archPeak, bs, root);
        ColorArch(pf, macs, center, false, baseHalf, archInner, archPeak, bs, root);
        ColorArch(pf, macs, center, false,-baseHalf, archInner, archPeak, bs, root);

        // TEPE ANTENİ: dikey kebaplar + en tepede macaron topuzu
        float aY = totalH;
        for (int j = 0; j < 6; j++) { SpawnElevatedRot(pf, center, aY, bs * 0.5f, up, root); aY += vLen * 0.5f * 0.9f; }
        if (macs != null)
        {
            SpawnElevated(macs[4 % macs.Length], center, aY, bs * 1.4f, 0f, root);          // topuz (büyük macaron)
            SpawnElevated(macs[0], center, aY + 0.4f * bs, bs * 0.9f, 0f, root);
        }
        // PARATONER: topuzun üstüne tek dikey şiş kebap
        SpawnElevatedRot(pf, center, aY + (macs != null ? 0.8f : 0.15f) * bs, bs * 0.6f, up, root);
    }

    void MacaronRing(GameObject[] macs, Vector2 center, float half, float y, float sc, Transform root)
    {
        int n = 12;
        for (int i = 0; i < n; i++)
        {
            float a = i * (2f * Mathf.PI / n);
            Vector2 p = center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (half * 1.05f);
            SpawnElevated(macs[i % macs.Length], p, y, sc, 0f, root);
        }
    }

    // Kemer: parabolik eğri + YÜKSEKSELDİKÇE İÇE (merkeze) kıvrılır. Uçlar (yere basan) bacaklarda kalır,
    // tepe merkeze doğru gelir (gerçek Eyfel kemeri). Her parça eğriye teğet döndürülür.
    void ColorArch(GameObject kebab, GameObject[] macs, Vector2 center, bool alongX, float fixedCoord, float innerHalf, float peakY, float sc, Transform root)
    {
        int n = 11; float baseAbs = Mathf.Abs(fixedCoord), span = baseAbs * 0.98f, sign = Mathf.Sign(fixedCoord);
        for (int i = 0; i < n; i++)
        {
            float u = (i / (n - 1f)) * 2f - 1f;
            float along = u * span;
            float frac = 1f - u * u;                                  // 0 uçlar(bacak) → 1 tepe
            float y = peakY * frac;
            float perp = sign * Mathf.Lerp(baseAbs, innerHalf, frac); // tepede merkeze doğru çekilir
            float ang = Mathf.Atan2(-2f * peakY * u, span) * Mathf.Rad2Deg;
            Vector3 pos = alongX
                ? new Vector3(center.x + along, y, center.y + perp)
                : new Vector3(center.x + perp, y, center.y + along);
            Vector3 euler = alongX ? new Vector3(0f, 0f, ang) : new Vector3(ang, 0f, 0f);
            GameObject pref = macs != null ? macs[i % macs.Length] : kebab;
            SpawnExactRot(pref, pos, euler, sc, root);
        }
    }

    static float EiffelHalf(float t, float baseHalf, float half1, float half2, float topHalf, float p1, float p2)
    {
        if (t < p1) return Mathf.Lerp(half1, baseHalf, Mathf.Pow(1f - t / p1, 1.7f));   // içbükey açılan ayaklar
        if (t < p2) return Mathf.Lerp(half1, half2, (t - p1) / (p2 - p1));               // orta gövde
        return Mathf.Lerp(half2, topHalf, (t - p2) / (1f - p2));                         // ince uzun üst
    }

    // Taban ayağı: her köşede KALIN "#" kafes kümesi (4 kebap) → 4 sağlam ayak, kenarlar AÇIK (kemer görünümü).
    void CornerLegs(GameObject pf, Vector2 center, float half, float y, float sc, Transform root)
    {
        float d = Footprint(pf) * sc * 0.30f;
        int[] sg = { -1, 1 };
        foreach (int sx in sg) foreach (int sz in sg)
        {
            Vector2 c = center + new Vector2(sx * half, sz * half);
            SpawnElevated(pf, c + new Vector2(0f,  d), y,            sc, 0f,  root);
            SpawnElevated(pf, c + new Vector2(0f, -d), y,            sc, 0f,  root);
            SpawnElevated(pf, c + new Vector2( d, 0f), y + sc * 0.05f, sc, 90f, root);
            SpawnElevated(pf, c + new Vector2(-d, 0f), y + sc * 0.05f, sc, 90f, root);
        }
    }

    // Bir kenar boyunca yukarı kavisli kebap kemeri. alongX=true → kenar X boyunca (z sabit), değilse Z boyunca (x sabit).
    void KebabArch(GameObject pf, Vector2 center, bool alongX, float fixedCoord, float peakY, float sc, Transform root)
    {
        int n = 7; float span = Mathf.Abs(fixedCoord) * 0.95f;
        for (int i = 0; i < n; i++)
        {
            float u = (i / (n - 1f)) * 2f - 1f;            // -1..1
            float along = u * span;
            float y = peakY * (1f - u * u);                // parabolik kemer
            Vector2 p = alongX ? new Vector2(along, fixedCoord) : new Vector2(fixedCoord, along);
            float yaw = alongX ? 0f : 90f;
            SpawnElevated(pf, center + p, y, sc, yaw, root);
        }
    }

    // Tek bir kare kafes katmanı: 4 kenara TEMAS HALİNDE (sık) yatay kebaplar dizilir.
    void SquareFrameFlat(GameObject prefab, Vector2 center, float half, float y, float sc, Transform root)
    {
        float Lk = Footprint(prefab) * sc;
        int per = Mathf.Max(2, Mathf.CeilToInt(2f * half / (Lk * 0.7f)));
        for (int i = 0; i < per; i++)
        {
            float p = Mathf.Lerp(-half, half, i / (per - 1f));
            SpawnElevated(prefab, center + new Vector2(p,  half), y, sc, 0f,  root);   // kuzey kenar (X yönlü)
            SpawnElevated(prefab, center + new Vector2(p, -half), y, sc, 0f,  root);   // güney kenar
            SpawnElevated(prefab, center + new Vector2( half, p), y, sc, 90f, root);   // doğu kenar (Z yönlü)
            SpawnElevated(prefab, center + new Vector2(-half, p), y, sc, 90f, root);   // batı kenar
        }
    }

    // "Küçük" eşiği (dünya birimi, max boyut): bunun altındaki nesneler dense-test'te sıkı yığına girer.
    const float DENSE_SMALL_MAXDIM = 0.95f;

    // Küçük-nesne yoğun-küme ŞEKİLLERİ (hücreye göre çeşitlenir).
    enum DenseShape { Disc, Spiral, Pentagon, Star, Towers }

    // Birim şablonlar (yukarı bakan): beşgen 5 köşe, yıldız 10 köşe (dış 1.0 / iç 0.42).
    static readonly Vector2[] PentaUnit = PolyUnit(5, 1f, 1f);
    static readonly Vector2[] StarUnit  = PolyUnit(10, 1f, 0.42f);
    static Vector2[] PolyUnit(int n, float rOut, float rIn)
    {
        var a = new Vector2[n];
        for (int i = 0; i < n; i++)
        {
            float rad = (i % 2 == 0) ? rOut : rIn;
            float ang = Mathf.PI * 0.5f + i * (2f * Mathf.PI / n);
            a[i] = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * rad;
        }
        return a;
    }

    // YOĞUN KÜME: küçük nesneleri ŞEKLE göre paketle → delik dalınca hızlı büyür. Şekil hücreye göre çeşitlenir:
    // daire / helezon(spiral) / beşgen / yıldız / kule-alanı. `filled` = dolu/boş varyant (~yarı yarıya, hücre hash'i).
    // ARALIK (kullanıcı 2026-07-22): temas DEĞİL — "yan yana çok yakın". ×1.15 temas gibiydi (iç içe geçmiş görünüm)
    // → ×1.38: nesneler arasında ince, görünür boşluk; küme yine sıkı okunur.
    void BuildDenseCluster((GameObject prefab, int stack, float scale) type, Vector2 center, float radius, Transform root, DenseShape shape, bool filled)
    {
        float u = Mathf.Max(0.25f, Footprint(type.prefab) * type.scale);
        float step = u * 1.38f;
        switch (shape)
        {
            case DenseShape.Spiral:   DenseSpiral(type, center, radius, step, root); break;
            case DenseShape.Pentagon: DensePolygon(type, center, radius, step, PentaUnit, root, filled); break;
            case DenseShape.Star:     DensePolygon(type, center, radius, step, StarUnit, root, filled); break;
            case DenseShape.Towers:   DenseTowers(type, center, radius, u, root); break;
            default:                  DenseCircles(type, center, radius, step, root, filled); break;
        }
    }

    // ÇEMBER — filled=true: eşmerkezli dolu disk; filled=false: TEK kontur halkası + merkez (tekerlek görünümü).
    // Kullanıcı (2026-07-22): şekillerin ~yarısı dolu ~yarısı boş olsun.
    void DenseCircles((GameObject prefab, int stack, float scale) type, Vector2 center, float radius, float step, Transform root, bool filled)
    {
        SpawnStack(type.prefab, 1, type.scale, center, Random.value * 360f, root);
        int rings = filled ? Mathf.Max(1, Mathf.FloorToInt(radius / step)) : 1;
        for (int r = 1; r <= rings; r++)
        {
            float rr = filled ? r * step : radius;   // boş: tek halka tam yarıçapta (kontur)
            int n = Mathf.Max(filled ? 6 : 8, Mathf.RoundToInt(2f * Mathf.PI * rr / step));
            float phase = Random.value * Mathf.PI * 2f;
            for (int i = 0; i < n; i++)
            {
                float a = phase + i * (2f * Mathf.PI / n);
                SpawnStack(type.prefab, 1, type.scale, center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rr, Random.value * 360f, root);
            }
        }
    }

    // HELEZON — phyllotaxis (altın açı) rozet dizilim. Komşu aralığı sıkı kalır (c≈step); üst sınır 60→24
    // (kullanıcı isteği 2026-07-22: öbek nesne sayısı azalsın, kolay puan olmasın).
    void DenseSpiral((GameObject prefab, int stack, float scale) type, Vector2 center, float radius, float step, Transform root)
    {
        float c = step * 0.90f;
        int n = Mathf.Clamp(Mathf.RoundToInt((radius / c) * (radius / c)), 6, 24);
        for (int i = 0; i < n; i++)
        {
            float rr = c * Mathf.Sqrt(i + 0.5f);
            if (rr > radius) break;
            float a = i * 2.399963f;   // altın açı ~137.5°
            SpawnStack(type.prefab, 1, type.scale, center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rr, Random.value * 360f, root);
        }
    }

    // BEŞGEN / YILDIZ — filled=true: eşmerkezli kontur halkaları (dolu şekil); filled=false: yalnız EN DIŞ kontur
    // + merkez. Kullanıcı (2026-07-22): ~yarısı dolu ~yarısı boş; kenar boyu aralık "çok yakın ama temas değil".
    void DensePolygon((GameObject prefab, int stack, float scale) type, Vector2 center, float radius, float step, Vector2[] unit, Transform root, bool filled)
    {
        SpawnStack(type.prefab, 1, type.scale, center, Random.value * 360f, root);
        int rings = filled ? Mathf.Max(1, Mathf.FloorToInt(radius / step)) : 1;
        int n = unit.Length;
        for (int r = 1; r <= rings; r++)
        {
            float sc = filled ? r * step : radius;   // boş: tek kontur tam yarıçapta
            for (int e = 0; e < n; e++)
            {
                Vector2 A = center + unit[e] * sc;
                Vector2 B = center + unit[(e + 1) % n] * sc;
                int segs = Mathf.Max(1, Mathf.RoundToInt((B - A).magnitude / step));
                for (int s = 0; s < segs; s++)   // s<segs: köşe (B) sonraki kenarın A'sı — çift spawn yok
                    SpawnStack(type.prefab, 1, type.scale, Vector2.Lerp(A, B, s / (float)segs), Random.value * 360f, root);
            }
        }
    }

    // KULE ALANI — merkez + çevrede 4 istifli kolon (eski 6; kullanıcı isteği 2026-07-22: öbek nesne sayısı ↓).
    // 4 kolon kare düzende → geometri net okunur; kolonlar arası mesafe değişmedi (sıkı görünüm korunur).
    void DenseTowers((GameObject prefab, int stack, float scale) type, Vector2 center, float radius, float u, Transform root)
    {
        const int stackH = 5;   // küçük nesne → en fazla 5 (kullanıcı kuralı)
        SpawnStack(type.prefab, stackH, type.scale, center, Random.value * 360f, root);
        float rr = Mathf.Max(u * 1.9f, radius * 0.55f);
        float phase = Random.value * Mathf.PI * 2f;
        for (int i = 0; i < 4; i++)
        {
            float a = phase + i * (Mathf.PI / 2f);
            SpawnStack(type.prefab, stackH, type.scale, center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rr, Random.value * 360f, root);
        }
    }

    void BuildFormation(int style, (GameObject prefab, int stack, float scale) type,
                        Vector2 anchor, float facing, Transform root, int k)
    {
        float u = Footprint(type.prefab) * type.scale;   // nesnenin dünya-birimi ayak izi (doğru aralık için)
        float h = ObjHeight(type.prefab) * type.scale;   // nesne yüksekliği (kademeli yapılar için)
        // İSTİF SINIRI (kullanıcı): boyuta göre max üst üste kaç nesne binebilir. Medium/Large (hantal) → EN FAZLA 3;
        // Small/Tiny → EN FAZLA 5. (Eşik MaxDim ~2.2 = Small 1.8 ile Medium 2.6 arası.) Kule/kolon stilleri buna göre kısalır.
        int maxStack = (MaxDim(type.prefab) * type.scale > 2.2f) ? 3 : 5;

        if (style == 0)   // ortada büyük + etrafında küçükler (2 halka)
        {
            SpawnStack(type.prefab, 1, type.scale * 2.2f, anchor, 0f, root);
            RingAround(type, anchor, 6, u * 2.0f, root);
            return;
        }
        if (style == 5)   // FISKİYE: geniş alt halkadan dar üst halkaya kademeli yükselen katmanlar
        {
            ElevatedRing(type, anchor, 8, u * 1.7f, 0f, root);
            ElevatedRing(type, anchor, 5, u * 1.0f, h * 0.9f, root);
            ElevatedRing(type, anchor, 1, 0f,       h * 1.7f, root);   // tepe
            return;
        }
        if (style == 6)   // EYFEL/PİRAMİT: kare katmanlar yukarı çıktıkça daralır
        {
            ElevatedGrid(type, anchor, 2, u, 0f,       root);          // 2x2 taban
            SpawnElevated(type.prefab, anchor, h * 0.92f, type.scale, 0f, root);  // tepe tek
            return;
        }

        List<(Vector2 off, int stack)> pts = style switch
        {
            1 => FSquareTowers(u, 5),              // 2x2 → 4 kolon, her biri 5 yüksek
            2 => FCampGrid(u, 2),                  // 3x2 grid, 2 katlı (asker kampı)
            3 => (k % 2 == 0) ? FRing(6, u * 1.2f) : FRing(5, u * 1.25f),  // çember / beşgen
            _ => FColumn(6),                        // tek yüksek kolon
        };
        float c = Mathf.Cos(facing), s = Mathf.Sin(facing);
        foreach (var p in pts)
        {
            Vector2 ro = new Vector2(p.off.x * c - p.off.y * s, p.off.x * s + p.off.y * c);
            SpawnStack(type.prefab, Mathf.Min(p.stack, maxStack), type.scale, anchor + ro, -facing * Mathf.Rad2Deg, root);
        }
    }

    // ARAÇ FORMASYONU (büyük-nesne dünyaları): varsayılan ızgaranın canlı çeşidini korur ama aralıklar aracın GÖRSEL
    // boyuna göre (collider/0.8) → görsel çakışma YOK, collider'lar da ayrık → delik uyandırınca fırlamaz. Zemin
    // formasyonları (havada/floating yok). Yoğunluk yatay değil DİKEY (kule istifi) ile artırılır → çakışma yaratmaz.
    void BuildCarFormation(int style, (GameObject prefab, int stack, float scale) type, Vector2 anchor, Transform root, int k)
    {
        var pf = type.prefab; float sc = type.scale;
        float len, wid;
        var bc = pf.GetComponent<BoxCollider>();
        if (bc != null) { len = Mathf.Max(0.6f, Mathf.Max(bc.size.x, bc.size.z) / 0.8f * sc); wid = Mathf.Max(0.4f, Mathf.Min(bc.size.x, bc.size.z) / 0.8f * sc); }
        else { var rs = RendererSize(pf); len = Mathf.Max(0.6f, Mathf.Max(rs.x, rs.z) * sc); wid = Mathf.Max(0.4f, Mathf.Min(rs.x, rs.z) * sc); }
        float baseYaw = (Mathf.Abs(LongAxis(pf).x) > 0.5f) ? 90f : 0f;
        // baseYaw HER aracın uzun eksenini dünya +Z'ye hizalar → yerleştirdikten sonra boy DAİMA +Z, en DAİMA +X.
        // (ÖNCEKİ HATA: offset yönlerini baseYaw'dan tekrar hesaplıyordum → baseYaw=90 araçlarda EN offset'i BOY
        //  yönüne düşüyor, araçlar boyları yönünde en-kadar aralıkla dizilip İÇİÇE geçiyordu — mavi klasikler gibi.)
        Vector2 lenDir = new(0f, 1f);   // boy yönü (dünya +Z)
        Vector2 widDir = new(1f, 0f);   // en yönü (dünya +X)

        switch (((style % 5) + 5) % 5)
        {
            case 0:   // KULE — tek ayak izi, dikey istif (yoğunluk; yatay çakışma yok)
                SpawnStack(pf, Random.Range(4, 7), sc, anchor, baseYaw, root);
                break;
            case 1:   // yan yana çift (en yönünde), görsel %15 açıklık — her biri istifli (dikey yoğunluk)
            {
                float sx = wid * 1.15f;
                SpawnStack(pf, Random.Range(2, 4), sc, anchor - widDir * (sx * 0.5f), baseYaw, root);
                SpawnStack(pf, Random.Range(2, 4), sc, anchor + widDir * (sx * 0.5f), baseYaw, root);
                break;
            }
            case 2:   // TEĞET HALKA (4-7) — araçlar çembere teğet, yay ≥ görsel boy; hafif istifli
            {
                float rad = len * 0.62f;
                int n = Mathf.Clamp(Mathf.FloorToInt(2f * Mathf.PI * rad / (len * 1.12f)), 4, 7);
                rad = Mathf.Max(rad, n * len * 1.12f / (2f * Mathf.PI));
                int rs = Random.Range(1, 3);
                for (int i = 0; i < n; i++)
                {
                    float a = i * (2f * Mathf.PI / n);
                    float cs = Mathf.Cos(a), sn = Mathf.Sin(a);
                    SpawnStack(pf, rs, sc, anchor + new Vector2(cs, sn) * rad, baseYaw + Mathf.Atan2(-sn, cs) * Mathf.Rad2Deg, root);
                }
                break;
            }
            case 3:   // art arda sıra (2-3, burun-kuyruk, görsel %8 açıklık) — istifli
            {
                int m = Random.Range(2, 4);
                for (int i = 0; i < m; i++)
                    SpawnStack(pf, Random.Range(2, 4), sc, anchor + lenDir * ((i - (m - 1) * 0.5f) * len * 1.08f), baseYaw, root);
                break;
            }
            default:  // 2×2 hizalı park bloğu (en×boy), görsel açıklıklı — hafif istifli
            {
                float sx = wid * 1.15f, sz = len * 1.08f;
                for (int i = 0; i < 2; i++)
                    for (int j = 0; j < 2; j++)
                        SpawnStack(pf, Random.Range(1, 3), sc, anchor + widDir * ((i - 0.5f) * sx) + lenDir * ((j - 0.5f) * sz), baseYaw, root);
                break;
            }
        }
    }

    void RingAround((GameObject prefab, int stack, float scale) type, Vector2 center, int n, float rad, Transform root)
    {
        for (int i = 0; i < n; i++)
        {
            float a = i * (2f * Mathf.PI / n);
            Vector2 p = center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rad;
            SpawnStack(type.prefab, 1, type.scale, p, -a * Mathf.Rad2Deg, root);
        }
    }

    // Belirli bir Y yüksekliğinde halka (fıskiye katmanı). rad=0 ise merkeze tek.
    void ElevatedRing((GameObject prefab, int stack, float scale) type, Vector2 center, int n, float rad, float baseY, Transform root)
    {
        if (rad < 0.01f) { SpawnElevated(type.prefab, center, baseY, type.scale, 0f, root); return; }
        for (int i = 0; i < n; i++)
        {
            float a = i * (2f * Mathf.PI / n);
            Vector2 p = center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rad;
            SpawnElevated(type.prefab, p, baseY, type.scale, -a * Mathf.Rad2Deg, root);
        }
    }

    // Belirli Y'de m×m kare ızgara (piramit katmanı), merkezde.
    void ElevatedGrid((GameObject prefab, int stack, float scale) type, Vector2 center, int m, float u, float baseY, Transform root)
    {
        float s = 1.02f * u, off = (m - 1) * 0.5f;
        for (int x = 0; x < m; x++)
            for (int z = 0; z < m; z++)
                SpawnElevated(type.prefab, center + new Vector2((x - off) * s, (z - off) * s), baseY, type.scale, 0f, root);
    }

    // Nesneyi belirli bir taban yüksekliğine (baseY) yerleştir (kademeli yapılar — kinematik donduğundan havada durur).
    void SpawnElevated(GameObject prefab, Vector2 xz, float baseY, float scale, float yaw, Transform parent)
    {
        // NOT: BİNALAR seyreltmesi burada UYGULANMAZ — fıskiye/piramit katmanları SpawnElevated ile kurulur; onları
        // seyreltmek YARIM/eksik yapı bırakır. Seyreltme yalnız SpawnStack'te (zemin formasyonları) → yapılar bütün kalır.
        yaw = ResolveYaw(yaw);
        xz = ClampToArena(xz, Footprint(prefab) * scale);
        var go = Instantiate(prefab, new Vector3(xz.x, baseY, xz.y), Quaternion.Euler(0f, yaw, 0f), parent);
        go.name = prefab.name;
        if (scale > 0.01f && Mathf.Abs(scale - 1f) > 0.001f) go.transform.localScale *= scale;
        ScaleEffect(go, scale);
        RestOn(go, baseY);   // çarpışma alt noktasını baseY'ye hizala
    }

    // Görsel ölçek varyantını puan+büyümeye de yansıt (büyük kopya = çok puan/size; küçük = az).
    static void ScaleEffect(GameObject go, float scale)
    {
        var sw = go.GetComponent<PhysicsSwallowable>();
        if (sw != null) sw.ApplySizeFactor(scale);
    }

    static float ObjHeight(GameObject prefab)
    {
        var bc = prefab.GetComponent<BoxCollider>();
        if (bc != null) return Mathf.Max(0.2f, bc.size.y);
        return Mathf.Max(0.2f, RendererSize(prefab).y);   // convex MeshCollider'lı nesneler (sweets/buildings)
    }

    // Prefabın görsel boyutu (dünya birimi). BoxCollider yoksa (convex MeshCollider) mesh bounds × lossyScale'den.
    // ⚠️ MaxDim/Footprint/ObjHeight/UprightEuler eskiden SADECE BoxCollider okuyordu → convex nesnelerde sahte sabit
    // (1/1.2) dönüyordu; bu yardımcı gerçek boyutu verir (yerleşim aralıkları doğru olsun).
    static Vector3 RendererSize(GameObject prefab)
    {
        Vector3 max = Vector3.zero; bool any = false;
        foreach (var mf in prefab.GetComponentsInChildren<MeshFilter>())
        {
            if (mf.sharedMesh == null) continue;
            Vector3 bs = mf.sharedMesh.bounds.size;
            Vector3 ls = mf.transform.lossyScale;
            Vector3 world = new Vector3(bs.x * Mathf.Abs(ls.x), bs.y * Mathf.Abs(ls.y), bs.z * Mathf.Abs(ls.z));
            max = Vector3.Max(max, world); any = true;
        }
        return any ? max : Vector3.one;
    }

    // Tam rotasyonla (dikey vb.) yerleştir, alt noktayı baseY'ye hizala (RestOn döndürülmüş bounds'u kullanır).
    void SpawnElevatedRot(GameObject prefab, Vector2 xz, float baseY, float scale, Vector3 euler, Transform parent)
    {
        xz = ClampToArena(xz, Footprint(prefab) * scale);
        var go = Instantiate(prefab, new Vector3(xz.x, baseY, xz.y), Quaternion.Euler(euler), parent);
        go.name = prefab.name;
        if (scale > 0.01f && Mathf.Abs(scale - 1f) > 0.001f) go.transform.localScale *= scale;
        ScaleEffect(go, scale);
        RestOn(go, baseY);
    }

    // Tam konum + rotasyon (RestOn YOK) — kemer gibi eğri üstüne teğet yerleştirmeler için.
    void SpawnExactRot(GameObject prefab, Vector3 pos, Vector3 euler, float scale, Transform parent)
    {
        Vector2 cxz = ClampToArena(new Vector2(pos.x, pos.z), Footprint(prefab) * scale);
        pos.x = cxz.x; pos.z = cxz.y;
        var go = Instantiate(prefab, pos, Quaternion.Euler(euler), parent);
        go.name = prefab.name;
        if (scale > 0.01f && Mathf.Abs(scale - 1f) > 0.001f) go.transform.localScale *= scale;
        ScaleEffect(go, scale);
    }

    // Nesnenin uzun eksenini DİKEY yapan euler (collider en büyük boyutuna göre).
    static Vector3 UprightEuler(GameObject prefab)
    {
        var bc = prefab.GetComponent<BoxCollider>();
        var s = bc != null ? bc.size : RendererSize(prefab);
        if (s.x >= s.y && s.x >= s.z) return new Vector3(0f, 0f, 90f);   // uzun X → dikey
        if (s.z >= s.x && s.z >= s.y) return new Vector3(90f, 0f, 0f);   // uzun Z → dikey
        return Vector3.zero;                                             // zaten dikey
    }

    static float MaxDim(GameObject prefab)
    {
        var bc = prefab.GetComponent<BoxCollider>();
        if (bc == null) { var s = RendererSize(prefab); return Mathf.Max(s.x, Mathf.Max(s.y, s.z)); }
        return Mathf.Max(bc.size.x, Mathf.Max(bc.size.y, bc.size.z));
    }

    static List<(Vector2, int)> FSquareTowers(float u, int h)
    {
        float s = 0.55f * u;   // 2x2 kolonlar, yaklaşık temas
        return new() { (new(-s, -s), h), (new(s, -s), h), (new(-s, s), h), (new(s, s), h) };
    }
    static List<(Vector2, int)> FCampGrid(float u, int h)
    {
        float s = 1.02f * u; var l = new List<(Vector2, int)>();   // temas halinde kare nizam
        for (int x = -1; x <= 1; x++) for (int z = 0; z <= 1; z++) l.Add((new Vector2(x * s, (z - 0.5f) * s), h));
        return l;
    }
    static List<(Vector2, int)> FRing(int n, float rad)
    {
        var l = new List<(Vector2, int)>();
        for (int i = 0; i < n; i++) { float a = i * (2f * Mathf.PI / n); l.Add((new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rad, 1)); }
        return l;
    }
    static List<(Vector2, int)> FColumn(int h) => new() { (Vector2.zero, h) };

    // Nesnenin yatay ayak izi (dünya birimi). Kök BoxCollider'dan (shrink geri alınarak görsele yakın); yoksa ~1.2.
    static float Footprint(GameObject prefab)
    {
        var bc = prefab.GetComponent<BoxCollider>();
        if (bc != null) return Mathf.Max(bc.size.x, bc.size.z) / 0.8f;
        var s = RendererSize(prefab);
        return Mathf.Max(0.25f, Mathf.Max(s.x, s.z));   // convex: gerçek görsel ayak izi
    }

    static void Shuffle<T>(List<T> a)
    {
        for (int i = a.Count - 1; i > 0; i--) { int j = Random.Range(0, i + 1); (a[i], a[j]) = (a[j], a[i]); }
    }

    // Az sayıda nesneyi bir yarıçapta eşit açıyla simetrik yerleştir (güç-up / bomba).
    void PlaceSparse(List<GameObject> items, float radius, Transform parent)
    {
        int n = items.Count;
        if (n == 0) return;
        // Güç-up'lar HER LEVELDA FARKLI, RASTGELE konumlarda. Per-level seed (SpawnObjects.InitState) → aynı level
        // her girişte aynı, ama dünyalar/levellar arası farklı yerlerde.
        // ⚠️ BİRLEŞİK-DOĞMA FIX (2026-08-02): eskiden altın-açı + jitter ile ÇAKIŞMA KONTROLSÜZ yerleştiriliyordu →
        // yoğun dünyalarda (Araba/Bina/Tatlılar formasyonları) güç-up bir nesnenin İÇİNE doğuyor, delik uyandırınca
        // ayrışıyordu. Çözüm: bu ana kadar YERLEŞMİŞ nesnelerin (formasyon + landmark) XZ dolu-alanlarını topla, her
        // güç-up'ı BOŞ bir cebe koy (altın-açı taraması). Cep bulunamazsa (çok yoğun) → ayak izini ClearArea ile aç.
        var occ = new List<(Vector2 p, float r)>();
        foreach (var psw in parent.GetComponentsInChildren<PhysicsSwallowable>())
        {
            if (psw == null || psw.powerUp != PowerUpType.None) continue;
            Bounds b; var cs = psw.GetComponentsInChildren<Collider>();
            if (cs.Length > 0) { b = cs[0].bounds; for (int i = 1; i < cs.Length; i++) b.Encapsulate(cs[i].bounds); }
            else { var rs = psw.GetComponentsInChildren<Renderer>(); if (rs.Length == 0) continue; b = rs[0].bounds; for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds); }
            occ.Add((new Vector2(b.center.x, b.center.z), Mathf.Max(b.extents.x, b.extents.z)));
        }

        float rMin = centerClearance + 2f;
        float rMax = Mathf.Max(rMin + 1f, playHalf - 2f);
        float baseAng = Random.value * Mathf.PI * 2f;
        int spot = 0;
        for (int j = 0; j < n; j++)
        {
            float pr = Footprint(items[j]) * 0.5f;
            Vector2 pos = Vector2.zero; bool found = false;
            for (int a = 0; a < 200; a++)
            {
                float ang = baseAng + spot * 2.399963f + Random.Range(-0.2f, 0.2f);
                float r = Mathf.Lerp(rMin, rMax, (spot * 0.61803399f) % 1f);
                spot++;
                Vector2 cand = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * r;
                if (InLandmarkArea(cand) || InBox(cand, pr)) continue;   // ŞEKİLLİ ARENA: kutuya koyma (yoksa SpawnStack atlar → güç-up KAYBOLUR)
                bool clear = true;
                for (int k = 0; k < occ.Count; k++)
                {
                    float rr = occ[k].r + pr + 0.35f;   // ayak-izi + pay → dokunmasın
                    if ((occ[k].p - cand).sqrMagnitude < rr * rr) { clear = false; break; }
                }
                if (clear) { pos = cand; found = true; break; }
            }
            if (!found)
            {
                // Boş cep yok → çakışmayı GÖZ ARDI et ama yine de KUTU/landmark DIŞINDA bir yer bul (güç-up kesin doğsun).
                for (int a = 0; a < 200; a++)
                {
                    float ang = baseAng + spot * 2.399963f; float r = Mathf.Lerp(rMin, rMax, (spot * 0.61803399f) % 1f); spot++;
                    Vector2 cand = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * r;
                    if (InLandmarkArea(cand) || InBox(cand, pr)) continue;
                    pos = cand; found = true; break;
                }
                if (found) ClearArea(parent, pos, pr + 0.9f);   // çevresini aç (EnsureObjectiveCounts hedefi geri doldurur)
            }
            if (!found) continue;   // (teorik) uygun yer yok → bu güç-up'ı atla, diğerlerini dene
            occ.Add((pos, pr));   // sonraki güç-up bunun üstüne gelmesin
            SpawnStack(items[j], 1, 1f, pos, Random.value * 360f, parent);
        }
    }

    // Bomba yerleşimi görünürlük kuralına göre:
    //  NORMAL → kameraya bakan ÖN yay (-Z tarafı), yakın-orta yarıçap → ekranın alt-ortasında NET görünür.
    //  HARD   → TÜM yönler (arka +Z dahil), dış yarıçap + jitter → öbekler arasına/arkasına düşer, ilk görüşte gizli.
    // Kamera -Z'den +Z'ye bakar (HoleCamera): -Z = ön/yakın (görünür), +Z = arka/uzak.
    void PlaceBombs(List<GameObject> items, bool hard, Transform parent)
    {
        int n = items.Count;
        if (n == 0) return;

        if (hard)
        {
            for (int j = 0; j < n; j++)
            {
                float ang = (j + 0.5f) * (2f * Mathf.PI / n) + Random.Range(-0.35f, 0.35f);
                float r = Random.Range(playHalf * 0.55f, playHalf * 0.92f);   // dışta/arkada, öbekler içinde
                Vector2 pos = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * r;
                SpawnStack(items[j], 1, 1f, pos, Random.value * 360f, parent);
            }
        }
        else
        {
            // NORMAL: bombalar ÖN KENARDA (kameraya en yakın sıra, -Z) → önlerinde nesne olmaz. Ayrıca her
            // bombanın etrafı TEMİZLENİR (yakın kule/nesne silinir) → hiçbir şey örtmez, oyuncu MUTLAKA görür
            // (adil kaçış şansı). ClearArea öndeki şeridi de kapsar (disk çerçeveye kadar uzanır).
            const float front = -Mathf.PI * 0.5f;    // -Z = kameraya doğru (ekran alt-ortası)
            const float arc   = Mathf.PI * 0.9f;      // ±81° geniş ön yay
            float r = playHalf * 0.88f;               // ön kenara yakın → arkasındaki hiçbir şey örtmez
            for (int j = 0; j < n; j++)
            {
                float t = (n == 1) ? 0.5f : j / (float)(n - 1);
                float ang = front + (t - 0.5f) * arc;
                Vector2 pos = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * r;
                ClearArea(parent, pos, 2.2f);          // etrafı + öndeki şeridi temizle → örtülmesin
                SpawnStack(items[j], 1, 1f, pos, 0f, parent);
            }
        }
    }

    // Verilen XZ noktası çevresindeki (radius) normal nesneleri siler (bomba/güç-up hariç) → o nokta AÇIK kalır.
    // Normal-level bombalarını görünür tutmak için: bombanın önünü/etrafını kapatan kule/nesneleri kaldırır.
    void ClearArea(Transform parent, Vector2 xz, float radius)
    {
        float r2 = radius * radius;
        var all = parent.GetComponentsInChildren<PhysicsSwallowable>();
        for (int i = 0; i < all.Length; i++)
        {
            var psw = all[i];
            if (psw == null) continue;
            if (psw.isBomb || psw.powerUp != PowerUpType.None) continue;   // bomba/güç-up koru
            Vector3 p = psw.transform.position;
            float dx = p.x - xz.x, dz = p.z - xz.y;
            if (dx * dx + dz * dz <= r2) DestroyImmediate(psw.gameObject);
        }
    }

    // Bir noktaya tekli ya da üst üste (stack) yerleştir; her parça bir öncekinin tepesine oturur.
    // Kameraya-yüz dünyalarında (faceCameraWorlds, örn. kediler) rastgele/radyal yaw'ı kamera-yüz sabitine çevirir.
    // Diğer dünyalarda yaw'ı olduğu gibi döndürür.
    float ResolveYaw(float yaw)
    {
        // BİNALAR (dünya 3): binaların/evlerin ÖN yüzü (kapılar) kameraya dönük olsun (kullanıcı 2026-07-29: bazıları
        // sırtı dönüktü). Kod-tabanlı (sahne faceCameraWorlds={9,10}'u değiştirmeye gerek yok; build'de sahne
        // kaydedilmese de çalışır). Binalarda az jitter → çoğu düz kameraya bakar, hafif çeşitlilik için ±7°.
        bool faceCam = CurrentWorld == 3;
        if (!faceCam && faceCameraWorlds != null)
            for (int i = 0; i < faceCameraWorlds.Length; i++)
                if (faceCameraWorlds[i] == CurrentWorld) { faceCam = true; break; }
        if (faceCam)
        {
            float jit = (CurrentWorld == 3) ? 7f : faceCameraJitter;
            return faceCameraYaw + Random.Range(-jit, jit);
        }
        return yaw;
    }

    /// <summary>Aktif dünyada nesnelerin SPAWN yaw'ının TABANI (jitter'sız). Kameraya-yüz dünyalarında (kediler/köpekler/
    /// binalar) faceCameraYaw, diğerlerinde 0. HUD hedef ikonu (IconRenderer) bunu kullanır → tabela resmi, nesnenin
    /// sahnedeki (kameraya dönük) görünümüyle eşleşir (sırtı değil).</summary>
    public float BaseFacingYaw()
    {
        bool faceCam = CurrentWorld == 3;
        if (!faceCam && faceCameraWorlds != null)
            for (int i = 0; i < faceCameraWorlds.Length; i++)
                if (faceCameraWorlds[i] == CurrentWorld) { faceCam = true; break; }
        return faceCam ? faceCameraYaw : 0f;
    }

    // Nesneyi görünmez arena duvarının İÇİNDE tutar: merkez + ayak izi/2 duvarı geçmesin (rim tuğlası kadar pay).
    Vector2 ClampToArena(Vector2 xz, float footprint)
    {
        float lim = _arenaLimit - footprint * 0.5f - 0.4f;
        if (lim < 0.5f) lim = 0.5f;
        return new Vector2(Mathf.Clamp(xz.x, -lim, lim), Mathf.Clamp(xz.y, -lim, lim));
    }

    void SpawnStack(GameObject prefab, int stack, float scale, Vector2 xz, float yaw, Transform parent)
    {
        // BİNALAR (dünya 3) SEYRELTME: ZEMİN formasyon kolonlarının ~%25'ini pozisyon-hash ile deterministik ATLA →
        // bina ~%25 azalır (kullanıcı 2026-07-28). Düzen (hücre tür-ataması) AYNI kalır, sadece boşluklar açılır.
        // Elevated yapılar (piramit/fıskiye) DOKUNULMAZ (bütün kalsın). Hedefler EnsureObjectiveCounts ile RUNTIME'da
        // required+3'e tamamlanır → kazanılabilirlik GARANTİ. (Az bina = delik kenarında daha az yığılma = perf'e İYİ.)
        if (_thinBuildings)
        {
            int hx = Mathf.RoundToInt(xz.x * 10f), hz = Mathf.RoundToInt(xz.y * 10f);
            if ((((hx * 73856093) ^ (hz * 19349663)) & 0x7fffffff) % 4 == 0) return;
        }
        yaw = ResolveYaw(yaw);
        // ŞEKİLLİ ARENA: dışlama kutusuna (engel/çıkıntı) düşen nesneyi YERLEŞTİRME (kompozisyon zaten kaçınır; bu son güvenlik).
        if (arenaShaped && InBox(xz, Footprint(prefab) * scale * 0.5f)) return;
        var clampedXz = ClampToArena(xz, Footprint(prefab) * scale);
        // ARAÇ/BÜYÜK-NESNE düzeninde (Arabalar/Binalar/İÇECEKLER — hepsi _carLayout): kenar duvarına sıkışacak nesneyi
        // (clamp konumu belirgin kaydırdıysa) YERLEŞTİRME → başka nesnelerin üstüne binip iç içe geçmesin. (Diğer
        // dünyaların formasyon düzeni etkilenmez; _carLayout yalnız ComposeCarCity'de açık = worldId 2/3/5.)
        if (_carLayout && (clampedXz - xz).sqrMagnitude > 0.01f) return;
        xz = clampedXz;
        if (_carLayout)
        {
            _occ.Add((xz, Footprint(prefab) * scale * 0.5f));   // dolu konum (hedef-tamamlama bindirmesin)
            _typeCount.TryGetValue(prefab.name, out int tc);
            _typeCount[prefab.name] = tc + Mathf.Max(1, stack);  // tür başına adet (istifteki her araç hedefe sayılır)
        }
        // ARAÇ delik-büyümesi: BOYUTLA orantılı, açık değerler (prefab'ın tutarsız yüksek growAmount'unu EZER).
        // küçük ~0.02, orta ~0.04, büyük ~0.05, dev ~0.10-0.13 (kullanıcı 2026-07-28). growMultiplier=1 (yukarıda).
        float baseY = 0f;
        for (int k = 0; k < stack; k++)
        {
            var go = Instantiate(prefab, new Vector3(xz.x, 0f, xz.y),
                                 Quaternion.Euler(0f, yaw, 0f), parent);
            go.name = prefab.name;   // "(Clone)" eki ResolvedType'ta temizleniyor
            if (scale > 0.01f && Mathf.Abs(scale - 1f) > 0.001f)
                go.transform.localScale *= scale;
            ScaleEffect(go, scale);
            baseY += RestOn(go, baseY) * 1.04f;   // +%4 NET boşluk → dikey çakışma yok (eski ×0.97 = %3 örtüşme; büyük
                                                  // araçta skin'i aşıp uyanınca fırlatıyordu). Küçük nesnede fark ihmal edilir.
        }
    }

    // Nesneyi baseY'ye oturt + GÖRSEL yüksekliğini döndür. GÖRSEL (renderer) sınırı kullanılır — collider bazı
    // araçlarda görsel mesh'ten KISA (colH<rendH); collider yüksekliğiyle istiflemek DİKEY görsel çakışma yapıyordu
    // (Batmobile/Renault vb.). Renderer ile istif görsel olarak doğru → üst araç alttakinin tavanına girmez.
    float RestOn(GameObject go, float baseY)
    {
        var rends = go.GetComponentsInChildren<Renderer>();
        if (rends.Length > 0)
        {
            Bounds rb = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) rb.Encapsulate(rends[i].bounds);
            go.transform.position += new Vector3(0f, baseY - rb.min.y + 0.02f, 0f);
            return rb.size.y;
        }
        var cols = go.GetComponentsInChildren<Collider>();
        if (cols.Length == 0) return 0.5f;
        Bounds b = cols[0].bounds;
        for (int i = 1; i < cols.Length; i++) b.Encapsulate(cols[i].bounds);
        go.transform.position += new Vector3(0f, baseY - b.min.y + 0.02f, 0f);
        return b.size.y;
    }

    /// <summary>ObjectiveTracker.Start bunu çeker (Active Awake'te hazır).</summary>
    public List<ObjectiveTracker.Objective> BuildObjectives()
    {
        var list = new List<ObjectiveTracker.Objective>();
        if (Active == null) return list;
        foreach (var o in Active.objectives)
            list.Add(new ObjectiveTracker.Objective { objectType = o.objectType, required = o.required, icon = o.icon });
        return list;
    }

    /// <summary>Aynı dünyada açılacak sonraki level var mı?</summary>
    public bool HasNext { get { var lv = CurrentLevels(); return lv != null && CurrentIndex + 1 < lv.Length; } }

    /// <summary>Sonraki level'a geç (varsa). Static CurrentIndex sahne reload'ları arası taşınır.</summary>
    public void AdvanceIndex() { if (HasNext) CurrentIndex++; }

    // ── İLERLEME KAYDI (PlayerPrefs) ──────────────────────────────────────────
    public static string UnlockKey(int worldId) => $"World{worldId}_Unlocked";

    /// <summary>Bu dünyada açılmış en yüksek level index'i (oynanmamışlar kilitli/sıralı).
    /// DEV/TEST build ve editörde HEPSİ açık (perf testinde en ağır levellara ulaşmak için); release'de normal ilerleme.</summary>
    public static int UnlockedIndex(int worldId)
    {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
        return WorldCatalog.PlayableLevels(worldId);   // test: tüm leveller açık
#else
        return PlayerPrefs.GetInt(UnlockKey(worldId), 0);
#endif
    }

    /// <summary>Level kazanılınca çağrılır: sonraki level'ı kalıcı olarak aç.</summary>
    public void SaveProgressOnSuccess()
    {
        if (Active == null) return;
        int next = CurrentIndex + 1;
        string key = UnlockKey(Active.worldId);
        if (next > PlayerPrefs.GetInt(key, 0))
        {
            PlayerPrefs.SetInt(key, next);
            PlayerPrefs.Save();
        }
    }
}
