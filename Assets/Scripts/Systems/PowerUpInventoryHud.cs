using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Oyun-içi GÜÇ-UP ENVANTERİ HUD'ı (2026-08-06 kullanıcı): X (kapat) tuşunun ALTINDA, yukarıdan aşağıya dikey
/// güç-up ikonları (Hız / Mıknatıs / Büyütme / Süper). Her ikonun SOLUNDA sahip olunan adet (PowerUpInventory).
/// İkona basınca — adet varsa — 1 tanesi TÜKETİLİR ve o güç anında uygulanır (yutmuş gibi). PowerUpManager tarafından
/// GameScene'de oluşturulur.
/// </summary>
public class PowerUpInventoryHud : MonoBehaviour
{
    static readonly PowerUpType[] Types = { PowerUpType.Speed, PowerUpType.Magnet, PowerUpType.SizeBurst, PowerUpType.Super };

    readonly Dictionary<PowerUpType, TMP_Text> counts = new();
    readonly Dictionary<PowerUpType, Image> icons = new();
    HoleController hole;
    GameObject hudRoot;

    /// <summary>Level bitince (success/fail) envanter HUD'ını gizle.</summary>
    public void Hide() { if (hudRoot) hudRoot.SetActive(false); }

    void Start() { StartCoroutine(BuildWhenReady()); }

    /// <summary>
    /// BUG FIX (2026-08-22): Eskiden Start'ta canvas null ise SESSİZCE vazgeçiliyordu → HUD o levelda hiç
    /// kurulmuyordu ("bazen güç-up'larım görünmüyor, çıkıp girince geliyor"). Canvas sahne kurulurken birkaç
    /// kare gecikebiliyor; artık hazır olana kadar BEKLİYORUZ (yarış koşulu kapandı).
    /// </summary>
    System.Collections.IEnumerator BuildWhenReady()
    {
        Canvas canvas = null;
        float waited = 0f;
        while ((canvas = UiRoot.GameCanvas()) == null && waited < 5f) { waited += Time.unscaledDeltaTime; yield return null; }
        if (canvas == null) { Debug.LogWarning("[PowerInvHud] Ana canvas bulunamadı — envanter HUD'ı kurulamadı."); yield break; }

        hole = FindAnyObjectByType<HoleController>();

        var root = new GameObject("PowerInvHud", typeof(RectTransform), typeof(SafeArea));
        hudRoot = root;
        var rt = (RectTransform)root.transform; rt.SetParent(canvas.transform, false);
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;

        const float icon = 58f, step = 66f;   // daha küçük ikon + daha sık alt-alta (kullanıcı 2026-08-06)
        float y = -400f;   // X (kapat) tuşunun ALTINDA — üst üste binmesin (kullanıcı 2026-08-17)
        foreach (var t in Types)
        {
            var ic = MakeImage("Ic_" + t, root.transform, IconFor(t));
            ic.preserveAspect = true; ic.raycastTarget = true;
            var ir = ic.rectTransform; ir.anchorMin = ir.anchorMax = new Vector2(1f, 1f); ir.pivot = new Vector2(1f, 1f);
            ir.anchoredPosition = new Vector2(-16f, y); ir.sizeDelta = new Vector2(icon, icon);
            var btn = ic.gameObject.AddComponent<Button>(); btn.transition = Selectable.Transition.None;
            var tt = t; btn.onClick.AddListener(() => Use(tt));
            icons[t] = ic;

            var cnt = MakeText("Cnt_" + t, root.transform, 26);   // daha küçük adet
            cnt.alignment = TextAlignmentOptions.MidlineRight; cnt.color = new Color(1f, 0.95f, 0.7f);
            var cr = cnt.rectTransform; cr.anchorMin = cr.anchorMax = new Vector2(1f, 1f); cr.pivot = new Vector2(1f, 0.5f);
            cr.anchoredPosition = new Vector2(-16f - icon - 6f, y - icon * 0.5f); cr.sizeDelta = new Vector2(84f, icon);
            counts[t] = cnt;

            y -= step;
        }
        Refresh();

        // TEŞHİS (2026-08-22): "bazen güç-up'larım görünmüyor" bug'ı — HUD mu kurulmadı, yoksa envanter mi boş?
        // logcat: adb logcat -s Unity | grep PowerInvHud
        Debug.Log($"[PowerInvHud] kuruldu ({waited:0.00}s bekledi) — envanter: " +
                  $"Hız={PowerUpInventory.Count(PowerUpType.Speed)} " +
                  $"Mıknatıs={PowerUpInventory.Count(PowerUpType.Magnet)} " +
                  $"Büyüme={PowerUpInventory.Count(PowerUpType.SizeBurst)} " +
                  $"Süper={PowerUpInventory.Count(PowerUpType.Super)}");

        // İKİNCİ YARIŞ: bulut senkronu HUD kurulduktan SONRA tamamlanırsa adetler eski kalırdı.
        // Senkron bitince kendini tazele (çevrimdışıysa/servis yoksa zararsızca atlanır).
        var cs = CloudSyncService.Instance;
        if (cs != null) { cs.OnSynced -= Refresh; cs.OnSynced += Refresh; }
    }

    void OnDestroy()
    {
        var cs = CloudSyncService.Instance;
        if (cs != null) cs.OnSynced -= Refresh;
    }

    void OnEnable() { if (counts.Count > 0) Refresh(); }

    void Use(PowerUpType t)
    {
        AudioManager.Instance?.PlayUiClick();   // her dokunuş sesli teyit (tık kaydını doğrular)
        if (!PowerUpInventory.TryConsume(t))
        {
            if (icons.TryGetValue(t, out var im)) StartCoroutine(DenyFlash(im));   // adet yok → kırmızı titret
            return;
        }
        Vector3 pos = hole != null ? hole.transform.position : Vector3.zero;
        PowerUpManager.Instance?.Activate(t, pos);
        Refresh();
    }

    // Adet yokken dokununca: ikon kısa süre kırmızı + hafif büyüyüp küçülür (tıklama kaydedildi ama envanter boş).
    System.Collections.IEnumerator DenyFlash(Image img)
    {
        if (img == null) yield break;
        var rt = img.rectTransform; Vector3 s0 = rt.localScale;
        float e = 0f;
        while (e < 0.3f)
        {
            e += Time.unscaledDeltaTime; float k = e / 0.3f;
            img.color = Color.Lerp(new Color(1f, 0.3f, 0.3f, 1f), new Color(1f, 1f, 1f, 0.32f), k);
            rt.localScale = s0 * (1f + 0.18f * Mathf.Sin(k * Mathf.PI));
            yield return null;
        }
        rt.localScale = s0; Refresh();
    }

    public void Refresh()
    {
        foreach (var t in Types)
        {
            int c = PowerUpInventory.Count(t);
            if (counts.TryGetValue(t, out var txt)) { txt.text = "×" + c; txt.color = c > 0 ? new Color(1f, 0.95f, 0.7f) : new Color(1f, 1f, 1f, 0.4f); }
            // Sahip olunan → TAM parlak (beyaz tint); olmayan → belirgin soluk gri. (Sadece super parlak kalma bug'ı fix.)
            if (icons.TryGetValue(t, out var img)) img.color = c > 0 ? Color.white : new Color(0.5f, 0.5f, 0.5f, 0.32f);
        }
    }

    static Sprite IconFor(PowerUpType t) => PowerUpIcons.Get(t);   // GLB'den üretilmiş PNG (Resources, ortak yükleyici)

    static Image MakeImage(string name, Transform parent, Sprite s)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>(); if (s != null) img.sprite = s;
        return img;
    }

    static TMP_Text MakeText(string name, Transform parent, float size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.fontSize = size; t.fontStyle = FontStyles.Bold; t.raycastTarget = false;
        return t;
    }
}
