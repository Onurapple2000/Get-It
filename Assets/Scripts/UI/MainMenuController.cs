using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// Ana menü (Sprint 3, Faz A/B) — iki ekran:
///  • LEVEL MENÜSÜ (ev): warm arka plan + maskot, üstte SEÇİLİ DÜNYA ikonu/adı, altta 5 duraklı PATİKA
///    (level durakları: bitti ✓ / açık / kilitli; ilerleme PlayerPrefs'ten). Durak seç → o level'ı başlat.
///  • DÜNYALAR: 18 dünya ikonu ızgarası (kilitli/açık). Dünya seç → o dünyanın level menüsü.
/// Sprite referansları MainMenuBuilder (editor) atar. Can göstergesi ayrı LivesHud.
/// </summary>
public class MainMenuController : MonoBehaviour
{
    [Header("Akış")]
    public string gameScene = "GameScene";

    [Header("Görseller (MainMenuBuilder atar)")]
    public Sprite bgSprite;
    public Sprite moleSprite;
    public Sprite buttonSprite;
    public Sprite[] worldIcons;          // 18, WorldCatalog sırasıyla

    static int SelectedWorld = 0;

    Canvas canvas;
    GameObject levelPanel, worldsPanel, levelContent;
    float _pathTopOffset = 760f;   // patika alanı üst ofseti — dünya ikonunun (responsive boyut) altına göre hesaplanır
    Sprite lockSprite;
    TMP_Text infoText;
    TMP_Text starBadgeText;        // sol üst toplam yıldız sayısı (uçan yıldızlar buraya girince artar)
    RectTransform starBadgeIcon;   // uçan yıldızların hedefi

    void Start()
    {
        canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null) { Debug.LogWarning("[MainMenu] Canvas yok."); return; }
        lockSprite = Resources.Load<Sprite>("burrow_lock_icon");

        StarRewards.CheckAndGrant();   // bekleyen yıldız hediyelerini ver (idempotent güvenlik ağı)
        SelectedWorld = Mathf.Clamp(LevelManager.CurrentWorld, 0, WorldCatalog.Count - 1);   // oynanan dünyanın patikası
        AudioManager.Instance?.PlayMenuMusic();   // ana menü müziği (dosya yoksa prosedürel)

        BuildBackground();
        BuildLevelPanel();
        BuildWorldsPanel();
        ShowLevelMenu();

        // Levellar arası: ödül özeti (yıldızlar) + dünyanın SON level'ı bitirildiyse dünya geçiş animasyonu.
        if (LevelResult.Pending)
        {
            int fw = LevelResult.World, fl = LevelResult.Level, nextW = WorldCatalog.NextWorld(fw);   // SIRADA sonraki (Order)
            bool reveal = (fl + 1 >= WorldCatalog.PlayableLevels(fw))            // son level bitti mi
                          && nextW >= 0 && WorldCatalog.HasContent(nextW)        // sonraki dünya içerikli mi
#if !UNITY_EDITOR
                          && PlayerPrefs.GetInt(WorldRevealKey(nextW), 0) == 0   // release: bir kez göster
#endif
                          ;
            StartCoroutine(PostLevelSequence(reveal ? nextW : -1, fw));
        }
    }

    // ════════ ARKA PLAN ════════
    void BuildBackground()
    {
        var bg = NewImage("Bg", canvas.transform, bgSprite);
        Stretch(bg.rectTransform);
        bg.raycastTarget = false;
        if (bgSprite == null) bg.color = new Color(0.22f, 0.15f, 0.11f);
    }

    // ════════ LEVEL MENÜSÜ ════════
    void BuildLevelPanel()
    {
        levelPanel = NewPanel("LevelPanel");
        // maskot (sağ-alt köşe)
        if (moleSprite != null)
        {
            var mole = NewImage("Mole", levelPanel.transform, moleSprite);
            mole.preserveAspect = true; mole.raycastTarget = false;
            var mr = mole.rectTransform;
            mr.anchorMin = mr.anchorMax = new Vector2(1f, 0f); mr.pivot = new Vector2(1f, 0f);
            mr.anchoredPosition = new Vector2(-8, 4); mr.sizeDelta = new Vector2(360, 360);
        }
        // değişen içerik (dünya ikonu + patika + butonlar + rozet) bu konteynerde — GÜVENLİ ALAN'a oturur
        // (çentik/kenar dışı; arka plan ve maskot levelPanel'de tam-ekran kalır).
        levelContent = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>().gameObject;
        var cr = (RectTransform)levelContent.transform; cr.SetParent(levelPanel.transform, false); Stretch(cr);
        levelContent.AddComponent<SafeArea>();

        // bilgi/uyarı satırı
        infoText = NewText("Info", levelPanel.transform, 30, FontStyles.Bold, TextAlignmentOptions.Center);
        infoText.color = new Color(1f, 0.55f, 0.45f);
        var ir = infoText.rectTransform;
        ir.anchorMin = ir.anchorMax = new Vector2(0.5f, 0f); ir.pivot = new Vector2(0.5f, 0f);
        ir.anchoredPosition = new Vector2(0, 30); ir.sizeDelta = new Vector2(900, 44); infoText.text = "";
    }

    void RebuildLevelContent()
    {
        foreach (Transform c in levelContent.transform) Destroy(c.gameObject);
        int w = SelectedWorld;

        // Üst-orta başlık: dünya adı ("Dünyalar" sol + "Devam Et" sağ butonlarıyla aynı hizada, aralarında).
        var name = NewText("WorldName", levelContent.transform, 56, FontStyles.Bold, TextAlignmentOptions.Center);
        name.text = WorldCatalog.Names[w]; name.color = new Color(1f, 0.95f, 0.75f);
        name.enableAutoSizing = true; name.fontSizeMin = 30; name.fontSizeMax = 60;   // dar ekranda taşmasın
        var nr = name.rectTransform;
        nr.anchorMin = new Vector2(0f, 1f); nr.anchorMax = new Vector2(1f, 1f); nr.pivot = new Vector2(0.5f, 1f);
        nr.offsetMin = new Vector2(346, -214); nr.offsetMax = new Vector2(-346, -110);   // butonlar arası şerit

        // Dünya ikonu: ÜST butonlar/başlık ile PATİKA arasındaki boşlukta DİKEY ORTALANIR (kullanıcı 2026-07-24).
        // Patika bölgesi ikondan BAĞIMSIZ: ekran yüksekliğinin ~%52'sinden başlar (alt ~%48 patika → yeri korunur,
        // responsive). İkon boyutu ekrana oranlı (genişlik %88 & boşluk %90 & max 980) → çakışmaz, her oranda orantılı.
        const float topLine = 244f;   // başlık/butonların altı
        Canvas.ForceUpdateCanvases();
        var lcRT = (RectTransform)levelContent.transform;
        float H = lcRT.rect.height;   if (H < 400f) H = 1920f;
        float safeW = lcRT.rect.width; if (safeW < 200f) safeW = 1080f;
        _pathTopOffset = Mathf.Max(H * 0.52f, 900f);   // patika alanı üstü (BuildPath bunu kullanır)
        float gap = _pathTopOffset - topLine;
        if (worldIcons != null && w < worldIcons.Length && worldIcons[w] != null)
        {
            float iconSize = Mathf.Min(safeW * 0.88f, gap * 0.9f, 980f);
            var icon = NewImage("WorldIcon", levelContent.transform, worldIcons[w]);
            icon.preserveAspect = true; icon.raycastTarget = false;
            var rt = icon.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f); rt.pivot = new Vector2(0.5f, 0.5f);   // pivot MERKEZ → dikey ortala
            rt.anchoredPosition = new Vector2(0, -(topLine + gap * 0.5f));                              // boşluğun dikey ortası
            rt.sizeDelta = new Vector2(iconSize, iconSize);
        }

        // Sol üst: toplam yıldız rozeti (oyun harici ekranlarda oyuncunun biriktirdiği yıldız)
        MakeStarBadge(levelContent.transform);

        // "Dünyalar" butonu (sol-üst, world_mixed ikonlu)
        MakeWorldsButton();

        // "Devam Et" — oynanacak sıradaki level'a atlar (sağ üst)
        MakeContinueButton();

        BuildPath(w);
    }

    // "Devam Et": seçili dünyada bir sonraki oynanacak level (kilitli sınırda klamplenir). Yoksa gizli.
    void MakeContinueButton()
    {
        int target = ResumeLevel(SelectedWorld);
        if (target < 0) return;   // oynanabilir level yok (kilitli dünya)
        // Sağ üst köşe ("Dünyalar" butonu sol üstte → simetrik).
        var b = UiButtons.Build(levelContent.transform, new Vector2(1f, 1f), new Vector2(-26, -110),
            new Vector2(300, 104), "Devam Et", UiButtons.Play(), new Color(0.88f, 1f, 0.88f), 32);
        b.onClick.AddListener(() => Play(SelectedWorld, target));
    }

    // Sıradaki oynanacak level indeksi: açık (unlocked) level; hepsi bittiyse son level (tekrar). -1 = oynanamaz.
    int ResumeLevel(int world)
    {
        int playable = WorldCatalog.PlayableLevels(world);
        if (playable <= 0) return -1;
        return Mathf.Clamp(LevelManager.UnlockedIndex(world), 0, playable - 1);
    }

    // Kaydırmasız YILAN (serpentine) patika: N durak, "PathArea" alanını ölçüp tüm alana eşit yayar → hepsi
    // ekranda görünür (kaydırma yok). PathArea responsive anchorlı (resmin altı ↔ ekran altı) → tüm ekran
    // boyutlarında oran korunur, başlık/resim/buton ile asla örtüşmez. Açık=beyaz, kilitli=kırmızı+kilit.
    void BuildPath(int world)
    {
        int n = WorldCatalog.PlayableLevels(world);
        if (n <= 0) return;
        int unlocked = LevelManager.UnlockedIndex(world);

        const int perRow = 5;
        int rows = Mathf.CeilToInt(n / (float)perRow);

        // Patika alanı: büyütülen dünya resminin altından ekran altına kadar (responsive; üst/alt sabit ofset).
        // 2026-07-24: üst ofset 620→760 (ikon 480'e büyüdü, çakışmasın).
        var areaGo = new GameObject("PathArea", typeof(RectTransform));
        var area = (RectTransform)areaGo.transform; area.SetParent(levelContent.transform, false);
        area.anchorMin = new Vector2(0.5f, 0f); area.anchorMax = new Vector2(0.5f, 1f); area.pivot = new Vector2(0.5f, 0.5f);
        area.offsetMin = new Vector2(-520, 40);            // sol/alt: genişlik 1040, alttan 40
        area.offsetMax = new Vector2(520, -_pathTopOffset); // sağ/üst: büyük ikonun altından DİNAMİK (responsive)

        Canvas.ForceUpdateCanvases();   // rect boyutunu bu cihazda ölç
        float W = area.rect.width  > 10f ? area.rect.width  : 1040f;
        float H = area.rect.height > 10f ? area.rect.height : 1120f;
        float xStep = W / perRow;
        // BASIK PATİKA (kullanıcı 2026-07-24): satır aralığını sınırla → satırlar birbirine yakın, patika kısa.
        // Alanı tamamen doldurmak yerine kompakt blok; alan MERKEZinde biraz AŞAĞI biasla (yBias) → aşağıya basık.
        float yStep = Mathf.Min(H / rows, 250f);
        float blockH = yStep * rows;
        float yBias = -Mathf.Min(60f, (H - blockH) * 0.35f);   // kompakt bloğu hafif aşağı kaydır (çakışmadan)

        // Durak konumu (area MERKEZ-orijinli): 5'li satır, tek satırlar ters (yılan) → dönüşler dikey bağlanır.
        Vector2 Pos(int i)
        {
            int r = i / perRow, c = i % perRow;
            if ((r & 1) == 1) c = perRow - 1 - c;
            float x = -W * 0.5f + (c + 0.5f) * xStep;
            float y = blockH * 0.5f - (r + 0.5f) * yStep + yBias;   // kompakt blok, alan merkezinde + hafif aşağı
            return new Vector2(x, y);
        }

        // Yol çizgileri (ardışık duraklar arası): koyu dış hat + açık iç dolgu.
        for (int i = 0; i < n - 1; i++)
        {
            Line(area, Pos(i), Pos(i + 1), new Color(0.33f, 0.24f, 0.14f, 0.95f), 30f);
            Line(area, Pos(i), Pos(i + 1), new Color(0.93f, 0.83f, 0.56f, 1f), 18f);
        }

        // Duraklar
        for (int i = 0; i < n; i++)
        {
            bool open = i == unlocked;
            bool locked = i > unlocked;
            bool hard = WorldCatalog.IsHard(world, i);   // her 10. level → HARD (kırmızı-siyah durak)

            var go = new GameObject($"Stop{i + 1}", typeof(RectTransform), typeof(Image), typeof(Button));
            var rt = (RectTransform)go.transform; rt.SetParent(area, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f); rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Pos(i); rt.sizeDelta = new Vector2(hard ? 150 : 132, hard ? 114 : 100);   // hard biraz büyük
            var img = go.GetComponent<Image>();
            img.sprite = UiButtons.Circle();
            if (hard) img.color = locked ? new Color(0.5f, 0.09f, 0.09f) : new Color(0.82f, 0.11f, 0.11f);
            else      img.color = locked ? new Color(1f, 0.5f, 0.45f) : Color.white;

            var num = NewText("Num", rt, hard ? 54 : 50, FontStyles.Bold, TextAlignmentOptions.Center);
            num.text = (i + 1).ToString();
            num.color = hard ? new Color(1f, 0.92f, 0.5f) : Color.white;   // hard rakam: sarımsı
            Stretch(num.rectTransform);

            if (locked) Badge(rt, lockSprite, Color.white, 96);

            var btn = go.GetComponent<Button>();
            btn.transition = Selectable.Transition.None;
            int idx = i;
            if (locked) btn.interactable = false;
            else btn.onClick.AddListener(() => { AudioManager.Instance?.PlayUiClick(); Play(world, idx); });

            if (open) go.AddComponent<Pulse>();   // sıradaki durak nabız
        }
    }

    void Play(int world, int level)
    {
        if (LivesManager.Instance != null && !LivesManager.Instance.HasLife)
        { infoText.text = "Can yok! Yenilenmesini bekle."; return; }
        LevelManager.CurrentWorld = world;   // dünya-bazlı level seti (Sprint 5)
        LevelManager.CurrentIndex = level;
        SceneManager.LoadScene(gameScene);
    }

    // ════════ DÜNYALAR EKRANI ════════
    void BuildWorldsPanel()
    {
        worldsPanel = NewPanel("WorldsPanel");
        var dim = NewImage("Dim", worldsPanel.transform, null); Stretch(dim.rectTransform);
        dim.color = new Color(0f, 0f, 0f, 0.55f); dim.raycastTarget = true;   // karartma tam-ekran

        // Kontroller (başlık/geri/ızgara) GÜVENLİ ALAN'a — çentik/kenar dışı.
        var wsafe = new GameObject("WSafe", typeof(RectTransform), typeof(SafeArea));
        var wsafeRt = (RectTransform)wsafe.transform; wsafeRt.SetParent(worldsPanel.transform, false);
        var worldsSafe = wsafe.transform;

        var title = NewText("Title", worldsSafe, 64, FontStyles.Bold, TextAlignmentOptions.Center);
        title.text = "Dünyalar"; title.color = new Color(1f, 0.95f, 0.75f);
        var tr = title.rectTransform; tr.anchorMin = tr.anchorMax = new Vector2(0.5f, 1f); tr.pivot = new Vector2(0.5f, 1f);
        tr.anchoredPosition = new Vector2(0, -50); tr.sizeDelta = new Vector2(900, 90);

        var back = UiButtons.Build(worldsSafe, new Vector2(0f, 1f), new Vector2(30, -50),
            new Vector2(230, 84), "Geri", null, Color.white, 32);
        back.onClick.AddListener(ShowLevelMenu);

        // Izgara (3 sütun) — RESPONSIVE: hücre boyutu ölçülen güvenli alana göre → 15 dünya ekranı tam kaplar,
        // alt boşluk kalmaz (2026-07-24, kullanıcı isteği).
        var grid = new GameObject("Grid", typeof(RectTransform), typeof(GridLayoutGroup)).GetComponent<RectTransform>();
        grid.SetParent(worldsSafe, false);
        grid.anchorMin = new Vector2(0.5f, 1f); grid.anchorMax = new Vector2(0.5f, 1f); grid.pivot = new Vector2(0.5f, 1f);

        const float topGap = 165f, botGap = 55f, gridW = 1020f, sp = 18f;
        const int cols = 3;
        Canvas.ForceUpdateCanvases();
        float safeH = ((RectTransform)worldsSafe).rect.height;
        if (safeH < 200f) safeH = 1720f;   // ölçüm hazır değilse makul yedek
        int count = WorldCatalog.Order.Length;
        int rows = Mathf.CeilToInt(count / (float)cols);
        float gh = safeH - topGap - botGap;
        float cellW = (gridW - (cols - 1) * sp) / cols;
        float cellH = (gh - (rows - 1) * sp) / rows;

        grid.anchoredPosition = new Vector2(0, -topGap); grid.sizeDelta = new Vector2(gridW, gh);
        var gl = grid.GetComponent<GridLayoutGroup>();
        gl.cellSize = new Vector2(cellW, cellH); gl.spacing = new Vector2(sp, sp);
        gl.childAlignment = TextAnchor.UpperCenter; gl.constraint = GridLayoutGroup.Constraint.FixedColumnCount; gl.constraintCount = cols;

        foreach (int w in WorldCatalog.Order) BuildWorldCell(grid, w);   // GÖRÜNTÜ SIRASI (Order), worldId değil
    }

    void BuildWorldCell(Transform parent, int w)
    {
        bool unlocked = WorldCatalog.WorldUnlocked(w);

        var cell = new GameObject($"World{w}", typeof(RectTransform), typeof(Image), typeof(Button));
        var rt = (RectTransform)cell.transform; rt.SetParent(parent, false);
        var img = cell.GetComponent<Image>();
        if (worldIcons != null && w < worldIcons.Length && worldIcons[w] != null) { img.sprite = worldIcons[w]; img.preserveAspect = true; }
        img.color = unlocked ? Color.white : new Color(1f, 1f, 1f, 0.8f);   // kilitli = soluk (resim görünür)

        var name = NewText("Name", rt, 26, FontStyles.Bold, TextAlignmentOptions.Center);
        name.text = WorldCatalog.Names[w]; name.color = Color.white;
        var nr = name.rectTransform; nr.anchorMin = new Vector2(0, 0); nr.anchorMax = new Vector2(1, 0); nr.pivot = new Vector2(0.5f, 0);
        nr.offsetMin = new Vector2(0, 4); nr.offsetMax = new Vector2(0, 40);

        if (!unlocked) Badge(rt, lockSprite, new Color(1, 1, 1, 0.95f), 70);

        var btn = cell.GetComponent<Button>();
        int idx = w;
        btn.interactable = unlocked;
        if (unlocked) btn.onClick.AddListener(() => { AudioManager.Instance?.PlayUiClick(); SelectedWorld = idx; ShowLevelMenu(); });
    }

    // ════════ EKRAN GEÇİŞ ════════
    void ShowLevelMenu() { RebuildLevelContent(); levelPanel.SetActive(true); worldsPanel.SetActive(false); PlayIntro(levelPanel); }
    void ShowWorlds()    { worldsPanel.SetActive(true); levelPanel.SetActive(false); PlayIntro(worldsPanel); }

    // Panel açılış animasyonu: fade (CanvasGroup alpha 0→1) + hafif scale (0.95→1), ~0.22s.
    void PlayIntro(GameObject panel)
    {
        var cg = panel.GetComponent<CanvasGroup>();
        if (cg == null) cg = panel.AddComponent<CanvasGroup>();
        StopAllCoroutines();
        StartCoroutine(IntroRoutine(cg, (RectTransform)panel.transform));
    }

    static IEnumerator IntroRoutine(CanvasGroup cg, RectTransform rt)
    {
        const float dur = 0.22f; float e = 0f;
        while (e < dur)
        {
            e += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(e / dur));   // easing (eşikleme değil)
            cg.alpha = k;
            float s = Mathf.Lerp(0.95f, 1f, k);
            rt.localScale = new Vector3(s, s, 1f);
            yield return null;
        }
        cg.alpha = 1f; rt.localScale = Vector3.one;
    }

    // ════════ UI YARDIMCI ════════
    GameObject NewPanel(string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = (RectTransform)go.transform; rt.SetParent(canvas.transform, false); Stretch(rt);
        return go;
    }

    // Sol ÜST köşe toplam yıldız rozeti: BÜYÜK dolu yıldız + yanında AYNI BOYUT & DİKEY TAM HİZALI sayı.
    // Her şey tek yükseklik biriminden (H) türetilir → tüm ekran boyutlarında (CanvasScaler) oran korunur.
    // Rakam pivot-MERKEZ ile ikonun dikey merkezine kilitlenir (font metriğinden bağımsız hizalama).
    // Bekleyen ödül varsa başlangıçta ESKİ toplamı gösterir; uçan yıldızlar girince artar.
    void MakeStarBadge(Transform parent)
    {
        const float H = 48f;            // rozet yükseklik birimi (ikon = kare H) — yarıya indirildi
        const float left = 28f, top = -18f, gap = 8f;
        float iconCenterY = top - H * 0.5f;
        // Yıldız akışı success ekranında oynandıysa toplam ZATEN artmış → tam toplamı göster; aksi halde eski toplam.
        int shown = LevelResult.StarsAnimated
            ? StarManager.Total()
            : Mathf.Max(0, StarManager.Total() - (LevelResult.Pending ? LevelResult.Delta : 0));

        var icon = NewImage("StarTotalIcon", parent, StarArt.Full());
        icon.preserveAspect = true; icon.raycastTarget = false;
        var ir = icon.rectTransform;
        ir.anchorMin = ir.anchorMax = new Vector2(0f, 1f); ir.pivot = new Vector2(0f, 1f);
        ir.anchoredPosition = new Vector2(left, top); ir.sizeDelta = new Vector2(H, H);
        starBadgeIcon = ir;

        // Rakam: font büyük tutulur ki rakam cap-yüksekliği ≈ kırpılmış yıldız yüksekliği (H) olsun.
        // Kutu yüksekliği bol (klipslenmesin), pivot dikey MERKEZ → font metriğinden bağımsız hizalı.
        var t = NewText("StarTotal", parent, H * 1.4f, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
        t.color = new Color(1f, 0.92f, 0.5f); t.text = shown.ToString();
        var tr = t.rectTransform;
        tr.anchorMin = tr.anchorMax = new Vector2(0f, 1f); tr.pivot = new Vector2(0f, 0.5f);
        tr.anchoredPosition = new Vector2(left + H + gap, iconCenterY); tr.sizeDelta = new Vector2(300, H * 2f);
        starBadgeText = t;
    }

    // Levellar arası ÖDÜL FLASH (açık/şeffaf zemin, SADECE yıldızlar): kazanılan yıldızlar dolar, sonra
    // içi dolu yıldızlar UÇARAK sol üstteki toplam sayaca girer ve giriş başına sayaç 1 artar. Kendi kendine
    // (buton yok) kısa süre sonra kaybolur. Uçan yıldız sayısı = bu oyunda toplam yıldıza net eklenen (Delta).
    static string WorldRevealKey(int w) => $"WorldRevealed_{w}";

    // Level bitişi akışı: önce ödül (yıldız) flash'ı, sonra (dünya bittiyse) dünya geçiş animasyonu.
    IEnumerator PostLevelSequence(int nextWorld, int finishedWorld)
    {
        // Yıldız akışı artık SUCCESS ekranında oynanıyor → burada reward-flash'ı ATLA (yalnız eski/yedek akışta oynat).
        if (LevelResult.StarsAnimated) LevelResult.Clear();
        else yield return RewardFlashRoutine();
        if (nextWorld >= 0) yield return WorldTransitionRoutine(finishedWorld, nextWorld);
    }

    IEnumerator Fade(CanvasGroup cg, float from, float to, float dur)
    {
        float t = 0f;
        while (t < dur) { t += Time.deltaTime; cg.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(t / dur)); yield return null; }
        cg.alpha = to;
    }

    // DÜNYA GEÇİŞ ANİMASYONU: "[X] Tamamlandı! / Yeni dünya açıldı" → sonraki dünya ikonu pop + isim → "Devam Et"
    // butonuna basınca yeni dünyanın patikasına geçer. Bir dünyanın son level'ı kazanılınca (Start'ta) tetiklenir.
    IEnumerator WorldTransitionRoutine(int fw, int nextW)
    {
#if !UNITY_EDITOR
        PlayerPrefs.SetInt(WorldRevealKey(nextW), 1); PlayerPrefs.Save();   // release: tekrar gösterme
#endif
        AudioManager.Instance?.PlaySuccess();   // kutlama jingle'ı

        var panel = NewPanel("WorldTransition");
        var cg = panel.AddComponent<CanvasGroup>(); cg.alpha = 0f;
        var dim = NewImage("Dim", panel.transform, null); Stretch(dim.rectTransform);
        dim.color = new Color(0.08f, 0.06f, 0.05f, 0.92f); dim.raycastTarget = true;

        var title = NewText("Title", panel.transform, 64, FontStyles.Bold, TextAlignmentOptions.Center);
        title.text = WorldCatalog.Names[fw] + " Tamamlandı!"; title.color = new Color(1f, 0.9f, 0.35f);
        title.enableAutoSizing = true; title.fontSizeMin = 34; title.fontSizeMax = 64;
        var tr = title.rectTransform; tr.anchorMin = tr.anchorMax = new Vector2(0.5f, 0.5f); tr.pivot = new Vector2(0.5f, 0.5f);
        tr.anchoredPosition = new Vector2(0, 470); tr.sizeDelta = new Vector2(1000, 130);

        var sub = NewText("Sub", panel.transform, 38, FontStyles.Bold, TextAlignmentOptions.Center);
        sub.text = "Yeni dünya açıldı!"; sub.color = new Color(1f, 1f, 1f, 0.85f);
        var sr = sub.rectTransform; sr.anchorMin = sr.anchorMax = new Vector2(0.5f, 0.5f); sr.pivot = new Vector2(0.5f, 0.5f);
        sr.anchoredPosition = new Vector2(0, 320); sr.sizeDelta = new Vector2(900, 60);

        // İkon arkasında hafif nabızlı ışıltı diski
        var glow = NewImage("Glow", panel.transform, UiButtons.Circle());
        glow.color = new Color(1f, 0.85f, 0.35f, 0.22f); glow.raycastTarget = false;
        var gr = glow.rectTransform; gr.anchorMin = gr.anchorMax = new Vector2(0.5f, 0.5f); gr.pivot = new Vector2(0.5f, 0.5f);
        gr.anchoredPosition = new Vector2(0, 30); gr.sizeDelta = new Vector2(560, 560);
        glow.gameObject.AddComponent<Pulse>();

        var iconSprite = (worldIcons != null && nextW < worldIcons.Length) ? worldIcons[nextW] : null;
        var icon = NewImage("NextIcon", panel.transform, iconSprite);
        icon.preserveAspect = true; icon.raycastTarget = false;
        if (iconSprite == null) icon.color = new Color(0.5f, 0.7f, 1f);
        var ir = icon.rectTransform; ir.anchorMin = ir.anchorMax = new Vector2(0.5f, 0.5f); ir.pivot = new Vector2(0.5f, 0.5f);
        ir.anchoredPosition = new Vector2(0, 30); ir.sizeDelta = new Vector2(420, 420); ir.localScale = Vector3.zero;

        var nm = NewText("NextName", panel.transform, 58, FontStyles.Bold, TextAlignmentOptions.Center);
        nm.text = WorldCatalog.Names[nextW]; nm.color = new Color(1f, 0.95f, 0.75f);
        var nr = nm.rectTransform; nr.anchorMin = nr.anchorMax = new Vector2(0.5f, 0.5f); nr.pivot = new Vector2(0.5f, 0.5f);
        nr.anchoredPosition = new Vector2(0, -280); nr.sizeDelta = new Vector2(900, 80);

        bool go = false;
        var btn = UiButtons.Build(panel.transform, new Vector2(0.5f, 0.5f), new Vector2(0, -470),
            new Vector2(460, 124), "Devam Et", UiButtons.Play(), new Color(0.88f, 1f, 0.88f), 44);
        btn.onClick.AddListener(() => { AudioManager.Instance?.PlayUiClick(); go = true; });
        var btnCg = btn.gameObject.AddComponent<CanvasGroup>(); btnCg.alpha = 0f;

        yield return Fade(cg, 0f, 1f, 0.30f);
        yield return new WaitForSeconds(0.15f);
        yield return PopIn(ir);                 // sonraki dünya ikonu pop
        yield return new WaitForSeconds(0.10f);
        yield return Fade(btnCg, 0f, 1f, 0.25f);

        while (!go) yield return null;

        SelectedWorld = nextW;
        LevelManager.CurrentWorld = nextW;      // oynanırsa yeni dünya yüklensin
        yield return Fade(cg, 1f, 0f, 0.30f);
        Destroy(panel);
        ShowLevelMenu();                        // yeni dünyanın patikası (kendi intro'suyla)
    }

    IEnumerator RewardFlashRoutine()
    {
        int earned = Mathf.Clamp(LevelResult.Stars, 0, 3);
        int delta  = Mathf.Clamp(LevelResult.Delta, 0, earned);
        var gifts  = LevelResult.Gifts;

        // Çok açık/şeffaf overlay (koyu karartma yok)
        var panel = NewPanel("RewardFlash");
        var cg = panel.AddComponent<CanvasGroup>();
        var dim = NewImage("Dim", panel.transform, null); Stretch(dim.rectTransform);
        dim.color = new Color(1f, 1f, 1f, 0.06f); dim.raycastTarget = true;

        // 3 büyük yıldız, ekranın üst-ortasına yakın
        float[] xs = { -330f, 0f, 330f };
        const float size = 300f, cy = 150f;
        var full = new RectTransform[3];
        for (int i = 0; i < 3; i++)
        {
            MakeFlashStar(panel.transform, StarArt.Empty(), xs[i], cy, size);        // boş slot
            var f = MakeFlashStar(panel.transform, StarArt.Full(), xs[i], cy, size); // dolu (animasyonla)
            f.localScale = Vector3.zero; full[i] = f;
        }

        // (opsiyonel) küçük hediye bilgisi — yıldızların altında
        if (gifts != null && gifts.Count > 0)
        {
            var gt = NewText("Gift", panel.transform, 40, FontStyles.Bold, TextAlignmentOptions.Center);
            gt.text = "HEDİYE: " + string.Join(" + ", gifts); gt.color = new Color(1f, 0.78f, 0.2f);
            var r = gt.rectTransform; r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f); r.pivot = new Vector2(0.5f, 0.5f);
            r.anchoredPosition = new Vector2(0, cy - size * 0.5f - 60f); r.sizeDelta = new Vector2(940, 60);
        }

        // Kazanılan yıldızları sırayla doldur
        yield return new WaitForSeconds(0.35f);
        for (int i = 0; i < earned; i++) { yield return PopIn(full[i]); yield return new WaitForSeconds(0.12f); }

        yield return new WaitForSeconds(0.45f);

        // Dolu yıldızlar uçarak sol üst sayaca girsin; her giriş sayacı 1 artırsın (delta kadar).
        Canvas.ForceUpdateCanvases();
        int running = Mathf.Max(0, StarManager.Total() - delta);
        if (starBadgeText != null) starBadgeText.text = running.ToString();
        for (int k = 0; k < delta; k++)
        {
            int idx = earned - 1 - k;   // sağdan sola (yeni kazanılanlar)
            if (idx < 0) break;
            yield return FlyToBadge(full[idx]);
            running++;
            if (starBadgeText != null) { starBadgeText.text = running.ToString(); yield return BadgePulse(); }
        }

        // Kısa bekle → yumuşakça kaybol
        yield return new WaitForSeconds((gifts != null && gifts.Count > 0) ? 1.1f : 0.5f);
        float t = 0f; while (t < 0.35f) { t += Time.deltaTime; cg.alpha = 1f - t / 0.35f; yield return null; }
        LevelResult.Clear();
        Destroy(panel);
    }

    RectTransform MakeFlashStar(Transform parent, Sprite s, float x, float y, float size)
    {
        var go = new GameObject("FStar", typeof(RectTransform), typeof(Image));
        var rt = (RectTransform)go.transform; rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f); rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(x, y); rt.sizeDelta = new Vector2(size, size);
        var img = go.GetComponent<Image>();
        img.sprite = s; img.preserveAspect = true; img.raycastTarget = false;
        if (s == null) img.color = new Color(1f, 1f, 1f, 0.15f);
        return rt;
    }

    IEnumerator PopIn(RectTransform rt)
    {
        const float dur = 0.30f; float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime; float k = Mathf.Clamp01(t / dur);
            float sc = k < 0.7f ? Mathf.Lerp(0f, 1.2f, k / 0.7f) : Mathf.Lerp(1.2f, 1f, (k - 0.7f) / 0.3f);
            rt.localScale = Vector3.one * sc; yield return null;
        }
        rt.localScale = Vector3.one;
    }

    IEnumerator FlyToBadge(RectTransform star)
    {
        if (starBadgeIcon == null) { star.gameObject.SetActive(false); yield break; }
        Vector3 start = star.position, end = starBadgeIcon.position;
        const float dur = 0.55f; float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime; float k = Mathf.SmoothStep(0f, 1f, t / dur);
            star.position = Vector3.Lerp(start, end, k);
            star.localScale = Vector3.one * Mathf.Lerp(1f, 0.22f, k);
            yield return null;
        }
        star.gameObject.SetActive(false);
    }

    IEnumerator BadgePulse()
    {
        if (starBadgeIcon == null) yield break;
        float t = 0f;
        while (t < 0.18f) { t += Time.deltaTime; starBadgeIcon.localScale = Vector3.one * (1f + 0.28f * Mathf.Sin(t / 0.18f * Mathf.PI)); yield return null; }
        starBadgeIcon.localScale = Vector3.one;
    }

    // "Dünyalar" butonu: pill (rect görsel) + başta world_mixed ikonu + yazı (sol-üst).
    void MakeWorldsButton()
    {
        var icon = (worldIcons != null && worldIcons.Length > 17) ? worldIcons[17] : null;
        var b = UiButtons.Build(levelContent.transform, new Vector2(0f, 1f), new Vector2(26, -110),
            new Vector2(300, 104), "Dünyalar", icon, Color.white, 32);
        b.onClick.AddListener(ShowWorlds);
    }

    static void Badge(RectTransform parent, Sprite s, Color c, float size)
    {
        var img = NewImage("Badge", parent, s); img.color = c; img.preserveAspect = true; img.raycastTarget = false;
        var r = img.rectTransform; r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f); r.pivot = new Vector2(0.5f, 0.5f);
        r.anchoredPosition = Vector2.zero; r.sizeDelta = new Vector2(size, size);
    }

    void Line(Transform parent, Vector2 a, Vector2 b, Color c, float thick)
    {
        var img = NewImage("Line", parent, null); img.color = c; img.raycastTarget = false;
        var rt = img.rectTransform; rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f); rt.pivot = new Vector2(0.5f, 0.5f);
        Vector2 mid = (a + b) * 0.5f; Vector2 d = b - a;
        rt.anchoredPosition = mid; rt.sizeDelta = new Vector2(d.magnitude, thick);
        rt.localEulerAngles = new Vector3(0, 0, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
    }

    static Image NewImage(string name, Transform parent, Sprite s)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>(); if (s != null) img.sprite = s;
        return img;
    }

    static TMP_Text NewText(string name, Transform parent, float size, FontStyles style, TextAlignmentOptions align)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.fontSize = size; t.fontStyle = style; t.alignment = align; t.raycastTarget = false;
        return t;
    }

    static void Stretch(RectTransform rt) { rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero; }
}

/// <summary>Açık (sıradaki) durağı hafifçe nabız gibi büyütüp küçülten basit efekt.</summary>
public class Pulse : MonoBehaviour
{
    RectTransform rt; float t;
    void Start() => rt = (RectTransform)transform;
    void Update() { t += Time.deltaTime; float s = 1f + 0.08f * Mathf.Sin(t * 4f); rt.localScale = new Vector3(s, s, 1f); }
}
