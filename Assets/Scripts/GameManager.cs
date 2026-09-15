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
    float scorePunch;                 // skor yazısı zıplama animasyonu (0..1, sönümlenir)
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
        AdManager.Instance?.HideBanner();   // Sprint 8: oynanışta banner yok (menüde açılır)
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
        if (scoreDirty && scoreText != null) { scoreText.text = Loc.T("hudScore") + " " + score; scoreDirty = false; scorePunch = 1f; }
        // Skor punch: değişince kısa bir ölçek zıplaması (1.18→1). Ucuz, karede bir localScale.
        if (scorePunch > 0f && scoreText != null)
        {
            scorePunch = Mathf.Max(0f, scorePunch - Time.unscaledDeltaTime * 5.5f);
            scoreText.rectTransform.localScale = Vector3.one * (1f + 0.18f * scorePunch);
        }
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

        AudioManager.Instance?.PlaySwallow(s.scoreValue);   // PUAN kademesine göre küçük/orta/büyük ses + haptik (collider çapı güvenilmez)

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

        // ── Sprint 7 cila: yutma partikülü + kamera shake (throttle'a saygılı) ──
        if (show)
            SwallowVFX.Play(worldPos, s.SwallowSize);              // toz+parıltı: yalnız görsel-gösterim karesinde (yoğun yutmada tavanlı)
        // Kamera shake: SADECE PUAN kuralı → 60 PUANIN ÜZERİNDE puan getiren nesneler titretir (≤60 titretmez).
        // Her dünyada çalışır (eski SizeFactor≥2 şartı kalktı; kitaplar/hediyeler gibi dev spawn'ı olmayan dünyalarda
        // çalışmıyordu). Aktif kamera HoleCamera (CameraController kapalı).
        if (s.scoreValue > 60)
            HoleCamera.Shake(Mathf.Clamp(0.25f + (s.scoreValue - 60) * 0.0025f, 0.25f, 0.5f));

        // Güç-Up: özel nesneyse otomatik geçici güç ver (Sprint 4)
        if (s.powerUp != PowerUpType.None && PowerUpManager.Instance != null)
            PowerUpManager.Instance.Activate(s.powerUp, worldPos);
    }





    /// <summary>
    /// Geriye-dönük uyumluluk: eski script'li <c>Swallowable</c> hâlâ bunu çağırıyor (GameScene'de
    /// kullanılmıyor; fizik sistemi <see cref="ReportSwallowed"/> kullanır). No-op — eski sahneler bozulmasın.
    /// </summary>
    public void ObjectSwallowed() { }

    // Level-sonu 2× ödül (reklam izle → o levelde kazanılan yıldız+puan bir kez daha eklenir)
    int successStars, successScore;
    bool doubledThisLevel;
    Button doubleButton;
    TMP_Text doubleLabel;

    void BuildDoubleButton()
    {
        if (doubleButton != null) return;
        // YERLEŞİM NOTU: yıldız satırı 300px yüksekliğinde ve merkezi -320 → ALT KENARI -470'te biter.
        // Buton -500'deyken üst kenarı -445 oluyordu, yani yıldızlarla ÜST ÜSTE biniyordu (kullanıcı: "yapışık").
        // -620 → buton üst kenarı -565, yıldızlarla arada ~95px nefes payı kalır.
        doubleButton = UiButtons.Build(successPanel.transform, new Vector2(0.5f, 0.5f), new Vector2(0, -620),
                                       new Vector2(560, 110), Loc.T("doubleReward"), UiButtons.Video(), new Color(1f, 0.88f, 0.4f), 40, true);
        doubleLabel = doubleButton.GetComponentInChildren<TMP_Text>();
        doubleButton.onClick.AddListener(OnWatchAdForDouble);
    }

    void OnWatchAdForDouble()
    {
        var am = AdManager.Instance; if (am == null || doubledThisLevel) return;
        doubleButton.interactable = false; doubleLabel.text = Loc.T("adLoading");
        am.ShowRewarded("double_reward",
            onReward: () =>
            {
                PlayerProfile.AddScore(successScore);            // puanı bir kez daha ekle → 2×
                PlayerProfile.AddEarnedStars(successStars);      // birikimli yıldızı bir kez daha ekle → 2×
                StarRewards.CheckAndGrant();                     // ekstra yıldızdan powerup gelebilir
                CloudSyncService.Instance?.FlushNow();
                doubledThisLevel = true;
                doubleButton.gameObject.SetActive(false);

                // Her İKİ skor göstergesi de katlanmış değeri "2X" etiketiyle göstersin (kullanıcı 2026-08-23:
                // ortadaki 25844 iken sol üstteki 12922'de kalıyordu → katlandığı anlaşılmıyordu).
                int dbl = successScore * 2;
                if (successScoreText != null) successScoreText.text = "2X " + Loc.T("score") + " " + dbl;
                if (scoreText != null) scoreText.text = "2X " + Loc.T("hudScore") + " " + dbl;
                // Reklamdan dönünce ödülün katlandığı ANLAŞILMIYORDU → önce "EKSTRA KAZANÇ" paneli,
                // ardından bölüm sonundaki gibi yıldızlar rozete UÇARAK sayacı artırır (kullanıcı 2026-08-23).
                StartCoroutine(ExtraRewardSequence(successStars, successScore));
            },
            onUnavailable: () => { doubleLabel.text = Loc.T("adFailed"); });
    }

    /// <summary>2× ödülü: önce "EKSTRA KAZANÇ" paneli, sonra ekstra yıldızlar rozete uçar.</summary>
    IEnumerator ExtraRewardSequence(int extraStars, int extraScore)
    {
        yield return ExtraRewardAnim(extraStars, extraScore);
        yield return ExtraStarsToBadge(extraStars);
    }

    /// <summary>
    /// 2× ile kazanılan EKSTRA yıldızlar, bölüm sonundaki animasyonun aynısıyla sol üst rozete uçar ve
    /// sayaç her yıldızda bir artar (kullanıcı 2026-08-23: "sayı öyle artsın").
    /// </summary>
    IEnumerator ExtraStarsToBadge(int count)
    {
        yield return null;   // reklam dönüş karesindeki delta sıçramasını yut
        int target = PlayerProfile.EarnedStars;
        int running = Mathf.Max(0, target - count);
        if (sStarBadgeText != null) sStarBadgeText.text = running.ToString();

        int flyCount = Mathf.Clamp(count, 0, 3);
        if (flyCount > 0 && sStarBadgeIcon != null)
        {
            float[] xs = { -330f, 0f, 330f };
            for (int k = 0; k < flyCount; k++)
            {
                int idx = Mathf.Clamp(flyCount - 1 - k, 0, 2);
                var flyer = MakeFlyerStar(new Vector2(xs[idx], -320f));
                yield return FlyStarToBadge(flyer);
                running++;
                if (sStarBadgeText != null) sStarBadgeText.text = running.ToString();
                yield return BadgePulse();
            }
        }
        if (sStarBadgeText != null) sStarBadgeText.text = target.ToString();   // 3'ten fazlaysa son değere sabitle
    }

    /// <summary>
    /// 2× reklamı bitince: "EKSTRA KAZANÇ" + kazanılan yıldız/puan, ekranın ortasında büyüyerek belirir,
    /// kısa süre durur, yukarı süzülüp kaybolur. Oyuncu ödülün gerçekten katlandığını görür.
    /// </summary>
    IEnumerator ExtraRewardAnim(int extraStars, int extraScore)
    {
        var go = new GameObject("ExtraReward", typeof(RectTransform), typeof(CanvasGroup));
        var rt = (RectTransform)go.transform; rt.SetParent(successPanel.transform, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0, -60); rt.sizeDelta = new Vector2(900, 320);
        rt.SetAsLastSibling();
        var cg = go.GetComponent<CanvasGroup>(); cg.blocksRaycasts = false;

        // Arka fon — yazı sahnenin üstünde okunaklı kalsın
        var bg = new GameObject("Bg", typeof(RectTransform), typeof(Image));
        var brt = (RectTransform)bg.transform; brt.SetParent(rt, false);
        brt.anchorMin = Vector2.zero; brt.anchorMax = Vector2.one; brt.offsetMin = brt.offsetMax = Vector2.zero;
        var bimg = bg.GetComponent<Image>();
        UiButtons.ApplyFrame(bimg, rt.sizeDelta.y);   // 9-slice, bozulmasız çerçeve
        bimg.color = new Color(0.10f, 0.07f, 0.03f, 0.92f); bimg.raycastTarget = false;

        TMP_Text Line(string s, float y, float size, Color c)
        {
            var t = new GameObject("L", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
            var r = t.rectTransform; r.SetParent(rt, false);
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0.5f);
            r.anchoredPosition = new Vector2(0, y); r.sizeDelta = new Vector2(860, size + 26);
            t.fontSize = size; t.fontStyle = FontStyles.Bold; t.alignment = TextAlignmentOptions.Center;
            t.color = c; t.raycastTarget = false; t.text = s;
            return t;
        }

        Line(Loc.T("extraReward"), 96, 52, new Color(1f, 0.85f, 0.3f));
        if (extraStars > 0) Line($"+{extraStars} ★", 14, 62, new Color(1f, 0.95f, 0.5f));
        Line($"+{extraScore}  {Loc.T("score").TrimEnd(':')}", extraStars > 0 ? -74f : -10f, 50, Color.white);

        AudioManager.Instance?.PlaySuccess();

        // ⚠️ Tam ekran reklamdan DÖNERKEN ilk karenin unscaledDeltaTime'ı devasa olur (uygulama duraklamıştı;
        // 15 sn'lik reklam → 15 sn'lik "kare"). Sınırlamazsak animasyon tek karede biter ve HİÇ GÖRÜNMEZ
        // (kullanıcı 2026-08-23: "ödül 2 katına çıkmış ama animasyon yok"). Kare başına tavan koy.
        yield return null;                       // dönüş karesindeki sıçramayı yut
        float t0 = 0f;
        while (t0 < 2.1f)
        {
            t0 += Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            float pop = t0 < 0.3f ? Mathf.SmoothStep(0.5f, 1.12f, t0 / 0.3f)
                      : t0 < 0.45f ? Mathf.Lerp(1.12f, 1f, (t0 - 0.3f) / 0.15f) : 1f;
            rt.localScale = Vector3.one * pop;
            rt.anchoredPosition = new Vector2(0, -60 + Mathf.Max(0f, t0 - 1.4f) * 190f);   // sonda yukarı süzül
            cg.alpha = t0 < 1.6f ? 1f : Mathf.Clamp01(1f - (t0 - 1.6f) / 0.5f);
            yield return null;
        }
        if (go != null) Destroy(go);
    }

    public void TriggerSuccess()
    {
        if (!gameActive) return;
        gameActive = false;
        successScoreText.text = Loc.T("score") + " " + score;
        successPanel.SetActive(true);
        StartCoroutine(PanelIntro(successPanel, "MoleMascot"));   // Sprint 7: köstebek zıplaması + buton pop
        SetPanelTitle(successPanel, Loc.T("congrats"));   // baked "TEBRİKLER!" → dile göre
        // Görsel cila (2026-09-15): skor = gradient+kontur; TEBRİKLER = logo stili + kalp gibi atan nabız.
        if (successScoreText.GetComponent<TitleFx>() == null)
        {
            successScoreText.fontStyle = FontStyles.Bold; successScoreText.fontSize += 6f;
            var fx = successScoreText.gameObject.AddComponent<TitleFx>(); fx.tiltDeg = 0f; fx.bobAmp = 0f; fx.outlineWidth = 0.34f;
        }
        var titleTr = successPanel.transform.Find("Title");
        if (titleTr != null && titleTr.GetComponent<TitleFx>() == null)
        {
            var tt = titleTr.GetComponent<TMP_Text>(); if (tt != null) { tt.fontStyle = FontStyles.Bold; tt.fontSize += 8f; }
            var fx = titleTr.gameObject.AddComponent<TitleFx>(); fx.tiltDeg = 5f; fx.bobAmp = 3f; fx.heartbeat = true; fx.outlineWidth = 0.36f;
        }
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
        int newBest = StarManager.Record(world, level, stars);   // en iyi yıldızı sakla (bölüm gösterimi)
        int delta = Mathf.Max(0, newBest - oldBest);             // patika/dünya rozetine net eklenen (en-iyi farkı)
        PlayerProfile.AddEarnedStars(stars);                     // BİRİKİMLİ ödül yıldızı (tekrar oynayınca da artar)
        var earned = StarRewards.CheckAndGrant();                // (tür,adet) — kazanılan güç-up'lar
        CloudSyncService.Instance?.FlushNow();                    // Sprint 10: ilerlemeyi buluta yaz
        StarRow.Build(successPanel.transform, stars, new Vector2(0, -320));   // 3 yıldız (skor ile buton ARASINDA; skordan uzak)
        if (earned != null && earned.Count > 0) ShowGiftRow(earned);   // "Kazanılan: 1× [ikon] + 1× [ikon]" (yazı yerine ikon)

        // Level-sonu "Reklam izle → yıldız+puan 2×" ödülü
        successStars = stars; successScore = score; doubledThisLevel = false;
        BuildDoubleButton();
        bool canDouble = AdManager.Instance != null && AdManager.Instance.RewardedReady && (stars > 0 || score > 0);
        doubleButton.gameObject.SetActive(canDouble);
        if (canDouble) { doubleButton.interactable = true; doubleLabel.text = Loc.T("doubleReward"); }

        // 2× ve Sonraki butonlarını ALT ALTA, boşlukla diz (2× yoksa Sonraki yukarı gelir).
        var nextBtnRT = successPanel.transform.Find("NextLevelButton") as RectTransform;
        if (nextBtnRT != null)
        {
            nextBtnRT.anchorMin = nextBtnRT.anchorMax = new Vector2(0.5f, 0.5f); nextBtnRT.pivot = new Vector2(0.5f, 0.5f);
            // ⚠️ Bu butonun RECT'i 440×375 (görsel kısmı daha küçük, bolca şeffaf pay var) → merkez konumunu
            // rect'e göre değil GÖRSEL boşluğa göre seç. -765'te 2X ile yapışık görünüyordu (kullanıcı 2026-08-23).
            nextBtnRT.anchoredPosition = new Vector2(0f, canDouble ? -800f : -600f);
        }

        // Patika ekranında (dünya reveal için) gösterilecek ödül özetini taşı.
        LevelResult.Set(world, level, stars, delta, StarRewards.Format(earned));   // patika ekranı hâlâ metin listesi kullanıyor
        LevelResult.StarsAnimated = true;   // ⭐ yıldız/ödül animasyonu ARTIK success ekranında → patika oynamasın (kullanıcı 2026-08-17)

        rewardEarned = stars;
        rewardDelta = delta;
        bool hasNextLevel = LevelManager.Instance != null && LevelManager.Instance.HasNext;
        int nextW = WorldCatalog.NextWorld(world);   // SIRADA sonraki dünya (Order; worldId+1 DEĞİL)
        bool nextWorldExists = nextW >= 0 && WorldCatalog.HasContent(nextW);
        rewardLastOfWorld = !hasNextLevel;   // dünyanın son level'ı → MainMenu'ye (dünya reveal veya menü)
        nextPressed = false;
        // Rozet ANA SAYFAYLA aynı metriği göstersin: PlayerProfile.EarnedStars (birikimli ödül yıldızı).
        // Eskiden StarManager.Total() (level başına EN İYİ toplamı) gösteriliyordu → ana sayfada 96, burada 46
        // gibi kafa karıştırıcı fark çıkıyordu ve 2X ödülü rozete YANSIMIYORDU (kullanıcı 2026-08-23).
        BuildSuccessStarBadge(Mathf.Max(0, PlayerProfile.EarnedStars - stars));
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

    // Yeni kazanılan yıldız hediyeleri: "Kazanılan:  1× [Hız ikonu]  +  1× [Büyüme ikonu]" — yazı yerine İKON
    // (kullanıcı 2026-09-15). Yatay layout, içerik ortalı; ikonlar hafifçe "pop" yaparak belirir.
    void ShowGiftRow(System.Collections.Generic.List<(PowerUpType type, int count)> earned)
    {
        var row = new GameObject("GiftRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        var rt = (RectTransform)row.transform; rt.SetParent(successPanel.transform, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f); rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0, -910); rt.sizeDelta = new Vector2(980, 72);   // buton altında
        var h = row.GetComponent<HorizontalLayoutGroup>();
        h.childAlignment = TextAnchor.MiddleCenter; h.spacing = 18f;   // '+' yok → boşluk biraz daha geniş
        h.childControlWidth = true; h.childControlHeight = true; h.childForceExpandWidth = false; h.childForceExpandHeight = false;

        TMP_Text Lbl(string txt, float size, Color c)
        {
            var t = new GameObject("T", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
            t.transform.SetParent(rt, false);
            t.text = txt; t.fontSize = size; t.fontStyle = FontStyles.Bold; t.color = c; t.raycastTarget = false;
            t.alignment = TextAlignmentOptions.Center; t.enableWordWrapping = false;
            t.gameObject.AddComponent<LayoutElement>().flexibleWidth = 0f;
            return t;
        }

        var head = Lbl(Loc.T("gained"), 34, new Color(1f, 0.85f, 0.3f));
        Loc.ApplyDir(head);
        for (int i = 0; i < earned.Count; i++)
        {
            Lbl($"{earned[i].count}×", 36, Color.white);
            var ig = new GameObject("Icon", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            ig.transform.SetParent(rt, false);
            var img = ig.GetComponent<Image>(); img.sprite = PowerUpIcons.Get(earned[i].type); img.preserveAspect = true; img.raycastTarget = false;
            var le = ig.GetComponent<LayoutElement>(); le.preferredWidth = 64f; le.preferredHeight = 64f;
            StartCoroutine(PopIn(ig.transform, 0.25f + i * 0.12f));
        }
    }

    // Küçük "pop" belirme: 0 → 1.25 → 1 (unscaled; success ekranında timeScale 0 olabilir).
    IEnumerator PopIn(Transform tr, float delay)
    {
        tr.localScale = Vector3.zero;
        float w = 0f; while (w < delay) { w += Time.unscaledDeltaTime; yield return null; }
        float t = 0f;
        while (t < 0.32f)
        {
            t += Mathf.Min(Time.unscaledDeltaTime, 0.05f); float k = t / 0.32f;
            float sc = k < 0.7f ? Mathf.Lerp(0f, 1.25f, k / 0.7f) : Mathf.Lerp(1.25f, 1f, (k - 0.7f) / 0.3f);
            if (tr != null) tr.localScale = Vector3.one * sc;
            yield return null;
        }
        if (tr != null) tr.localScale = Vector3.one;
    }

    string failReason = "";
    TMP_Text failReasonText;

    // ── Fail can gösterimi: 5 kalp + durum/geri-sayım + can bitince Retry engeli ──
    Image[] failHearts;
    TMP_Text failLivesText;
    Button retryButton;
    int failLivesShown = -1;   // animasyon için son gösterilen can

    // Sprint 8/10: fail panelinde teklifler — her biri REKLAM (izle) + COIN (öde) ikilisi
    Button failAdLifeButton;   TMP_Text failAdLifeLabel;    // reklamla +1 can
    Button failAdTimeButton;   TMP_Text failAdTimeLabel;    // reklamla devam (+20sn süre-fail'inde)
    Button failCoinLifeButton; TMP_Text failCoinLifeLabel;  // coinle +1 can (80)
    Button failCoinTimeButton; TMP_Text failCoinTimeLabel;  // coinle devam (100)
    TMP_Text failLifeAction, failTimeAction;   // teklif eylem etiketi (satır solu): "Can Yenile" / "Devam Et"

    bool lastFailWasTimeUp;   // Sprint 8: süre-uzatma teklifini yalnız süre-doldu fail'inde göster
    bool failResumable;       // "reklam izle → kaldığın yerden devam" teklifi (süre-doldu VEYA bomba)

    public void TriggerFail(string reason = null)
    {
        if (!gameActive) return;
        gameActive = false;
        lastFailWasTimeUp = reason == null;   // Update'ten süre bitince arg'sız çağrılır
        failResumable = reason == null;       // süre-doldu → devam edilebilir
        failReason = reason ?? Loc.T("timeUp");
        ShowFailPanel();
    }

    // Bomba yutuldu: oyunu durdur, delikte patlama + duman göster, fail panelini kısa gecikmeyle aç.
    void OnBombSwallowed(Vector3 pos)
    {
        if (!gameActive) return;
        gameActive = false;
        lastFailWasTimeUp = false;
        failResumable = true;        // bomba → reklamla kaldığın yerden devam edilebilir
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

    // Devam sonrası (fail'den dönünce): ekran ortasında büyük şeffaf beyaz 3-2-1 geri sayımı (oyun donuk).
    // Bitince gameActive=true; süre timerStarted=false olduğu için OYUNCU DELİĞİ HAREKET ETTİRİNCE kaldığı yerden azalır.
    IEnumerator ResumeCountdown()
    {
        var canvas = FindAnyObjectByType<Canvas>();
        Transform parent = canvas != null ? canvas.transform : null;
        for (int n = 3; n >= 1; n--)
        {
            var go = new GameObject("ResumeCount", typeof(RectTransform));
            var t = go.AddComponent<TextMeshProUGUI>();
            var rt = t.rectTransform; if (parent != null) rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f); rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero; rt.sizeDelta = new Vector2(500, 500);
            t.fontSize = 340; t.fontStyle = FontStyles.Bold; t.alignment = TextAlignmentOptions.Center;
            t.raycastTarget = false; t.text = n.ToString();
            float dur = 0.7f, e = 0f;
            while (e < dur)
            {
                e += Time.unscaledDeltaTime; float k = e / dur;
                rt.localScale = Vector3.one * Mathf.Lerp(0.7f, 1.5f, k);
                t.color = new Color(1f, 1f, 1f, 0.65f * (1f - k));   // büyüyüp kaybolan şeffaf beyaz rakam
                yield return null;
            }
            Destroy(go);
        }
        gameActive = true;   // geri sayım bitti → oyun aktif (süre delik hareketiyle başlar)
    }

    void ShowFailPanel()
    {
        if (LivesManager.Instance != null) LivesManager.Instance.LoseLife();   // kalıcı can -1 (sınırsız aktifse eksilmez)
        CloudSyncService.Instance?.FlushNow();                                  // Sprint 10: can kaybını buluta yaz
        failPanel.SetActive(true);
        StartCoroutine(PanelIntro(failPanel, "SadMole"));   // Sprint 7: üzgün köstebek + buton pop
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
            rt.anchoredPosition = new Vector2(x0 + i * (sz + gap), -100f); rt.sizeDelta = new Vector2(sz, sz);
            var img = go.GetComponent<Image>(); img.preserveAspect = true; img.raycastTarget = false; img.sprite = HeartArt.Full();
            failHearts[i] = img;
        }

        var stGo = new GameObject("FailLivesStatus", typeof(RectTransform));
        failLivesText = stGo.AddComponent<TextMeshProUGUI>();
        var srt = (RectTransform)stGo.transform; srt.SetParent(failPanel.transform, false);
        failLivesText.fontSize = 56; failLivesText.fontStyle = FontStyles.Bold; failLivesText.alignment = TextAlignmentOptions.Center;   // 34→56
        failLivesText.color = new Color(1f, 0.85f, 0.6f); failLivesText.raycastTarget = false;
        srt.anchorMin = srt.anchorMax = new Vector2(0.5f, 0.5f); srt.pivot = new Vector2(0.5f, 0.5f);
        srt.anchoredPosition = new Vector2(0, -230f); srt.sizeDelta = new Vector2(1000, 84);

        retryButton = failPanel.transform.Find("RetryButton")?.GetComponent<Button>();

        // Sprint 8: ödüllü reklam butonları (durum yazısı ile Retry butonu arasında).
        (failAdLifeButton, failAdLifeLabel) = MakeFailAdButton("FailAdLife", new Color(1f, 0.74f, 0.76f), Loc.T("watch"), OnWatchAdForLife);
        (failAdTimeButton, failAdTimeLabel) = MakeFailAdButton("FailAdTime", new Color(0.72f, 0.86f, 1f), Loc.T("watch"), OnWatchAdForTime);
        (failCoinLifeButton, failCoinLifeLabel) = MakeFailCoinButton("FailCoinLife", Economy.RefillLifeCost, OnCoinForLife);
        (failCoinTimeButton, failCoinTimeLabel) = MakeFailCoinButton("FailCoinTime", Economy.ContinueCost, OnCoinForTime);
        failLifeAction = MakeFailActionLabel("FailLifeAction");   // "Can Yenile" (satır solu)
        failTimeAction = MakeFailActionLabel("FailTimeAction");   // "Devam Et" (satır solu)

        // Coin bakiyesi (sol-üst) — coinle öderken görünür (CoinHud ile otomatik güncellenir)
        var cbGo = new GameObject("FailCoinBadge", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        var cbrt = (RectTransform)cbGo.transform; cbrt.SetParent(failPanel.transform, false);
        cbrt.anchorMin = cbrt.anchorMax = new Vector2(0f, 1f); cbrt.pivot = new Vector2(0f, 1f);
        cbrt.anchoredPosition = new Vector2(28, -28); cbrt.sizeDelta = new Vector2(200, 60);
        var cbhlg = cbGo.GetComponent<HorizontalLayoutGroup>();
        cbhlg.childAlignment = TextAnchor.MiddleLeft; cbhlg.spacing = 8f;
        cbhlg.childControlWidth = cbhlg.childControlHeight = false; cbhlg.childForceExpandWidth = cbhlg.childForceExpandHeight = false;
        var cbIc = new GameObject("Ic", typeof(RectTransform), typeof(Image), typeof(LayoutElement)); cbIc.transform.SetParent(cbrt, false);
        ((RectTransform)cbIc.transform).sizeDelta = new Vector2(56, 56);
        var cble = cbIc.GetComponent<LayoutElement>(); cble.preferredWidth = 56; cble.preferredHeight = 56;
        var cbImg = cbIc.GetComponent<Image>(); cbImg.sprite = UiButtons.Coin(); cbImg.preserveAspect = true; cbImg.raycastTarget = false;
        var cbTg = new GameObject("V", typeof(RectTransform), typeof(LayoutElement)); cbTg.transform.SetParent(cbrt, false);
        var cbT = cbTg.AddComponent<TextMeshProUGUI>();
        cbT.fontSize = 40; cbT.fontStyle = FontStyles.Bold; cbT.alignment = TextAlignmentOptions.Left;
        cbT.color = new Color(1f, 0.92f, 0.5f); cbT.raycastTarget = false; cbT.text = PlayerProfile.Coins.ToString();
        ((RectTransform)cbTg.transform).sizeDelta = new Vector2(130, 56);
        var cbtle = cbTg.GetComponent<LayoutElement>(); cbtle.preferredWidth = 130; cbtle.preferredHeight = 56;
        cbT.gameObject.AddComponent<CoinHud>();
    }

    // Fail teklifi — REKLAM butonu: video ikonu + "İzle" (izleyerek yap). Konum LayoutFailButtons'ta.
    (Button, TMP_Text) MakeFailAdButton(string name, Color col, string label, UnityEngine.Events.UnityAction onClick)
    {
        var btn = UiButtons.Build(failPanel.transform, new Vector2(0.5f, 0.5f), Vector2.zero,
                                  new Vector2(210, 100), label, UiButtons.Video(), col, 36, true);
        btn.name = name;
        btn.onClick.AddListener(onClick);
        var lbl = btn.GetComponentInChildren<TMP_Text>();
        btn.gameObject.SetActive(false);
        return (btn, lbl);
    }

    // Fail teklifi — COIN butonu: altın coin ikonu + fiyat ("coinle öde" alternatifi).
    (Button, TMP_Text) MakeFailCoinButton(string name, int price, UnityEngine.Events.UnityAction onClick)
    {
        var btn = UiButtons.Build(failPanel.transform, new Vector2(0.5f, 0.5f), Vector2.zero,
                                  new Vector2(210, 100), price.ToString(), UiButtons.Coin(), new Color(1f, 0.9f, 0.5f), 40, true);
        btn.name = name;
        btn.onClick.AddListener(onClick);
        var lbl = btn.GetComponentInChildren<TMP_Text>();
        btn.gameObject.SetActive(false);
        return (btn, lbl);
    }

    // Fail teklifi — EYLEM etiketi (satırın solunda, sağa yaslı): "Can Yenile" / "Devam Et".
    TMP_Text MakeFailActionLabel(string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var t = go.AddComponent<TextMeshProUGUI>();
        var rt = t.rectTransform; rt.SetParent(failPanel.transform, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f); rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(280, 92);
        t.fontSize = 42; t.fontStyle = FontStyles.Bold; t.alignment = TextAlignmentOptions.MidlineRight;
        t.color = new Color(1f, 0.95f, 0.82f); t.raycastTarget = false;
        t.enableAutoSizing = true; t.fontSizeMin = 22f; t.fontSizeMax = 42f; t.enableWordWrapping = false; t.overflowMode = TextOverflowModes.Ellipsis;
        go.SetActive(false);
        return t;
    }

    // Coinle +1 can (fail'de can bitince). Yeterli coin varsa harca → can ekle → paneli yenile.
    void OnCoinForLife()
    {
        if (!PlayerProfile.TrySpendCoins(Economy.RefillLifeCost)) return;
        LivesManager.Instance?.AddLife(1);
        RefreshFailLives(false);
    }

    // Coinle devam (fail'de). Yeterli coin varsa harca → kaldığın yerden devam (süre-fail'inde +20sn).
    void OnCoinForTime()
    {
        if (!PlayerProfile.TrySpendCoins(Economy.ContinueCost)) return;
        ResumeWithBonusTime();
    }

    // "Reklam izle → +1 Can": ödül gelirse can ekle, panel canlarını yenile (Retry açılır).
    void OnWatchAdForLife()
    {
        var am = AdManager.Instance; if (am == null) return;
        failAdLifeButton.interactable = false;
        failAdLifeLabel.text = Loc.T("adLoading");
        am.ShowRewarded("fail_life",
            onReward: () => { LivesManager.Instance?.AddLife(1); RefreshFailLives(false); },
            onUnavailable: () => { failAdLifeLabel.text = Loc.T("adFailed"); RefreshFailLives(false); });
    }

    // "İzle → +20sn Devam": ödül gelirse aynı bölümü yeniden başlatmadan sürdür (+süre, can iadesi).
    void OnWatchAdForTime()
    {
        var am = AdManager.Instance; if (am == null) return;
        failAdTimeButton.interactable = false;
        failAdTimeLabel.text = Loc.T("adLoading");
        am.ShowRewarded("fail_time",
            onReward: ResumeWithBonusTime,
            onUnavailable: () => { failAdTimeLabel.text = Loc.T("adFailed"); RefreshFailLives(false); });
    }

    // Süre-doldu fail'inden ödüllü ile geri dön: kaybedilen canı iade et, paneli kapat, süreyi uzat, oyunu sürdür.
    void ResumeWithBonusTime()
    {
        var lm = LivesManager.Instance;
        if (lm != null && !lm.UnlimitedActive) lm.AddLife(1);   // ShowFailPanel'de düşen canı iade et (sınırsızsa zaten düşmedi)
        failPanel.SetActive(false);
        if (lastFailWasTimeUp) timeLeft += Economy.ContinueBonusSeconds;   // süre-doldu → +20sn; bomba → kalan süreyle devam (ekleme yok)
        if (timerText != null) timerText.text = Loc.T("hudTime") + " " + Mathf.CeilToInt(timeLeft);   // güncel süreyi hemen göster
        timerStarted = false;   // süre HEMEN azalmasın: geri sayım + delik hareketine kadar beklesin
        StartCoroutine(ResumeCountdown());   // 3-2-1 → gameActive=true (süre delik hareketiyle başlar)
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

        // Sprint 10: fail teklifleri — her biri REKLAM (izle) + COIN (öde). Reklam yoksa coin butonu yine görünür.
        var am = AdManager.Instance;
        bool adReady = am != null && am.RewardedReady;
        bool lifeOffer = !unlimited && lives <= 0;                 // can bitti → +1 can teklifi
        bool contOffer = failResumable;    // süre-doldu VEYA bomba → devam teklifi (HER fail'de, sınırsız)

        SetFailOffer(failAdLifeButton, failAdLifeLabel, lifeOffer && adReady, Loc.T("watch"), true);
        SetFailOffer(failCoinLifeButton, failCoinLifeLabel, lifeOffer, Economy.RefillLifeCost.ToString(), PlayerProfile.CanAfford(Economy.RefillLifeCost));
        SetFailOffer(failAdTimeButton, failAdTimeLabel, contOffer && adReady, Loc.T("watch"), true);
        SetFailOffer(failCoinTimeButton, failCoinTimeLabel, contOffer, Economy.ContinueCost.ToString(), PlayerProfile.CanAfford(Economy.ContinueCost));
        if (failLifeAction != null) { failLifeAction.gameObject.SetActive(lifeOffer); if (lifeOffer) failLifeAction.text = Loc.T("refillLife"); }
        if (failTimeAction != null) { failTimeAction.gameObject.SetActive(contOffer); if (contOffer) failTimeAction.text = lastFailWasTimeUp ? Loc.T("continueGame") + " +" + (int)Economy.ContinueBonusSeconds + Loc.T("secShort") : Loc.T("continueGame"); }

        LayoutFailButtons();   // reklam + Tekrar + Çıkış butonlarını çakışmasız, alttan-hizalı diz

        // Kaybedilen kalbi (ilk boş) animasyonla vurgula.
        if (animateLoss && !unlimited && lives < failHearts.Length && lives >= 0)
            StartCoroutine(PulseHeart(failHearts[lives].rectTransform));
        failLivesShown = lives;
    }

    // Fail panelinin alt aksiyon butonlarını ALTTAN hizalı, çakışmasız dizer (responsive; hangi reklam butonu
    // görünürse ona göre otomatik ayarlanır). Alttan üste: Çıkış, Tekrar, [+can], [devam].
    // Bir teklif butonunu göster/gizle + etiketle + karşılanabilirliğe göre soluklaştır (harcanamıyorsa pasif).
    void SetFailOffer(Button btn, TMP_Text lbl, bool show, string label, bool affordable)
    {
        if (btn == null) return;
        btn.gameObject.SetActive(show);
        if (!show) return;
        btn.interactable = affordable;
        if (lbl != null) lbl.text = label;
        var img = btn.GetComponent<Image>();
        if (img != null) { var c = img.color; c.a = affordable ? 1f : 0.4f; img.color = c; }
    }

    // Fail teklifleri: SEBEP yazısının ALTINA, çakışmasız satırlar [eylem etiketi + İzle + coin]; en altta Retry/Cancel
    // satırı (yan yana, X korunur). Top-down → sebeple araya garantili boşluk; kaç teklif varsa Retry ona göre iner.
    void LayoutFailButtons()
    {
        if (retryButton == null) return;
        Canvas.ForceUpdateCanvases();
        float y = -480f; const float step = 122f;   // ilk teklif satırı (sebep -330 altında, boşlukla)
        bool any = false;
        if (PlaceFailRow(failTimeAction, failAdTimeButton, failCoinTimeButton, y)) { y -= step; any = true; }   // Devam üstte
        if (PlaceFailRow(failLifeAction, failAdLifeButton, failCoinLifeButton, y)) { y -= step; any = true; }   // +Can altta

        float btnY = any ? y - 4f : -500f;   // Retry/Cancel satırı: son teklifin altında (teklif yoksa sabit)
        SetFailCenter((RectTransform)retryButton.transform, retryButton.transform.localPosition.x, btnY);
        var cancel = failPanel.transform.Find("CancelButton") as RectTransform;
        if (cancel != null) SetFailCenter(cancel, cancel.localPosition.x, btnY);
    }

    // Bir teklif satırı: eylem etiketi (sol, sağa yaslı) + reklam butonu (orta) + coin butonu (sağ). Yerleştirdiyse true.
    bool PlaceFailRow(TMP_Text action, Button ad, Button coin, float y)
    {
        bool aAd = ad != null && ad.gameObject.activeSelf;
        bool aCoin = coin != null && coin.gameObject.activeSelf;
        if (!aAd && !aCoin) return false;
        if (action != null && action.gameObject.activeSelf) SetFailCenter((RectTransform)action.transform, -230f, y);
        if (aAd) SetFailCenter((RectTransform)ad.transform, 30f, y);
        if (aCoin) SetFailCenter((RectTransform)coin.transform, 255f, y);
        return true;
    }

    static void SetFailCenter(RectTransform rt, float x, float y)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f); rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(x, y);
    }

    static string Fmt(int s) => $"{s / 60:00}:{s % 60:00}";

    // ── Sprint 7: sonuç ekranı açılış animasyonu (maskot zıplaması + başlık/buton pop) ──
    // Yalnız İSİMLİ ÇOCUKLARIN localScale'ini canlandırır (panel root'una/konumlara dokunmaz) →
    // yıldız/ödül uçuş animasyonları ve fail-kalp pulse'ı ile ÇAKIŞMAZ. Ölçek 1'e yerleşir.
    IEnumerator PanelIntro(GameObject panel, string mascotName)
    {
        var mascot = panel.transform.Find(mascotName) as RectTransform;
        var title  = panel.transform.Find("Title") as RectTransform;
        var bNext  = panel.transform.Find("NextLevelButton") as RectTransform;
        var bRetry = panel.transform.Find("RetryButton") as RectTransform;
        var bCancel= panel.transform.Find("CancelButton") as RectTransform;

        if (mascot) mascot.localScale = Vector3.zero;
        if (title)  title.localScale  = Vector3.zero;
        if (bNext)  bNext.localScale  = Vector3.zero;
        if (bRetry) bRetry.localScale = Vector3.zero;
        if (bCancel)bCancel.localScale= Vector3.zero;

        if (mascot)  StartCoroutine(PopIn(mascot, 0.00f, 0.50f, 1.7f));   // köstebek: güçlü overshoot (zıplama)
        if (title)   StartCoroutine(PopIn(title,  0.12f, 0.40f, 1.4f));
        if (bNext)   StartCoroutine(PopIn(bNext,  0.26f, 0.38f, 1.5f));
        if (bRetry)  StartCoroutine(PopIn(bRetry, 0.26f, 0.38f, 1.5f));
        if (bCancel) StartCoroutine(PopIn(bCancel,0.34f, 0.38f, 1.5f));
        yield break;
    }

    IEnumerator PopIn(RectTransform rt, float delay, float dur, float overshoot)
    {
        float t = -delay;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / dur);
            if (rt == null) yield break;
            rt.localScale = Vector3.one * EaseOutBack(k, overshoot);
            yield return null;
        }
        if (rt != null) rt.localScale = Vector3.one;
    }

    // Geriye zıplayarak yerleşen ölçek eğrisi (easeOutBack): 0→1, sona doğru 1'i aşıp geri gelir.
    static float EaseOutBack(float x, float s)
    {
        x -= 1f;
        return 1f + (s + 1f) * x * x * x + s * x * x;
    }

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
            rt.anchoredPosition = new Vector2(0, -330); rt.sizeDelta = new Vector2(1000, 100);   // durum altı; reklam butonlarından uzak
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
            // Sprint 8: her 3 levelda bir geçiş reklamı — SORU SORULMAZ, doğrudan gösterilir
            // (kullanıcı 2026-08-22: "coinle geçme olmasın, direk izlesin").
            var am = AdManager.Instance;
            if (am == null) { LoadNextLevelScene(); return; }
            StartCoroutine(InterstitialThenLoad(am));
        }
    }

    // Sahne yüklemeyi TEK noktadan yap → reklam callback'i + zaman aşımı ikisi birden tetiklense bile iki kez yüklenmez.
    bool sceneLoadStarted;
    void LoadNextLevelScene()
    {
        if (sceneLoadStarted) return;
        sceneLoadStarted = true;
        Time.timeScale = 1f;   // güvenlik: herhangi bir overlay donuk bıraktıysa yeni sahne donuk açılmasın
        ReloadGameSceneWhenReady();
    }

    /// <summary>
    /// ASSET DELIVERY FAZ 2: GameScene'i yeniden yüklemeden ÖNCE hedef level'ın prefab'larını belleğe al
    /// (aynı dünya → paket zaten inik, ~anlık). Hata (çok nadir) → toast + ana menü (boş level açılmasın).
    /// Kalıcı koşucuda çalışır → bu nesnenin StopAllCoroutines'i kesmez.
    /// </summary>
    static void ReloadGameSceneWhenReady()
    {
        int scene = SceneManager.GetActiveScene().buildIndex;
        WorldContentLoader.Prepare(LevelManager.CurrentWorld, LevelManager.CurrentIndex, null, ok =>
        {
            if (ok) { SceneManager.LoadScene(scene); return; }
            ToastUI.Show(Loc.T("downloadFail"), null, ToastUI.Style.Error);
            var gm = Instance;
            if (gm != null) gm.ExitToMainMenu(); else SceneManager.LoadScene("MainMenu");
        });
    }

    /// <summary>
    /// Geçiş reklamını göster, kapanınca sonraki levela geç.
    /// GÜVENLİK AĞI: reklam SDK'sı callback'i hiç çağırmazsa (gösterim hatası, yutulan olay vb.) oyun
    /// KİLİTLENMESİN — zaman aşımında yine de devam edilir. Reklam ekranda iken uygulama duraklatıldığı
    /// için sayaç ilerlemez; süre yalnız oyuna dönülünce işler.
    /// </summary>
    IEnumerator InterstitialThenLoad(AdManager am)
    {
        bool done = false;
        am.NotifyLevelEndAndMaybeInterstitial(() => { done = true; LoadNextLevelScene(); });

        float t = 0f;
        while (!done && t < 10f) { t += Time.unscaledDeltaTime; yield return null; }
        if (!done)
        {
            Debug.LogWarning("[Ads] Geçiş reklamı geri bildirimi gelmedi (10 sn) — sahne yine de yükleniyor.");
            LoadNextLevelScene();
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
        int running = Mathf.Max(0, PlayerProfile.EarnedStars - rewardEarned);
        if (sStarBadgeText != null) sStarBadgeText.text = running.ToString();
        int flyCount = Mathf.Clamp(rewardEarned, 0, 3);
        if (flyCount <= 0 || sStarBadgeIcon == null) { yield return new WaitForSeconds(0.12f); yield break; }

        // Rozet artık birikimli yıldızı gösteriyor → uçan HER yıldız sayacı artırır (eskiden yalnız "net en iyi farkı"
        // kadar artıyordu; tekrar oynayınca yıldızlar uçup sayaç sabit kalıyordu → tutarsız görünüyordu).
        int incLeft = flyCount;
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
        ReloadGameSceneWhenReady();
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
