using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// DÜNYA HARİKALARI (landmark) sistemi — LevelManager'ın partial parçası (2026-07-24).
/// Her dünyanın L3/L6/L9/L12'sinde (levelIndex 2/5/8/11) sahnenin ARKA-ORTASINA, o dünyanın KENDİ yutulabilir
/// nesnelerinden inşa edilmiş İKONİK bir yapı kurar (Eyfel/Atomium'un foods'taki el yapımı kalıbının
/// genelleştirilmişi). MİMARİ PRENSİPLER:
///   • ROL-TABANLI NESNE SEÇİMİ: yapının kolon/kirişi için UZUN nesne, kubbe/tekerlek için YUVARLAK,
///     duvar/blok için KUTUMSU, platform için YASSI nesne seçilir (RendererSize oranlarından sınıflandırma).
///   • RENK UYUMU: Resources/prefab_avg_colors.txt kataloğundan (PrefabColorCatalog üretir) hedef renge
///     en yakın uygun-şekilli nesne tercih edilir (örn. köprü=kırmızı, Pisa=beyaz, piramit=altın).
///   • TEMİZ ALAN: yapı bölgesi (landmarkClear yarıçapı) ComposeFormations/ComposeDrinks'te boş bırakılır —
///     yapının altında/çevresinde başka nesne olmaz, silüet net okunur.
/// Foods (worldId 1) HARİÇ — oradaki el yapımı yapılar (ChichenItza/Atomium/TajMahal/Eyfel) korunur.
/// Parçalar kinematik-donuk spawn edilir (freezeUntilNear) → yapı yerinde durur, delik yaklaşınca yutulur.
/// </summary>
public partial class LevelManager
{
    // ── LANDMARK TİPLERİ (herkesçe tanınabilir ikonik yapılar) ──
    enum Landmark
    {
        GreatPyramid,   // Mısır — Büyük Giza Piramidi
        FerrisWheel,    // Londra — London Eye dönme dolabı (dikey çember)
        PisaTower,      // İtalya — eğik Pisa Kulesi
        Colosseum,      // Roma — yıkık oval amfitiyatro
        TowerBridge,    // köprü — 2 kule + tabliye + kırmızı halat kavisleri (Chicago/Tower Bridge)
        BigBen,         // Londra — saat kulesi
        Lighthouse,     // deniz feneri — kırmızı-beyaz şeritli
        Cappadocia,     // Türkiye — peri bacası + havada sıcak hava balonu
        ToriiGate,      // Japonya — kırmızı tapınak kapısı
        Stonehenge,     // İngiltere — taş çemberi (dikme + lento)
        ArcTriomphe,    // Paris — Zafer Takı
        Windmill,       // Hollanda — yel değirmeni (çapraz kanatlar)
        Basilica,       // Vatikan — büyük kubbeli bazilika
    }

    // Dünya → 4 landmark (L3, L6, L9, L12 sırasıyla). Foods(1) YOK (el yapımı yapıları var).
    static readonly Dictionary<int, Landmark[]> LandmarkPlan = new()
    {
        { 0,  new[]{ Landmark.FerrisWheel, Landmark.Stonehenge,  Landmark.Windmill,    Landmark.Basilica } },     // Park
        { 2,  new[]{ Landmark.TowerBridge, Landmark.BigBen,      Landmark.ArcTriomphe, Landmark.FerrisWheel } },  // Arabalar
        { 3,  new[]{ Landmark.GreatPyramid,Landmark.PisaTower,   Landmark.Colosseum,   Landmark.Basilica } },     // Binalar
        { 4,  new[]{ Landmark.FerrisWheel, Landmark.Cappadocia,  Landmark.Basilica,    Landmark.PisaTower } },    // Tatlılar
        { 5,  new[]{ Landmark.PisaTower,   Landmark.Lighthouse,  Landmark.ToriiGate,   Landmark.GreatPyramid } }, // İçecekler
        { 6,  new[]{ Landmark.FerrisWheel, Landmark.BigBen,      Landmark.Basilica,    Landmark.Stonehenge } },   // Hediyeler
        { 7,  new[]{ Landmark.BigBen,      Landmark.GreatPyramid,Landmark.ArcTriomphe, Landmark.Stonehenge } },   // Kitaplar
        { 9,  new[]{ Landmark.ToriiGate,   Landmark.GreatPyramid,Landmark.Colosseum,   Landmark.Windmill } },     // Kediler
        { 10, new[]{ Landmark.GreatPyramid,Landmark.Lighthouse,  Landmark.ToriiGate,   Landmark.FerrisWheel } },  // Köpekler (2026-07-24: Stonehenge organik nesnelerle okunmuyor → dikdörtgen dünyalarda kaldı: Books/Gifts L12, Park L6)
        { 11, new[]{ Landmark.Lighthouse,  Landmark.TowerBridge, Landmark.Colosseum,   Landmark.Windmill } },     // Gemiler
        { 12, new[]{ Landmark.TowerBridge, Landmark.BigBen,      Landmark.FerrisWheel, Landmark.GreatPyramid } }, // Uçaklar
        { 13, new[]{ Landmark.GreatPyramid,Landmark.Basilica,    Landmark.BigBen,      Landmark.ArcTriomphe } },  // Hazine
        { 15, new[]{ Landmark.Basilica,    Landmark.FerrisWheel, Landmark.Cappadocia,  Landmark.PisaTower } },    // Mücevher
        { 17, new[]{ Landmark.TowerBridge, Landmark.GreatPyramid,Landmark.FerrisWheel, Landmark.Colosseum } },    // Karma
    };

    // Aktif level landmark durumu (SpawnObjects başında set edilir; Compose* temiz alan için okur).
    bool _lmActive; Vector2 _lmPos; float _lmClear;

    /// <summary>Bu levelda landmark var mı? (L3/6/9/12 = idx 2/5/8/11; foods hariç)</summary>
    bool LandmarkThisLevel(out Landmark lm)
    {
        lm = default;
        if (Active == null || Active.worldId == 1) return false;
        int i = Active.levelIndex;
        if (i != 2 && i != 5 && i != 8 && i != 11) return false;
        if (!LandmarkPlan.TryGetValue(Active.worldId, out var arr)) return false;
        lm = arr[(i - 2) / 3];
        return true;
    }

    // SpawnObjects başında çağrılır: konum + temiz alan yarıçapını hazırla.
    void SetupLandmarkArea()
    {
        _lmActive = LandmarkThisLevel(out var lm);
        if (!_lmActive) { _lmClear = 0f; return; }
        _lmPos = new Vector2(0f, playHalf * 0.62f);   // ARKA-ORTA (Atomium konumu — kameradan net görünür)
        _lmClear = lm switch
        {
            Landmark.TowerBridge => 9.5f,   // yatayda geniş
            Landmark.Stonehenge  => 8f,
            Landmark.Colosseum   => 8f,
            Landmark.FerrisWheel => 7.5f,
            _ => 7f,
        };
    }

    /// <summary>Bu anchor landmark temiz alanına giriyor mu? (Compose* hücre atlamada kullanır.)</summary>
    bool InLandmarkArea(Vector2 anchor) => _lmActive && (anchor - _lmPos).magnitude < _lmClear;

    // ComposeFormations/ComposeDrinks BİTİMİNDE çağrılır: yapıyı kur.
    void BuildWorldLandmark(List<(GameObject prefab, int stack, float scale)> types, Transform root)
    {
        if (!_lmActive || !LandmarkThisLevel(out var lm)) return;
        EnsureColorCatalog();
        switch (lm)
        {
            case Landmark.GreatPyramid: LM_GreatPyramid(types, _lmPos, root); break;
            case Landmark.FerrisWheel:  LM_FerrisWheel(types, _lmPos, root); break;
            case Landmark.PisaTower:    LM_PisaTower(types, _lmPos, root); break;
            case Landmark.Colosseum:    LM_Colosseum(types, _lmPos, root); break;
            case Landmark.TowerBridge:  LM_TowerBridge(types, _lmPos, root); break;
            case Landmark.BigBen:       LM_BigBen(types, _lmPos, root); break;
            case Landmark.Lighthouse:   LM_Lighthouse(types, _lmPos, root); break;
            case Landmark.Cappadocia:   LM_Cappadocia(types, _lmPos, root); break;
            case Landmark.ToriiGate:    LM_ToriiGate(types, _lmPos, root); break;
            case Landmark.Stonehenge:   LM_Stonehenge(types, _lmPos, root); break;
            case Landmark.ArcTriomphe:  LM_ArcTriomphe(types, _lmPos, root); break;
            case Landmark.Windmill:     LM_Windmill(types, _lmPos, root); break;
            case Landmark.Basilica:     LM_Basilica(types, _lmPos, root); break;
        }
    }

    // ── ŞEKİL SINIFLANDIRMA (mimari roller) ──
    enum Role { Column, Round, Block, Flat }

    static Role Classify(GameObject pf)
    {
        Vector3 s = RendererSize(pf);
        float max = Mathf.Max(s.x, Mathf.Max(s.y, s.z));
        float min = Mathf.Min(s.x, Mathf.Min(s.y, s.z));
        float mid = s.x + s.y + s.z - max - min;
        if (max >= mid * 2.1f) return Role.Column;            // bir eksen belirgin uzun → kolon/kiriş
        if (min <= max * 0.42f) return Role.Flat;             // bir eksen belirgin ince → disk/plaka
        if (max <= min * 1.45f) return Role.Round;            // üç eksen dengeli → küre/top benzeri
        return Role.Block;                                    // kutumsu
    }

    // ── RENK KATALOĞU (Resources/prefab_avg_colors.txt — PrefabColorCatalog üretir) ──
    static Dictionary<string, Color> _avgColors;
    static void EnsureColorCatalog()
    {
        if (_avgColors != null) return;
        _avgColors = new Dictionary<string, Color>();
        var ta = Resources.Load<TextAsset>("prefab_avg_colors");
        if (ta == null) return;   // katalog yoksa renk kriteri devre dışı (şekil yine çalışır)
        foreach (var line in ta.text.Split('\n'))
        {
            var p = line.Trim().Split(' ');
            if (p.Length < 4) continue;
            if (float.TryParse(p[p.Length-3], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float r) &&
                float.TryParse(p[p.Length-2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float g) &&
                float.TryParse(p[p.Length-1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float b))
                _avgColors[string.Join(" ", p, 0, p.Length-3)] = new Color(r, g, b);
        }
    }

    // ── ROL + RENK TABANLI NESNE SEÇİMİ ──
    // Level'ın kendi türlerinden isteneni seç: önce şekil rolü, sonra renk yakınlığı, sonra boyut uygunluğu.
    // wantColor null → renk önemsiz. Bulunamazsa en yakın alternatif (asla null dönmez; types boş değilse).
    GameObject PickRole(List<(GameObject prefab, int stack, float scale)> types, Role role, Color? wantColor = null,
                        float maxDim = 99f)
    {
        GameObject best = null; float bestScore = float.MinValue;
        foreach (var t in types)
        {
            if (t.prefab == null || t.prefab.name == "Tray") continue;
            float dim = MaxDim(t.prefab) * t.scale;
            if (dim > maxDim) continue;
            float score = Classify(t.prefab) == role ? 10f : 0f;
            if (wantColor.HasValue && _avgColors != null && _avgColors.TryGetValue(t.prefab.name, out var c))
            {
                float d = Mathf.Abs(c.r - wantColor.Value.r) + Mathf.Abs(c.g - wantColor.Value.g) + Mathf.Abs(c.b - wantColor.Value.b);
                score += (1.5f - d) * 3f;   // yakın renk bonusu
            }
            score += Random.Range(0f, 0.01f);   // eşitlik kırıcı (deterministik seed altında)
            if (score > bestScore) { bestScore = score; best = t.prefab; }
        }
        return best;
    }

    // İkinci bir tür (birinciden FARKLI olsun — kontrast/detay için).
    GameObject PickRoleOther(List<(GameObject prefab, int stack, float scale)> types, Role role, GameObject not,
                             Color? wantColor = null, float maxDim = 99f)
    {
        GameObject best = null; float bestScore = float.MinValue;
        foreach (var t in types)
        {
            if (t.prefab == null || t.prefab == not || t.prefab.name == "Tray") continue;
            float dim = MaxDim(t.prefab) * t.scale;
            if (dim > maxDim) continue;
            float score = Classify(t.prefab) == role ? 10f : 0f;
            if (wantColor.HasValue && _avgColors != null && _avgColors.TryGetValue(t.prefab.name, out var c))
            {
                float d = Mathf.Abs(c.r - wantColor.Value.r) + Mathf.Abs(c.g - wantColor.Value.g) + Mathf.Abs(c.b - wantColor.Value.b);
                score += (1.5f - d) * 3f;
            }
            if (score > bestScore) { bestScore = score; best = t.prefab; }
        }
        return best != null ? best : not;
    }

    // Nesneyi hedef dünya-boyutuna getirecek scale çarpanı (görsel tutarlılık: yapı parçaları eş boyut).
    static float ScaleTo(GameObject pf, float targetDim) => targetDim / Mathf.Max(0.05f, MaxDim(pf));

    // DİKEY kat adımı = nesnenin GERÇEK yüksekliği (yassı nesnelerde katlar havada boşluklu kalmasın; 2026-07-24 fix).
    static float VStep(GameObject pf, float sc) => Mathf.Max(0.14f, ObjHeight(pf) * sc * 0.97f);

    // Uzun ekseni RENDERER boyutlarından bul (LongAxis BoxCollider ister — convex nesnelerde yanılır).
    static Vector3 LongAxisR(GameObject pf)
    {
        var s = RendererSize(pf);
        if (s.x >= s.y && s.x >= s.z) return Vector3.right;
        if (s.z >= s.x && s.z >= s.y) return Vector3.forward;
        return Vector3.up;
    }

    // İKİNCİ en uzun eksen (örn. uçak kanat açıklığı — gövdeden sonraki en geniş yön).
    static Vector3 SecondAxisR(GameObject pf)
    {
        var s = RendererSize(pf);
        float mx = Mathf.Max(s.x, Mathf.Max(s.y, s.z));
        float mn = Mathf.Min(s.x, Mathf.Min(s.y, s.z));
        // ne en büyük ne en küçük olan eksen
        if (Mathf.Approximately(s.x, mx) || Mathf.Approximately(s.x, mn)) return (s.y >= s.z) ? Vector3.up : Vector3.forward;
        if (Mathf.Approximately(s.y, mx) || Mathf.Approximately(s.y, mn)) return (s.x >= s.z) ? Vector3.right : Vector3.forward;
        return (s.x >= s.y) ? Vector3.right : Vector3.up;
    }

    // Nesnenin uzun eksenini istenen DÜNYA yönüne çeviren euler (dönme dolabı kolu, değirmen kanadı, ip vb.).
    // ⚠️ UprightEuler+ek-açı bileşimi Unity euler sırası yüzünden yanılıyordu (sokak lambası yanlış açı) → quaternion.
    static Vector3 AlignEuler(GameObject pf, Vector3 worldDir)
        => Quaternion.FromToRotation(LongAxisR(pf), worldDir.normalized).eulerAngles;

    // ═══════════════ LANDMARK BUILDER'LARI ═══════════════
    static readonly Color GOLD = new(0.85f, 0.68f, 0.25f);
    static readonly Color REDC = new(0.75f, 0.15f, 0.12f);
    static readonly Color WHITEC = new(0.85f, 0.83f, 0.78f);
    static readonly Color GRAYC = new(0.55f, 0.55f, 0.55f);

    // MISIR BÜYÜK PİRAMİDİ — dolu kare katmanlar yukarı daralır (düz üçgen silüet; altın/kum rengi).
    void LM_GreatPyramid(List<(GameObject prefab, int stack, float scale)> t, Vector2 c, Transform root)
    {
        var block = PickRole(t, Role.Block, GOLD);
        if (block == null) return;
        // 2026-07-25 PERF: bloklar DAHA BÜYÜK + SADECE ÇERÇEVE (içi boş) + daha az kat → nesne ~330'dan ~90'a.
        // Stepped-piramit silüeti korunur (dıştan dolu görünür), mobil donma riski düşer. (kullanıcı: sayıyı iyice azalt, boyut büyüt.)
        float u = 1.25f; float sc = ScaleTo(block, u);
        float fp = Mathf.Max(0.3f, Footprint(block) * sc);   // gerçek ayak izi → bloklar TEMAS eder (aralık yok)
        float step = VStep(block, sc);
        int layers = 6;
        float baseHalf = 3.5f, y = 0f;
        for (int L = 0; L < layers; L++)
        {
            float half = Mathf.Lerp(baseHalf, 0.1f, L / (float)(layers - 1));
            int per = Mathf.Max(1, Mathf.CeilToInt(2f * half / (fp * 0.95f)));
            for (int i = 0; i <= per; i++)
                for (int j = 0; j <= per; j++)
                {
                    bool edge = i == 0 || j == 0 || i == per || j == per;
                    if (!edge) continue;   // SADECE çerçeve → az nesne, yine dolu stepped-piramit silüeti
                    Vector2 p = c + new Vector2(-half + i * (2f * half / Mathf.Max(1, per)), -half + j * (2f * half / Mathf.Max(1, per)));
                    SpawnElevated(block, p, y, sc, 0f, root);
                }
            y += step;
        }
        SpawnElevated(PickRole(t, Role.Round, GOLD, 1.4f) ?? block, c, y, sc * 0.9f, 0f, root);   // tepe taşı
    }

    // LONDON EYE — DİKEY dev çember. 2026-07-24 KOMPLE REVİZYON (kullanıcı): (1) kabinleri birleştiren BELİRGİN
    // RIM çemberi — uzun nesne varsa (uçak gövdesi) TEĞET parçalar, yoksa sık küçük parça dizisi; (2) jant kolları
    // "saat yelkovanı gibi" — tek döndürülmüş uzun nesne DEĞİL (lamba pivot/eksen sorunu), merkezden dışa KÜÇÜK
    // PARÇA DİZİSİ (her nesneyle doğru çalışır); (3) kabinler rim'in hemen iç tarafında sarkar.
    void LM_FerrisWheel(List<(GameObject prefab, int stack, float scale)> t, Vector2 c, Transform root)
    {
        var pod = PickRole(t, Role.Round, null, 1.6f);
        var armLong = PickRole(t, Role.Column);            // uzun nesne: RADYAL kol (yelkovan — lamba/uçak gövdesi)
        var leg = PickRole(t, Role.Block);
        if (pod == null) pod = leg; if (pod == null) return;

        float R = 4.2f, hubY = R + 1.1f;
        float z = c.y;

        // Uzun nesne KOL olarak sadece İNCE (yüksek en-boy) nesnelerde iyi okunur — lamba EVET, kanatlı uçak HAYIR.
        // Kanatlı/hacimli uzun nesneler radyal kol yerine küçük-parça kola düşer (uçaklarda temiz çark).
        Vector3 als = armLong != null ? RendererSize(armLong) : Vector3.one;
        float alMax = Mathf.Max(als.x, Mathf.Max(als.y, als.z));
        float alMid = als.x + als.y + als.z - alMax - Mathf.Min(als.x, Mathf.Min(als.y, als.z));
        bool thinArm = armLong != null && alMax >= alMid * 2.6f;

        // 1) RIM ÇEMBERİ — HER ZAMAN sık küçük parça dizisi (kapalı, belirgin büyük çember)
        {
            float ssc = ScaleTo(pod, 0.55f);
            int n2 = Mathf.Max(18, Mathf.RoundToInt(2f * Mathf.PI * R / (MaxDim(pod) * ssc * 1.0f)));
            for (int i = 0; i < n2; i++)
            {
                float a = i * (2f * Mathf.PI / n2);
                SpawnExactRot(pod, new Vector3(c.x + Mathf.Cos(a) * R, hubY + Mathf.Sin(a) * R, z), Vector3.zero, ssc, root);
            }
        }

        // 2) JANT KOLLARI — "saat yelkovanı gibi" (kullanıcı): uzun nesne varsa 6 RADYAL kol (AlignEuler),
        //    pivot belirsizliğine dayanıklı konum (R*0.33'ten başlar); yoksa küçük parça dizisi kolları.
        //    İNCE nesne (lamba) → uzun radyal kol; kanatlı/hacimli (uçak) → küçük-parça kol (else).
        if (thinArm)
        {
            float asc = ScaleTo(armLong, R * 0.8f);
            for (int k = 0; k < 6; k++)
            {
                float a = k * (Mathf.PI / 3f);
                Vector3 dir = new(Mathf.Cos(a), Mathf.Sin(a), 0f);
                Vector3 pos = new(c.x + dir.x * R * 0.33f, hubY + dir.y * R * 0.33f, z);
                SpawnExactRot(armLong, pos, AlignEuler(armLong, dir), asc, root);
            }
        }
        else
        {
            float armSc = ScaleTo(pod, 0.5f);
            for (int k = 0; k < 6; k++)
            {
                float a = k * (Mathf.PI / 3f);
                for (int s = 1; s <= 4; s++)
                {
                    float rr = R * s / 5f;
                    SpawnExactRot(pod, new Vector3(c.x + Mathf.Cos(a) * rr, hubY + Mathf.Sin(a) * rr, z), Vector3.zero, armSc, root);
                }
            }
        }
        // Merkez göbek (büyükçe)
        SpawnExactRot(pod, new Vector3(c.x, hubY, z), Vector3.zero, ScaleTo(pod, 1.15f), root);

        // 3) KABİNLER: 8 kabin, rim noktalarının hemen İÇ-ALTINDA sarkar
        float podSc = ScaleTo(pod, 0.95f);
        for (int i = 0; i < 8; i++)
        {
            float a = i * (2f * Mathf.PI / 8f) + Mathf.PI / 8f;
            Vector3 p = new(c.x + Mathf.Cos(a) * R, hubY + Mathf.Sin(a) * R - 0.55f, z);
            SpawnExactRot(pod, p, Vector3.zero, podSc, root);
        }

        // 4) DESTEK A-BACAKLARI (zeminden göbeğe daralan çift kolon)
        if (leg != null)
        {
            float legSc = ScaleTo(leg, 1.0f);
            float lstep = VStep(leg, legSc);
            for (float ly = 0f; ly < hubY - 0.8f; ly += lstep)
            {
                float off = Mathf.Lerp(1.6f, 0.3f, ly / hubY);
                SpawnElevated(leg, c + new Vector2(-off, -0.4f), ly, legSc, 0f, root);
                SpawnElevated(leg, c + new Vector2( off, -0.4f), ly, legSc, 0f, root);
            }
        }
    }

    // PİSA KULESİ — beyaz silindirik katlar, yukarı çıktıkça sabit yönde offset (EĞİK silüet).
    void LM_PisaTower(List<(GameObject prefab, int stack, float scale)> t, Vector2 c, Transform root)
    {
        var ring = PickRole(t, Role.Round, WHITEC, 1.6f);
        if (ring == null) ring = PickRole(t, Role.Block, WHITEC);
        if (ring == null) return;
        float u = 0.9f; float sc = ScaleTo(ring, u);
        float step = VStep(ring, sc);
        int floors = 8; float rad = 1.15f;
        float lean = 0.16f;   // kat başına yatay kayma → ~7° eğim
        for (int f = 0; f < floors; f++)
        {
            float y = f * step;
            Vector2 fc = c + new Vector2(f * lean, 0f);
            int n = f == floors - 1 ? 4 : 7;                  // en üst kat dar (çan katı)
            float rr = f == floors - 1 ? rad * 0.55f : rad;
            for (int i = 0; i < n; i++)
            {
                float a = i * (2f * Mathf.PI / n) + f * 0.35f;
                SpawnElevated(ring, fc + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rr, y, sc, 0f, root);
            }
        }
    }

    // COLOSSEUM — oval çift-kat kemer duvarı; bir bölümü ALÇAK (yıkık kısım — ikonik silüet).
    void LM_Colosseum(List<(GameObject prefab, int stack, float scale)> t, Vector2 c, Transform root)
    {
        var stone = PickRole(t, Role.Block, new Color(0.72f, 0.62f, 0.45f));
        if (stone == null) return;
        float u = 0.8f; float sc = ScaleTo(stone, u);
        float step = VStep(stone, sc);
        float rx = 4.6f, rz = 3.4f;
        int n = 26;
        for (int i = 0; i < n; i++)
        {
            float a = i * (2f * Mathf.PI / n);
            // yıkık bölge: ön-sol çeyrekte duvar alçalır (ikonik yıkık silüet)
            int height = (a > 3.3f && a < 4.7f) ? 1 : 3;
            Vector2 p = c + new Vector2(Mathf.Cos(a) * rx, Mathf.Sin(a) * rz);
            for (int h = 0; h < height; h++)
                if ((i + h) % 2 == 0 || h == height - 1)   // kemer boşluğu deseni (bir dolu bir boş)
                    SpawnElevated(stone, p, h * step, sc, a * Mathf.Rad2Deg, root);
        }
    }

    // KULE KÖPRÜ (Tower/Chicago) — 2 blok kule + üst tabliye (yatay uzunlar) + KIRMIZI halat kavisleri.
    void LM_TowerBridge(List<(GameObject prefab, int stack, float scale)> t, Vector2 c, Transform root)
    {
        var towerB = PickRole(t, Role.Block, GRAYC);
        var deck = PickRole(t, Role.Column, REDC);
        var cable = PickRoleOther(t, Role.Round, deck, REDC, 1.1f);
        if (towerB == null || deck == null) return;

        float span = 4.2f;                                   // kuleler arası yarı-açıklık
        float u = 0.85f; float tsc = ScaleTo(towerB, u);
        float tstep = VStep(towerB, tsc);
        float towerH = Mathf.Min(5.5f, tstep * 9f);          // yassı nesnede kule kısalır ama DOLU kalır
        // 2 kule (2x2 kolon her biri) + KÜÇÜK NESNELERDEN SİVRİ ÇATI (2026-07-24: kullanıcı — çatılar benzemiyordu)
        foreach (float sx in new[] { -span, span })
        {
            for (float y = 0f; y < towerH; y += tstep)
            {
                SpawnElevated(towerB, c + new Vector2(sx - u * 0.5f, 0f), y, tsc, 0f, root);
                SpawnElevated(towerB, c + new Vector2(sx + u * 0.5f, 0f), y, tsc, 0f, root);
            }
            // Çatı: geniş saçak katı + 4 kademeli daralan külah + tepe topuzu (küçük parçalar, sc*0.5)
            float rsc0 = tsc * 0.62f;
            float rstep = Mathf.Max(0.12f, VStep(towerB, rsc0));
            float ry = towerH + 0.05f;
            for (int L = 0; L < 4; L++)
            {
                float rh = Mathf.Lerp(u * 0.72f, 0.08f, L / 3f);
                float rsc = tsc * Mathf.Lerp(0.62f, 0.34f, L / 3f);
                int[] sg = { -1, 1 };
                if (rh > 0.14f)
                    foreach (int i in sg) foreach (int j in sg)
                        SpawnElevated(towerB, c + new Vector2(sx + i * rh, j * rh), ry, rsc, 45f, root);
                else SpawnElevated(towerB, c + new Vector2(sx, 0f), ry, rsc, 45f, root);
                ry += rstep;
            }
            SpawnElevated(PickRole(t, Role.Round, GOLD, 0.8f) ?? towerB, c + new Vector2(sx, 0f), ry, tsc * 0.4f, 0f, root);  // topuz
        }
        // Tabliye: kuleler arası yatay uzun nesneler (köprü yolu) alçak seviyede
        float deckLen = MaxDim(deck); float dsc = ScaleTo(deck, 1.9f);
        int nd = Mathf.Max(3, Mathf.CeilToInt(2f * span / (1.9f * 0.75f)));
        for (int i = 0; i < nd; i++)
        {
            float x = Mathf.Lerp(-span + 0.6f, span - 0.6f, i / (float)(nd - 1));
            SpawnElevated(deck, c + new Vector2(x, 0f), 1.5f, dsc, 90f, root);   // yol X boyunca
        }
        // Halat kavisleri: kule tepelerinden ortaya sarkan ters parabol (kırmızı yuvarlaklar)
        if (cable != null)
        {
            float csc = ScaleTo(cable, 0.7f);
            int nc = 9;
            for (int i = 0; i < nc; i++)
            {
                float uu = i / (nc - 1f) * 2f - 1f;           // -1..1 (sol kule→sağ kule)
                float x = uu * span;
                float y = Mathf.Lerp(towerH, 1.9f, 1f - uu * uu);   // uçlar kule tepesi, orta sarkık
                SpawnExactRot(cable, new Vector3(c.x + x, y, c.y), Vector3.zero, csc, root);
            }
        }
    }

    // BIG BEN — ince yüksek kare kule + tepede 4 yüzlü SAAT (yuvarlak) + sivri çatı.
    void LM_BigBen(List<(GameObject prefab, int stack, float scale)> t, Vector2 c, Transform root)
    {
        var brick = PickRole(t, Role.Block, GOLD);
        var clock = PickRole(t, Role.Round, WHITEC, 1.5f);
        var spire = PickRole(t, Role.Column);
        if (brick == null) return;
        float u = 0.62f; float sc = ScaleTo(brick, u);   // KÜÇÜK tuğlalar → detay (2026-07-24 revizyon)
        float bstep = VStep(brick, sc);
        float half = 0.55f, towerH = Mathf.Min(5.6f, bstep * 12f);
        // Gövde: 3x3 çevre duvarı (köşe + kenar ortası → dolu görünüm), içi boş
        for (float y = 0f; y < towerH; y += bstep)
            for (int i = -1; i <= 1; i++)
                for (int j = -1; j <= 1; j++)
                {
                    if (i == 0 && j == 0) continue;
                    SpawnElevated(brick, c + new Vector2(i * half, j * half), y, sc, 0f, root);
                }
        // SAAT KATI: gövdeden GENİŞ korniş (2 kat) + 4 yüze BÜYÜK beyaz saat diski (net okunur)
        float cw = half + 0.45f;
        for (int k = 0; k < 2; k++)
            for (int i = -1; i <= 1; i++)
                for (int j = -1; j <= 1; j++)
                {
                    if (i == 0 && j == 0) continue;
                    SpawnElevated(brick, c + new Vector2(i * cw, j * cw), towerH + k * bstep, sc, 0f, root);
                }
        float clockBandTop = towerH + 2f * bstep;
        if (clock != null)
        {
            float csc = ScaleTo(clock, 1.9f);   // BÜYÜK saat
            float cy = towerH + bstep;          // korniş bandının ortası
            // 2026-07-24 fix (kullanıcı): yan yüz saatleri (0,0,90) nesneyi DİKEY deviriyordu (araba yan durdu).
            // Tüm yüzler aynı YATAY duruşta: X-90 duruş + yüz yönü kadar yaw (quaternion bileşimi — euler sırası güvenli).
            Vector3 eN = new(90f, 0f, 0f);
            Vector3 eE = (Quaternion.Euler(0f, 90f, 0f) * Quaternion.Euler(90f, 0f, 0f)).eulerAngles;
            SpawnExactRot(clock, new Vector3(c.x, cy, c.y - cw - 0.5f), eN, csc, root);
            SpawnExactRot(clock, new Vector3(c.x, cy, c.y + cw + 0.5f), eN, csc, root);
            SpawnExactRot(clock, new Vector3(c.x - cw - 0.5f, cy, c.y), eE, csc, root);
            SpawnExactRot(clock, new Vector3(c.x + cw + 0.5f, cy, c.y), eE, csc, root);
        }
        // ÇATI — 2026-07-24 (kullanıcı: kitaplarda tepe kitap-yığını oldu; TEK kitap uzunlamasına istiyor).
        // `brick` bir KİTAP-YIĞINI modeli olabilir → külahı ondan DEĞİL, `roofPc` (tekil düz kitap = clock kadranı,
        // yoksa Flat/Round tekil) ile kur: 4 kat daralan İNCE piramit + TEPEDE TEK DİKEY kitap (uzunlamasına) + topuz.
        float ry = clockBandTop;
        var roofPc = clock ?? PickRole(t, Role.Flat, REDC, 1.4f) ?? PickRole(t, Role.Round, REDC, 1.4f) ?? brick;
        float rpStep = Mathf.Max(0.12f, VStep(roofPc, ScaleTo(roofPc, cw)));
        for (int L = 0; L < 4; L++)
        {
            float rh = Mathf.Lerp(cw * 0.8f, 0.12f, L / 3f);
            float rsc = ScaleTo(roofPc, Mathf.Max(0.35f, rh * 1.4f));
            int[] sg = { -1, 1 };
            if (rh > 0.22f)
                foreach (int i in sg) foreach (int j in sg)
                    SpawnElevated(roofPc, c + new Vector2(i * rh, j * rh), ry, rsc, 45f, root);
            else SpawnElevated(roofPc, c, ry, rsc, 45f, root);
            ry += rpStep * 0.8f;
        }
        // TEPEDE TEK DİKEY KİTAP (uzunlamasına ayakta) — spire; sonra altın topuz
        var spireBook = roofPc;
        float sl = 2.6f; float ssc = ScaleTo(spireBook, sl);
        SpawnExactRot(spireBook, new Vector3(c.x, ry + sl * 0.5f, c.y), AlignEuler(spireBook, Vector3.up), ssc, root);
        SpawnElevated(PickRole(t, Role.Round, GOLD, 0.9f) ?? spireBook, c, ry + sl + 0.15f, sc * 0.55f, 0f, root);
    }

    // DENİZ FENERİ — kırmızı/beyaz DÖNÜŞÜMLü şerit katmanlar, yukarı daralır; tepede ışık (parlak yuvarlak).
    void LM_Lighthouse(List<(GameObject prefab, int stack, float scale)> t, Vector2 c, Transform root)
    {
        var red = PickRole(t, Role.Round, REDC, 1.6f) ?? PickRole(t, Role.Block, REDC);
        var white = PickRoleOther(t, Role.Round, red, WHITEC, 1.6f);
        var light = PickRoleOther(t, Role.Round, white, GOLD, 1.2f);
        if (red == null || white == null) return;
        float u = 0.85f;
        int floors = 7; float baseRad = 1.25f;
        float y2 = 0f;   // kümülatif yükseklik (red/white farklı boyda olabilir)
        for (int f = 0; f < floors; f++)
        {
            var pf = (f % 2 == 0) ? red : white;             // şerit deseni
            float sc = ScaleTo(pf, u);
            float rr = Mathf.Lerp(baseRad, 0.55f, f / (float)(floors - 1));
            int n = Mathf.Max(4, Mathf.RoundToInt(2f * Mathf.PI * rr / (u * 0.95f)));
            for (int i = 0; i < n; i++)
            {
                float a = i * (2f * Mathf.PI / n) + f * 0.3f;
                SpawnElevated(pf, c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rr, y2, sc, 0f, root);
            }
            y2 += VStep(pf, sc);
        }
        float topY = y2;
        // FENER ODASI (2026-07-24 detay revizyonu): korkuluklu galeri + çerçeve dikmeleri + İÇERİDE parlak fener + konik çatı
        float wsc = ScaleTo(white, 0.5f);   // küçük çerçeve parçaları
        float gR = 0.85f;
        // galeri tabanı: geniş halka (platform)
        int ng = Mathf.Max(8, Mathf.RoundToInt(2f * Mathf.PI * gR / (Footprint(white) * wsc * 0.9f)));
        for (int i = 0; i < ng; i++)
        {
            float a = i * (2f * Mathf.PI / ng);
            SpawnElevated(white, c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * gR, topY, wsc, 0f, root);
        }
        float galY = topY + VStep(white, wsc);
        // çerçeve dikmeleri: 6 küçük dikme çemberde (fener camı çerçevesi)
        float postH = VStep(white, wsc) * 3f;
        for (int i = 0; i < 6; i++)
        {
            float a = i * (Mathf.PI / 3f);
            Vector2 pp = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (gR * 0.72f);
            for (int k = 0; k < 3; k++)
                SpawnElevated(white, pp, galY + k * VStep(white, wsc), wsc, a * Mathf.Rad2Deg, root);
        }
        // İÇ FENER: merkezde parlak (altın) büyükçe yuvarlak — çerçevenin ortasında net görünür
        SpawnElevated(light ?? red, c, galY + postH * 0.25f, ScaleTo(light ?? red, 1.0f), 0f, root);
        // KONİK ÇATI: 2 daralan kırmızı kat + tepe ucu
        float roofY = galY + postH;
        int nr = 8;
        for (int i = 0; i < nr; i++)
        {
            float a = i * (2f * Mathf.PI / nr);
            SpawnElevated(red, c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (gR * 0.6f), roofY, ScaleTo(red, 0.45f), 0f, root);
        }
        SpawnElevated(red, c, roofY + VStep(red, ScaleTo(red, 0.45f)), ScaleTo(red, 0.55f), 0f, root);   // tepe
    }

    // KAPADOKYA — konik peri bacası (şapkalı) + YANINDA HAVADA sıcak hava balonu (büyük yuvarlak + sepet).
    void LM_Cappadocia(List<(GameObject prefab, int stack, float scale)> t, Vector2 c, Transform root)
    {
        var rock = PickRole(t, Role.Block, new Color(0.78f, 0.66f, 0.5f));
        var cap = PickRole(t, Role.Flat, GRAYC) ?? PickRoleOther(t, Role.Block, rock, GRAYC);
        var balloon = PickRole(t, Role.Round, REDC);
        if (rock == null) return;
        float u = 0.6f; float sc = ScaleTo(rock, u);        // küçük parçalar (detay)
        float rstep = VStep(rock, sc);
        float rfp = Mathf.Max(0.18f, Footprint(rock) * sc);
        // PERİ BACASI (2026-07-24 revizyon): MANTAR profili — geniş gövde → İNCE BOYUN → üstte GENİŞ ŞAPKA (kaya).
        foreach (float bx in new[] { -1.7f, 1.5f })
        {
            Vector2 bc = c + new Vector2(bx, bx * 0.3f);
            int floors = bx < 0 ? 7 : 6;
            for (int f = 0; f < floors; f++)
            {
                float tt = f / (float)(floors - 1);
                // profil: taban 0.8 → boyun 0.22 (üstte iyice incelir; SmoothStep ile mantar boynu)
                float rr = Mathf.Lerp(0.8f, 0.22f, Mathf.SmoothStep(0f, 1f, tt * 1.15f));
                float y = f * rstep;
                int n = Mathf.Max(3, Mathf.RoundToInt(2f * Mathf.PI * Mathf.Max(rr, 0.15f) / (rfp * 0.9f)));
                for (int i = 0; i < n; i++)
                {
                    float a = i * (2f * Mathf.PI / n) + f * 0.5f;
                    SpawnElevated(rock, bc + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rr, y, sc, 0f, root);
                }
            }
            // ŞAPKA: ince boynun üstünde GENİŞ kaya katı (halka + üstünde tek büyük parça = mantar kafası)
            float capY = floors * rstep;
            var capPf = cap ?? rock;
            float capSc = ScaleTo(capPf, 0.85f);
            for (int i = 0; i < 5; i++)
            {
                float a = i * (2f * Mathf.PI / 5f);
                SpawnElevated(capPf, bc + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 0.45f, capY, capSc, a * Mathf.Rad2Deg, root);
            }
            SpawnElevated(capPf, bc, capY + VStep(capPf, capSc), capSc * 1.2f, 30f, root);   // tepe kayası
        }
        // SICAK HAVA BALONU (2026-07-24 revizyon): KÜRE KABUK zarf (küçük yuvarlaklardan halkalar — armut profili),
        // altında İNCE İPLER (küçük parçalarla 4 çizgi) + hasır SEPET. Daha büyük + detaylı.
        if (balloon != null)
        {
            Vector2 bp = c + new Vector2(0.1f, -1.4f);
            float bsc = ScaleTo(balloon, 0.55f);            // kabuk parçaları küçük
            float bfp = Mathf.Max(0.16f, Footprint(balloon) * bsc);
            float BR = 1.55f;                                // zarf yarıçapı (BÜYÜK balon)
            float centerY = 7.2f;
            // zarf: -60°..+90° enlem halkaları (altta armut gibi daralır, üstte kapanır)
            int lats = 7;
            for (int L = 0; L < lats; L++)
            {
                float phi = Mathf.Lerp(-1.05f, 1.45f, L / (float)(lats - 1));   // radyan enlem
                float rr = BR * Mathf.Cos(phi);
                if (rr < 0.12f) continue;
                float y = centerY + BR * Mathf.Sin(phi);
                int n = Mathf.Max(3, Mathf.RoundToInt(2f * Mathf.PI * rr / (bfp * 0.95f)));
                for (int i = 0; i < n; i++)
                {
                    float a = i * (2f * Mathf.PI / n) + L * 0.3f;
                    SpawnElevated(balloon, bp + new Vector2(Mathf.Cos(a) * rr, Mathf.Sin(a) * rr), y - ObjHeight(balloon) * bsc * 0.5f, bsc, 0f, root);
                }
            }
            SpawnElevated(balloon, bp, centerY + BR, bsc, 0f, root);   // tepe kapama
            // SEPET: zarfın altında küçük hasır kutu (2x2 blok + taban)
            var basket = PickRoleOther(t, Role.Block, balloon, new Color(0.5f, 0.36f, 0.2f), 1.0f);
            float bottomY = centerY - BR * 0.87f;            // zarfın alt ağzı
            float basketY = bottomY - 1.15f;
            if (basket != null)
            {
                float ksc = ScaleTo(basket, 0.5f);
                float kg = Footprint(basket) * ksc * 0.55f;
                int[] sg = { -1, 1 };
                foreach (int i in sg) foreach (int j in sg)
                    SpawnElevated(basket, bp + new Vector2(i * kg, j * kg), basketY, ksc, 0f, root);
                // İPLER: sepet köşelerinden zarf ağzına 4 ince çizgi (en küçük parçadan 2'şer nokta)
                var rope = PickRole(t, Role.Column, new Color(0.4f, 0.3f, 0.2f), 0.9f) ?? basket;
                float rsc2 = ScaleTo(rope, 0.55f);
                foreach (int i in sg) foreach (int j in sg)
                {
                    Vector3 from = new(bp.x + i * kg, basketY + 0.35f, bp.y + j * kg);
                    Vector3 to = new(bp.x + i * BR * 0.4f, bottomY, bp.y + j * BR * 0.4f);
                    Vector3 mid = (from + to) * 0.5f;
                    SpawnExactRot(rope, mid, AlignEuler(rope, to - from), rsc2, root);
                }
            }
        }
    }

    // TORII KAPISI — 2 KIRMIZI dikey kolon + üstte 2 yatay kiriş (üstteki uçları taşar).
    // 2026-07-24 revizyon (kullanıcı): tek uzun nesne yerine ÇOK SAYIDA KÜÇÜK KIRMIZI parça (kedilerde de çalışır)
    // + tam anatomi: nuki (alt kiriş), gakuzuka (orta dikme), daiwa (kolon başlığı), kasagi (üst kiriş, uçlar kalkık).
    void LM_ToriiGate(List<(GameObject prefab, int stack, float scale)> t, Vector2 c, Transform root)
    {
        var piece = PickRole(t, Role.Block, REDC, 1.4f) ?? PickRole(t, Role.Round, REDC, 1.4f);
        if (piece == null) return;
        float u = 0.5f; float sc = ScaleTo(piece, u);       // KÜÇÜK parçalar → kolon dokusu
        float step = VStep(piece, sc);
        float fp = Mathf.Max(0.18f, Footprint(piece) * sc);
        float half = 1.9f;                                   // kolonlar arası yarı-açıklık
        float colH = Mathf.Max(2.6f, step * 8f);             // kolon boyu (en az 8 parça)

        // 2 ana kolon: küçük parçaların dikey istifi (hafif İÇE eğik — gerçek Torii'de kolonlar içe yatar)
        int nCol = Mathf.CeilToInt(colH / step);
        foreach (float sx in new[] { -half, half })
        {
            float leanDir = -Mathf.Sign(sx) * 0.05f;         // içe eğim
            for (int k = 0; k < nCol; k++)
                SpawnElevated(piece, c + new Vector2(sx + k * leanDir, 0f), k * step, sc, 0f, root);
            // daiwa: kolon başlığı (bir boy büyük parça)
            SpawnElevated(piece, c + new Vector2(sx + nCol * leanDir, 0f), nCol * step, sc * 1.45f, 0f, root);
        }
        float daiwaTop = nCol * step + step * 1.5f;

        // NUKI (alt kiriş): kolonların ~%76 yüksekliğinde, uçları hafif taşan yatay parça sırası
        float nukiY = colH * 0.76f;
        int nNuki = Mathf.CeilToInt(2f * (half + 0.35f) / fp);
        for (int i = 0; i <= nNuki; i++)
        {
            float x = Mathf.Lerp(-half - 0.35f, half + 0.35f, i / (float)nNuki);
            SpawnElevated(piece, c + new Vector2(x, 0f), nukiY, sc, 0f, root);
        }
        // GAKUZUKA: nuki ile kasagi arasında ORTA dikme
        for (float gy = nukiY + step; gy < daiwaTop; gy += step)
            SpawnElevated(piece, c, gy, sc, 0f, root);

        // SHIMAKI + KASAGI: çift üst kiriş — alt sıra kolon genişliği; üst sıra DAHA UZUN + UÇLARI KALKIK (ikonik)
        float beamY = daiwaTop;
        int nB1 = Mathf.CeilToInt(2f * (half + 0.5f) / fp);
        for (int i = 0; i <= nB1; i++)
        {
            float x = Mathf.Lerp(-half - 0.5f, half + 0.5f, i / (float)nB1);
            SpawnElevated(piece, c + new Vector2(x, 0f), beamY, sc, 0f, root);
        }
        float kasagiY = beamY + step * 0.72f;                 // shimaki'nin hemen üstü (boşluksuz)
        float ext = half + 1.0f;                              // üst kiriş uzun (taşan uçlar)
        int nB2 = Mathf.CeilToInt(2f * ext / fp);
        for (int i = 0; i <= nB2; i++)
        {
            float x = Mathf.Lerp(-ext, ext, i / (float)nB2);
            float lift = Mathf.Pow(Mathf.Abs(x) / ext, 3f) * 0.55f;   // uçlara doğru YUKARI kalkış
            float tilt = Mathf.Sign(x) * lift * 40f;
            SpawnExactRot(piece, new Vector3(c.x + x, kasagiY + lift + ObjHeight(piece) * sc * 0.5f, c.y),
                          new Vector3(0f, 0f, tilt), sc, root);
        }
    }

    // STONEHENGE — çemberde dikey taş çiftleri + üst lento (yatay); birkaç eksik (yıkık görünüm).
    void LM_Stonehenge(List<(GameObject prefab, int stack, float scale)> t, Vector2 c, Transform root)
    {
        // 2026-07-24 revizyon (kullanıcı): DİKDÖRTGEN nesnelerden, gerçek Stonehenge oranları — sarsen dikmeler
        // GENİŞLİĞİNDEN ~2 KAT YÜKSEK (küçük blokların 3'lü istifi), üstte yatay lento; dış halka 6 trilithon +
        // İÇ AT NALI 3 daha yüksek trilithon (gerçek yerleşim). Her dikme tek nesne değil İSTİF → oran doğru.
        var stone = PickRole(t, Role.Block, GRAYC, 1.6f);
        if (stone == null) return;
        float u = 0.68f; float sc = ScaleTo(stone, u);      // küçük bloklar
        float step = VStep(stone, sc);
        float fp = Mathf.Max(0.2f, Footprint(stone) * sc);

        // Trilithon kurucu: 2 dikme (blok istifi, 'tiers' kat) + üstte lento sırası (yatay bloklar)
        void Trilithon(Vector2 p, float ang, int tiers, float scMul)
        {
            Vector2 tangent = new(-Mathf.Sin(ang), Mathf.Cos(ang));
            float g = fp * scMul * 0.85f;                    // dikmeler arası (lento taşacak kadar dar)
            foreach (float s in new[] { -g, g })
            {
                Vector2 q = p + tangent * s;
                for (int k = 0; k < tiers; k++)
                    SpawnElevated(stone, q, k * step * scMul, sc * scMul, ang * Mathf.Rad2Deg, root);
            }
            // lento: dikme tepesinde TEĞET yönlü 3 blok (uçlar dikmelerden hafif taşar)
            float ly = tiers * step * scMul;
            for (int i = -1; i <= 1; i++)
                SpawnElevated(stone, p + tangent * (i * g), ly, sc * scMul, ang * Mathf.Rad2Deg + 90f, root);
        }

        // DIŞ HALKA: 6 trilithon (2 boşluk = yıkık kısımlar, gerçekte de eksik)
        float R = 3.9f; int n = 8;
        for (int i = 0; i < n; i++)
        {
            if (i == 3 || i == 6) continue;
            float a = i * (2f * Mathf.PI / n);
            Trilithon(c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * R, a, 3, 1f);
        }
        // İÇ AT NALI: 3 DAHA YÜKSEK trilithon (merkez etrafında açık U — açık ucu kameraya/-Z'ye bakar)
        float r2 = 1.7f;
        foreach (float a in new[] { Mathf.PI * 0.5f, Mathf.PI * 0.9f, Mathf.PI * 0.1f })
            Trilithon(c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r2, a, 4, 1.05f);
        // Birkaç devrik taş (ikonik dağınıklık)
        SpawnElevated(stone, c + new Vector2(R * 0.7f, -R * 0.55f), 0f, sc * 1.1f, 40f, root);
        SpawnElevated(stone, c + new Vector2(-R * 0.6f, -R * 0.5f), 0f, sc, 70f, root);
    }

    // ZAFER TAKI — 2 kalın bacak + üst blok + iç kemer boşluğu.
    void LM_ArcTriomphe(List<(GameObject prefab, int stack, float scale)> t, Vector2 c, Transform root)
    {
        var stone = PickRole(t, Role.Block, WHITEC);
        if (stone == null) return;
        float u = 0.85f; float sc = ScaleTo(stone, u);
        float step = VStep(stone, sc);
        float legHalf = 1.9f;
        float archH = Mathf.Min(3.2f, step * 6f), topH = step * 2.2f;
        // 2 bacak (2x2 kolon)
        foreach (float sx in new[] { -legHalf, legHalf })
            for (float y = 0f; y < archH; y += step)
                foreach (float dz in new[] { -u * 0.5f, u * 0.5f })
                    SpawnElevated(stone, c + new Vector2(sx, dz), y, sc, 0f, root);
        // Kemer kavisi: bacak içlerinden yukarı-ortaya (parça yay)
        int na = 5;
        for (int i = 0; i < na; i++)
        {
            float uu = i / (na - 1f) * 2f - 1f;
            float x = uu * (legHalf - u * 0.4f);
            float y = archH - 0.2f + (1f - uu * uu) * 0.7f;
            SpawnExactRot(stone, new Vector3(c.x + x, y, c.y), new Vector3(0f, 0f, -uu * 35f), sc * 0.8f, root);
        }
        // Üst blok: tam genişlik 2 sıra
        float topBase = archH + 0.9f;
        for (float y = topBase; y < topBase + topH; y += step)
        {
            int nb = Mathf.CeilToInt(2f * (legHalf + u) / u);
            for (int i = 0; i <= nb; i++)
            {
                float x = Mathf.Lerp(-legHalf - u * 0.5f, legHalf + u * 0.5f, i / (float)nb);
                SpawnElevated(stone, c + new Vector2(x, 0f), y, sc, 0f, root);
            }
        }
    }

    // YEL DEĞİRMENİ — konik gövde + tepede 4 ÇAPRAZ kanat (X biçimi, dikey düzlemde).
    void LM_Windmill(List<(GameObject prefab, int stack, float scale)> t, Vector2 c, Transform root)
    {
        var body = PickRole(t, Role.Block, new Color(0.6f, 0.45f, 0.3f));
        var blade = PickRole(t, Role.Column, WHITEC);
        if (body == null) return;
        float u = 0.85f; float sc = ScaleTo(body, u);
        float step = VStep(body, sc);
        int floors = 6; float baseRad = 1.3f;
        for (int f = 0; f < floors; f++)
        {
            float rr = Mathf.Lerp(baseRad, 0.6f, f / (float)(floors - 1));
            float y = f * step;
            int n = Mathf.Max(4, Mathf.RoundToInt(2f * Mathf.PI * rr / (u * 0.9f)));
            for (int i = 0; i < n; i++)
            {
                float a = i * (2f * Mathf.PI / n) + f * 0.4f;
                SpawnElevated(body, c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rr, y, sc, 0f, root);
            }
        }
        // KANATLAR (2026-07-24 BAŞTAN — kullanıcı: kollar/yapraklar olmamış): 4 kanat, her kanat = göbekten dışa
        // KÜÇÜK PARÇA DİZİSİ (4 parça kol) + ucunda 2 GENİŞ parça (yaprak/pala). Tek döndürülmüş uzun nesne yok →
        // pivot/eksen sorunu yok, yelkovan gibi net okunur. Dikey düzlemde, gövdenin kamera (-Z) tarafında.
        {
            float hubY = floors * step + 0.35f;
            float zFront = c.y - 0.95f;
            var arm = PickRole(t, Role.Block, new Color(0.55f, 0.4f, 0.25f), 1.2f) ?? body;
            var sail = blade ?? PickRole(t, Role.Flat, WHITEC, 1.6f) ?? arm;
            float armSc = ScaleTo(arm, 0.42f);
            float bl = 2.5f;
            for (int i = 0; i < 4; i++)
            {
                float rad = (45f + i * 90f) * Mathf.Deg2Rad;
                Vector3 dir = new(Mathf.Cos(rad), Mathf.Sin(rad), 0f);
                // kol: 4 küçük parça merkezden dışa
                for (int s2 = 1; s2 <= 4; s2++)
                {
                    float rr = bl * s2 / 5f;
                    SpawnExactRot(arm, new Vector3(c.x + dir.x * rr, hubY + dir.y * rr, zFront), Vector3.zero, armSc, root);
                }
                // yaprak/pala: kol ucunda 2 daha geniş BEYAZ parça (pervane hissi)
                float ssc = ScaleTo(sail, 0.75f);
                SpawnExactRot(sail, new Vector3(c.x + dir.x * bl, hubY + dir.y * bl, zFront),
                              AlignEuler(sail, dir), ssc, root);
                SpawnExactRot(sail, new Vector3(c.x + dir.x * (bl + 0.55f), hubY + dir.y * (bl + 0.55f), zFront),
                              AlignEuler(sail, dir), ssc, root);
            }
            // göbek
            SpawnExactRot(PickRole(t, Role.Round, null, 1.0f) ?? body, new Vector3(c.x, hubY, zFront), Vector3.zero, sc * 1.0f, root);
        }
    }

    // BAZİLİKA (Vatikan) — geniş taban + merkez BÜYÜK KUBBE (yarımküre yuvarlaklar) + 2 yan küçük kubbe + kolonlu cephe.
    void LM_Basilica(List<(GameObject prefab, int stack, float scale)> t, Vector2 c, Transform root)
    {
        var wall = PickRole(t, Role.Block, WHITEC);
        var dome = PickRole(t, Role.Round, GOLD, 1.7f);
        var col = PickRole(t, Role.Column, WHITEC);
        if (wall == null) return;
        float u = 0.85f; float sc = ScaleTo(wall, u);
        float wstep = VStep(wall, sc);
        // Taban: geniş dikdörtgen duvar (2 kat çerçeve)
        float hx = 3.4f, hz = 1.7f;
        for (int L = 0; L < 2; L++)
        {
            float y = L * wstep;
            int nx = Mathf.CeilToInt(2f * hx / u), nz = Mathf.CeilToInt(2f * hz / u);
            for (int i = 0; i <= nx; i++)
            {
                float x = Mathf.Lerp(-hx, hx, i / (float)nx);
                SpawnElevated(wall, c + new Vector2(x, -hz), y, sc, 0f, root);
                SpawnElevated(wall, c + new Vector2(x,  hz), y, sc, 0f, root);
            }
            for (int i = 1; i < nz; i++)
            {
                float z = Mathf.Lerp(-hz, hz, i / (float)nz);
                SpawnElevated(wall, c + new Vector2(-hx, z), y, sc, 0f, root);
                SpawnElevated(wall, c + new Vector2( hx, z), y, sc, 0f, root);
            }
        }
        // Merkez kubbe (2026-07-24 detay revizyonu): önce KASNAK (dikey silindirik duvar), üstüne SIK yarımküre
        // kubbe (küçük yuvarlaklar), tepede fener + altın topuz. Parça sayısı artırıldı → net kubbe silüeti.
        if (dome != null)
        {
            float dsc = ScaleTo(dome, 0.62f);               // KÜÇÜK parçalar (1.0→0.62) → sık örgü
            float dfp = Mathf.Max(0.18f, Footprint(dome) * dsc);
            float DR = 1.9f;
            float domeBase = 2f * wstep;
            // KASNAK: 2 kat dikey halka (kubbenin oturduğu silindir)
            for (int k = 0; k < 2; k++)
            {
                float y = domeBase + k * VStep(dome, dsc);
                int n = Mathf.Max(8, Mathf.RoundToInt(2f * Mathf.PI * DR / (dfp * 0.9f)));
                for (int i = 0; i < n; i++)
                {
                    float a = i * (2f * Mathf.PI / n) + k * 0.25f;
                    SpawnElevated(dome, c + new Vector2(Mathf.Cos(a) * DR, Mathf.Sin(a) * DR), y, dsc, 0f, root);
                }
            }
            float drumTop = domeBase + 2f * VStep(dome, dsc);
            // KUBBE: 6 SIK halka (küçük parçalar; halka başına çevre/ayak-izi kadar nesne)
            int rings = 6;
            for (int r = 0; r < rings; r++)
            {
                float phi = (r + 0.5f) / rings * (Mathf.PI * 0.5f);
                float rr = DR * Mathf.Cos(phi);
                float y = drumTop + DR * Mathf.Sin(phi) * 0.75f;
                int n = Mathf.Max(3, Mathf.RoundToInt(2f * Mathf.PI * rr / (dfp * 0.9f)));
                for (int i = 0; i < n; i++)
                {
                    float a = i * (2f * Mathf.PI / n) + r * 0.35f;
                    SpawnElevated(dome, c + new Vector2(Mathf.Cos(a) * rr, Mathf.Sin(a) * rr), y, dsc, 0f, root);
                }
            }
            float domeTop = drumTop + DR * 0.8f;
            SpawnElevated(dome, c, domeTop, dsc * 1.1f, 0f, root);                       // fener
            SpawnElevated(PickRoleOther(t, Role.Round, dome, GOLD, 0.8f) ?? dome, c, domeTop + VStep(dome, dsc), dsc * 0.8f, 0f, root);  // altın topuz
            // 2 yan küçük kubbe: 2'şer halkalı mini kubbeler
            foreach (float sx in new[] { -hx * 0.72f, hx * 0.72f })
            {
                for (int r = 0; r < 2; r++)
                {
                    float phi = (r + 0.5f) / 2f * (Mathf.PI * 0.5f);
                    float rr = 0.7f * Mathf.Cos(phi);
                    float y = domeBase + 0.7f * Mathf.Sin(phi) * 0.7f;
                    int n = Mathf.Max(3, Mathf.RoundToInt(2f * Mathf.PI * rr / (dfp * 0.9f)));
                    for (int i = 0; i < n; i++)
                    {
                        float a = i * (2f * Mathf.PI / n);
                        SpawnElevated(dome, c + new Vector2(sx + Mathf.Cos(a) * rr, Mathf.Sin(a) * rr), y, dsc, 0f, root);
                    }
                }
                SpawnElevated(dome, c + new Vector2(sx, 0f), domeBase + 0.55f, dsc, 0f, root);   // mini tepe
            }
        }
        // ÖN CEPHE (kameraya bakan -Z): kolonlu giriş + üstünde ÜÇGEN ALINLIK (küçük bloklar)
        if (col != null)
        {
            float csc = ScaleTo(col, 1.6f);
            Vector3 up = UprightEuler(col);
            for (int i = 0; i < 6; i++)
            {
                float x = Mathf.Lerp(-hx * 0.62f, hx * 0.62f, i / 5f);
                SpawnElevatedRot(col, c + new Vector2(x, -hz - 0.7f), 0f, csc, up, root);
            }
        }
        // Alınlık: kolonların üstünde daralan 3 blok sırası (üçgen)
        float pedY = 1.7f;
        for (int L = 0; L < 3; L++)
        {
            float ph = hx * 0.62f * (1f - L / 3f);
            int np = Mathf.Max(1, Mathf.CeilToInt(2f * ph / (Footprint(wall) * sc * 0.95f)));
            for (int i = 0; i <= np; i++)
            {
                float x = Mathf.Lerp(-ph, ph, np == 0 ? 0.5f : i / (float)np);
                SpawnElevated(wall, c + new Vector2(x, -hz - 0.7f), pedY + L * wstep, sc * 0.85f, 0f, root);
            }
        }
    }
}
