using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// Oyun içi duraklatma + çıkış. Beyaz PAUSE butonu (sol-üst, Score altında): süreyi durdurur
/// (Time.timeScale=0), şaşkın maskot sahnenin önüne gelir; Devam'a basınca kalınan yerden sürer.
/// Kırmızı X butonu (sağ-üst, süre altında): Quit (menüye dön) / Retry (level baştan) / Cancel (devam).
/// timeScale sahne değişiminden önce 1'e döndürülür (yoksa yeni sahne donuk açılır). GameScene'e eklenir.
/// </summary>
public class PauseMenu : MonoBehaviour
{
    public string mainMenuScene = "MainMenu";

    // Pause/X overlay AÇIK mı? VirtualJoystick bunu okur → açıkken joystick çizilmez + işlevsiz (arkada/gizli).
    // Static + Start'ta sıfırlanır (joystick DontDestroyOnLoad kalıcı; yeni level PauseMenu.Start ile temizler).
    public static bool OverlayOpen;

    Canvas canvas;
    GameObject pauseOverlay, xOverlay;
    Sprite circle;

    void Start()
    {
        OverlayOpen = false;   // yeni sahne → joystick bastırması temiz
        canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null) { Debug.LogWarning("[PauseMenu] Canvas yok."); return; }
        circle = Resources.Load<Sprite>("burrow_button_empty_circle");
        BuildButtons();
        BuildPauseOverlay();
        BuildXOverlay();
    }

    // ── ÜST BUTONLAR ───────────────────────────────────────────────────────────
    void BuildButtons()
    {
        // PAUSE — sol-üst, sol duvara yaslı; LEVEL+Score yazılarının ALTINDA (çakışmasın → üst kenar -145)
        var pause = RoundButton("PauseBtn", new Vector2(0f, 1f), new Vector2(16, -145), Color.white);
        AddBar(pause.transform, -8, new Color(1, 1, 1, 0.97f));
        AddBar(pause.transform, 8, new Color(1, 1, 1, 0.97f));
        pause.GetComponent<Button>().onClick.AddListener(OpenPause);

        // X — sağ-üst, sağ duvara yaslı; süre+yutulan sayacının ALTINDA (çakışmasın → üst kenar -145)
        var x = RoundButton("CloseBtn", new Vector2(1f, 1f), new Vector2(-16, -145), new Color(1f, 0.5f, 0.45f));
        AddCross(x.transform, new Color(1, 1, 1, 0.97f));
        x.GetComponent<Button>().onClick.AddListener(OpenX);
    }

    GameObject RoundButton(string name, Vector2 anchor, Vector2 pos, Color bg)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        var rt = (RectTransform)go.transform; rt.SetParent(UiRoot.SafeContent(canvas), false);   // üst köşe butonları çentik dışı
        rt.anchorMin = rt.anchorMax = anchor; rt.pivot = anchor;
        rt.anchoredPosition = pos; rt.sizeDelta = new Vector2(94, 72);   // circle görsel oranı (1.31:1)
        var img = go.GetComponent<Image>(); img.sprite = circle; img.color = bg;
        go.GetComponent<Button>().onClick.AddListener(() => AudioManager.Instance?.PlayUiClick());
        return go;
    }

    void AddBar(Transform parent, float x, Color c)
    {
        var img = NewImage("Bar", parent, null); img.color = c; img.raycastTarget = false;
        var rt = img.rectTransform; rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f); rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(x, 0); rt.sizeDelta = new Vector2(8, 26);   // küçültüldü
    }

    void AddCross(Transform parent, Color c)
    {
        for (int i = 0; i < 2; i++)
        {
            var img = NewImage("X", parent, null); img.color = c; img.raycastTarget = false;
            var rt = img.rectTransform; rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f); rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero; rt.sizeDelta = new Vector2(30, 8);   // küçültüldü
            rt.localEulerAngles = new Vector3(0, 0, i == 0 ? 45 : -45);
        }
    }

    // ── PAUSE OVERLAY (şaşkın maskot + Devam) ──────────────────────────────────
    // Overlay'i KENDİ yüksek-sortingOrder canvas'ına al → HardLevelIntro (kurukafa, sortingOrder 6000) ve HUD dahil
    // HER ŞEYİN ÜSTÜNDE çizilir. Pause/Exit menüsü basıldığında daima en üstte olmalı (kullanıcı 2026-08-05).
    static void Overlayify(GameObject panel)
    {
        var cv = panel.AddComponent<Canvas>();
        cv.overrideSorting = true;
        cv.sortingOrder = 7000;
        panel.AddComponent<GraphicRaycaster>();   // yüksek canvas'ta butonlar tıklanabilir kalsın
    }

    void BuildPauseOverlay()
    {
        pauseOverlay = FullPanel("PauseOverlay", new Color(0.10f, 0.08f, 0.06f, 0.97f));
        Overlayify(pauseOverlay);

        // Maskot: YUKARI + BÜYÜK (2026-07-25 kullanıcı: ~3x). 380→860 (ekran genişliği 1080 → 3x=1140 taşardı; 860≈2.3x
        // ekrana sığar, responsive canvas ScaleWithScreenSize 1080x1920 ile oranlı). Yukarı: y +60→+470 (üst yarıyı doldurur).
        var mole = NewImage("SurprisedMole", pauseOverlay.transform, Resources.Load<Sprite>("mole_mascot_surprised"));
        mole.preserveAspect = true; mole.raycastTarget = false;
        var mr = mole.rectTransform; mr.anchorMin = mr.anchorMax = new Vector2(0.5f, 0.5f); mr.pivot = new Vector2(0.5f, 0.5f);
        mr.anchoredPosition = new Vector2(0, 470); mr.sizeDelta = new Vector2(860, 860);   // spans +40..+900 (üst kenar 960'ın altında)

        var label = NewText("Paused", pauseOverlay.transform, 48, FontStyles.Bold, TextAlignmentOptions.Center);
        label.text = Loc.T("paused"); label.color = new Color(1f, 0.95f, 0.75f);
        var lr = label.rectTransform; lr.anchorMin = lr.anchorMax = new Vector2(0.5f, 0.5f); lr.pivot = new Vector2(0.5f, 0.5f);
        lr.anchoredPosition = new Vector2(0, -30); lr.sizeDelta = new Vector2(700, 70);   // maskot altı (maskot bottom +40, label top -5 → çakışmaz)

        // ── Ayarlar toggle'ları (Ses / Müzik / Titreşim) — PlayerPrefs kalıcı ──
        AddToggle(pauseOverlay.transform, -175, Loc.T("sfx"),
            () => AudioManager.SfxOn, v => AudioManager.SfxOn = v);
        AddToggle(pauseOverlay.transform, -285, Loc.T("music"),
            () => AudioManager.MusicOn, v => AudioManager.MusicOn = v);
        AddToggle(pauseOverlay.transform, -395, Loc.T("vibration"),
            () => AudioManager.HapticOn, v =>
            {
                AudioManager.HapticOn = v;
                // AÇIK yapılınca GÜÇLÜ çift-darbe titreşim → cihazda haptik testi tek dokunuşla doğrulanır.
                if (v) AudioManager.Instance?.HapticTest();
            });

        var resume = UiButtons.Build(pauseOverlay.transform, new Vector2(0.5f, 0.5f), new Vector2(0, -540),
            new Vector2(460, 122), Loc.T("resume"), UiButtons.Play(), new Color(0.88f, 1f, 0.88f), 44);
        resume.onClick.AddListener(ClosePause);

        pauseOverlay.SetActive(false);
    }

    void OpenPause() { Time.timeScale = 0f; pauseOverlay.transform.SetAsLastSibling(); pauseOverlay.SetActive(true); OverlayOpen = true; }
    void ClosePause() { Time.timeScale = 1f; pauseOverlay.SetActive(false); OverlayOpen = false; }

    // ── AYAR TOGGLE (satır tıkla → değeri çevir, AÇIK/KAPALI rozeti güncellenir, PlayerPrefs kalıcı) ──
    void AddToggle(Transform parent, float y, string label, Func<bool> get, Action<bool> set)
    {
        // Satır: koyu pill + tıklanabilir Button
        var row = new GameObject("Toggle_" + label, typeof(RectTransform), typeof(Image), typeof(Button));
        var rt = (RectTransform)row.transform; rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f); rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0, y); rt.sizeDelta = new Vector2(560, 96);
        row.GetComponent<Image>().color = new Color(0.16f, 0.13f, 0.10f, 0.95f);

        // Sol etiket
        var lbl = NewText("L", rt, 38, FontStyles.Bold, TextAlignmentOptions.Left);
        lbl.text = label; lbl.color = new Color(1f, 0.95f, 0.82f);
        var lr = lbl.rectTransform; lr.anchorMin = new Vector2(0, 0); lr.anchorMax = new Vector2(1, 1);
        lr.offsetMin = new Vector2(34, 0); lr.offsetMax = new Vector2(-190, 0);

        // Sağ durum rozeti (AÇIK yeşil / KAPALI gri)
        var badge = new GameObject("State", typeof(RectTransform), typeof(Image));
        var brt = (RectTransform)badge.transform; brt.SetParent(rt, false);
        brt.anchorMin = brt.anchorMax = new Vector2(1f, 0.5f); brt.pivot = new Vector2(1f, 0.5f);
        brt.anchoredPosition = new Vector2(-22, 0); brt.sizeDelta = new Vector2(150, 64);
        var bImg = badge.GetComponent<Image>(); bImg.raycastTarget = false;

        var stateTxt = NewText("S", brt, 30, FontStyles.Bold, TextAlignmentOptions.Center);
        stateTxt.raycastTarget = false;
        Stretch(stateTxt.rectTransform);

        Action refresh = () =>
        {
            bool on = get();
            bImg.color = on ? new Color(0.30f, 0.75f, 0.35f, 1f) : new Color(0.40f, 0.38f, 0.36f, 1f);
            stateTxt.text = on ? Loc.T("on") : Loc.T("off");   // sabit Türkçe idi → 7 dile çevrildi (kullanıcı 2026-08-23)
            Loc.ApplyDir(stateTxt);
            stateTxt.color = Color.white;
        };
        refresh();

        row.GetComponent<Button>().onClick.AddListener(() =>
        {
            set(!get());
            refresh();
            AudioManager.Instance?.PlayUiClick();
        });
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    // ── X OVERLAY (Quit / Retry / Cancel) ──────────────────────────────────────
    void BuildXOverlay()
    {
        xOverlay = FullPanel("XOverlay", new Color(0f, 0f, 0f, 0.7f));
        Overlayify(xOverlay);

        var a = new Vector2(0.5f, 0.5f); var size = new Vector2(480, 128);
        var q = UiButtons.Build(xOverlay.transform, a, new Vector2(0, 175), size, Loc.T("quit"), UiButtons.Power(), new Color(1f, 0.82f, 0.8f));
        q.onClick.AddListener(Quit);
        var r = UiButtons.Build(xOverlay.transform, a, new Vector2(0, 30), size, Loc.T("retry"), UiButtons.Refresh(), Color.white);
        r.onClick.AddListener(Retry);
        var c = UiButtons.Build(xOverlay.transform, a, new Vector2(0, -115), size, Loc.T("resume"), UiButtons.Play(), new Color(0.88f, 1f, 0.88f));
        c.onClick.AddListener(CloseX);

        xOverlay.SetActive(false);
    }

    void OpenX() { Time.timeScale = 0f; xOverlay.transform.SetAsLastSibling(); xOverlay.SetActive(true); OverlayOpen = true; }
    void CloseX() { Time.timeScale = 1f; xOverlay.SetActive(false); OverlayOpen = false; }
    void Retry()
    {
        OverlayOpen = false; Time.timeScale = 1f;
        int scene = SceneManager.GetActiveScene().buildIndex;
        // FAZ 2: aynı level → prefab'lar cache'te, anında; yine de tek kapıdan geç (güvenlik)
        WorldContentLoader.Prepare(LevelManager.CurrentWorld, LevelManager.CurrentIndex, null,
            ok => { if (ok) SceneManager.LoadScene(scene); else { ToastUI.Show(Loc.T("downloadFail"), null, ToastUI.Style.Error); SceneManager.LoadScene(mainMenuScene); } });
    }
    void Quit() { AudioManager.Instance?.PlayExit(); Time.timeScale = 1f; SceneManager.LoadScene(mainMenuScene); }

    // ── YARDIMCI ───────────────────────────────────────────────────────────────
    GameObject FullPanel(string name, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        var rt = (RectTransform)go.transform; rt.SetParent(canvas.transform, false);
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
        var img = go.GetComponent<Image>(); img.color = color; img.raycastTarget = true;
        return go;
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
}
