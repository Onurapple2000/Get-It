using System.Collections;
using System.Collections.Generic;
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
    public Sprite proudMoleSprite;       // ana sayfa büyük maskotu (mole_mascot_proud); boşsa Resources/moleSprite yedeği
    public Sprite buttonSprite;
    public Sprite[] worldIcons;          // 18, WorldCatalog sırasıyla

    static int SelectedWorld = 0;

    Canvas canvas;
    GameObject levelPanel, worldsPanel, levelContent;
    GameObject homePanel, settingsPanel;   // Sprint: karşılama (Home) + Ayarlar
    Transform homeContent, settingsContent, worldsContent;
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

        StarRewards.GrantFirstLaunchGift();   // yeni oyuncuya başlangıç hediyesi (her powerup'tan 3, bir kez)
        StarRewards.CheckAndGrant();   // bekleyen yıldız hediyelerini ver (idempotent güvenlik ağı)
        SelectedWorld = Mathf.Clamp(LevelManager.CurrentWorld, 0, WorldCatalog.Count - 1);   // oynanan dünyanın patikası
        AudioManager.Instance?.PlayMenuMusic();   // ana menü müziği (dosya yoksa prosedürel)
        AdManager.Instance?.ShowBanner();   // Sprint 8: menüde banner (oynanışa girince gizlenir)

        BuildBackground();
        BuildHomePanel();
        BuildLevelPanel();
        BuildWorldsPanel();
        BuildSettingsPanel();

        // Levellar arası dönüş: patika ekranını göster + ödül/geçiş animasyonu. TAZE açılış (dönüş yok): KARŞILAMA (Home).
        if (LevelResult.Pending)
        {
            ShowLevelMenu();
            int fw = LevelResult.World, fl = LevelResult.Level, nextW = WorldCatalog.NextWorld(fw);   // SIRADA sonraki (Order)
            bool reveal = (fl + 1 >= WorldCatalog.PlayableLevels(fw))            // son level bitti mi
                          && nextW >= 0 && WorldCatalog.HasContent(nextW)        // sonraki dünya içerikli mi
#if !UNITY_EDITOR
                          && PlayerPrefs.GetInt(WorldRevealKey(nextW), 0) == 0   // release: bir kez göster
#endif
                          ;
            StartCoroutine(PostLevelSequence(reveal ? nextW : -1, fw));
        }
        else
        {
            ShowHome();
            StartCoroutine(FirstRunPromptsAfterSync());   // bulut dil/isim getirebilir → önce kısa süre senkronu bekle
        }
    }

    // İlk açılış dil/isim sorularını, bulut geri-yüklemesinin dil+ismi getirebilme ihtimaline karşı kısa süre bekletir.
    // Dönen oyuncuda (reinstall) bulut dil+isim gelir → soru HİÇ çıkmaz; gerçek ilk oyuncuda ~3sn sonra çıkar.
    System.Collections.IEnumerator FirstRunPromptsAfterSync()
    {
        float t = 0f;
        while ((!Loc.Chosen || !PlayerProfile.NameChosen) && t < 3f)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }
        // Dil hâlâ seçilmemişse (yeni oyuncu, bulut da getirmedi) → CİHAZ DİLİNİ otomatik seç (dil ekranı yok).
        if (!Loc.Chosen) Loc.Current = Loc.SystemDefault();
        RefreshHome();   // bulut/oto dil + isim → Home selamlama & metinleri güncellensin
        if (!PlayerProfile.NameChosen) ShowNameEntry(true);
    }

    // ════════ ARKA PLAN ════════
    void BuildBackground()
    {
        var bg = NewImage("Bg", canvas.transform, bgSprite);
        Stretch(bg.rectTransform);
        bg.raycastTarget = false;
        if (bgSprite == null) { bg.color = new Color(0.22f, 0.15f, 0.11f); return; }

        // BOZULMASIZ ARKA PLAN (2026-09-15): eskiden Stretch ile tam ekrana geriliyordu → yatay (1536×1024) resim
        // dikey ekranda 2-3× sıkışıyordu. "Kapla + kırp" (cover): oran korunur, ekranı tamamen doldurur,
        // taşan kenarlar kırpılır (CSS background-size: cover gibi). Her ekran oranında geçerli.
        var fit = bg.gameObject.AddComponent<AspectRatioFitter>();
        fit.aspectMode  = AspectRatioFitter.AspectMode.EnvelopeParent;
        fit.aspectRatio = bgSprite.rect.width / bgSprite.rect.height;
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

        // Üst-orta başlık: dünya adı ("Dünyalar" sol + Loc.T("continue") sağ butonlarıyla aynı hizada, aralarında).
        var name = NewText("WorldName", levelContent.transform, 56, FontStyles.Bold, TextAlignmentOptions.Center);
        name.text = WorldCatalog.LocalizedName(w); name.color = new Color(1f, 0.95f, 0.75f);
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

        // Loc.T("continue") — oynanacak sıradaki level'a atlar (sağ üst)
        MakeContinueButton();

        BuildPath(w);
    }

    // Loc.T("continue"): seçili dünyada bir sonraki oynanacak level (kilitli sınırda klamplenir). Yoksa gizli.
    void MakeContinueButton()
    {
        int target = ResumeLevel(SelectedWorld);
        if (target < 0) return;   // oynanabilir level yok (kilitli dünya)
        // Sağ üst köşe ("Dünyalar" butonu sol üstte → simetrik).
        var b = UiButtons.Build(levelContent.transform, new Vector2(1f, 1f), new Vector2(-26, -110),
            new Vector2(300, 104), Loc.T("continue"), UiButtons.Play(), new Color(0.88f, 1f, 0.88f), 32);
        b.onClick.AddListener(() => Play(SelectedWorld, target, (RectTransform)b.transform, b.image));
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
            else
            {
                btn.onClick.AddListener(() => { AudioManager.Instance?.PlayUiClick(); Play(world, idx, rt, img); });
                go.AddComponent<PressFeedback>();   // dokununca küçülüp koyulaşır → "basıldı" hissi (kullanıcı 2026-08-17)
            }

            if (open) go.AddComponent<Pulse>();   // sıradaki durak nabız
        }
    }

    bool loadingLevel;   // aynı anda tek yükleme; ekstra dokunuşları yut

    void Play(int world, int level, RectTransform stopRT, Image stopImg)
    {
        if (loadingLevel) return;
        // Can yoksa: eskiden altta küçük bir yazı çıkıyordu ve banner reklam onu KAPATIYORDU (kullanıcı 2026-08-22).
        // Artık ekranın ortasında, can satın alma seçenekleri sunan bir panel açılır.
        if (LivesManager.Instance != null && !LivesManager.Instance.HasLife)
        { ShowNoLivesPanel(); return; }
        loadingLevel = true;
        LevelManager.CurrentWorld = world;   // dünya-bazlı level seti (Sprint 5)
        LevelManager.CurrentIndex = level;

        // Basılan durağı "seçili" renge boya (yeşil) + üstüne "Yükleniyor..." yaz + diğer butonları kilitle (kullanıcı 2026-08-17)
        Color stopOrig = stopImg != null ? stopImg.color : Color.white;
        if (stopImg != null) stopImg.color = new Color(0.30f, 0.80f, 0.38f);
        var lbl = NewText("LoadingLbl", stopRT.parent, 34, FontStyles.Bold, TextAlignmentOptions.Center);
        lbl.isRightToLeftText = false;                                   // akan nokta efekti hep soldan-sağa
        lbl.color = new Color(1f, 0.95f, 0.7f);
        var lr = lbl.rectTransform;
        lr.anchorMin = stopRT.anchorMin; lr.anchorMax = stopRT.anchorMax;   // basılan butonla aynı hizada dur
        lr.pivot = new Vector2(0.5f, 0.5f);
        lr.anchoredPosition = stopRT.anchoredPosition + new Vector2(0f, stopRT.sizeDelta.y * 0.5f + 46f);
        lr.sizeDelta = new Vector2(420f, 64f);

        // Tam-ekran şeffaf tıklama engelleyici (siyah DEĞİL) → yükleme boyunca başka butona basılamaz
        var blk = new GameObject("LoadBlocker", typeof(RectTransform), typeof(Image));
        var brt = (RectTransform)blk.transform; brt.SetParent(levelPanel.transform, false); Stretch(brt);
        var bi = blk.GetComponent<Image>(); bi.color = new Color(0, 0, 0, 0f); bi.raycastTarget = true;

        StartCoroutine(LoadRoutine(lbl, blk, stopImg, stopOrig));
    }

    IEnumerator LoadRoutine(TMP_Text lbl, GameObject blocker, Image stopImg, Color stopOrig)
    {
        string baseText = Loc.T("loading");
        // Görsel geri bildirimin render olması için birkaç kare bekle (yoksa yükleme donması önce olur)
        for (int i = 0; i < 2; i++) { LoadTick(lbl, baseText); yield return null; }

        // ASSET DELIVERY FAZ 2: sahne AÇILMADAN ÖNCE dünya paketi (gerekirse indir) + prefab'lar belleğe.
        // İndirme sürerken "Dünya indiriliyor %xx"; hata → kırmızı toast, buton eski hâline, oyuncu tekrar deneyebilir.
        bool done = false, ok = false; string plbl = baseText; float pct = 0f;
        WorldContentLoader.Prepare(LevelManager.CurrentWorld, LevelManager.CurrentIndex,
            (l, p) => { plbl = l; pct = p; }, r => { ok = r; done = true; });
        while (!done)
        {
            if (lbl != null) lbl.text = (pct > 0f && pct < 1f) ? $"{plbl} %{Mathf.RoundToInt(pct * 100f)}" : plbl + new string('.', (int)((Time.realtimeSinceStartup * 3f) % 4f));
            yield return null;
        }
        if (!ok)
        {
            ToastUI.Show(Loc.T("downloadFail"), null, ToastUI.Style.Error);
            if (lbl != null) Destroy(lbl.gameObject);
            if (blocker != null) Destroy(blocker);
            if (stopImg != null) stopImg.color = stopOrig;
            loadingLevel = false;
            yield break;
        }
        WorldContentLoader.PrefetchAhead(LevelManager.CurrentWorld);   // sıradaki dünyalar arka planda insin

        var op = SceneManager.LoadSceneAsync(gameScene);
        op.allowSceneActivation = true;
        while (op != null && !op.isDone) { LoadTick(lbl, baseText); yield return null; }
        // Sahne değişince bu MonoBehaviour zaten yok olur; ek temizlik gerekmez.
    }

    static void LoadTick(TMP_Text lbl, string baseText)
    {
        if (lbl == null) return;
        int dots = (int)((Time.realtimeSinceStartup * 3f) % 4f);   // 0..3 nokta, soldan-sağa akar
        lbl.text = baseText + new string('.', dots);
    }

    // ════════ DÜNYALAR EKRANI ════════
    void BuildWorldsPanel()
    {
        worldsPanel = NewPanel("WorldsPanel");
        var dim = NewImage("Dim", worldsPanel.transform, null); Stretch(dim.rectTransform);
        dim.color = new Color(0f, 0f, 0f, 0.55f); dim.raycastTarget = true;   // karartma tam-ekran

        // İçerik GÜVENLİ ALAN'a (çentik/kenar dışı) — kalıcı konteyner; içerik her gösterimde YENİDEN kurulur (dil için).
        var wsafe = new GameObject("WSafe", typeof(RectTransform), typeof(SafeArea));
        var wsafeRt = (RectTransform)wsafe.transform; wsafeRt.SetParent(worldsPanel.transform, false); Stretch(wsafeRt);
        worldsContent = wsafe.transform;
    }

    // Dünyalar içeriğini (başlık/geri/ızgara) aktif dile göre YENİDEN kurar (dil değişince eski dilde kalmasın — 2026-08-17).
    void RebuildWorldsContent()
    {
        ClearChildren(worldsContent);
        var worldsSafe = worldsContent;

        var title = NewText("Title", worldsSafe, 64, FontStyles.Bold, TextAlignmentOptions.Center);
        title.text = Loc.T("worlds"); title.color = new Color(1f, 0.95f, 0.75f);
        var tr = title.rectTransform; tr.anchorMin = tr.anchorMax = new Vector2(0.5f, 1f); tr.pivot = new Vector2(0.5f, 1f);
        tr.anchoredPosition = new Vector2(0, -68); tr.sizeDelta = new Vector2(900, 90);   // çentikten biraz uzak (grid'e dokunma: topGap 165 sabit)

        var back = UiButtons.Build(worldsSafe, new Vector2(0f, 1f), new Vector2(30, -68),   // başlıkla aynı hizada aşağı
            new Vector2(230, 84), Loc.T("back"), null, Color.white, 32);
        back.onClick.AddListener(ShowHome);   // Dünyalar → Geri → Ana Sayfa (kullanıcı 2026-08-06)

        // Izgara (3 sütun) — RESPONSIVE: hücre boyutu ölçülen güvenli alana göre → 15 dünya ekranı tam kaplar,
        // alt boşluk kalmaz (2026-07-24, kullanıcı isteği).
        var grid = new GameObject("Grid", typeof(RectTransform), typeof(GridLayoutGroup)).GetComponent<RectTransform>();
        grid.SetParent(worldsSafe, false);
        grid.anchorMin = new Vector2(0.5f, 1f); grid.anchorMax = new Vector2(0.5f, 1f); grid.pivot = new Vector2(0.5f, 1f);

        const float topGap = 165f, gridW = 1020f, sp = 18f;
        // Alt boşluk: reklam banner'ı en alt dünya sırasının İSİMLERİNİ kapatmasın diye banner yüksekliği kadar boşluk
        // bırak. Reklamsız (NoAds) kullanıcıda banner yok → boşluk da yok (ekranı tam kaplasın).
        float botGap = PlayerProfile.NoAds ? 55f : 300f;
        const int cols = 3;
        // Güvenli-alan yüksekliğini RectTransform'dan ÖLÇME: bu noktada panel henüz AKTİF değil (ShowWorlds önce
        // Rebuild, sonra SetActive yapıyor) → ölçüm yanlış (çoğunlukla tam-ekran) → ızgara büyük çıkıp alt sırayı
        // banner altına taşırıyordu. Bunun yerine Screen.safeArea'yı canvas ölçeğine çevir → aktiflikten bağımsız doğru.
        var cv = worldsSafe.GetComponentInParent<Canvas>();
        float safeH = (cv != null && cv.scaleFactor > 0f) ? Screen.safeArea.height / cv.scaleFactor : 1720f;
        if (safeH < 200f) safeH = 1720f;   // yine de bir yedek
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

        var name = NewText("Name", rt, 34, FontStyles.Bold, TextAlignmentOptions.Center);
        name.text = WorldCatalog.LocalizedName(w); name.color = Color.white;
        var nr = name.rectTransform; nr.anchorMin = new Vector2(0, 0); nr.anchorMax = new Vector2(1, 0); nr.pivot = new Vector2(0.5f, 0);
        nr.offsetMin = new Vector2(0, 6); nr.offsetMax = new Vector2(0, 58);

        if (!unlocked) Badge(rt, lockSprite, new Color(1, 1, 1, 0.95f), 70);

        var btn = cell.GetComponent<Button>();
        int idx = w;
        btn.interactable = unlocked;
        if (unlocked) btn.onClick.AddListener(() => { AudioManager.Instance?.PlayUiClick(); SelectedWorld = idx; ShowLevelMenu(); });
    }

    // ════════ EKRAN GEÇİŞ ════════
    void HideAllPanels()
    {
        if (homePanel) homePanel.SetActive(false);
        if (levelPanel) levelPanel.SetActive(false);
        if (worldsPanel) worldsPanel.SetActive(false);
        if (settingsPanel) settingsPanel.SetActive(false);
    }
    void ShowHome()      { RefreshHome(); HideAllPanels(); homePanel.SetActive(true); PlayIntro(homePanel); }
    void ShowSettings()  { RefreshSettings(); HideAllPanels(); settingsPanel.SetActive(true); PlayIntro(settingsPanel); }
    void ShowLevelMenu() { RebuildLevelContent(); HideAllPanels(); levelPanel.SetActive(true); PlayIntro(levelPanel); }
    void ShowWorlds()    { RebuildWorldsContent(); HideAllPanels(); worldsPanel.SetActive(true); PlayIntro(worldsPanel); }

    // ════════ KARŞILAMA (HOME) ════════
    void BuildHomePanel()
    {
        homePanel = NewPanel("HomePanel");
        var safe = new GameObject("HomeSafe", typeof(RectTransform), typeof(SafeArea));
        var srt = (RectTransform)safe.transform; srt.SetParent(homePanel.transform, false); Stretch(srt);
        homeContent = safe.transform;
    }

    void RefreshHome()
    {
        ClearChildren(homeContent);

        // ══ KÖŞE WİDGET'LARI (sabit): sol üst coin rozeti + Bedava Coin, sağ üst Ayarlar ══
        var setBtn = UiButtons.Build(homeContent, new Vector2(1f, 1f), new Vector2(-24, -34),
            new Vector2(290, 92), Loc.T("settings"), UiButtons.Gear(), Color.white, 34);
        setBtn.onClick.AddListener(ShowSettings);
        var coinBadge = MakeCoinBadge(homeContent);
        var freeBtn = MakeFreeCoinsButton(homeContent, new Vector2(0f, 1f), new Vector2(24, -136), new Vector2(240, 116));
        const float cornerBandBottom = -252f;   // köşe widget'larının alt kenarı (rozet -34..-126, Bedava Coin -136..-252)

        // ══ ANA SÜTUN — TEK AKIŞ, RESPONSIVE (2026-09-15) ══
        // Yukarıdan aşağı imleçle dizilir: logo → selamlama → istatistik → maskot(ESNEK) → OYNA → aksiyon butonları.
        // Güvenli alanın yüksekliği ölçülür; maskot boş kalan alana göre büyür/küçülür (min/max), artan boşluk
        // sütunun üstüne/altına eşit dağıtılır → hiçbir cihazda üst üste binme olmaz, altta banner için pay kalır.
        Canvas.ForceUpdateCanvases();
        float safeH = ((RectTransform)homeContent).rect.height; if (safeH < 400f) safeH = 1720f;
        const float bannerReserve = 170f;                    // alt banner reklamı için pay
        const float logoW = 760f, nameH = 64f, statH = 92f, playH = 150f, btnH = 104f, btnGap = 14f;
        const float g1 = 8f, g2 = 12f, g3 = 18f, g4 = 26f;   // logo↔ad, ad↔stat, stat↔maskot, maskot↔OYNA
        var logoSprite = Resources.Load<Sprite>("logo_getit");
        float logoH = logoSprite != null ? logoW * logoSprite.rect.height / logoSprite.rect.width : 150f;
        int btnCount = PlayerProfile.NoAds ? 4 : 5;
        var mSprite = proudMoleSprite != null ? proudMoleSprite : moleSprite;

        float fixedH = logoH + g1 + nameH + g2 + statH + g3 + g4 + playH + 22f + btnCount * btnH + (btnCount - 1) * btnGap;
        float avail = safeH + cornerBandBottom - bannerReserve;   // cornerBandBottom negatif → üst bant düşülür
        float mascotH = mSprite != null ? Mathf.Clamp(avail - fixedH, 180f, 400f) : 0f;
        float extra = Mathf.Max(0f, avail - fixedH - mascotH);   // artan boşluk → üst/alt eşit
        float y = cornerBandBottom - extra * 0.5f;

        // Logo (Meshy görseli; yoksa yazı-logo yedek)
        if (logoSprite != null)
        {
            var logo = NewImage("Title", homeContent, logoSprite);
            logo.preserveAspect = true; logo.raycastTarget = false;
            Top(logo.rectTransform, 0, y, logoW, logoH);
            logo.gameObject.AddComponent<Pulse>().gentle = true;
        }
        else
        {
            var title = NewText("Title", homeContent, 112, FontStyles.Bold, TextAlignmentOptions.Center);
            title.text = "GET IT"; title.isRightToLeftText = false;
            Top(title.rectTransform, 0, y, 900, logoH);
            title.gameObject.AddComponent<TitleFx>();
        }
        y -= logoH + g1;

        // Selamlama
        var nm = NewText("PlayerName", homeContent, 46, FontStyles.Bold, TextAlignmentOptions.Center);
        nm.text = Loc.T("hello") + " " + PlayerProfile.Name; nm.color = Color.white;
        nm.isRightToLeftText = ContainsRTL(PlayerProfile.Name);   // Latin ad Arapça'da bile soldan-sağa
        Top(nm.rectTransform, 0, y, 900, nameH);
        y -= nameH + g2;

        // Yıldız + skor rozetleri
        MakeStat(homeContent, new Vector2(-260, y), StarArt.Full(), PlayerProfile.EarnedStars.ToString(), 300f);
        MakeStat(homeContent, new Vector2(240, y), null, Loc.T("score") + " " + PlayerProfile.TotalScore, 480f);
        y -= statH + g3;

        // Maskot (esnek yükseklik; genişlik oranla)
        if (mSprite != null && mascotH > 0f)
        {
            var mascot = NewImage("HomeMascot", homeContent, mSprite);
            mascot.preserveAspect = true; mascot.raycastTarget = false;
            float mw = mascotH * mSprite.rect.width / mSprite.rect.height;
            Top(mascot.rectTransform, 0, y, Mathf.Min(mw, 700f), mascotH);
            y -= mascotH + g4;
        }

        // OYNA → DÜNYALAR
        var play = UiButtons.Build(homeContent, new Vector2(0.5f, 1f), new Vector2(0, y),
            new Vector2(520, playH), Loc.T("play"), UiButtons.Play(), new Color(0.85f, 1f, 0.85f), 60);
        play.onClick.AddListener(ShowWorlds);
        y -= playH + 22f;

        // Aksiyon butonları (hepsi aynı renk — kullanıcı 2026-09-15)
        float bw = 640f, gap = btnH + btnGap;
        AddHomeButton(Loc.T("powerupsTitle"), ref y, gap, bw, btnH, Color.white, ShowPowerupShop);
        AddHomeButton(Loc.T("tellFriend"), ref y, gap, bw, btnH, Color.white, () => { Social.ShareGame(); Toast(Loc.T("linkReady")); });
        AddHomeButton(Loc.T("rate"), ref y, gap, bw, btnH, Color.white, Social.Rate);
        if (!PlayerProfile.NoAds)
            AddHomeButton(Loc.T("removeAds"), ref y, gap, bw, btnH, Color.white, ShowNoAds);
        AddHomeButton(Loc.T("buy"), ref y, gap, bw, btnH, Color.white, ShowStore);
    }

    // Coin bakiyesi rozeti: sol-üst köşe pill + altın coin ikonu + değer (otomatik-küçülme). Transform döner (pin için).
    Transform MakeCoinBadge(Transform parent)
    {
        var pill = NewImage("CoinBadge", parent, UiButtons.Rect());
        UiButtons.ApplyFrame(pill, 92f);
        pill.color = new Color(1f, 1f, 1f, 0.92f); pill.raycastTarget = false;
        var rt = pill.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f); rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(24, -34); rt.sizeDelta = new Vector2(240, 92);
        var t = CenteredIconValue(pill.transform, UiButtons.Coin(), PlayerProfile.Coins.ToString(), 60f, 40f, 130f);
        t.gameObject.AddComponent<CoinHud>();   // coin değişince otomatik güncellenir
        return pill.transform;
    }

    // Dikey "bedava coin" butonu: yukarıdan aşağıya Free / ▶(video) / Coins — buton görselinin içinde, çerçeveden uzak.
    Button MakeFreeCoinsButton(Transform parent, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        var go = new GameObject("Btn_FreeCoins", typeof(RectTransform), typeof(Image), typeof(Button));
        var rt = (RectTransform)go.transform; rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = anchor; rt.pivot = anchor; rt.anchoredPosition = pos; rt.sizeDelta = size;
        var im = go.GetComponent<Image>(); UiButtons.ApplyFrame(im, size.y); im.color = new Color(1f, 0.92f, 0.6f);
        var btn = go.GetComponent<Button>();
        btn.onClick.AddListener(() => { AudioManager.Instance?.PlayUiClick(); WatchAdForCoins(); });

        // YERLEŞİM (kullanıcı 2026-09-15): SOLDA büyük video ikonu, SAĞDA alt alta iki satır ("Bedava" / "Coin").
        // Çerçeve kalın olduğu için içerik kenarlardan iyice içeride; grup butonun ortasında.
        float padX = Mathf.Max(30f, size.x * 0.14f), padY = Mathf.Max(16f, size.y * 0.16f);
        var row = new GameObject("Content", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        var rrt = (RectTransform)row.transform; rrt.SetParent(rt, false);
        rrt.anchorMin = Vector2.zero; rrt.anchorMax = Vector2.one;
        rrt.offsetMin = new Vector2(padX, padY); rrt.offsetMax = new Vector2(-padX, -padY);
        var hlg = row.GetComponent<HorizontalLayoutGroup>();
        hlg.childAlignment = TextAnchor.MiddleCenter; hlg.spacing = 10f;
        hlg.childControlWidth = true; hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;

        // Sol: ikon — iç yüksekliğin tamamına yakın (büyük)
        float inner = size.y - padY * 2f;
        var ig = new GameObject("Icon", typeof(RectTransform), typeof(Image), typeof(LayoutElement)); ig.transform.SetParent(rrt, false);
        var iimg = ig.GetComponent<Image>(); iimg.sprite = UiButtons.Video(); iimg.preserveAspect = true; iimg.raycastTarget = false;
        var le = ig.GetComponent<LayoutElement>(); le.preferredWidth = inner * 0.9f; le.preferredHeight = inner * 0.9f;

        // Sağ: iki satır (ilk boşluk/tireden böl → "Bedava"/"Coin", "Free"/"Coins", "Gratis"/"Münzen"; bölünemezse tek satır)
        var col = new GameObject("Lines", typeof(RectTransform), typeof(VerticalLayoutGroup));
        var crt = (RectTransform)col.transform; crt.SetParent(rrt, false);
        var vlg = col.GetComponent<VerticalLayoutGroup>();
        vlg.childAlignment = TextAnchor.MiddleLeft; vlg.spacing = 0f;
        vlg.childControlWidth = true; vlg.childControlHeight = true;
        vlg.childForceExpandWidth = false; vlg.childForceExpandHeight = false;
        string full = Loc.T("freeCoins");
        int cut = full.IndexOfAny(new[] { ' ', '-' });
        if (cut > 0) { AddFreeLine(crt, full.Substring(0, cut)); AddFreeLine(crt, full.Substring(cut + 1)); }
        else AddFreeLine(crt, full);
        return btn;
    }

    void AddFreeLine(Transform parent, string text)
    {
        var t = NewText("L", parent, 30, FontStyles.Bold, TextAlignmentOptions.Left);
        t.color = new Color(0.2f, 0.14f, 0.07f); t.text = text; t.raycastTarget = false;
        t.enableAutoSizing = true; t.fontSizeMin = 12f; t.fontSizeMax = 30f; t.enableWordWrapping = false; t.overflowMode = TextOverflowModes.Ellipsis;
        Loc.ApplyDir(t);
        t.gameObject.AddComponent<LayoutElement>().flexibleHeight = 0f;
    }

    void AddHomeButton(string label, ref float y, float gap, float bw, float bh, Color tint, System.Action onClick)
    {
        // Üst-ankraj: y = butonun ÜST kenarı (akış imleci) — sütun tek akışta dizilir (2026-09-15).
        var b = UiButtons.Build(homeContent, new Vector2(0.5f, 1f), new Vector2(0, y), new Vector2(bw, bh), label, null, tint, 36);
        b.onClick.AddListener(() => onClick());
        y -= gap;
    }

    // Skor/yıldız rozeti: pill (verilen genişlik) + (opsiyonel) ikon + değer (otomatik-küçülme → taşmaz).
    void MakeStat(Transform parent, Vector2 pos, Sprite icon, string value, float width = 320f)
    {
        var pill = NewImage("Stat", parent, UiButtons.Rect());
        UiButtons.ApplyFrame(pill, 92f);
        pill.color = new Color(1f, 1f, 1f, 0.92f); pill.raycastTarget = false;
        Top(pill.rectTransform, pos.x, pos.y, width, 92);
        CenteredIconValue(pill.transform, icon, value, 58f, 42f, width - 60f);
    }

    // Rozet içeriği: [ikon][boşluk][değer] — bitişik ve pill'in ORTASINDA (kullanıcı 2026-09-15: ikonla sayı arası
    // çok açıktı). Yatay layout, tek karakterlik boşluk; değer oto-küçülür → uzun sayı taşmaz.
    TMP_Text CenteredIconValue(Transform pill, Sprite icon, string value, float iconSize, float fontSize, float maxTextW)
    {
        var row = new GameObject("Row", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        var rrt = (RectTransform)row.transform; rrt.SetParent(pill, false);
        rrt.anchorMin = Vector2.zero; rrt.anchorMax = Vector2.one; rrt.offsetMin = new Vector2(16, 0); rrt.offsetMax = new Vector2(-16, 0);
        var hl = row.GetComponent<HorizontalLayoutGroup>();
        hl.childAlignment = TextAnchor.MiddleCenter; hl.spacing = 10f;   // ≈ bir karakter
        hl.childControlWidth = true; hl.childControlHeight = true; hl.childForceExpandWidth = false; hl.childForceExpandHeight = false;
        if (icon != null)
        {
            var ic = new GameObject("Ic", typeof(RectTransform), typeof(Image), typeof(LayoutElement)); ic.transform.SetParent(rrt, false);
            var ii = ic.GetComponent<Image>(); ii.sprite = icon; ii.preserveAspect = true; ii.raycastTarget = false;
            var le = ic.GetComponent<LayoutElement>(); le.preferredWidth = iconSize; le.preferredHeight = iconSize;
        }
        var t = NewText("V", rrt, fontSize, FontStyles.Bold, TextAlignmentOptions.Center);
        t.color = new Color(0.2f, 0.14f, 0.07f); t.text = value; t.raycastTarget = false;
        t.enableAutoSizing = true; t.fontSizeMin = 16f; t.fontSizeMax = fontSize; t.enableWordWrapping = false; t.overflowMode = TextOverflowModes.Ellipsis;
        // Metin DOĞAL genişliğinde kalsın (sabit preferredWidth vermek metni pill'e yayıp ikonu sola itiyordu);
        // sığmazsa layout daraltır, TMP oto-küçültür.
        var tle = t.gameObject.AddComponent<LayoutElement>(); tle.flexibleWidth = 0f;
        return t;
    }

    // Bir güvenli-alan kökünün DOĞRUDAN çocuklarını (pinli köşe butonları HARİÇ) ekranda DİKEY ORTALAR.
    // İçerik güvenli alandan uzunsa üstü çentiğe/kameraya SOKMAZ (üstte kalır). Oyun + Dünyalar ekranları çağırmaz.
    void CenterVertically(Transform safeRoot, params Transform[] pinned)
    {
        Canvas.ForceUpdateCanvases();
        var sr = (RectTransform)safeRoot;
        var pinnedSet = new HashSet<Transform>(pinned);
        var kids = new List<RectTransform>();
        float minY = float.MaxValue, maxY = float.MinValue;
        var wc = new Vector3[4];
        foreach (Transform ch in safeRoot)
        {
            if (!(ch is RectTransform rt) || pinnedSet.Contains(ch)) continue;
            kids.Add(rt);
            rt.GetWorldCorners(wc);
            for (int i = 0; i < 4; i++)
            {
                float ly = sr.InverseTransformPoint(wc[i]).y;
                if (ly < minY) minY = ly;
                if (ly > maxY) maxY = ly;
            }
        }
        if (kids.Count == 0 || minY > maxY) return;
        float dy = -(minY + maxY) * 0.5f;                  // içerik merkezini safe-alan merkezine (y=0) taşı
        float topLimit = sr.rect.height * 0.5f;            // güvenli alanın üst kenarı (pivot merkez)
        if (maxY + dy > topLimit) dy = topLimit - maxY;    // uzun içerikte üstü kameraya sokma → üstte kal
        foreach (var rt in kids) rt.anchoredPosition += new Vector2(0f, dy);
    }

    // Bir kökün çocuklarını hemen HİYERARŞİDEN AYIRIP siler. Destroy kare sonuna ertelenir; aynı karede yapılan
    // ölçüm (CenterVertically/GetWorldCorners) eski öğeleri de sayıyordu → her yeniden kurulumda kayma (2026-09-15).
    static void ClearChildren(Transform root)
    {
        for (int i = root.childCount - 1; i >= 0; i--)
        {
            var c = root.GetChild(i);
            c.SetParent(null, false);
            Destroy(c.gameObject);
        }
    }

    // ════════ AYARLAR ════════
    void BuildSettingsPanel()
    {
        settingsPanel = NewPanel("SettingsPanel");
        var dim = NewImage("Dim", settingsPanel.transform, null); Stretch(dim.rectTransform);
        dim.color = new Color(0.08f, 0.06f, 0.05f, 0.9f); dim.raycastTarget = true;
        var safe = new GameObject("SetSafe", typeof(RectTransform), typeof(SafeArea));
        var srt = (RectTransform)safe.transform; srt.SetParent(settingsPanel.transform, false); Stretch(srt);
        settingsContent = safe.transform;
    }

    void RefreshSettings()
    {
        ClearChildren(settingsContent);   // hemen ayır + sil (Destroy gecikmeli → ölçüm eski öğeleri de sayıyordu, dil değişince kayıyordu)

        var title = NewText("Title", settingsContent, 64, FontStyles.Bold, TextAlignmentOptions.Center);
        title.text = Loc.T("settings"); title.color = new Color(1f, 0.95f, 0.75f);
        Top(title.rectTransform, 0, -40, 600, 90);

        var x = UiButtons.Build(settingsContent, new Vector2(1f, 1f), new Vector2(-24, -30), new Vector2(92, 92), "X", null, new Color(1f, 0.8f, 0.75f), 44);
        x.onClick.AddListener(ShowHome);

        // İsim GİRİLMİŞSE title-case ile göster; girilmemişse boş → placeholder ("İsim"/"Name") görünür.
        string shownName = PlayerProfile.NameChosen ? TitleCaseName(PlayerProfile.Name) : "";
        var input = MakeInputField(settingsContent, shownName, new Vector2(0, -202), new Vector2(560, 92));
        input.onEndEdit.AddListener(v => { if (!string.IsNullOrWhiteSpace(v)) PlayerProfile.Name = TitleCaseName(v); });

        MakeToggle(settingsContent, -320, Loc.T("sfx"), () => AudioManager.SfxOn, v => AudioManager.SfxOn = v);
        MakeToggle(settingsContent, -420, Loc.T("music"), () => AudioManager.MusicOn, v => AudioManager.MusicOn = v);
        MakeToggle(settingsContent, -520, Loc.T("vibration"), () => AudioManager.HapticOn, v => { AudioManager.HapticOn = v; if (v) AudioManager.Instance?.HapticTest(); });

        var dl = NewText("DL", settingsContent, 34, FontStyles.Bold, TextAlignmentOptions.Center);
        dl.color = new Color(1f, 1f, 1f, 0.92f); dl.text = Loc.T("difficulty"); Top(dl.rectTransform, 0, -620, 400, 54);
        var cur = DifficultySettings.Current;
        DiffButton(settingsContent, -300, -700, Difficulty.Easy, cur);
        DiffButton(settingsContent, 0, -700, Difficulty.Normal, cur);
        DiffButton(settingsContent, 300, -700, Difficulty.Hard, cur);

        // Dil satırı: etiket + 7 bayrak (tek sıra). Seçili bayrak parlak+büyük.
        var ll = NewText("LL", settingsContent, 34, FontStyles.Bold, TextAlignmentOptions.Center);
        ll.color = new Color(1f, 1f, 1f, 0.92f); ll.text = Loc.T("language"); Top(ll.rectTransform, 0, -800, 400, 54);
        for (int i = 0; i < Langs.Length; i++)
            LangFlag(settingsContent, (i - 3) * 135f, -900, Langs[i].lang, Langs[i].flag);

#if UNITY_ANDROID
        // Hesap bağlama (Google Play Games) — ilerlemeyi Google hesabına kaydeder (silip-kurunca/cihaz değişince kaybolmaz).
        MakeAccountRow(settingsContent, -1080, -1160);   // bayrakların altında, biraz daha aşağı (kullanıcı 2026-09-15)
        if (AccountManager.Instance != null)   // bağlama bitince butonu otomatik "✓ Bağlı" yap
        {
            AccountManager.Instance.OnAccountLinked -= RefreshSettings;
            AccountManager.Instance.OnAccountLinked += RefreshSettings;
        }
#endif

        CenterVertically(settingsContent, x.transform);   // dikey ortala (X köşede kalır)
    }

    // Desteklenen diller (bayrak + endonim etiket). Sıra Language enum ile aynı.
    static readonly (Language lang, string flag, string label)[] Langs =
    {
        (Language.Turkish, "turkey_flag",  "Türkçe"),
        (Language.English, "us_flag",      "English"),
        (Language.Spanish, "spain_flag",   "Español"),
        (Language.German,  "germany_flag", "Deutsch"),
        (Language.Arabic,  "saudi_flag",   "العربية"),
        (Language.Korean,  "korea_flag",   "한국어"),
        (Language.Russian, "russia_flag",  "Русский"),
    };

    // Ayarlar dil bayrağı (yalnız bayrak). Seçili = parlak + büyük; diğerleri soluk. Basınca dili değiştir + yenile.
    void LangFlag(Transform parent, float x, float y, Language lang, string flagRes)
    {
        bool sel = Loc.Current == lang;
        var go = new GameObject("Lang_" + lang, typeof(RectTransform), typeof(Image), typeof(Button));
        var rt = (RectTransform)go.transform; rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f); rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(x, y); rt.sizeDelta = sel ? new Vector2(112, 112) : new Vector2(90, 90);
        var img = go.GetComponent<Image>(); img.sprite = FlagSprite(flagRes); img.preserveAspect = true;
        img.color = sel ? Color.white : new Color(1f, 1f, 1f, 0.55f);
        var btn = go.GetComponent<Button>(); btn.transition = Selectable.Transition.None;
        btn.onClick.AddListener(() => { Loc.Current = lang; RefreshSettings(); });
    }

    // Metin sağdan-sola script (İbranice/Arapça) içeriyor mu? Latin ad → false (LTR).
    static bool ContainsRTL(string s)
    {
        if (string.IsNullOrEmpty(s)) return false;
        foreach (char c in s)
            if ((c >= 0x0590 && c <= 0x08FF) || (c >= 0xFB1D && c <= 0xFDFF) || (c >= 0xFE70 && c <= 0xFEFF)) return true;
        return false;
    }

    static Sprite FlagSprite(string res)
    {
        var tex = Resources.Load<Texture2D>(res);
        return tex == null ? null : Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
    }

    void DiffButton(Transform parent, float x, float y, Difficulty d, Difficulty cur)
    {
        bool sel = d == cur;
        string key = d == Difficulty.Easy ? "easy" : d == Difficulty.Hard ? "hard" : "normal";
        var b = UiButtons.Build(parent, new Vector2(0.5f, 1f), new Vector2(x, y), new Vector2(280, 100),
            Loc.T(key), null, sel ? new Color(0.65f, 1f, 0.65f) : new Color(1f, 1f, 1f, 0.7f), 36);
        b.onClick.AddListener(() => { DifficultySettings.Current = d; RefreshSettings(); });
    }

    void MakeToggle(Transform parent, float y, string label, System.Func<bool> get, System.Action<bool> set)
    {
        var lbl = NewText("TgL", parent, 36, FontStyles.Bold, TextAlignmentOptions.Left);
        lbl.color = Color.white; lbl.text = label;
        Top(lbl.rectTransform, -300, y, 440, 70, 0f);
        bool on = get();
        var b = UiButtons.Build(parent, new Vector2(0.5f, 1f), new Vector2(300, y - 6), new Vector2(220, 84),
            on ? Loc.T("on") : Loc.T("off"), null, on ? new Color(0.7f, 1f, 0.7f) : new Color(1f, 0.75f, 0.7f), 34);
        b.onClick.AddListener(() => { set(!get()); RefreshSettings(); });
    }

#if UNITY_ANDROID
    // Ayarlar'da hesap satırı: bağlı değilse "İlerlemeyi Kaydet" (tıkla → Google Play Games bağla),
    // bağlıysa "✓ Bağlı" (pasif). Android'e özel (iOS'ta Apple sign-in henüz yok).
    void MakeAccountRow(Transform parent, float labelY, float btnY)
    {
        var lbl = NewText("AccL", parent, 34, FontStyles.Bold, TextAlignmentOptions.Center);
        lbl.color = new Color(1f, 1f, 1f, 0.92f); lbl.text = Loc.T("account");
        Top(lbl.rectTransform, 0, labelY, 500, 54);

        bool linked = AccountManager.Instance != null && AccountManager.Instance.IsLinked;
        var b = UiButtons.Build(parent, new Vector2(0.5f, 1f), new Vector2(0, btnY), new Vector2(560, 100),
            linked ? Loc.T("account_linked") : Loc.T("save_progress"), null,
            linked ? new Color(0.65f, 1f, 0.65f) : Color.white, 34);
        if (linked) b.interactable = false;
        else b.onClick.AddListener(() => AccountManager.Instance?.LinkCurrentPlatform());

        // Hesap/veri silme (Google Play zorunluluğu) — kırmızı, çift onaylı.
        var del = UiButtons.Build(parent, new Vector2(0.5f, 1f), new Vector2(0, btnY - 118f), new Vector2(560, 88),   // kaydet butonunun ALTINDA (eskiden +118 → etiketle üst üste biniyordu)
            Loc.T("deleteAccount"), null, new Color(1f, 0.62f, 0.55f), 30);
        del.onClick.AddListener(ShowDeleteAccountConfirm);
    }

    // "Emin misin?" onayı — yanlışlıkla basılıp ilerlemenin uçmasını engeller.
    void ShowDeleteAccountConfirm()
    {
        var panel = NewPanel("DeleteConfirmPanel");
        var dim = NewImage("Dim", panel.transform, null); Stretch(dim.rectTransform);
        dim.color = new Color(0.08f, 0.04f, 0.03f, 0.94f); dim.raycastTarget = true;
        var safe = new GameObject("DelSafe", typeof(RectTransform), typeof(SafeArea));
        var srt = (RectTransform)safe.transform; srt.SetParent(panel.transform, false); Stretch(srt);

        var title = NewText("T", safe.transform, 56, FontStyles.Bold, TextAlignmentOptions.Center);
        title.text = Loc.T("deleteSure"); title.color = new Color(1f, 0.8f, 0.72f);
        var tr = title.rectTransform; tr.anchorMin = tr.anchorMax = new Vector2(0.5f, 0.5f); tr.pivot = new Vector2(0.5f, 0.5f);
        tr.anchoredPosition = new Vector2(0, 230); tr.sizeDelta = new Vector2(800, 90);

        var body = NewText("B", safe.transform, 34, FontStyles.Normal, TextAlignmentOptions.Center);
        body.text = Loc.T("deleteWarn"); body.color = new Color(1f, 1f, 1f, 0.92f);
        var br = body.rectTransform; br.anchorMin = br.anchorMax = new Vector2(0.5f, 0.5f); br.pivot = new Vector2(0.5f, 0.5f);
        br.anchoredPosition = new Vector2(0, 40); br.sizeDelta = new Vector2(860, 300);

        var yes = UiButtons.Build(safe.transform, new Vector2(0.5f, 0.5f), new Vector2(0, -180), new Vector2(520, 110),
            Loc.T("deleteYes"), null, new Color(1f, 0.55f, 0.48f), 38);
        yes.onClick.AddListener(async () =>
        {
            yes.interactable = false;
            bool ok = AccountManager.Instance != null && await AccountManager.Instance.DeleteAccountAndDataAsync();
            Destroy(panel);
            Toast(Loc.T(ok ? "deleteDone" : "deleteFail"));
            if (ok) { RefreshHome(); ShowHome(); }   // temiz başlangıç: Home'a dön, değerler sıfırlanmış
        });

        var no = UiButtons.Build(safe.transform, new Vector2(0.5f, 0.5f), new Vector2(0, -320), new Vector2(520, 110),
            Loc.T("cancel"), null, new Color(0.9f, 1f, 0.9f), 38);
        no.onClick.AddListener(() => Destroy(panel));
    }
#endif

    // ════════ İSİM GİRME + SATIN AL + TOAST ════════
    // İlk açılış DİL SEÇİMİ: "Türkçe" (TR bayrağı) + "English" (US bayrağı). Yazı ya da bayrağa basınca dil seçilir,
    // tüm ekran o dile döner; sonra (isim seçilmemişse) isim ekranı gelir.
    void ShowLanguagePicker()
    {
        var panel = NewPanel("LangPicker");
        var dim = NewImage("Dim", panel.transform, null); Stretch(dim.rectTransform);
        dim.color = new Color(0.05f, 0.04f, 0.03f, 0.95f); dim.raycastTarget = true;
        var safe = new GameObject("LSafe", typeof(RectTransform), typeof(SafeArea));
        var srt = (RectTransform)safe.transform; srt.SetParent(panel.transform, false); Stretch(srt);

        var t = NewText("T", safe.transform, 54, FontStyles.Bold, TextAlignmentOptions.Center);
        t.text = "Dil / Language"; t.color = new Color(1f, 0.95f, 0.75f);
        var tr = t.rectTransform; tr.anchorMin = tr.anchorMax = new Vector2(0.5f, 0.5f); tr.pivot = new Vector2(0.5f, 0.5f);
        tr.anchoredPosition = new Vector2(0, 470); tr.sizeDelta = new Vector2(900, 90);

        float y = 360f;
        foreach (var L in Langs) { LangPick(safe.transform, y, L.lang, L.flag, L.label, panel); y -= 112f; }

        CenterVertically(safe.transform);   // dikey ortala
    }

    void LangPick(Transform parent, float y, Language lang, string flagRes, string label, GameObject panel)
    {
        // Bayrak (solda) + yazı; ikisi de aynı butonda → yazıya da bayrağa da basınca seçilir.
        var b = UiButtons.Build(parent, new Vector2(0.5f, 0.5f), new Vector2(0, y), new Vector2(540, 96),
            "   " + label, FlagSprite(flagRes), Color.white, 40);
        b.onClick.AddListener(() =>
        {
            Loc.Current = lang;
            Destroy(panel);
            RefreshHome();                                   // menü o dile döner
            if (!PlayerProfile.NameChosen) ShowNameEntry(true);
        });
    }

    void ShowNameEntry(bool firstTime)
    {
        var panel = NewPanel("NameEntry");
        var dim = NewImage("Dim", panel.transform, null); Stretch(dim.rectTransform);
        dim.color = new Color(0.05f, 0.04f, 0.03f, 0.92f); dim.raycastTarget = true;
        var safe = new GameObject("NSafe", typeof(RectTransform), typeof(SafeArea));
        var srt = (RectTransform)safe.transform; srt.SetParent(panel.transform, false); Stretch(srt);

        var t = NewText("T", safe.transform, 54, FontStyles.Bold, TextAlignmentOptions.Center);
        t.text = firstTime ? Loc.T("askName") : Loc.T("changeName"); t.color = new Color(1f, 0.95f, 0.75f);
        var tr = t.rectTransform; tr.anchorMin = tr.anchorMax = new Vector2(0.5f, 0.5f); tr.pivot = new Vector2(0.5f, 0.5f);
        tr.anchoredPosition = new Vector2(0, 220); tr.sizeDelta = new Vector2(900, 90);

        var input = MakeInputField(safe.transform, PlayerProfile.Name, new Vector2(0, 60), new Vector2(620, 110), center: true);

        var ok = UiButtons.Build(safe.transform, new Vector2(0.5f, 0.5f), new Vector2(0, -120), new Vector2(360, 120),
            Loc.T("ok"), UiButtons.Play(), new Color(0.85f, 1f, 0.85f), 44);
        ok.onClick.AddListener(() => { PlayerProfile.Name = input.text; Destroy(panel); RefreshHome(); });

        CenterVertically(safe.transform);   // dikey ortala
    }

    // ── REKLAMLARI KALDIR (ayrı ekran: ücret + satın al) ──
    void ShowNoAds()
    {
        var panel = NewPanel("NoAdsPanel");
        var dim = NewImage("Dim", panel.transform, null); Stretch(dim.rectTransform);
        dim.color = new Color(0.08f, 0.06f, 0.05f, 0.92f); dim.raycastTarget = true;
        var safe = new GameObject("NoAdsSafe", typeof(RectTransform), typeof(SafeArea));
        var srt = (RectTransform)safe.transform; srt.SetParent(panel.transform, false); Stretch(srt);

        var title = NewText("Title", safe.transform, 60, FontStyles.Bold, TextAlignmentOptions.Center);
        title.text = Loc.T("removeAds"); title.color = new Color(1f, 0.95f, 0.75f); Top(title.rectTransform, 0, -70, 800, 90);

        var desc = NewText("Desc", safe.transform, 38, FontStyles.Normal, TextAlignmentOptions.Center);
        desc.text = Loc.T("noAdsDesc"); desc.color = new Color(1f, 1f, 1f, 0.9f);
        var dr = desc.rectTransform; dr.anchorMin = dr.anchorMax = new Vector2(0.5f, 0.5f); dr.pivot = new Vector2(0.5f, 0.5f);
        dr.anchoredPosition = new Vector2(0, 160); dr.sizeDelta = new Vector2(840, 160);

        var price = NewText("Price", safe.transform, 72, FontStyles.Bold, TextAlignmentOptions.Center);
        price.text = IapService.Instance?.GetPrice(IapService.NoAdsId) ?? "₺49,99"; price.color = new Color(1f, 0.9f, 0.35f);
        var pr = price.rectTransform; pr.anchorMin = pr.anchorMax = new Vector2(0.5f, 0.5f); pr.pivot = new Vector2(0.5f, 0.5f);
        pr.anchoredPosition = new Vector2(0, 20); pr.sizeDelta = new Vector2(600, 100);

        var buy = UiButtons.Build(safe.transform, new Vector2(0.5f, 0.5f), new Vector2(0, -150), new Vector2(520, 130),
            Loc.T("buy"), UiButtons.Power(), new Color(0.85f, 1f, 0.85f), 46);
        buy.onClick.AddListener(() =>
        {
            if (IapService.Instance == null || !IapService.Instance.IsReady) { ToastUI.Show(Loc.T("storeUnavailable"), null, ToastUI.Style.Error); return; }
            Store.RemoveAds(ok => { if (ok) { ToastUI.Show(Loc.T("adsRemoved"), null, ToastUI.Style.Success); Destroy(panel); RefreshHome(); } });
        });

        var x = UiButtons.Build(safe.transform, new Vector2(1f, 1f), new Vector2(-24, -30), new Vector2(92, 92), "X", null, new Color(1f, 0.8f, 0.75f), 44);
        x.onClick.AddListener(() => Destroy(panel));

        CenterVertically(safe.transform, x.transform);   // dikey ortala (X köşede kalır)
    }

    // ════════ CAN YOK PANELİ ════════
    // Oyuncu cansızken level'a basınca açılır. Fail ekranıyla AYNI fiyat/akış: reklam → +1 can, coin → +1 can.
    // Coin bakiyesi görünür; kazanım animasyonla gösterilir; Vazgeç ile kapanır.
    void ShowNoLivesPanel()
    {
        var panel = NewPanel("NoLivesPanel");
        var dim = NewImage("Dim", panel.transform, null); Stretch(dim.rectTransform);
        dim.color = new Color(0.08f, 0.05f, 0.05f, 0.93f); dim.raycastTarget = true;
        var safe = new GameObject("NLSafe", typeof(RectTransform), typeof(SafeArea));
        var srt = (RectTransform)safe.transform; srt.SetParent(panel.transform, false); Stretch(srt);
        var C = safe.transform;

        var title = NewText("T", C, 60, FontStyles.Bold, TextAlignmentOptions.Center);
        title.text = Loc.T("noLivesTitle"); title.color = new Color(1f, 0.82f, 0.75f);
        Mid(title.rectTransform, 0, 430, 840, 90);

        // Boş kalpler — durumu tek bakışta anlatır
        var hearts = new GameObject("Hearts", typeof(RectTransform));
        var hrt = (RectTransform)hearts.transform; hrt.SetParent(C, false);
        hrt.anchorMin = hrt.anchorMax = hrt.pivot = new Vector2(0.5f, 0.5f);
        hrt.anchoredPosition = new Vector2(0, 300); hrt.sizeDelta = new Vector2(560, 90);
        var heartImgs = new Image[LivesManager.MaxLives];
        for (int i = 0; i < LivesManager.MaxLives; i++)
        {
            var h = NewImage("H" + i, hrt, HeartArt.Empty());
            h.preserveAspect = true; h.raycastTarget = false;
            var r = h.rectTransform; r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0.5f);
            r.anchoredPosition = new Vector2((i - (LivesManager.MaxLives - 1) * 0.5f) * 96f, 0);
            r.sizeDelta = new Vector2(78, 78);
            heartImgs[i] = h;
        }

        var body = NewText("B", C, 34, FontStyles.Normal, TextAlignmentOptions.Center);
        body.text = Loc.T("noLivesBody"); body.color = new Color(1f, 1f, 1f, 0.9f);
        Mid(body.rectTransform, 0, 170, 860, 130);

        // Geri sayım (sonraki can ne zaman) — canlı güncellenir
        var timer = NewText("Timer", C, 32, FontStyles.Bold, TextAlignmentOptions.Center);
        timer.color = new Color(1f, 0.95f, 0.7f, 0.95f);
        Mid(timer.rectTransform, 0, 70, 700, 56);

        // Coin bakiyesi
        var coinRow = NewText("Coins", C, 38, FontStyles.Bold, TextAlignmentOptions.Center);
        coinRow.color = new Color(1f, 0.9f, 0.45f);
        Mid(coinRow.rectTransform, 0, -10, 700, 60);

        var am = AdManager.Instance;
        bool adReady = am != null && am.RewardedReady;
        int cost = Economy.RefillLifeCost;

        // İki buton da AYNI şeyi verir (+1 can); fark yalnızca ÖDEME YOLU. Bu yüzden etiketler simetrik:
        // sol taraf "nasıl ödediğin" (reklam / coin), sağ taraf hep "+1 CAN" (kullanıcı 2026-08-23: "kafa karıştırıyor").
        string gain = Loc.T("lifeGained");   // "+1 CAN"

        var adBtn = UiButtons.Build(C, new Vector2(0.5f, 0.5f), new Vector2(0, -140), new Vector2(620, 116),
            $"{Loc.T("watchAd")}   →   {gain}", UiButtons.Video(), new Color(0.85f, 1f, 0.85f), 34, true);
        var adLbl = adBtn.GetComponentInChildren<TMP_Text>();
        adBtn.interactable = adReady;
        if (!adReady) adLbl.text = Loc.T("adFailed");

        var coinBtn = UiButtons.Build(C, new Vector2(0.5f, 0.5f), new Vector2(0, -290), new Vector2(620, 116),
            $"{cost}   →   {gain}", UiButtons.Coin(), new Color(1f, 0.9f, 0.5f), 34, true);
        var coinLbl = coinBtn.GetComponentInChildren<TMP_Text>();

        var cancel = UiButtons.Build(C, new Vector2(0.5f, 0.5f), new Vector2(0, -440), new Vector2(620, 106),
            Loc.T("cancel"), null, new Color(1f, 1f, 1f, 0.85f), 36);
        cancel.onClick.AddListener(() => Destroy(panel));

        // Ortak tazeleme: kalpler, coin bakiyesi, buton durumları, geri sayım
        System.Action refresh = () =>
        {
            var lm = LivesManager.Instance;
            int lives = lm != null ? lm.Lives : 0;
            for (int i = 0; i < heartImgs.Length; i++)
                if (heartImgs[i] != null) heartImgs[i].sprite = i < lives ? HeartArt.Full() : HeartArt.Empty();
            coinRow.text = Loc.T("coins") + " " + PlayerProfile.Coins;
            bool afford = PlayerProfile.CanAfford(cost);
            coinBtn.interactable = afford;
            coinLbl.color = afford ? new Color(0.2f, 0.15f, 0.05f) : new Color(0.45f, 0.4f, 0.35f);
        };
        refresh();

        // Can kazanınca: animasyon + tazele; can varsa paneli kapat (oyuncu tekrar basıp oynasın)
        System.Action<int> onGained = n =>
        {
            LivesManager.Instance?.AddLife(n);
            CloudSyncService.Instance?.FlushNow();
            refresh();
            AudioManager.Instance?.PlayUiClick();
            StartCoroutine(LifeGainedAnim(C, () => { if (panel != null) Destroy(panel); }));
        };

        adBtn.onClick.AddListener(() =>
        {
            var a = AdManager.Instance;
            if (a == null || !a.RewardedReady) { adLbl.text = Loc.T("adFailed"); adBtn.interactable = false; return; }
            adBtn.interactable = false; adLbl.text = Loc.T("adLoading");
            a.ShowRewarded("no_lives_refill",
                onReward: () => onGained(1),
                onUnavailable: () => { adLbl.text = Loc.T("adFailed"); });
        });

        coinBtn.onClick.AddListener(() =>
        {
            if (!PlayerProfile.TrySpendCoins(cost)) { ToastUI.Show(Loc.T("notEnoughCoins"), null, ToastUI.Style.Error); refresh(); return; }
            onGained(1);
        });

        StartCoroutine(NoLivesTicker(panel, timer, refresh));
    }

    // Geri sayımı canlı tutar (sonraki can) + panel açıkken bakiye/kalp durumunu tazeler.
    System.Collections.IEnumerator NoLivesTicker(GameObject panel, TMP_Text timer, System.Action refresh)
    {
        while (panel != null)
        {
            var lm = LivesManager.Instance;
            if (lm != null && !lm.IsFull)
            {
                int s = Mathf.Max(0, lm.SecondsToNextLife());
                timer.text = $"{Loc.T("nextLifeIn")} {s / 60:00}:{s % 60:00}";
            }
            else timer.text = "";
            refresh();
            yield return new WaitForSecondsRealtime(1f);
        }
    }

    // "+1 CAN" — merkezden büyüyerek belirir, yukarı süzülüp kaybolur; sonra onDone (paneli kapat).
    System.Collections.IEnumerator LifeGainedAnim(Transform parent, System.Action onDone)
    {
        var go = new GameObject("LifeGained", typeof(RectTransform), typeof(CanvasGroup));
        var rt = (RectTransform)go.transform; rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0, 300); rt.sizeDelta = new Vector2(600, 220);
        var cg = go.GetComponent<CanvasGroup>();

        var heart = NewImage("H", rt, HeartArt.Full());
        heart.preserveAspect = true; heart.raycastTarget = false;
        var hr = heart.rectTransform; hr.anchorMin = hr.anchorMax = hr.pivot = new Vector2(0.5f, 0.5f);
        hr.anchoredPosition = new Vector2(0, 40); hr.sizeDelta = new Vector2(130, 130);

        var txt = NewText("T", rt, 56, FontStyles.Bold, TextAlignmentOptions.Center);
        txt.text = Loc.T("lifeGained"); txt.color = new Color(1f, 0.55f, 0.55f);
        var tr = txt.rectTransform; tr.anchorMin = tr.anchorMax = tr.pivot = new Vector2(0.5f, 0.5f);
        tr.anchoredPosition = new Vector2(0, -70); tr.sizeDelta = new Vector2(560, 80);

        // ⚠️ Ödüllü reklamdan dönerken ilk karenin unscaledDeltaTime'ı devasa olur (uygulama duraklamıştı) →
        // sınırlanmazsa animasyon tek karede biter ve hiç görünmez. Kare başına tavan koy + dönüş karesini yut.
        yield return null;
        float t = 0f;
        while (t < 1.15f)
        {
            t += Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            float pop = t < 0.28f ? Mathf.SmoothStep(0.4f, 1.18f, t / 0.28f)
                      : t < 0.42f ? Mathf.Lerp(1.18f, 1f, (t - 0.28f) / 0.14f) : 1f;
            rt.localScale = Vector3.one * pop;
            rt.anchoredPosition = new Vector2(0, 300 + Mathf.Max(0f, t - 0.5f) * 150f);
            cg.alpha = t < 0.8f ? 1f : 1f - (t - 0.8f) / 0.35f;
            yield return null;
        }
        if (go != null) Destroy(go);
        onDone?.Invoke();
    }

    // Ekran ortasına göre konumlandırma yardımcısı (panel içi öğeler).
    static void Mid(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(x, y); rt.sizeDelta = new Vector2(w, h);
    }

    // ── MAĞAZA (paketler: coin + güç-up karışımı, fiyatlı; seç → ödeme) ──
    void ShowStore()
    {
        var panel = NewPanel("StorePanel");
        var dim = NewImage("Dim", panel.transform, null); Stretch(dim.rectTransform);
        dim.color = new Color(0.08f, 0.06f, 0.05f, 0.92f); dim.raycastTarget = true;
        var safe = new GameObject("StoreSafe", typeof(RectTransform), typeof(SafeArea));
        var srt = (RectTransform)safe.transform; srt.SetParent(panel.transform, false); Stretch(srt);

        var title = NewText("Title", safe.transform, 58, FontStyles.Bold, TextAlignmentOptions.Center);
        title.text = Loc.T("store"); title.color = new Color(1f, 0.95f, 0.75f); Top(title.rectTransform, 0, -40, 600, 84);

        var coinBadge = MakeCoinBadge(safe.transform);   // sol-üst coin rozeti (otomatik güncellenir)
        var free = UiButtons.Build(safe.transform, new Vector2(0.5f, 1f), new Vector2(0, -140), new Vector2(460, 92),
            Loc.T("freeCoins"), UiButtons.Video(), new Color(1f, 0.92f, 0.6f), 34, true);
        free.onClick.AddListener(WatchAdForCoins);

        string coin = Loc.En ? "Coins" : "Coin";
        float y = -260f;
        foreach (var p in IapService.Packs)
        {
            if (p.noAds) continue;   // Reklamsız ayrı ekranda (ShowNoAds)
            var pk = p;   // closure için sabitle
            string price = IapService.Instance?.GetPrice(pk.id) ?? Loc.Price(pk.priceTier);   // gerçek fiyat yoksa placeholder
            StoreCard(safe.transform, Loc.T(pk.locKey), PackContents(pk, coin), price, ref y,
                () => Buy(Loc.T(pk.locKey), pk.id));
        }

        var x = UiButtons.Build(safe.transform, new Vector2(1f, 1f), new Vector2(-24, -30), new Vector2(92, 92), "X", null, new Color(1f, 0.8f, 0.75f), 44);
        x.onClick.AddListener(() => Destroy(panel));

        CenterVertically(safe.transform, x.transform, coinBadge);   // dikey ortala (X + coin rozeti köşede kalır)
    }

    void StoreCard(Transform parent, string name, string contents, string price, ref float y, System.Action onBuy)
    {
        var go = new GameObject("Pack_" + name, typeof(RectTransform), typeof(Image), typeof(Button));
        var rt = (RectTransform)go.transform; rt.SetParent(parent, false); Top(rt, 0, y, 780, 196);   // daha yüksek kart
        var img = go.GetComponent<Image>(); UiButtons.ApplyFrame(img, 196f); img.color = new Color(1f, 0.97f, 0.85f);

        // Ad (üst-sol): padding + otomatik küçülme (taşma yok)
        var nameT = NewText("N", rt, 40, FontStyles.Bold, TextAlignmentOptions.BottomLeft);
        nameT.color = new Color(0.2f, 0.14f, 0.07f); nameT.text = name;
        nameT.enableAutoSizing = true; nameT.fontSizeMin = 22f; nameT.fontSizeMax = 40f; nameT.enableWordWrapping = false; nameT.overflowMode = TextOverflowModes.Ellipsis;
        var nr = nameT.rectTransform; nr.anchorMin = new Vector2(0, 0.5f); nr.anchorMax = new Vector2(0.62f, 1f); nr.offsetMin = new Vector2(56, 4); nr.offsetMax = new Vector2(-8, -18);
        // İçerik (alt-sol): 2 satıra sarabilir + padding
        var contT = NewText("C", rt, 28, FontStyles.Normal, TextAlignmentOptions.TopLeft);
        contT.color = new Color(0.35f, 0.28f, 0.18f); contT.text = contents;
        contT.enableAutoSizing = true; contT.fontSizeMin = 18f; contT.fontSizeMax = 28f; contT.enableWordWrapping = true; contT.overflowMode = TextOverflowModes.Ellipsis;
        var cr = contT.rectTransform; cr.anchorMin = new Vector2(0, 0f); cr.anchorMax = new Vector2(0.64f, 0.5f); cr.offsetMin = new Vector2(56, 18); cr.offsetMax = new Vector2(-8, -4);
        // Fiyat (sağ, dikey ortalı): padding + otomatik küçülme
        var priceT = NewText("P", rt, 44, FontStyles.Bold, TextAlignmentOptions.Right);
        priceT.color = new Color(0.15f, 0.55f, 0.2f); priceT.text = price;
        priceT.enableAutoSizing = true; priceT.fontSizeMin = 26f; priceT.fontSizeMax = 44f; priceT.enableWordWrapping = false; priceT.overflowMode = TextOverflowModes.Ellipsis;
        var pr = priceT.rectTransform; pr.anchorMin = new Vector2(0.64f, 0f); pr.anchorMax = new Vector2(1f, 1f); pr.offsetMin = new Vector2(6, 12); pr.offsetMax = new Vector2(-56, -12);

        var btn = go.GetComponent<Button>(); btn.transition = Selectable.Transition.None;
        btn.onClick.AddListener(() => { AudioManager.Instance?.PlayUiClick(); onBuy(); });
        y -= 218f;   // kart yüksekliği + boşluk
    }

    void Buy(string name, string productId)
    {
        // IAP hazır değilse (ürünler mağazada yok / Play'den kurulmamış / çevrimdışı) SESSİZ kalma — kullanıcıya söyle.
        if (IapService.Instance == null || !IapService.Instance.IsReady) { ToastUI.Show(Loc.T("storeUnavailable"), null, ToastUI.Style.Error); return; }
        Store.Buy(productId, ok =>
        {
            if (ok) ToastUI.Show(Loc.T("purchased") + " " + name, null, ToastUI.Style.Success);   // ok=false → kullanıcı iptal etti; sessiz kalmak doğru
        });
    }

    // Paket içeriğini (coin + güç-up'lar) yerelleştirilmiş metne çevirir (katalog = tek kaynak, IapService.Packs).
    string PackContents(IapService.Pack p, string coin)
    {
        var parts = new System.Collections.Generic.List<string>();
        if (p.coins > 0) parts.Add($"{p.coins} {coin}");
        if (p.powers != null) foreach (var (t, a) in p.powers) parts.Add($"{a} {Loc.PowerName(t)}");
        return string.Join(" + ", parts);
    }

    // ════════ GÜÇLER MAĞAZASI (powerup'ı COIN veya REKLAM ile al) ════════
    // 4 sütun: üstte powerup ikonu + sahip olunan miktar (×N); altında [coin fiyatı] + [reklam] butonu.
    // Satın alınca / reklam ödülünde: uçan ikon miktar yazısına gider → sayı artar (pop).
    static readonly PowerUpType[] ShopPowers = { PowerUpType.Speed, PowerUpType.Magnet, PowerUpType.SizeBurst, PowerUpType.Super };
    Dictionary<PowerUpType, TMP_Text> _invCounts;
    Dictionary<PowerUpType, RectTransform> _invSlots;

    void ShowPowerupShop()
    {
        _invCounts = new Dictionary<PowerUpType, TMP_Text>();
        _invSlots = new Dictionary<PowerUpType, RectTransform>();

        var panel = NewPanel("PowerupShop");
        var dim = NewImage("Dim", panel.transform, null); Stretch(dim.rectTransform);
        dim.color = new Color(0.08f, 0.06f, 0.05f, 0.92f); dim.raycastTarget = true;
        var safe = new GameObject("PShopSafe", typeof(RectTransform), typeof(SafeArea));
        var srt = (RectTransform)safe.transform; srt.SetParent(panel.transform, false); Stretch(srt);

        var title = NewText("Title", safe.transform, 58, FontStyles.Bold, TextAlignmentOptions.Center);
        title.text = Loc.T("powerupsTitle"); title.color = new Color(1f, 0.95f, 0.75f); Top(title.rectTransform, 0, -40, 600, 84);

        var coinBadge = MakeCoinBadge(safe.transform);

        var free = UiButtons.Build(safe.transform, new Vector2(0.5f, 1f), new Vector2(0, -150), new Vector2(460, 92),
            Loc.T("freeCoins"), UiButtons.Video(), new Color(1f, 0.92f, 0.6f), 34, true);
        free.onClick.AddListener(WatchAdForCoins);

        float[] xs = { -330f, -110f, 110f, 330f };
        for (int i = 0; i < ShopPowers.Length; i++) PowerupColumn(safe.transform, ShopPowers[i], xs[i]);

        var x = UiButtons.Build(safe.transform, new Vector2(1f, 1f), new Vector2(-24, -30), new Vector2(92, 92), "X", null, new Color(1f, 0.8f, 0.75f), 44);
        x.onClick.AddListener(() => Destroy(panel));

        CenterVertically(safe.transform, x.transform, coinBadge);
    }

    // Bir powerup sütunu: ikon + ×miktar + coin-fiyat butonu + reklam butonu.
    void PowerupColumn(Transform parent, PowerUpType t, float x)
    {
        var ic = NewImage("PU_" + t, parent, PowerUpIcons.Get(t)); ic.preserveAspect = true; ic.raycastTarget = false;
        var ir = ic.rectTransform; ir.anchorMin = ir.anchorMax = new Vector2(0.5f, 1f); ir.pivot = new Vector2(0.5f, 1f);
        ir.anchoredPosition = new Vector2(x, -250); ir.sizeDelta = new Vector2(130, 130);

        var cnt = NewText("Cnt_" + t, parent, 42, FontStyles.Bold, TextAlignmentOptions.Center);
        cnt.color = new Color(1f, 0.95f, 0.7f); cnt.text = "×" + PowerUpInventory.Count(t);
        var cr = cnt.rectTransform; cr.anchorMin = cr.anchorMax = new Vector2(0.5f, 1f); cr.pivot = new Vector2(0.5f, 1f);
        cr.anchoredPosition = new Vector2(x, -392); cr.sizeDelta = new Vector2(200, 54);
        _invCounts[t] = cnt; _invSlots[t] = cr;

        var coinBtn = UiButtons.Build(parent, new Vector2(0.5f, 1f), new Vector2(x, -452), new Vector2(200, 88),
            Economy.PowerupCost(t).ToString(), UiButtons.Coin(), new Color(1f, 0.9f, 0.5f), 34, true);
        var crt = (RectTransform)coinBtn.transform;
        coinBtn.onClick.AddListener(() => BuyPowerupWithCoins(t, crt));

        var adBtn = UiButtons.Build(parent, new Vector2(0.5f, 1f), new Vector2(x, -548), new Vector2(200, 88),
            "", UiButtons.Video(), new Color(0.72f, 0.86f, 1f), 32, true);
        var art = (RectTransform)adBtn.transform;
        adBtn.onClick.AddListener(() => WatchAdForPowerup(t, art));
    }

    // ── Ekonomi aksiyonları (coin / reklam) ──
    void WatchAdForCoins()
    {
        var am = AdManager.Instance;
        if (am == null || !am.RewardedReady) { ToastUI.Show(Loc.T("adFailed"), null, ToastUI.Style.Error); return; }
        am.ShowRewarded("free_coins",
            onReward: () => { PlayerProfile.AddCoins(Economy.FreeCoinsPerAd); ToastUI.Show("+" + Economy.FreeCoinsPerAd, UiButtons.Coin(), ToastUI.Style.Reward); },
            onUnavailable: () => ToastUI.Show(Loc.T("adFailed"), null, ToastUI.Style.Error));
    }

    void BuyPowerupWithCoins(PowerUpType t, RectTransform from)
    {
        if (!PlayerProfile.TrySpendCoins(Economy.PowerupCost(t))) { ToastUI.Show(Loc.T("notEnoughCoins"), null, ToastUI.Style.Error); return; }
        PowerUpInventory.Add(t, 1);
        CloudSyncService.Instance?.FlushNow();
        StartCoroutine(FlyPowerupToSlot(t, from));
    }

    void WatchAdForPowerup(PowerUpType t, RectTransform from)
    {
        var am = AdManager.Instance;
        if (am == null || !am.RewardedReady) { ToastUI.Show(Loc.T("adFailed"), null, ToastUI.Style.Error); return; }
        am.ShowRewarded("pu_" + t,
            onReward: () => { PowerUpInventory.Add(t, 1); CloudSyncService.Instance?.FlushNow(); StartCoroutine(FlyPowerupToSlot(t, from)); },
            onUnavailable: () => ToastUI.Show(Loc.T("adFailed"), null, ToastUI.Style.Error));
    }

    // Kazanılan powerup ikonu 'from' butonundan ×miktar yazısına uçar → sayıyı günceller + pop.
    IEnumerator FlyPowerupToSlot(PowerUpType t, RectTransform from)
    {
        if (_invSlots == null || !_invSlots.TryGetValue(t, out var slot) || slot == null || from == null) { UpdateInvCount(t); yield break; }
        var go = new GameObject("Fly", typeof(RectTransform), typeof(Image));
        var fr = (RectTransform)go.transform; fr.SetParent(slot.parent, false);
        var img = go.GetComponent<Image>(); img.sprite = PowerUpIcons.Get(t); img.preserveAspect = true; img.raycastTarget = false;
        fr.position = from.position; fr.sizeDelta = new Vector2(110, 110);
        Vector3 p0 = fr.position, target = slot.position; float dur = 0.5f, tt = 0f;
        while (tt < dur)
        {
            tt += Time.unscaledDeltaTime; float k = Mathf.SmoothStep(0f, 1f, tt / dur);
            fr.position = Vector3.Lerp(p0, target, k); fr.localScale = Vector3.one * Mathf.Lerp(1.3f, 0.6f, k);
            yield return null;
        }
        Destroy(go);
        UpdateInvCount(t);
        yield return PulseRect(slot);
    }

    void UpdateInvCount(PowerUpType t)
    {
        if (_invCounts != null && _invCounts.TryGetValue(t, out var c) && c != null) c.text = "×" + PowerUpInventory.Count(t);
    }

    IEnumerator PulseRect(RectTransform rt)
    {
        float dur = 0.3f, tt = 0f;
        while (tt < dur) { tt += Time.unscaledDeltaTime; float k = tt / dur; if (rt != null) rt.localScale = Vector3.one * (1f + 0.4f * Mathf.Sin(k * Mathf.PI)); yield return null; }
        if (rt != null) rt.localScale = Vector3.one;
    }

    // Bildirimler ortak bileşende (ToastUI) — tüm ekranlarda aynı görünüm/animasyon.
    void Toast(string msg) => ToastUI.Show(msg);

    // Üst-ankraj yerleştirme kısayolu (pivot varsayılan üst-orta; px=0 → sol-hizalı pivot).
    static void Top(RectTransform rt, float x, float y, float w, float h, float px = 0.5f)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f); rt.pivot = new Vector2(px, 1f);
        rt.anchoredPosition = new Vector2(x, y); rt.sizeDelta = new Vector2(w, h);
    }

    // Her kelimenin baş harfi büyük (title-case), kalanı küçük. Türkçe'de i/İ doğru olsun diye tr-TR culture'ı;
    // diğer dillerde invariant (ALL-CAPS'i de normalleştirmek için önce küçült).
    static string TitleCaseName(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return "";
        var ci = Loc.Current == Language.Turkish
            ? new System.Globalization.CultureInfo("tr-TR")
            : System.Globalization.CultureInfo.InvariantCulture;
        return ci.TextInfo.ToTitleCase(s.Trim().ToLower(ci));
    }

    TMP_InputField MakeInputField(Transform parent, string initial, Vector2 pos, Vector2 size, bool center = false)
    {
        var go = new GameObject("Input", typeof(RectTransform), typeof(Image));
        go.SetActive(false);   // ⚠️ referanslar atanana kadar PASİF → TMP_InputField.OnEnable doğru başlar (yoksa tıklama/klavye ölü)
        var rt = (RectTransform)go.transform; rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = center ? new Vector2(0.5f, 0.5f) : new Vector2(0.5f, 1f);
        rt.pivot = center ? new Vector2(0.5f, 0.5f) : new Vector2(0.5f, 1f);
        rt.anchoredPosition = pos; rt.sizeDelta = size;
        var bg = go.GetComponent<Image>(); UiButtons.ApplyFrame(bg, size.y); bg.color = Color.white; bg.raycastTarget = true;

        var input = go.AddComponent<TMP_InputField>();
        var ta = new GameObject("TextArea", typeof(RectTransform), typeof(RectMask2D));
        var tart = (RectTransform)ta.transform; tart.SetParent(rt, false);
        tart.anchorMin = Vector2.zero; tart.anchorMax = Vector2.one; tart.offsetMin = new Vector2(22, 8); tart.offsetMax = new Vector2(-22, -8);

        var ph = NewText("Placeholder", tart, 40, FontStyles.Italic, TextAlignmentOptions.Center);
        ph.color = new Color(0.4f, 0.35f, 0.3f, 0.6f); ph.text = Loc.T("name"); Stretch(ph.rectTransform);
        var txt = NewText("Text", tart, 40, FontStyles.Bold, TextAlignmentOptions.Center);
        txt.color = new Color(0.15f, 0.1f, 0.05f); Stretch(txt.rectTransform);

        input.textViewport = tart; input.textComponent = txt; input.placeholder = ph;
        input.characterLimit = 16; input.lineType = TMP_InputField.LineType.SingleLine;
        input.text = initial;

        // İSİM ALANI kullanıcı içeriği taşır: Latin bir ad ("Onur") Arapça arayüzde bile SOLDAN-SAĞA yazılmalı.
        // Yönü metnin kendisine göre belirle ve yazdıkça güncelle (kullanıcı 2026-08-23).
        Loc.ApplyDir(txt);
        Loc.ApplyDir(ph);                                   // placeholder çevrilmiş metin → kendi yönünü alır
        input.onValueChanged.AddListener(_ => Loc.ApplyDir(txt));

        go.SetActive(true);    // referanslar hazır → şimdi OnEnable düzgün çalışır, tıkla-yaz aktif
        return input;
    }

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
        // METRİK: ana sayfa ve success ekranıyla AYNI olmalı → PlayerProfile.EarnedStars (birikimli ödül yıldızı).
        // Eskiden StarManager.Total() (level başına EN İYİ toplamı) kullanılıyordu → ana sayfada 111, burada 49
        // gibi kafa karıştırıcı fark çıkıyordu (kullanıcı 2026-08-23).
        int shown = LevelResult.StarsAnimated
            ? PlayerProfile.EarnedStars
            : Mathf.Max(0, PlayerProfile.EarnedStars - (LevelResult.Pending ? LevelResult.Stars : 0));

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

    // DÜNYA GEÇİŞ ANİMASYONU: "[X] Tamamlandı! / Yeni dünya açıldı" → sonraki dünya ikonu pop + isim → Loc.T("continue")
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
        title.text = WorldCatalog.LocalizedName(fw) + " " + Loc.T("worldDone"); title.color = new Color(1f, 0.9f, 0.35f);
        title.enableAutoSizing = true; title.fontSizeMin = 34; title.fontSizeMax = 64;
        var tr = title.rectTransform; tr.anchorMin = tr.anchorMax = new Vector2(0.5f, 0.5f); tr.pivot = new Vector2(0.5f, 0.5f);
        tr.anchoredPosition = new Vector2(0, 470); tr.sizeDelta = new Vector2(1000, 130);

        var sub = NewText("Sub", panel.transform, 38, FontStyles.Bold, TextAlignmentOptions.Center);
        sub.text = Loc.T("newWorld"); sub.color = new Color(1f, 1f, 1f, 0.85f);
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
        nm.text = WorldCatalog.LocalizedName(nextW); nm.color = new Color(1f, 0.95f, 0.75f);
        var nr = nm.rectTransform; nr.anchorMin = nr.anchorMax = new Vector2(0.5f, 0.5f); nr.pivot = new Vector2(0.5f, 0.5f);
        nr.anchoredPosition = new Vector2(0, -280); nr.sizeDelta = new Vector2(900, 80);

        bool go = false;
        var btn = UiButtons.Build(panel.transform, new Vector2(0.5f, 0.5f), new Vector2(0, -470),
            new Vector2(460, 124), Loc.T("continue"), UiButtons.Play(), new Color(0.88f, 1f, 0.88f), 44);
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
            gt.text = Loc.T("reward") + " " + string.Join(" + ", gifts); gt.color = new Color(1f, 0.78f, 0.2f);
            var r = gt.rectTransform; r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f); r.pivot = new Vector2(0.5f, 0.5f);
            r.anchoredPosition = new Vector2(0, cy - size * 0.5f - 60f); r.sizeDelta = new Vector2(940, 60);
        }

        // Kazanılan yıldızları sırayla doldur
        yield return new WaitForSeconds(0.35f);
        for (int i = 0; i < earned; i++) { yield return PopIn(full[i]); yield return new WaitForSeconds(0.12f); }

        yield return new WaitForSeconds(0.45f);

        // Dolu yıldızlar uçarak sol üst sayaca girsin; UÇAN HER yıldız sayacı 1 artırır.
        // (Rozet artık birikimli EarnedStars gösteriyor → "net en iyi farkı" değil, kazanılanın tamamı sayılır.)
        Canvas.ForceUpdateCanvases();
        int running = Mathf.Max(0, PlayerProfile.EarnedStars - earned);
        if (starBadgeText != null) starBadgeText.text = running.ToString();
        for (int k = 0; k < earned; k++)
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
            new Vector2(300, 104), Loc.T("worlds"), icon, Color.white, 32);
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
        // Arayüz metinleri için dil-bazlı varsayılan (Arapça arayüz RTL render edilmeli).
        // ⚠️ İSTİSNA: içeriği KULLANICIDAN gelen alanlar (oyuncu adı) Latin olabilir → oralarda
        // Loc.ApplyDir(t) ile yön METNE göre ayarlanır, yoksa "Onur" ters görünür.
        t.isRightToLeftText = Loc.Current == Language.Arabic;
        return t;
    }

    static void Stretch(RectTransform rt) { rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero; }
}

/// <summary>Açık (sıradaki) durağı hafifçe nabız gibi büyütüp küçülten basit efekt.</summary>
public class Pulse : MonoBehaviour
{
    public bool gentle;   // logo: küçük genlik, yavaş "nefes" (varsayılan: patika durağı nabzı)
    RectTransform rt; float t;
    void Start() => rt = (RectTransform)transform;
    void Update()
    {
        t += Time.deltaTime;
        float s = gentle ? 1f + 0.025f * Mathf.Sin(t * 1.6f) : 1f + 0.08f * Mathf.Sin(t * 4f);
        rt.localScale = new Vector3(s, s, 1f);
    }
}
