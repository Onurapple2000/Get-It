using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// SADECE Yiyecekler L1 (worldId 1, levelIndex 0 — oyuncunun ilk leveli) ön-tanıtımı (onboarding).
/// Parmakla gösteren el (tutorial_hand_point_01/02 animasyonlu; joystick'te tutorial_hand_joystick_thumb) eşliğinde
/// 3 kısa adım: (1) hedef tabelaları — "topla", (2) joystick — oyuncu GERÇEKTEN oynatana kadar bekler, (3) güç-up.
/// Oyun donar (Time.timeScale=0); adım 2'de joystick responsive kalır (VirtualJoystick.TutorialActive; delik dt=0
/// olduğundan hareket etmez). Bir kez gösterilir (PlayerPrefs); editörde her seferinde (test). Self-bootstrap.
/// </summary>
// [Preserve]: sınıf hiçbir koddan referans edilmiyor (yalnız RuntimeInitializeOnLoadMethod). IL2CPP managed
// stripping onu release build'de SİLEBİLİR → bootstrap hiç çalışmaz (telefonda tanıtım görünmez). Preserve engeller.
[UnityEngine.Scripting.Preserve]
public class FoodsL1Tutorial : MonoBehaviour
{
    const string PrefKey = "FoodsL1Tutorial_Shown_v2";   // v2: eski build'de pref set olduysa yeniden gösterilsin

    // GELİŞTİRME BAYRAĞI: true iken Yiyecekler L1'e HER girişte tanıtım gösterilir (pref kontrolü atlanır).
    // Dev/editör'de açık (test kolaylığı), RELEASE'de kapalı → oyuncu tanıtımı yalnızca BİR KEZ görür.
#if DEVELOPMENT_BUILD || UNITY_EDITOR
    const bool AlwaysShowDev = true;
#else
    const bool AlwaysShowDev = false;
#endif

    // ⚠️ Self-bootstrap (RuntimeInitializeOnLoadMethod/sceneLoaded) telefonda GÜVENİLMEZ: (a) RuntimeInitialize
    // yalnız açılışta 1 kez çalışır, (b) sınıf referanssız → IL2CPP stripping silebilir. GÜVENLİ ÇÖZÜM: LevelManager.
    // Start (sahnede, referanslı, build'de kesin çalışır) her level açılışında bunu çağırır. Yiyecekler L1'de kurar.
    public static void CheckAndStart()
    {
        if (LevelManager.CurrentWorld != 1 || LevelManager.CurrentIndex != 0) return;
#if !UNITY_EDITOR
        if (!AlwaysShowDev && PlayerPrefs.GetInt(PrefKey, 0) != 0) return;   // AlwaysShowDev true iken her seferinde
#endif
        if (FindAnyObjectByType<FoodsL1Tutorial>() != null) return;   // zaten kurulmuş
        new GameObject("FoodsL1Tutorial").AddComponent<FoodsL1Tutorial>();
    }

    RectTransform overlay;
    Image dim, hand, ring;
    TMP_Text text, tapHint;
    Sprite point1, point2, thumb, ringSprite, rectSprite;

    void Start() { StartCoroutine(Run()); }

    IEnumerator Run()
    {
        Time.timeScale = 0f;   // hemen dondur (timer sızmasın); Start'lar timeScale'den bağımsız çalışır

        // HUD (ObjectiveBar) + level hazır olana kadar bekle (gerçek zaman).
        float t0 = Time.realtimeSinceStartup;
        GameObject bar = null;
        while ((bar = GameObject.Find("ObjectiveBar")) == null && Time.realtimeSinceStartup - t0 < 3f)
            yield return null;
        yield return null; yield return null;   // bir-iki kare daha (layout otursun)

        point1 = Load("tutorial_hand_point_01");
        point2 = Load("tutorial_hand_point_02");
        thumb  = Load("tutorial_hand_joystick_thumb");
        ringSprite = MakeRingSprite(160, 0.82f);
        rectSprite = MakeRectBorderSprite(64, 7);   // "topla" için dikdörtgen çerçeve (halka yerine)

        BuildOverlay();

        // ADIM 1 — HEDEF TABELALARI
        var barRt = bar != null ? (RectTransform)bar.transform : null;
        PlaceText(new Vector2(0.5f, 1f), new Vector2(0, -700), new Vector2(900, 160));   // ÜSTTE ama elden ~270px aşağı (el ile çakışmasın)
        yield return Step(
            Loc.T("tutCollect"),
            () => barRt != null ? RectTransformUtility.WorldToScreenPoint(null, barRt.position) : new Vector2(Screen.width * 0.5f, Screen.height * 0.9f),
            new Vector2(70f, -150f), pointAnim: true, waitDrag: false, markerOffset: new Vector2(0f, -85f));   // dörtgen+el biraz aşağı (tabelaların içine)

        // ADIM 2 — JOYSTICK (oyuncu gerçekten oynatana kadar bekle). handOffset: başparmak ucu joystick MERKEZİNDE
        // olacak şekilde el yukarı (2026-07-25 kullanıcı). Yatay ofset küçük (merkezde), Y pozitif (yukarı).
        VirtualJoystick.TutorialActive = true;
        PlaceText(new Vector2(0.5f, 0.5f), new Vector2(0, -480), new Vector2(900, 200));   // ekranın ALT YARISININ ortası
        yield return Step(
            Loc.T("tutDrag"),
            () => VirtualJoystick.BaseScreenPos(),
            new Vector2(30f, 95f), pointAnim: false, waitDrag: true);
        VirtualJoystick.TutorialActive = false;

        // ADIM 3 — GÜÇ-UP TURU: kamera sahnedeki 3 güç-up'a sırayla gidip zoom yapar, her birinde 3 sn bekler.
        yield return CameraTour();

        Finish();
    }

    // Kamera 3 güç-up'a navigate edip zoom yapar (her birinde 3 sn); zoom boyunca İŞARET ELİ güç-up'ı gösterir
    // (2026-07-25 kullanıcı). Dim şeffaflaşır → nesneler net; ring gizli, sadece el + etiket.
    IEnumerator CameraTour()
    {
        ring.enabled = false;
        hand.enabled = true;
        hand.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 180f);   // işaret eli başaşağı (Adım 1/3 ile aynı)
        tapHint.gameObject.SetActive(false);
        dim.color = new Color(0f, 0f, 0f, 0.12f);   // powerup'lar net görünsün
        PlaceText(new Vector2(0.5f, 0.5f), new Vector2(0, -480), new Vector2(900, 240));   // ekranın ALT YARISININ ortası (2 satırlı etiket)

        var list = FindPowerUps(3);
        var holeCam = FindAnyObjectByType<HoleCamera>();
        Camera cam = holeCam != null ? holeCam.GetComponent<Camera>() : Camera.main;
        if (cam == null) yield break;

        if (holeCam != null) holeCam.enabled = false;   // kamerayı biz sürelim (yoksa deliğe geri kilitler)
        float angle = holeCam != null ? holeCam.angle : 45f;
        float rad = angle * Mathf.Deg2Rad;
        Vector3 back = new Vector3(0f, Mathf.Sin(rad), -Mathf.Cos(rad));   // oyun kamerasıyla aynı açı
        Quaternion rot = Quaternion.Euler(angle, 0f, 0f);
        const float zoomDist = 5.5f;
        var handOffset = new Vector2(70f, -150f);   // işaret parmağı ucu hedefte olacak şekilde (Adım 1/3 ile aynı)

        // İşaret elini güç-up'ın EKRAN konumuna koyup animasyonlayan yardımcı (her karede çağrılır).
        void PointAt(Vector3 worldPos, float ta)
        {
            Vector3 sp3 = cam.WorldToScreenPoint(worldPos);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(overlay, new Vector2(sp3.x, sp3.y), null, out Vector2 lp);
            hand.sprite = point1;   // point2 (ileri kare) parmak ucu KIRPIK → hep tam-parmak point1
            float bob = Mathf.Abs(Mathf.Sin(ta * 5f)) * 16f;
            hand.rectTransform.anchoredPosition = lp + handOffset + new Vector2(-bob * 0.4f, bob);
        }

        float anim = 0f;
        foreach (var (pos, label) in list)
        {
            Vector3 targetPos = new Vector3(pos.x, 0f, pos.z) + back * zoomDist;
            Vector3 sPos = cam.transform.position; Quaternion sRot = cam.transform.rotation;
            float t = 0f;
            while (t < 1f)   // ~0.8 sn yumuşak geçiş (el bu sırada da gösterir)
            {
                t += Time.unscaledDeltaTime / 0.8f; anim += Time.unscaledDeltaTime;
                float e = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
                cam.transform.position = Vector3.Lerp(sPos, targetPos, e);
                cam.transform.rotation = Quaternion.Slerp(sRot, rot, e);
                PointAt(pos, anim);
                yield return null;
            }
            text.text = label;
            float hold = 0f;
            while (hold < 3f)   // 3 sn bekle — el işaret etmeye devam eder
            {
                hold += Time.unscaledDeltaTime; anim += Time.unscaledDeltaTime;
                PointAt(pos, anim);
                yield return null;
            }
        }

        if (holeCam != null) holeCam.enabled = true;   // oyun kamerası deliğe geri döner
    }

    // Bir adım: metin + hedefi işaret eden el + halka; koşul sağlanınca döner.
    IEnumerator Step(string msg, System.Func<Vector2> targetScreen, Vector2 handOffset, bool pointAnim, bool waitDrag, Vector2 markerOffset = default)
    {
        text.text = msg;
        tapHint.gameObject.SetActive(!waitDrag);
        tapHint.text = waitDrag ? "" : Loc.T("tutTap");
        hand.sprite = pointAnim ? point1 : thumb;
        // İşaret eli 180° çevrili (kullanıcı: resimler ters yönü gösteriyordu → başaşağı); başparmak düz.
        hand.rectTransform.localRotation = Quaternion.Euler(0f, 0f, pointAnim ? 180f : 0f);

        // İşaretleyici: "topla" adımı (pointAnim) → DİKDÖRTGEN çerçeve; joystick → halka.
        if (pointAnim) { ring.sprite = rectSprite; ring.type = Image.Type.Sliced; ring.rectTransform.sizeDelta = new Vector2(460, 165); }
        else           { ring.sprite = ringSprite; ring.type = Image.Type.Simple;  ring.rectTransform.sizeDelta = new Vector2(320, 320); }

        yield return WaitRelease();   // önceki dokunuş bıraksın (aynı tıkla iki adım atlanmasın)

        float t = 0f, held = 0f;
        while (true)
        {
            t += Time.unscaledDeltaTime;

            // Hedefi ekran→overlay-yerel çevir, halka + el konumla.
            Vector2 sp = targetScreen();
            RectTransformUtility.ScreenPointToLocalPointInRectangle(overlay, sp, null, out Vector2 lp);
            lp += markerOffset;   // işaretleyiciyi (ve eli) hedefe göre kaydır (ör. tabelaların içine)
            ring.rectTransform.anchoredPosition = lp;
            float pulse = 1f + 0.12f * Mathf.Sin(t * 6f);
            ring.rectTransform.localScale = Vector3.one * pulse;

            if (pointAnim)
            {
                // İki kare arası geçiş (dokunma hissi) + hedefe doğru küçük "vuruş" salınımı
                hand.sprite = point1;   // point2 (ileri kare) resmi PARMAK UCU KIRPIK → hep tam-parmak point1 kullan
                float bob = Mathf.Abs(Mathf.Sin(t * 5f)) * 16f;   // hareketi bob verir (kare geçişi yok)
                hand.rectTransform.anchoredPosition = lp + handOffset + new Vector2(-bob * 0.4f, bob);
            }
            else
            {
                // Joystick: başparmak; yatay sürükleme ipucu salınımı
                float dx = Mathf.Sin(t * 3.2f) * 70f;
                hand.rectTransform.anchoredPosition = lp + handOffset + new Vector2(dx, -Mathf.Abs(dx) * 0.2f);
            }

            if (waitDrag)
            {
                if (VirtualJoystick.Direction.magnitude > 0.55f) held += Time.unscaledDeltaTime;
                else held = 0f;
                if (held > 0.25f) yield break;   // oyuncu joystick'i belirgin oynattı → geç
            }
            else
            {
                if (t > 0.3f && Pressed()) yield break;   // dokununca geç
            }
            yield return null;
        }
    }

    void Finish()
    {
        PlayerPrefs.SetInt(PrefKey, 1); PlayerPrefs.Save();
        VirtualJoystick.TutorialActive = false;
        Time.timeScale = 1f;
        if (overlay != null) Destroy(overlay.gameObject);
        Destroy(gameObject);
    }

    // ── UI KURULUM ──
    void BuildOverlay()
    {
        var go = new GameObject("TutorialOverlay", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var cv = go.GetComponent<Canvas>();
        cv.renderMode = RenderMode.ScreenSpaceOverlay; cv.sortingOrder = 7000;   // HUD üstünde
        var sc = go.GetComponent<CanvasScaler>();
        sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; sc.referenceResolution = new Vector2(1080, 1920); sc.matchWidthOrHeight = 0.5f;
        overlay = (RectTransform)go.transform;
        overlay.anchorMin = Vector2.zero; overlay.anchorMax = Vector2.one; overlay.offsetMin = overlay.offsetMax = Vector2.zero;

        dim = NewImg("Dim", overlay, null); dim.color = new Color(0f, 0f, 0f, 0.55f); dim.raycastTarget = true;
        Stretch(dim.rectTransform);

        ring = NewImg("Ring", overlay, ringSprite); ring.color = new Color(1f, 0.95f, 0.4f, 0.95f); ring.raycastTarget = false;
        Center(ring.rectTransform, new Vector2(320, 320));

        hand = NewImg("Hand", overlay, point1); hand.preserveAspect = true; hand.raycastTarget = false;
        Center(hand.rectTransform, new Vector2(240, 240));   // parmağın yukarı erişimi az → ekran üstünü aşıp kesilmesin

        text = NewText("Text", overlay, 52, TextAlignmentOptions.Center);
        var trr = text.rectTransform; trr.anchorMin = trr.anchorMax = new Vector2(0.5f, 1f); trr.pivot = new Vector2(0.5f, 1f);
        trr.anchoredPosition = new Vector2(0, -430); trr.sizeDelta = new Vector2(900, 160);

        tapHint = NewText("TapHint", overlay, 34, TextAlignmentOptions.Center);
        tapHint.color = new Color(1f, 1f, 1f, 0.8f);
        var thr = tapHint.rectTransform; thr.anchorMin = thr.anchorMax = new Vector2(0.5f, 0f); thr.pivot = new Vector2(0.5f, 0f);
        thr.anchoredPosition = new Vector2(0, 90); thr.sizeDelta = new Vector2(700, 60);

        hand.transform.SetAsLastSibling();   // el EN ÜSTTE render olsun → parmak ucu metin/işaretleyici arkasında kalıp kesilmesin
    }

    // Tanıtım metnini adım-adım konumlar. Adım 1: üstte ama elden aşağıda; Adım 2/3: ekranın alt-yarısının ortası.
    void PlaceText(Vector2 anchorPivot, Vector2 pos, Vector2 size)
    {
        var rt = text.rectTransform;
        rt.anchorMin = anchorPivot; rt.anchorMax = anchorPivot; rt.pivot = anchorPivot;
        rt.anchoredPosition = pos; rt.sizeDelta = size;
    }

    // ── YARDIMCILAR ──
    static bool Pressed() => Input.GetMouseButtonDown(0) || (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began);
    static IEnumerator WaitRelease()
    {
        while (Input.GetMouseButton(0) || Input.touchCount > 0) yield return null;
    }

    static IEnumerator WaitRealtime(float sec)
    {
        float t = 0f;
        while (t < sec) { t += Time.unscaledDeltaTime; yield return null; }
    }

    // Sahnedeki güç-up'lardan en fazla `max` tane seçer (mümkünse FARKLI tür → Hız/Mıknatıs/Büyüme çeşitliliği).
    // Her biri için (konum, etiket) döner.
    static System.Collections.Generic.List<(Vector3 pos, string label)> FindPowerUps(int max)
    {
        var res = new System.Collections.Generic.List<(Vector3, string)>();
        var seen = new System.Collections.Generic.HashSet<PowerUpType>();
        var all = FindObjectsByType<PhysicsSwallowable>(FindObjectsSortMode.None);
        // 1. tur: her türden bir tane (çeşitlilik)
        foreach (var s in all)
        {
            if (res.Count >= max) break;
            if (s == null || s.powerUp == PowerUpType.None || seen.Contains(s.powerUp)) continue;
            seen.Add(s.powerUp);
            res.Add((s.transform.position, Label(s.powerUp)));
        }
        // 2. tur: hâlâ eksikse aynı türden ek kopyalarla tamamla
        if (res.Count < max)
            foreach (var s in all)
            {
                if (res.Count >= max) break;
                if (s == null || s.powerUp == PowerUpType.None) continue;
                var e = (s.transform.position, Label(s.powerUp));
                if (!res.Contains(e)) res.Add(e);
            }
        return res;
    }

    static string Label(PowerUpType t) => t switch
    {
        PowerUpType.Speed     => "HIZ\nYut → bir süre hızlan!",
        PowerUpType.Magnet    => "MIKNATIS\nYut → nesneleri çek!",
        PowerUpType.SizeBurst => "BÜYÜME\nYut → delik büyür!",
        _ => "Güç-up\nYut → geçici güç!",
    };

    static Sprite Load(string n)
    {
        var tex = Resources.Load<Texture2D>(n);
        return tex != null ? Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f) : null;
    }

    // İnce parlak halka sprite (hedef vurgusu).
    static Sprite MakeRingSprite(int s, float innerFrac)
    {
        var t = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        float c = (s - 1) * 0.5f, rad = c, inner = rad * innerFrac;
        var px = new Color32[s * s];
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                float aOut = Mathf.Clamp01((rad - d) / 2.5f);
                float aIn = Mathf.Clamp01((d - inner) / 2.5f);
                px[y * s + x] = new Color32(255, 255, 255, (byte)(Mathf.Min(aOut, aIn) * 255));
            }
        t.SetPixels32(px); t.Apply();
        return Sprite.Create(t, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f);
    }

    // İnce dikdörtgen ÇERÇEVE sprite (9-slice): "topla" vurgusu için halka yerine dörtgen (her boyutta ince kalır).
    static Sprite MakeRectBorderSprite(int s, int thick)
    {
        var t = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var px = new Color32[s * s];
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                bool border = x < thick || x >= s - thick || y < thick || y >= s - thick;
                px[y * s + x] = new Color32(255, 255, 255, border ? (byte)255 : (byte)0);
            }
        t.SetPixels32(px); t.Apply();
        // border (9-slice) = thick → Image.Type.Sliced ile herhangi bir W×H'de çerçeve kalınlığı sabit kalır.
        return Sprite.Create(t, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(thick, thick, thick, thick));
    }

    static Image NewImg(string n, Transform p, Sprite s)
    {
        var go = new GameObject(n, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(p, false);
        var img = go.GetComponent<Image>(); if (s != null) img.sprite = s;
        return img;
    }
    static TMP_Text NewText(string n, Transform p, float size, TextAlignmentOptions align)
    {
        var go = new GameObject(n, typeof(RectTransform));
        go.transform.SetParent(p, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.fontSize = size; t.fontStyle = FontStyles.Bold; t.alignment = align; t.raycastTarget = false;
        t.color = new Color(1f, 0.97f, 0.8f); t.enableWordWrapping = true;
        return t;
    }
    static void Stretch(RectTransform rt) { rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero; }
    static void Center(RectTransform rt, Vector2 size) { rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f); rt.pivot = new Vector2(0.5f, 0.5f); rt.sizeDelta = size; }
}
