using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>Yutulan özel nesnenin verdiği güç türü (Sprint 4 — Materyal/Güç-Up sistemi).</summary>
public enum PowerUpType { None, Speed, Magnet, SizeBurst, Super }

/// <summary>
/// Güç-Up / Materyal sistemi (Sprint 4). Belirli nesneler yutulunca OTOMATİK geçici güç verir
/// (oyuncu seçim yapmaz). GameManager.ReportSwallowed → Activate çağırır.
///  • Speed (Altın): delik hızı süreli artar.
///  • Magnet (Elmas): yakındaki yutulabilirler deliğe doğru çekilir (bombalar hariç).
///  • SizeBurst (Araç): delik anında büyür (patlama hissi) — anlık, süresiz.
/// Aktif süreli güçler için HUD'da renkli etiket + boşalan bar gösterilir.
/// GameScene'de GameManager objesine eklenir (sahneye bir tane).
/// </summary>
public class PowerUpManager : MonoBehaviour
{
    public static PowerUpManager Instance;

    [Header("Speed (Altın)")]
    public float speedDuration = 15f;
    public float speedMultiplier = 1.5f;

    [Header("Magnet (Elmas)")]
    public float magnetDuration = 15f;
    [Tooltip("Delik yarıçapına eklenen çekim menzili.")]
    public float magnetRange = 6f;   // kinematik kaydırma ucuz olduğu için eski geniş menzil geri (güç korunur, kasma yok)
    [Tooltip("Çekim ivmesi (yerçekiminden bağımsız).")]
    public float magnetForce = 14f;

    [Header("SizeBurst (Araç)")]
    [Tooltip("Burst'te deliğe DOĞRUDAN eklenen boyut (GrowInstant → growMultiplier'sız). Belirgin anlık büyüme.")]
    public float burstGrow = 0.8f;

    [Header("Super (SÜRESİZ — sadece ödül/satın alma; sahnede doğmaz)")]
    [Tooltip("Kinematik süpürme menzili (delik yarıçapına eklenir). KISA → sadece çok yakın nesneleri çeker (kullanıcı 2026-08-17: 5→3).")]
    public float superRange = 3f;
    [Tooltip("Nesneleri deliğe kaydırma hızı (fizik-ötesi). 2026-08-17: 24→16 (çekiş gücü biraz azaltıldı).")]
    public float superSweepSpeed = 16f;
    [Tooltip("Süper aktifken delik büyüme çarpanı katı (genişleme hızlandırıcı).")]
    public float superGrowMult = 3f;
    bool superActive;

    HoleController hole;
    float baseSpeed;
    float speedTime, magnetTime, flashTime;

    // HUD
    Image speedBar, magnetBar;
    GameObject speedRow, magnetRow;
    TMP_Text flashText;
    GameObject flashRoot;
    CanvasGroup flashCg;
    float flashDur;

    void Awake() { Instance = this; }

    void Start()
    {
        hole = FindAnyObjectByType<HoleController>();
        if (hole != null) baseSpeed = hole.moveSpeed;
        BuildHud();
        if (GetComponent<PowerUpInventoryHud>() == null) gameObject.AddComponent<PowerUpInventoryHud>();   // oyun-içi envanter HUD'ı
    }

    /// <summary>Level bitince (success): devam eden güç-up göstergelerini (süre barları/toast) + envanter HUD'ını gizle
    /// ve etkileri durdur (kullanıcı 2026-08-17: bunlar success ekranında kalmasın).</summary>
    public void EndLevelHud()
    {
        speedTime = 0f; magnetTime = 0f; superActive = false; flashTime = 0f;
        if (hole != null) hole.moveSpeed = baseSpeed;
        if (speedRow != null) speedRow.SetActive(false);
        if (magnetRow != null) magnetRow.SetActive(false);
        if (flashRoot != null) flashRoot.SetActive(false);
        GetComponent<PowerUpInventoryHud>()?.Hide();
    }

    /// <summary>Bir güç-up nesnesi yutulunca çağrılır (GameManager.ReportSwallowed).</summary>
    public void Activate(PowerUpType type, Vector3 worldPos)
    {
        switch (type)
        {
            case PowerUpType.Speed:
                speedTime = speedDuration;
                if (hole != null) hole.moveSpeed = baseSpeed * speedMultiplier;
                break;
            case PowerUpType.Magnet:
                magnetTime = magnetDuration;
                break;
            case PowerUpType.SizeBurst:
                if (hole != null) hole.GrowInstant(burstGrow);   // DOĞRUDAN (growMultiplier'sız) → belirgin büyüme
                break;
            case PowerUpType.Super:
                // SÜRESİZ: (1) genişleme hızlandırıcı — büyüme çarpanını kalıcı yükselt + anlık büyüme,
                // (2) KİNEMATİK SÜPÜRME MIKNATISI (FixedUpdate/SuperSweep) — menzildeki her şeyi deliğe kaydırır (cap yok).
                superActive = true;
                if (hole != null) { hole.GrowInstant(burstGrow); hole.growMultiplier *= superGrowMult; }
                break;
        }
        AudioManager.Instance?.PlayPowerup();   // tüm güçler tek ses (powerups.mp3), yoksa prosedürel
        ShowFeedback(type);
    }

    // İlk birkaç karşılaşmada açıklayıcı ipucu (öğretme); sonra sadece anlık güçte kısa teyit.
    const int TIP_TIMES = 3;
    void ShowFeedback(PowerUpType type)
    {
        string key = "PowerTip_" + type;
        int shown = PlayerPrefs.GetInt(key, 0);
        if (shown < TIP_TIMES)
        {
            Flash(TipText(type), TipColor(type), 2.6f);
            PlayerPrefs.SetInt(key, shown + 1); PlayerPrefs.Save();
        }
        else if (type == PowerUpType.SizeBurst)
        {
            Flash(Loc.T("flashGrow"), TipColor(type), 1.0f);   // anlık güç (HUD bar'ı yok) — kısa teyit
        }
        else if (type == PowerUpType.Super)
        {
            Flash(Loc.T("flashSuper"), TipColor(type), 1.4f);
        }
    }

    static string TipText(PowerUpType t) => t switch
    {
        PowerUpType.Speed     => Loc.T("tipSpeed"),
        PowerUpType.Magnet    => Loc.T("tipMagnet"),
        PowerUpType.SizeBurst => Loc.T("tipSize"),
        PowerUpType.Super     => Loc.T("tipSuper"),
        _ => "",
    };

    static Color TipColor(PowerUpType t) => t switch
    {
        PowerUpType.Speed     => new Color(1f, 0.82f, 0.25f),
        PowerUpType.Magnet    => new Color(0.4f, 0.85f, 1f),
        PowerUpType.SizeBurst => new Color(0.45f, 0.95f, 0.5f),
        PowerUpType.Super     => new Color(1f, 0.5f, 0.2f),
        _ => Color.white,
    };

    void Update()
    {
        float dt = Time.deltaTime;

        if (speedTime > 0f)
        {
            speedTime -= dt;
            if (speedTime <= 0f && hole != null) hole.moveSpeed = baseSpeed;
        }
        if (magnetTime > 0f) magnetTime -= dt;
        if (flashTime > 0f)
        {
            flashTime -= dt;
            if (flashCg != null)
            {
                float inA  = Mathf.Clamp01((flashDur - flashTime) / 0.15f);   // ilk 0.15s belir
                float outA = Mathf.Clamp01(flashTime / 0.4f);                 // son 0.4s sön
                flashCg.alpha = Mathf.Min(inA, outA);
            }
            if (flashTime <= 0f && flashRoot != null) flashRoot.SetActive(false);
        }

        UpdateHud();
    }

    // Mıknatıs (ORİJİNAL hali — 2026-07-31 sıfırdan değerlendirme için geri döndürüldü): menzildeki tüm uyanık
    // yutulabilirler deliğe doğru çekilir (AddForce, bombalar hariç). Cap/Active-liste/kinematik YOK.
    void FixedUpdate()
    {
        if (hole == null) return;
        if (superActive) SuperSweep();
        if (magnetTime > 0f) MagnetPull();
    }

    // SÜPER: KİNEMATİK SÜPÜRME MIKNATISI (2026-08-06, [[project-powerups]] saklı fikri). Menzildeki nesneleri kinematik
    // yapıp MovePosition ile HIZLICA deliğe kaydırır (fizik-ötesi, cap yok — "hepsini süpür"). Merkeze ulaşınca kinematiği
    // bırakır → normal yutma/suction devralır (sığıyorsa yutulur; büyükse delik büyüdükçe girer). Güç-up'ları süpürmez.
    void SuperSweep()
    {
        Vector3 hp = hole.transform.position;
        float hr = hole.currentSize * 0.5f;
        float range = hr + superRange, range2 = range * range, releaseR = hr * 0.85f;
        var list = PhysicsSwallowable.All;
        for (int i = 0; i < list.Count; i++)
        {
            var s = list[i];
            if (s == null || s.IsSwallowed || s.isBomb || s.powerUp != PowerUpType.None) continue;
            var rb = s.Body; if (rb == null) continue;
            Vector3 d = hp - s.transform.position; d.y = 0f;
            float sq = d.sqrMagnitude;
            if (sq > range2 || sq <= releaseR * releaseR)
            {
                if (rb.isKinematic) rb.isKinematic = false;   // menzil dışı / merkezde → normal fizik devralsın
                continue;
            }
            if (!rb.isKinematic) rb.isKinematic = true;
            Vector3 pos = s.transform.position, tgt = new Vector3(hp.x, pos.y, hp.z);
            rb.MovePosition(Vector3.MoveTowards(pos, tgt, superSweepSpeed * Time.fixedDeltaTime));
        }
    }

    void MagnetPull()
    {
        Vector3 hp = hole.transform.position;
        float range = hole.currentSize * 0.5f + magnetRange;
        float range2 = range * range;

        // ⚠️ KASMA FIX (2026-08-01, L12 makaron Pisa kulesi): mıknatıs+hız ile onlarca nesne deliği HIZLI takip
        // ederken AddForce(Acceleration) + tavan-yok → nesneler ÇILGIN yörünge hızına ulaşıp (fırtına hortumu)
        // birbirine/hareketli zemine DERİN penetrasyon yapıyor → PhysX solver/contact spike → kasma. Yavaş
        // harekette hız düşük kalıyor → kasma yok (kullanıcı gözlemi: hız = tek değişken). Çözüm: yatay hızı
        // deliği YAKALAMAYA yetecek ama taşkın olmayacak bir tavana kıstır. Çekilen nesne SETİ + kuvvet AYNI
        // (güç korunur) — sadece runaway hız sönümlenir; normal mıknatıs kullanımı bu tavana ulaşmaz.
        float maxV = Mathf.Max(hole.moveSpeed * 1.8f, 10f);
        float maxV2 = maxV * maxV;

        var list = PhysicsSwallowable.All;
        for (int i = 0; i < list.Count; i++)
        {
            var s = list[i];
            if (s == null || s.IsSwallowed || s.isBomb) continue;   // bombaları çekme (haksızlık)
            var rb = s.Body;
            if (rb == null || rb.isKinematic) continue;

            Vector3 d = hp - s.transform.position; d.y = 0f;
            float sq = d.sqrMagnitude;
            if (sq > range2 || sq < 0.0001f) continue;
            rb.AddForce(d.normalized * magnetForce, ForceMode.Acceleration);

            // Yatay hız tavanı (dikey hıza DOKUNMA → yutma/suction/gravity bozulmaz).
            Vector3 v = rb.linearVelocity;
            float hv2 = v.x * v.x + v.z * v.z;
            if (hv2 > maxV2)
            {
                float k = maxV / Mathf.Sqrt(hv2);
                v.x *= k; v.z *= k;
                rb.linearVelocity = v;
            }
        }
    }

    // ── HUD ─────────────────────────────────────────────────────────────────────
    void BuildHud()
    {
        var canvas = UiRoot.GameCanvas();   // GÜVENİLİR ana canvas (yanlış canvas yarışı fix)
        if (canvas == null) return;
        var safe = UiRoot.SafeContent(canvas);   // HUD güvenli alana (çentik/kenar dışı)

        // ⚠️ 2026-07-25 (hız powerup YALPALAMA fix): süre çizgisi her kare boyut değiştirince ana HUD canvas'ı
        // yeniden batch'leniyordu → kare atlaması → hızlı delikte sıçrama. Güç HUD'ını AYRI alt-Canvas'a al →
        // rebuild yalnız bu küçük canvas'ta kalır, oyun HUD'ı etkilenmez. (Canvas nested: render mode inherit.)
        var powHud = new GameObject("PowerHud", typeof(RectTransform), typeof(Canvas));
        var phRt = (RectTransform)powHud.transform; phRt.SetParent(safe, false);
        phRt.anchorMin = Vector2.zero; phRt.anchorMax = Vector2.one; phRt.offsetMin = phRt.offsetMax = Vector2.zero;

        speedRow  = MakeRow(powHud.transform, -210, Loc.T("hudSpeed"),      new Color(1f, 0.78f, 0.20f), out speedBar);
        magnetRow = MakeRow(powHud.transform, -290, Loc.T("hudMagnet"), new Color(0.35f, 0.85f, 1f),  out magnetBar);
        speedRow.SetActive(false); magnetRow.SetActive(false);

        // Güç ipucu "toast": yuvarlak koyu pill + renkli metin, üst-orta, fade in/out
        flashRoot = new GameObject("PowerToast", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
        var pr = (RectTransform)flashRoot.transform; pr.SetParent(safe, false);
        pr.anchorMin = pr.anchorMax = new Vector2(0.5f, 1f); pr.pivot = new Vector2(0.5f, 1f);
        pr.anchoredPosition = new Vector2(0, -230); pr.sizeDelta = new Vector2(620, 84);
        flashCg = flashRoot.GetComponent<CanvasGroup>();
        var pbg = flashRoot.GetComponent<Image>();
        pbg.color = new Color(0.09f, 0.07f, 0.05f, 0.84f);   // düz koyu pill (builtin sprite Unity 6'da yok)
        pbg.raycastTarget = false;

        var tg = new GameObject("Text", typeof(RectTransform));
        tg.transform.SetParent(pr, false);
        flashText = tg.AddComponent<TextMeshProUGUI>();
        flashText.fontSize = 32; flashText.fontStyle = FontStyles.Bold;
        flashText.alignment = TextAlignmentOptions.Center; flashText.enableWordWrapping = true;
        flashText.raycastTarget = false; flashText.color = new Color(1f, 0.55f, 0.2f);
        var tr = flashText.rectTransform; tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one;
        tr.offsetMin = new Vector2(22, 6); tr.offsetMax = new Vector2(-22, -6);

        flashRoot.SetActive(false);
    }

    // Süre çizgisi ölçüleri (satır genişliği 300; iki kenardan 8 pay → 284; yükseklik 10).
    const float BarLeft = 8f, BarFullW = 284f, BarH = 10f;

    // Sol kenara yaslı satır: renkli etiket + altında boşalan bar. anchor sol-üst.
    GameObject MakeRow(Transform parent, float y, string label, Color color, out Image bar)
    {
        var row = new GameObject("Pow_" + label, typeof(RectTransform));
        var rt = (RectTransform)row.transform; rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f); rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(20, y); rt.sizeDelta = new Vector2(300, 64);

        // arka plan pill
        var bgImg = row.AddComponent<Image>();
        bgImg.color = new Color(0f, 0f, 0f, 0.45f); bgImg.raycastTarget = false;

        // etiket
        var tg = new GameObject("Label", typeof(RectTransform));
        tg.transform.SetParent(rt, false);
        var t = tg.AddComponent<TextMeshProUGUI>();
        t.text = label; t.fontSize = 32; t.fontStyle = FontStyles.Bold; t.color = color;
        t.alignment = TextAlignmentOptions.Left; t.raycastTarget = false;
        var tr = t.rectTransform; tr.anchorMin = new Vector2(0, 0); tr.anchorMax = new Vector2(1, 1);
        tr.offsetMin = new Vector2(16, 14); tr.offsetMax = new Vector2(-12, -4);

        // ── SÜRE ÇİZGİSİ (2026-07-25 kullanıcı): süreyle orantılı, SAĞDAN SOLA azalan alt şerit. ──
        // ⚠️ Eski: Image.Type.Filled + sprite YOK → fillAmount GÖRÜNMEZ (çizgi hep dolu kalıyordu). Yeni: düz Image,
        // sol kenara sabit, GENİŞLİĞİ kalan orana göre küçülür (sağ kenar sola çekilir = sağdan sola boşalma).
        // Altta soluk TRACK (tam genişlik) → çizginin ne kadar azaldığı net görünür.
        var trackGo = new GameObject("BarTrack", typeof(RectTransform));
        trackGo.transform.SetParent(rt, false);
        var track = trackGo.AddComponent<Image>();
        track.color = new Color(0f, 0f, 0f, 0.40f); track.raycastTarget = false;
        var trr = track.rectTransform;
        trr.anchorMin = new Vector2(0, 0); trr.anchorMax = new Vector2(0, 0); trr.pivot = new Vector2(0, 0);
        trr.anchoredPosition = new Vector2(BarLeft, 6f); trr.sizeDelta = new Vector2(BarFullW, BarH);

        var bg2 = new GameObject("BarFill", typeof(RectTransform));
        bg2.transform.SetParent(rt, false);
        bar = bg2.AddComponent<Image>();
        bar.color = color; bar.raycastTarget = false;
        var br = bar.rectTransform;
        br.anchorMin = new Vector2(0, 0); br.anchorMax = new Vector2(0, 0); br.pivot = new Vector2(0, 0);   // sol-alt köşe sabit
        br.anchoredPosition = new Vector2(BarLeft, 6f); br.sizeDelta = new Vector2(BarFullW, BarH);          // başlangıçta tam

        return row;
    }

    void UpdateHud()
    {
        if (speedRow != null)
        {
            bool on = speedTime > 0f;
            if (speedRow.activeSelf != on) speedRow.SetActive(on);
            if (on) SetBarWidth(speedBar, ref lastSpeedW, BarFullW * Mathf.Clamp01(speedTime / speedDuration));
        }
        if (magnetRow != null)
        {
            bool on = magnetTime > 0f;
            if (magnetRow.activeSelf != on) magnetRow.SetActive(on);
            if (on) SetBarWidth(magnetBar, ref lastMagnetW, BarFullW * Mathf.Clamp01(magnetTime / magnetDuration));
        }
    }

    // Çizgi genişliğini SADECE ≥1px değişince günceller → gereksiz canvas rebuild yok (her kare değil).
    float lastSpeedW = -1f, lastMagnetW = -1f;
    static void SetBarWidth(Image bar, ref float last, float w)
    {
        if (bar == null || Mathf.Abs(w - last) < 1f) return;
        last = w;
        bar.rectTransform.sizeDelta = new Vector2(w, BarH);
    }

    void Flash(string msg, Color color, float duration)
    {
        if (flashRoot == null) return;
        flashText.text = msg;
        flashText.color = color;
        flashDur = duration; flashTime = duration;
        flashCg.alpha = 0f;
        flashRoot.SetActive(true);
    }
}
