using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using System.Collections;


public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    [Header("UI")]
    public TMP_Text scoreText;
    public TMP_Text timerText;
    public GameObject successPanel;
    public GameObject failPanel;
    public TMP_Text successScoreText;
    public TMP_Text livesText;

    [Header("Ayarlar")]
    public float levelTime = 60f;

    [Header("Kota/Hedef sistemi")]
    [Tooltip("Hedef/kota takibi + HUD. Boşsa sahnede aranır. Atanmazsa kazanma koşulu çalışmaz.")]
    public ObjectiveTracker objectives;

    int score = 0;
    int maxScore = 0;               // bu levelde alınabilecek toplam puan (yıldız değerlendirmesi için)
    // Oyun-içi HUD ek: sol üst "LEVEL X" + sağ üst yutulan/toplam nesne sayacı.
    TMP_Text levelLabel, swallowLabel;
    int swallowedCount, totalCount;

    // ── YOĞUN YUTMA GERİ-BİLDİRİM THROTTLE (perf) ──────────────────────────────
    // Hız+mıknatısla öbeğe dalınca bir karede onlarca nesne yutuluyordu → her biri için GameObject (skor baloncuğu +
    // hedef ghost) + TMP rebuild = spike/atlama. ÇÖZÜM: PUAN/SAYAÇ tam işlenir ama GÖRSEL geri-bildirim yoğun girişte
    // her ~GROUP nesnede 1'e düşer. Yavaş (izole) yutmada 1:1 (his korunur). Metin karede bir güncellenir (coalesce).
    const int   FB_GROUP    = 3;      // yoğun girişte kaç nesnede 1 baloncuk/ghost
    const float FB_BURSTGAP = 0.12f;  // ardışık yutma bu süreden yakınsa "yoğun giriş" say
    float lastSwallowTime = -1f;
    int   burstCount;                 // son gösterimden beri yutulan (grup sayacı)
    int   burstScore;                 // son gösterimden beri biriken puan (baloncuk bunu gösterir)
    Vector3 lastSwallowPos;
    bool  scoreDirty, labelDirty;     // metinleri karede bir güncelle (rebuild flood önle)
    // Oyun-içi can göstergesi (sol üst, pause altında): kalp + sayı (∞ = sınırsız).
    Image livesHeart;
    TMP_Text livesCountText;
    float timeLeft;
    bool gameActive = true;
    bool timerStarted = false;       // süre, oyuncu deliği İLK hareket ettirince başlar
    HoleController hole;
    public bool IsActive => gameActive;

    // ── Success ekranı yıldız akışı (Next Level'e basınca uçan yıldız → sol üst toplam sayaç) ──
    RectTransform sStarBadgeIcon;
    TMP_Text sStarBadgeText;
    int rewardEarned, rewardDelta;
    bool rewardLastOfWorld, nextPressed;

    void Awake()
    {
        Instance = this;
        // Veri-odaklı: LevelManager (execution order -100) varsa süre aktif LevelData'dan gelir.
        if (LevelManager.Instance != null && LevelManager.Instance.Active != null)
        {
            var active = LevelManager.Instance.Active;
            levelTime = active.levelTime;   // süre DOĞRUDAN LevelData'dan (hard levellar da birebir; yarıya indirme YOK — 2026-07-25 kullanıcı)
            levelTime *= DifficultySettings.TimeMultiplier;   // KOLAY zorlukta süre ×2 (2026-08-06 kullanıcı)
        }
        timeLeft = levelTime;
    }

    void Start()
    {
        successPanel.SetActive(false);
        failPanel.SetActive(false);
        if (objectives == null) objectives = FindAnyObjectByType<ObjectiveTracker>();
        hole = FindAnyObjectByType<HoleController>();

        // Skor/süre yazılarını GÜVENLİ ALAN'a al (üst çentik/status bar altında kalmasın; köşe hizaları korunur).
        var uiCanvas = FindAnyObjectByType<Canvas>();
        if (uiCanvas != null)
        {
            var safe = UiRoot.SafeContent(uiCanvas);
            if (scoreText != null)
            {
                scoreText.transform.SetParent(safe, false);
                scoreText.rectTransform.anchoredPosition = new Vector2(120, -45);   // ÜST satır (LEVEL ile yer değişti)
            }
            if (timerText != null)
            {
                timerText.transform.SetParent(safe, false);
                var trt = timerText.rectTransform;
                trt.anchorMin = trt.anchorMax = new Vector2(1f, 1f); trt.pivot = new Vector2(1f, 0.5f);
                trt.anchoredPosition = new Vector2(-24, -45); trt.sizeDelta = new Vector2(440, 60);   // LEVEL ile aynı hiza (sağ)
                timerText.alignment = TextAlignmentOptions.Right;
            }
            BuildTopHud(safe);
        }

        timerText.text = Loc.T("hudTime") + " " + Mathf.CeilToInt(timeLeft);   // başlamadan tam süreyi göster
        if (scoreText != null) scoreText.text = Loc.T("hudScore") + " " + score;   // başlangıç skoru dile göre

        // Yıldız değerlendirmesi için MAKS puan = sahnedeki tüm yutulabilir (bomba hariç) nesnelerin skoru.
        // LevelManager (execution order -100) spawn'ları Awake'te bitirdi → burada All hazır. Toplam nesne = aynı sayım.
        maxScore = 0; totalCount = 0; swallowedCount = 0;
        var all = PhysicsSwallowable.All;
        for (int i = 0; i < all.Count; i++)
            if (all[i] != null && !all[i].isBomb) { maxScore += all[i].scoreValue; totalCount++; }
        UpdateSwallowLabel();

        // HARD level: başlangıçta "HARD LEVEL" + kurukafa intro'su (1sn dur → 2sn büyüyerek kaybol).
        if (LevelManager.Instance != null && LevelManager.Instance.Active != null && LevelManager.Instance.Active.IsHard)
            HardLevelIntro.Show();
    }

    // Oyun-içi HUD: sol üst "LEVEL X" (skorun üstünde) + sağ üst yutulan/toplam nesne sayacı.
    void BuildTopHud(Transform safe)
    {
        var font = scoreText != null ? scoreText.font : null;

        int li = (LevelManager.Instance != null && LevelManager.Instance.Active != null)
            ? LevelManager.Instance.Active.levelIndex : LevelManager.CurrentIndex;
        levelLabel = MakeHudText(safe, "LevelLabel", new Vector2(0f, 1f), new Vector2(120, -100), new Vector2(320, 48), font);   // Score ile yer değişti (alt satır)
        levelLabel.text = Loc.T("level") + " " + (li + 1);

        // Sağ üst yutulan/toplam — Score ile aynı hiza (y=-100). "Time : XX" ise timer metnine gömülü (BuildTopHud öncesi konumlandı).
        swallowLabel = MakeHudText(safe, "SwallowCount", new Vector2(1f, 1f), new Vector2(-140, -100), new Vector2(320, 48), font);

        // Can göstergesi: kalp + sayı (sol üst, pause ALTINDA). HorizontalLayoutGroup + MiddleLeft → GARANTİ dikey ortalı.
        const float hb = 54f;
        var grpGo = new GameObject("LivesGroup", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        var grt = (RectTransform)grpGo.transform; grt.SetParent(safe, false);
        grt.anchorMin = grt.anchorMax = new Vector2(0f, 1f); grt.pivot = new Vector2(0f, 1f);
        grt.anchoredPosition = new Vector2(24, -238); grt.sizeDelta = new Vector2(180, hb);
        var hlg = grpGo.GetComponent<HorizontalLayoutGroup>();
        hlg.childAlignment = TextAnchor.MiddleLeft; hlg.spacing = 10f;
        hlg.childControlWidth = hlg.childControlHeight = false;
        hlg.childForceExpandWidth = hlg.childForceExpandHeight = false;

        var hGo = new GameObject("LivesHeart", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        hGo.transform.SetParent(grt, false);
        ((RectTransform)hGo.transform).sizeDelta = new Vector2(hb, hb);
        var hle = hGo.GetComponent<LayoutElement>(); hle.preferredWidth = hb; hle.preferredHeight = hb;
        livesHeart = hGo.GetComponent<Image>(); livesHeart.sprite = HeartArt.Full(); livesHeart.preserveAspect = true; livesHeart.raycastTarget = false;

        var cGo = new GameObject("LivesCount", typeof(RectTransform), typeof(LayoutElement));
        cGo.transform.SetParent(grt, false);
        livesCountText = cGo.AddComponent<TextMeshProUGUI>();
        if (font != null) livesCountText.font = font;
        livesCountText.fontSize = 42; livesCountText.fontStyle = FontStyles.Bold;
        livesCountText.alignment = TextAlignmentOptions.Left;
        livesCountText.color = new Color(1f, 0.9f, 0.9f); livesCountText.raycastTarget = false;
        ((RectTransform)cGo.transform).sizeDelta = new Vector2(80f, hb);
        var cle = cGo.GetComponent<LayoutElement>(); cle.preferredWidth = 80f; cle.preferredHeight = hb;
        UpdateLivesIndicator();

        // Üst satır (Score/Time) ↔ alt satır (LEVEL/Swallow) yazı stillerini (font/size/bold) TAKAS et.
        SwapTextStyle(scoreText, levelLabel);
        SwapTextStyle(timerText, swallowLabel);
    }

    // İki TMP metnin font/fontSize/fontStyle değerlerini takas eder (renk/konum/hizalama korunur).
    static void SwapTextStyle(TMP_Text a, TMP_Text b)
    {
        if (a == null || b == null) return;
        var fa = a.font; float sa = a.fontSize; var ya = a.fontStyle;
        a.font = b.font; a.fontSize = b.fontSize; a.fontStyle = b.fontStyle;
        b.font = fa; b.fontSize = sa; b.fontStyle = ya;
    }

    // Oyun-içi can göstergesini güncelle (sınırsızsa ∞; 0'da boş kalp).
    void UpdateLivesIndicator()
    {
        if (livesCountText == null) return;
        var lm = LivesManager.Instance;
        if (lm != null && lm.UnlimitedActive)
        {
            livesCountText.text = "∞";   // ∞
            if (livesHeart != null) livesHeart.sprite = HeartArt.Full();
        }
        else
        {
            int l = lm != null ? lm.Lives : LivesManager.MaxLives;
            livesCountText.text = l.ToString();
            if (livesHeart != null) livesHeart.sprite = l > 0 ? HeartArt.Full() : HeartArt.Empty();
        }
    }

    TMP_Text MakeHudText(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size, TMP_FontAsset font)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var t = go.AddComponent<TextMeshProUGUI>();
        var rt = (RectTransform)go.transform; rt.SetParent(parent, false);
        if (font != null) t.font = font;
        t.fontSize = 38; t.fontStyle = FontStyles.Bold; t.alignment = TextAlignmentOptions.Center;
        t.color = new Color(1f, 0.95f, 0.8f); t.raycastTarget = false;
        rt.anchorMin = rt.anchorMax = anchor; rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos; rt.sizeDelta = size;
        return t;
    }

    void UpdateSwallowLabel()
    {
        if (swallowLabel != null) swallowLabel.text = swallowedCount + " / " + totalCount;
    }

    void Update()
    {
        // Yutma geri-bildirimi coalesce: metinleri karede BİR güncelle (yoğun yutmada N rebuild yerine 1).
        if (scoreDirty && scoreText != null) { scoreText.text = Loc.T("hudScore") + " " + score; scoreDirty = false; }
        if (labelDirty) { UpdateSwallowLabel(); labelDirty = false; }
        // Yoğun giriş bitince (kısa boşluk) grupta kalan puanı tek baloncukta göster (kuyruk flush → puan görsel kaybolmaz).
        if (burstCount > 0 && Time.unscaledTime - lastSwallowTime > FB_BURSTGAP)
        {
            if (objectives != null) objectives.SpawnScorePopup(burstScore, lastSwallowPos);
            burstScore = 0; burstCount = 0;
        }

        // Fail panelindeyken canları canlı tut (geri sayım tik + can gelince Retry aktifleşir).
        if (failPanel != null && failPanel.activeSelf) { RefreshFailLives(false); return; }
        if (!gameActive) return;

        UpdateLivesIndicator();   // regen/sınırsız değişimi oyun içinde yansısın (ucuz)

        // Süre, oyuncu deliği İLK kez hareket ettirene kadar başlamaz (oyun direkt başlar, sayaç bekler).
        if (!timerStarted)
        {
            if (hole != null && hole.CurrentVelocity.sqrMagnitude > 0.0001f) timerStarted = true;
            else return;
        }

        timeLeft -= Time.deltaTime;

        if (timeLeft <= 0f)
        {
            timeLeft = 0f;
            if (timerText != null) timerText.text = Loc.T("hudTime") + " 0";   // sayaç 0 göstersin (eskiden son karedeki 1'de donuyordu)
            TriggerFail();
            return;
        }

        timerText.text = Loc.T("hudTime") + " " + Mathf.CeilToInt(timeLeft);
    }

    public void AddScore(int points)
    {
        if (!gameActive) return;
        score += points;
        scoreDirty = true;   // metin karede bir güncellenir (Update coalesce)
    }

    /// <summary>
    /// Bir nesne yutulduğunda PhysicsSwallowable buraya bildirir.
    /// Bomba → anında fail. Değilse skor + hedef/kota sistemine ilet (ghost + sayaç).
    /// Kazanma koşulu artık ObjectiveTracker'dan gelir (eski "hepsini ye" köprüsü kaldırıldı).
    /// </summary>
    public void ReportSwallowed(PhysicsSwallowable s, Vector3 worldPos)
    {
        if (!gameActive || s == null) return;

        if (s.isBomb) { OnBombSwallowed(worldPos); return; }

        AudioManager.Instance?.PlaySwallow(s.SwallowSize);   // gerçek boyuta göre (sahne dağılımı) küçük/orta/büyük + haptik

        // PUAN + SAYAÇ: HER ZAMAN tam işlenir (arka planda). Metin karede bir güncellenir (coalesce → rebuild flood yok).
        score += s.scoreValue; scoreDirty = true;
        swallowedCount++; labelDirty = true;

        // GÖRSEL geri-bildirim (baloncuk + ghost): yoğun girişte her ~FB_GROUP nesnede 1; izole yutmada 1:1.
        float now = Time.unscaledTime;
        bool burst = (now - lastSwallowTime) < FB_BURSTGAP;
        lastSwallowTime = now; lastSwallowPos = worldPos;
        burstScore += s.scoreValue; burstCount++;
        bool show = !burst || burstCount >= FB_GROUP;

        if (objectives != null)
        {
            if (show)
            {
                objectives.SpawnScorePopup(burstScore, worldPos);   // birikmiş puanı tek baloncukta (his korunur)
                burstScore = 0; burstCount = 0;
            }
            objectives.ReportSwallow(s.ResolvedType, worldPos, show);   // hedef sayacı hep işler; ghost sadece show'da uçar
        }

        // Güç-Up: özel nesneyse otomatik geçici güç ver (Sprint 4)
        if (s.powerUp != PowerUpType.None && PowerUpManager.Instance != null)
            PowerUpManager.Instance.Activate(s.powerUp, worldPos);
    }





    /// <summary>
    /// Geriye-dönük uyumluluk: eski script'li <c>Swallowable</c> hâlâ bunu çağırıyor (GameScene'de
    /// kullanılmıyor; fizik sistemi <see cref="ReportSwallowed"/> kullanır). No-op — eski sahneler bozulmasın.
    /// </summary>
    public void ObjectSwallowed() { }

    public void TriggerSuccess()
    {
        if (!gameActive) return;
        gameActive = false;
        successScoreText.text = Loc.T("score") + " " + score;
        successPanel.SetActive(true);
        SetPanelTitle(successPanel, Loc.T("congrats"));   // baked "TEBRİKLER!" → dile göre
        AudioManager.Instance?.PlaySuccess();
        // İlerleme: sonraki level'ı kalıcı aç (PlayerPrefs)
        if (LevelManager.Instance != null) LevelManager.Instance.SaveProgressOnSuccess();

        // ── Yıldız derecelendirme + kayıt + kilometre taşı hediyeleri ──
        int world = 0, level = 0;
        if (LevelManager.Instance != null && LevelManager.Instance.Active != null)
        { world = LevelManager.Instance.Active.worldId; level = LevelManager.Instance.Active.levelIndex; }

        PlayerProfile.AddScore(score);                           // ömür boyu toplam skora ekle (ana sayfada gösterilir)
        int stars = StarManager.Evaluate(score, maxScore);
        int oldBest = StarManager.Best(world, level);
        int newBest = StarManager.Record(world, level, stars);   // en iyi yıldızı sakla
        int delta = Mathf.Max(0, newBest - oldBest);             // toplam yıldıza net eklenen
        var earned = StarRewards.CheckAndGrant();                // (tür,adet) — kazanılan güç-up'lar
        var giftStrs = StarRewards.Format(earned);
        StarRow.Build(successPanel.transform, stars, new Vector2(0, -320));   // 3 yıldız (skor ile buton ARASINDA; skordan uzak)
        if (giftStrs.Count > 0) ShowGiftText(giftStrs);

        // Patika ekranında (dünya reveal için) gösterilecek ödül özetini taşı.
        LevelResult.Set(world, level, stars, delta, giftStrs);
        LevelResult.StarsAnimated = true;   // ⭐ yıldız/ödül animasyonu ARTIK success ekranında → patika oynamasın (kullanıcı 2026-08-17)

        rewardEarned = stars;
        rewardDelta = delta;
        bool hasNextLevel = LevelManager.Instance != null && LevelManager.Instance.HasNext;
        int nextW = WorldCatalog.NextWorld(world);   // SIRADA sonraki dünya (Order; worldId+1 DEĞİL)
        bool nextWorldExists = nextW >= 0 && WorldCatalog.HasContent(nextW);
        rewardLastOfWorld = !hasNextLevel;   // dünyanın son level'ı → MainMenu'ye (dünya reveal veya menü)
        nextPressed = false;
        BuildSuccessStarBadge(Mathf.Max(0, StarManager.Total() - delta));
        // Etiket: sonraki bölüm var → "Sonraki Bölüm"; son level + sonraki dünya içerikli → "Sonraki Dünya"; yoksa "Ana Menü".
        SetNextButtonLabel(hasNextLevel ? Loc.T("nextLevel") : (nextWorldExists ? Loc.T("nextWorld") : Loc.T("mainMenu")));

        // Devam eden güç-up göstergelerini (süre barları) success ekranında GİZLE (kullanıcı 2026-08-17).
        PowerUpManager.Instance?.EndLevelHud();
        // Yıldız + güç-up uçuş animasyonu OTOMATİK başlar (Sonraki Bölüm'e basmaya gerek yok).
        StartCoroutine(SuccessRewardSequence(earned));
        StartCoroutine(FireworksRoutine());   // kutlama: arka planda renkli havai fişekler
    }

    // Success ekranı ödül akışı (OTOMATİK): önce kazanılan yıldızlar uçarak sol-üst toplam sayaca girer, sonra
    // (varsa) kazanılan güç-up'lar uçarak sağ-üstteki envanter hedeflerine girer ve adet artar. Kullanıcı 2026-08-17.
    IEnumerator SuccessRewardSequence(System.Collections.Generic.List<(PowerUpType type, int count)> earned)
    {
        yield return new WaitForSeconds(0.4f);
        yield return AnimateStarsToBadge();
        if (earned != null && earned.Count > 0)
        {
            yield return new WaitForSeconds(0.25f);
            yield return AnimatePowerupsToInventory(earned);
        }
    }

    // Sol ÜST toplam yıldız rozeti (success paneli). MainMenu'deki MakeStarBadge ile aynı görünüm.
    void BuildSuccessStarBadge(int startValue)
    {
        const float H = 60f, left = 30f, top = -22f, gap = 10f;
        float iconCenterY = top - H * 0.5f;

        var iconGo = new GameObject("StarTotalIcon", typeof(RectTransform), typeof(Image));
        var ir = (RectTransform)iconGo.transform; ir.SetParent(successPanel.transform, false);
        var iImg = iconGo.GetComponent<Image>();
        iImg.sprite = StarArt.Full(); iImg.preserveAspect = true; iImg.raycastTarget = false;
        ir.anchorMin = ir.anchorMax = new Vector2(0f, 1f); ir.pivot = new Vector2(0f, 1f);
        ir.anchoredPosition = new Vector2(left, top); ir.sizeDelta = new Vector2(H, H);
        sStarBadgeIcon = ir;

        var tGo = new GameObject("StarTotal", typeof(RectTransform));
        var t = tGo.AddComponent<TextMeshProUGUI>();
        var tr = (RectTransform)tGo.transform; tr.SetParent(successPanel.transform, false);
        t.fontSize = H * 1.4f; t.fontStyle = FontStyles.Bold; t.alignment = TextAlignmentOptions.MidlineLeft;
        t.color = new Color(1f, 0.92f, 0.5f); t.raycastTarget = false; t.text = startValue.ToString();
        tr.anchorMin = tr.anchorMax = new Vector2(0f, 1f); tr.pivot = new Vector2(0f, 0.5f);
        tr.anchoredPosition = new Vector2(left + H + gap, iconCenterY); tr.sizeDelta = new Vector2(300, H * 2f);
        sStarBadgeText = t;
    }

    void SetNextButtonLabel(string text)
    {
        var lbl = successPanel.transform.Find("NextLevelButton/Label");
        if (lbl != null) { var t = lbl.GetComponent<TMP_Text>(); if (t != null) t.text = text; }
    }

    // Panelin baked "Title" çocuğunu (SuccessScreenBuilder) aktif dile göre günceller.
    static void SetPanelTitle(GameObject panel, string text)
    {
        var tr = panel.transform.Find("Title");
        if (tr != null) { var t = tr.GetComponent<TMP_Text>(); if (t != null) t.text = text; }
    }

    // Yeni kazanılan yıldız hediyelerini başarı ekranında göster.
    void ShowGiftText(System.Collections.Generic.List<string> gifts)
    {
        var go = new GameObject("GiftText", typeof(RectTransform));
        go.transform.SetParent(successPanel.transform, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.fontSize = 34; t.fontStyle = FontStyles.Bold; t.alignment = TextAlignmentOptions.Center;
        t.color = new Color(1f, 0.85f, 0.3f); t.raycastTarget = false;
        t.text = Loc.T("reward") + " " + string.Join(" + ", gifts);
        var rt = t.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f); rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0, -880); rt.sizeDelta = new Vector2(940, 60);   // buton altında (2026-07-24 -740→-880, büyük buton)
    }

    string failReason = "";
    TMP_Text failReasonText;

    // ── Fail can gösterimi: 5 kalp + durum/geri-sayım + can bitince Retry engeli ──
    Image[] failHearts;
    TMP_Text failLivesText;
    Button retryButton;
    int failLivesShown = -1;   // animasyon için son gösterilen can

    public void TriggerFail(string reason = null)
    {
        if (!gameActive) return;
        gameActive = false;
        failReason = reason ?? Loc.T("timeUp");
        ShowFailPanel();
    }

    // Bomba yutuldu: oyunu durdur, delikte patlama + duman göster, fail panelini kısa gecikmeyle aç.
    void OnBombSwallowed(Vector3 pos)
    {
        if (!gameActive) return;
        gameActive = false;
        failReason = Loc.T("bombExploded");
        AudioManager.Instance?.PlayBomb();
        ExplosionEffect.Spawn(pos);
        StartCoroutine(DelayedFail(0.7f));
    }

    IEnumerator DelayedFail(float delay)
    {
        yield return new WaitForSeconds(delay);
        ShowFailPanel();
    }

    void ShowFailPanel()
    {
        if (LivesManager.Instance != null) LivesManager.Instance.LoseLife();   // kalıcı can -1 (sınırsız aktifse eksilmez)
        failPanel.SetActive(true);
        SetPanelTitle(failPanel, Loc.T("failTitle"));   // baked "OLMADI!" → dile göre
        AudioManager.Instance?.PlayFail();
        ShowFailReason();
        BuildFailLives();
        RefreshFailLives(true);   // kaybedilen kalbi animasyonla göster
    }

    // Fail panelinde 5 kalp + durum yazısı (geri sayım) kur (bir kez). Eski statik "LivesText"i gizle, Retry butonunu al.
    void BuildFailLives()
    {
        if (failHearts != null) return;
        var oldLt = failPanel.transform.Find("LivesText");
        if (oldLt != null) { var tmp = oldLt.GetComponent<TMP_Text>(); if (tmp != null) tmp.enabled = false; }

        int n = LivesManager.MaxLives;
        failHearts = new Image[n];
        const float sz = 100f, gap = 20f;   // ~2x kalpler
        float total = n * sz + (n - 1) * gap;
        float x0 = -total * 0.5f + sz * 0.5f;
        for (int i = 0; i < n; i++)
        {
            var go = new GameObject("FailHeart" + i, typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform; rt.SetParent(failPanel.transform, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f); rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(x0 + i * (sz + gap), -110f); rt.sizeDelta = new Vector2(sz, sz);   // 200px aşağı
            var img = go.GetComponent<Image>(); img.preserveAspect = true; img.raycastTarget = false; img.sprite = HeartArt.Full();
            failHearts[i] = img;
        }

        var stGo = new GameObject("FailLivesStatus", typeof(RectTransform));
        failLivesText = stGo.AddComponent<TextMeshProUGUI>();
        var srt = (RectTransform)stGo.transform; srt.SetParent(failPanel.transform, false);
        failLivesText.fontSize = 56; failLivesText.fontStyle = FontStyles.Bold; failLivesText.alignment = TextAlignmentOptions.Center;   // 34→56
        failLivesText.color = new Color(1f, 0.85f, 0.6f); failLivesText.raycastTarget = false;
        srt.anchorMin = srt.anchorMax = new Vector2(0.5f, 0.5f); srt.pivot = new Vector2(0.5f, 0.5f);
        srt.anchoredPosition = new Vector2(0, -250f); srt.sizeDelta = new Vector2(1000, 84);   // 200px aşağı

        retryButton = failPanel.transform.Find("RetryButton")?.GetComponent<Button>();
    }

    // Kalpleri + durum yazısını güncelle; can bitince Retry'yi engelle. animateLoss=true ise kaybedilen kalbi pulse'la.
    void RefreshFailLives(bool animateLoss)
    {
        var lm = LivesManager.Instance;
        if (failHearts == null || lm == null) return;
        int lives = lm.Lives;
        bool unlimited = lm.UnlimitedActive;

        for (int i = 0; i < failHearts.Length; i++)
            failHearts[i].sprite = (unlimited || i < lives) ? HeartArt.Full() : HeartArt.Empty();

        if (unlimited)
            failLivesText.text = $"∞  Sınırsız can  ({Fmt(lm.UnlimitedSecondsLeft)})";
        else if (lives <= 0)
            failLivesText.text = Loc.T("livesOut") + "  " + Loc.T("nextLife") + " " + lm.NextLifeClock();
        else if (lm.IsFull)
            failLivesText.text = "";
        else
            failLivesText.text = Loc.T("nextLife") + " " + lm.NextLifeClock();

        // Can yoksa tekrar oynanamaz → Retry pasif (yalnız Çıkış çalışır); can gelince otomatik aktifleşir.
        bool canPlay = lm.HasLife;
        if (retryButton != null)
        {
            retryButton.interactable = canPlay;
            var img = retryButton.GetComponent<Image>();
            if (img != null) img.color = canPlay ? Color.white : new Color(1f, 1f, 1f, 0.4f);
        }

        // Kaybedilen kalbi (ilk boş) animasyonla vurgula.
        if (animateLoss && !unlimited && lives < failHearts.Length && lives >= 0)
            StartCoroutine(PulseHeart(failHearts[lives].rectTransform));
        failLivesShown = lives;
    }

    static string Fmt(int s) => $"{s / 60:00}:{s % 60:00}";

    IEnumerator PulseHeart(RectTransform rt)
    {
        float t = 0f;
        while (t < 0.4f)
        {
            t += Time.unscaledDeltaTime;
            float k = t / 0.4f;
            rt.localScale = Vector3.one * (1f + 0.5f * Mathf.Sin(k * Mathf.PI));   // 1→1.5→1
            yield return null;
        }
        rt.localScale = Vector3.one;
    }

    // Fail sebebini panelde göster (süre doldu / bomba). İlk çağrıda prosedürel olarak oluşturur.
    void ShowFailReason()
    {
        if (failReasonText == null)
        {
            var go = new GameObject("FailReason", typeof(RectTransform));
            go.transform.SetParent(failPanel.transform, false);
            failReasonText = go.AddComponent<TextMeshProUGUI>();
            failReasonText.fontSize = 72; failReasonText.fontStyle = FontStyles.Bold;   // 40→72 (~2x)
            failReasonText.alignment = TextAlignmentOptions.Center;
            failReasonText.color = new Color(1f, 0.78f, 0.5f); failReasonText.raycastTarget = false;
            var rt = failReasonText.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f); rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0, -390); rt.sizeDelta = new Vector2(1000, 100);   // durum altı, butonlar üstü (200px aşağı)
        }
        failReasonText.text = failReason;
    }

    // "Sonraki Bölüm/Dünya" akışı: kazanılan yıldızlar success ekranında UÇARAK sol üst toplam sayaca girer
    // (hızlı), sonra: dünyanın son level'ı DEĞİLSE → doğrudan sonraki bölüm açılır (GameScene reload); SON
    // LEVEL ise → MainMenu'ye gidip yeni dünya reveal animasyonu oynar (yıldız akışı zaten burada oynandı).
    // TODO(Reklam sprint'i): son level dünya geçişinden önce reklam.
    // "Sonraki Bölüm/Dünya": yıldız/ödül animasyonu ZATEN success ekranında otomatik oynandı → buton yalnız İLERLETİR.
    public void NextLevel()
    {
        if (nextPressed) return;
        nextPressed = true;
        var btn = successPanel.transform.Find("NextLevelButton")?.GetComponent<Button>();
        if (btn != null) btn.interactable = false;
        StopAllCoroutines();   // sürmekte olan ödül animasyonunu bitir

        if (rewardLastOfWorld)
        {
            LevelResult.StarsAnimated = true;   // MainMenu reward-flash'ı ATLASIN (zaten burada oynadık)
            ExitToMainMenu();                    // dünya reveal animasyonu MainMenu'de
        }
        else
        {
            LevelResult.Clear();
            LevelManager.Instance?.AdvanceIndex();
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);   // GameScene reload → sonraki bölüm
        }
    }

    // Kazanılan güç-up'lar: success panelinin SAĞ-ÜSTünde hedef ikonlar + adet; merkezden uçup hedefe girer, adet artar.
    IEnumerator AnimatePowerupsToInventory(System.Collections.Generic.List<(PowerUpType type, int count)> earned)
    {
        Canvas.ForceUpdateCanvases();
        const float isz = 74f, istep = 92f;
        float ty = -440f;   // X kapat tuşunun ALTINDA (kullanıcı 2026-08-17: çakışmasın)
        var tIcon = new System.Collections.Generic.List<RectTransform>();
        var tCnt  = new System.Collections.Generic.List<TMP_Text>();
        var finals = new System.Collections.Generic.List<int>();
        foreach (var e in earned)
        {
            var icon = MakePwIcon(new Vector2(-40f, ty), isz, e.type, new Vector2(1f, 1f));   // sağ-üst
            int final = PowerUpInventory.Count(e.type);
            var cnt = MakePwCount(new Vector2(-40f - isz - 8f, ty - isz * 0.5f), isz, Mathf.Max(0, final - e.count));
            tIcon.Add((RectTransform)icon.transform); tCnt.Add(cnt); finals.Add(final);
            ty -= istep;
        }
        yield return new WaitForSeconds(0.25f);
        for (int i = 0; i < earned.Count; i++)
        {
            // Ekran ORTASINDA doğ → titreyerek büyü → YAVAŞÇA hedefe uç (kullanıcı 2026-08-17).
            var flyer = MakePwIcon(new Vector2(0f, 60f), isz * 1.9f, earned[i].type, new Vector2(0.5f, 0.5f));
            yield return PopShake((RectTransform)flyer.transform);
            yield return new WaitForSeconds(0.12f);
            yield return FlyRectTo((RectTransform)flyer.transform, tIcon[i], 0.75f, 0.48f);
            if (tCnt[i] != null) tCnt[i].text = "×" + finals[i];
            yield return PulseRect(tIcon[i]);
        }
        yield return new WaitForSeconds(0.7f);
    }

    // Ortada doğan güç-up: 0'dan titreyerek büyür (sarsıntı + hafif dönme, sona doğru sönümlenir).
    IEnumerator PopShake(RectTransform rt)
    {
        Vector2 basePos = rt.anchoredPosition; float dur = 0.5f, t = 0f;
        rt.localScale = Vector3.zero;
        while (t < dur)
        {
            t += Time.deltaTime; float k = Mathf.Clamp01(t / dur);
            rt.localScale = Vector3.one * Mathf.SmoothStep(0f, 1f, Mathf.Min(1f, k * 1.2f));
            float sh = 12f * (1f - k);
            rt.anchoredPosition = basePos + new Vector2(Mathf.Sin(t * 70f) * sh, Mathf.Cos(t * 61f) * sh * 0.5f);
            rt.localEulerAngles = new Vector3(0, 0, Mathf.Sin(t * 55f) * 7f * (1f - k));
            yield return null;
        }
        rt.localScale = Vector3.one; rt.anchoredPosition = basePos; rt.localEulerAngles = Vector3.zero;
    }

    Image MakePwIcon(Vector2 pos, float size, PowerUpType t, Vector2 anchor)
    {
        var go = new GameObject("PwIcon", typeof(RectTransform), typeof(Image));
        var rt = (RectTransform)go.transform; rt.SetParent(successPanel.transform, false);
        rt.anchorMin = rt.anchorMax = anchor; rt.pivot = anchor;
        rt.anchoredPosition = pos; rt.sizeDelta = new Vector2(size, size);
        var img = go.GetComponent<Image>(); img.sprite = PowerUpIcons.Get(t); img.preserveAspect = true; img.raycastTarget = false;
        return img;
    }

    TMP_Text MakePwCount(Vector2 pos, float size, int start)
    {
        var go = new GameObject("PwCnt", typeof(RectTransform));
        var rt = (RectTransform)go.transform; rt.SetParent(successPanel.transform, false);
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f); rt.pivot = new Vector2(1f, 0.5f);
        rt.anchoredPosition = pos; rt.sizeDelta = new Vector2(90f, size);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.fontSize = 30; t.fontStyle = FontStyles.Bold; t.alignment = TextAlignmentOptions.MidlineRight;
        t.color = new Color(1f, 0.95f, 0.7f); t.raycastTarget = false; t.text = "×" + start;
        return t;
    }

    IEnumerator FlyRectTo(RectTransform mover, RectTransform target, float dur = 0.34f, float endScale = 0.55f)
    {
        Vector3 start = mover.position, end = target.position;
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime; float k = Mathf.SmoothStep(0f, 1f, t / dur);
            mover.position = Vector3.Lerp(start, end, k);
            mover.localScale = Vector3.one * Mathf.Lerp(1f, endScale, k);
            yield return null;
        }
        Destroy(mover.gameObject);
    }

    // ── KUTLAMA: renkli havai fişekler (success arka planı; içeriğin ALTINDA) ──
    static readonly Color[] FwColors =
    {
        new(1f, 0.35f, 0.35f), new(1f, 0.85f, 0.25f), new(0.35f, 0.85f, 1f),
        new(0.5f, 1f, 0.55f), new(1f, 0.5f, 1f), new(1f, 0.66f, 0.2f),
    };

    IEnumerator FireworksRoutine()
    {
        var container = new GameObject("Fireworks", typeof(RectTransform));
        var crt = (RectTransform)container.transform; crt.SetParent(successPanel.transform, false);
        crt.anchorMin = Vector2.zero; crt.anchorMax = Vector2.one; crt.offsetMin = crt.offsetMax = Vector2.zero;
        crt.SetSiblingIndex(2);   // backdrop(0)+arka plan(1) ÜSTÜNDE, içerik (başlık/maskot/…) ALTINDA
        int i = 0;
        while (successPanel != null && successPanel.activeSelf)
        {
            Vector2 p = new Vector2(UnityEngine.Random.Range(-380f, 380f), UnityEngine.Random.Range(150f, 640f));
            StartCoroutine(FireworkBurst(container.transform, p, FwColors[i % FwColors.Length]));
            i++;
            yield return new WaitForSeconds(UnityEngine.Random.Range(0.4f, 0.75f));
        }
    }

    IEnumerator FireworkBurst(Transform parent, Vector2 center, Color col)
    {
        int n = UnityEngine.Random.Range(11, 17);
        var rts = new RectTransform[n]; var imgs = new Image[n]; var vel = new Vector2[n];
        for (int k = 0; k < n; k++)
        {
            var go = new GameObject("fw", typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform; rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f); rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = center; rt.sizeDelta = new Vector2(16f, 16f);
            var img = go.GetComponent<Image>(); img.sprite = FwDisc(); img.color = col; img.raycastTarget = false;
            float ang = (k / (float)n) * 6.2832f + UnityEngine.Random.Range(-0.2f, 0.2f);
            float spd = UnityEngine.Random.Range(280f, 620f);
            vel[k] = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * spd;
            rts[k] = rt; imgs[k] = img;
        }
        float t = 0f; const float dur = 0.95f;
        while (t < dur)
        {
            float dt = Time.deltaTime; t += dt; float k01 = t / dur;
            for (int k = 0; k < n; k++)
            {
                vel[k].y -= 780f * dt;   // yerçekimi → doğal düşüş
                rts[k].anchoredPosition += vel[k] * dt;
                var c = imgs[k].color; c.a = Mathf.Clamp01(1f - k01); imgs[k].color = c;
                rts[k].localScale = Vector3.one * Mathf.Lerp(1f, 0.5f, k01);
            }
            yield return null;
        }
        for (int k = 0; k < n; k++) if (rts[k]) Destroy(rts[k].gameObject);
    }

    static Sprite _fwDisc;
    static Sprite FwDisc()
    {
        if (_fwDisc) return _fwDisc;
        const int s = 32; var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        float c = (s - 1) * 0.5f;
        for (int y = 0; y < s; y++) for (int x = 0; x < s; x++)
        {
            float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
            float a = Mathf.Clamp01((c - d) / 1.5f);
            tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
        }
        tex.Apply();
        _fwDisc = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f);
        return _fwDisc;
    }

    IEnumerator PulseRect(RectTransform rt)
    {
        float t = 0f;
        while (t < 0.16f) { t += Time.deltaTime; rt.localScale = Vector3.one * (1f + 0.3f * Mathf.Sin(t / 0.16f * Mathf.PI)); yield return null; }
        rt.localScale = Vector3.one;
    }

    // Bu oyunda KAZANILAN (earned) yıldızların HEPSİ StarRow konumundan sol üst rozete uçar (her levelda görünür);
    // toplam sayaç yalnız NET YENİ (delta) kadar artar (daha önce 3★ alınan level'da delta=0 → uçar ama sayaç sabit).
    IEnumerator AnimateStarsToBadge()
    {
        Canvas.ForceUpdateCanvases();
        int running = Mathf.Max(0, StarManager.Total() - rewardDelta);
        if (sStarBadgeText != null) sStarBadgeText.text = running.ToString();
        int flyCount = Mathf.Clamp(rewardEarned, 0, 3);
        if (flyCount <= 0 || sStarBadgeIcon == null) { yield return new WaitForSeconds(0.12f); yield break; }

        int incLeft = Mathf.Clamp(rewardDelta, 0, flyCount);   // sayaç bu kadar artacak (net yeni yıldız)
        float[] xs = { -330f, 0f, 330f };
        for (int k = 0; k < flyCount; k++)
        {
            int idx = Mathf.Clamp(flyCount - 1 - k, 0, 2);   // sağdan sola
            var flyer = MakeFlyerStar(new Vector2(xs[idx], -320f));   // StarRow ile aynı Y
            yield return FlyStarToBadge(flyer);
            if (incLeft > 0) { running++; incLeft--; if (sStarBadgeText != null) sStarBadgeText.text = running.ToString(); }
            yield return BadgePulse();
        }
        yield return new WaitForSeconds(0.1f);
    }

    RectTransform MakeFlyerStar(Vector2 panelPos)
    {
        var go = new GameObject("FlyStar", typeof(RectTransform), typeof(Image));
        var rt = (RectTransform)go.transform; rt.SetParent(successPanel.transform, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f); rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = panelPos; rt.sizeDelta = new Vector2(200f, 200f);
        var img = go.GetComponent<Image>();
        img.sprite = StarArt.Full(); img.preserveAspect = true; img.raycastTarget = false;
        return rt;
    }

    IEnumerator FlyStarToBadge(RectTransform star)
    {
        Vector3 start = star.position, end = sStarBadgeIcon.position;
        const float dur = 0.30f; float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime; float k = Mathf.SmoothStep(0f, 1f, t / dur);
            star.position = Vector3.Lerp(start, end, k);
            star.localScale = Vector3.one * Mathf.Lerp(1f, 0.25f, k);
            yield return null;
        }
        Destroy(star.gameObject);
    }

    IEnumerator BadgePulse()
    {
        float t = 0f;
        while (t < 0.14f) { t += Time.deltaTime; sStarBadgeIcon.localScale = Vector3.one * (1f + 0.3f * Mathf.Sin(t / 0.14f * Mathf.PI)); yield return null; }
        sStarBadgeIcon.localScale = Vector3.one;
    }

    // Aynı level'ı taze kur (CurrentIndex değişmez).
    public void RetryLevel()
    {
        var lm = LivesManager.Instance;
        if (lm != null && !lm.HasLife) { RefreshFailLives(false); return; }   // can yok → tekrar yok (geri sayım göster)
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    [Header("Ana Sayfa")]
    public string mainMenuScene = "MainMenu";

    // Cancel: bölümden çık, ana sayfaya dön. Ana sayfa sahnesi henüz yoksa
    // (Build Settings'te değilse) uyarı verir, oyunu bozmaz.
    public void ExitToMainMenu()
    {
        if (Application.CanStreamedLevelBeLoaded(mainMenuScene))
            SceneManager.LoadScene(mainMenuScene);
        else
            Debug.LogWarning($"[GameManager] '{mainMenuScene}' sahnesi Build Settings'te yok. " +
                             "Ana sayfayı yapınca bu sahneyi ekle.");
    }
}
