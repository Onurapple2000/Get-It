using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Fail panelinin altındaki can rozeti (eski "Can: 4/5 (+1: 00:30)" yazısının yerine). Yatay, ortalı:
/// [3B kalp] "4 / 5"   [+1 kalp] "00:30". Dolu ise "+1 kalp" + süre gizlenir. LivesManager'a bağlı.
/// Bu component, üzerinde bulunduğu objedeki eski TMP yazısını gizleyip kendi öğelerini kurar.
/// </summary>
public class LivesBadge : MonoBehaviour
{
    public float heartSize = 54f;
    public float plusHeartSize = 64f;

    TMP_Text countText, clockText;
    GameObject regenGroup;

    void Awake()
    {
        var old = GetComponent<TMP_Text>();
        if (old != null) old.enabled = false;   // eski düz yazıyı gizle
        Build();
    }

    void Update() => Refresh();

    void Build()
    {
        // Ortalı yatay grup (içeriğe göre boyutlanır → parent içinde ortalanır)
        var c = new GameObject("Badge", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
        var cr = (RectTransform)c.transform;
        cr.SetParent(transform, false);
        cr.anchorMin = cr.anchorMax = new Vector2(0.5f, 0.5f);
        cr.pivot = new Vector2(0.5f, 0.5f);
        cr.anchoredPosition = Vector2.zero;
        var hlg = c.GetComponent<HorizontalLayoutGroup>();
        hlg.spacing = 10f; hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childControlWidth = hlg.childControlHeight = false;
        hlg.childForceExpandWidth = hlg.childForceExpandHeight = false;
        var fit = c.GetComponent<ContentSizeFitter>();
        fit.horizontalFit = fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        HeartIcon("Heart", cr, HeartArt.Full(), heartSize);
        countText = Label("Count", cr, 120f, heartSize, 38, "4 / 5");

        regenGroup = new GameObject("Regen", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        var rr = (RectTransform)regenGroup.transform;
        rr.SetParent(cr, false);
        var rhlg = regenGroup.GetComponent<HorizontalLayoutGroup>();
        rhlg.spacing = 8f; rhlg.childAlignment = TextAnchor.MiddleCenter;
        rhlg.childControlWidth = rhlg.childControlHeight = false;
        rhlg.childForceExpandWidth = rhlg.childForceExpandHeight = false;
        var rle = regenGroup.GetComponent<LayoutElement>();
        rle.preferredWidth = plusHeartSize + 8f + 110f; rle.preferredHeight = heartSize;

        HeartIcon("PlusHeart", rr, HeartArt.Plus1(), plusHeartSize);   // görselde zaten "+1" var
        clockText = Label("Clock", rr, 110f, heartSize, 32, "00:30");
    }

    void Refresh()
    {
        var lm = LivesManager.Instance;
        if (lm == null || countText == null) return;
        int l = lm.Lives;
        countText.text = $"{l} / {LivesManager.MaxLives}";
        bool full = lm.IsFull;
        if (regenGroup != null) regenGroup.SetActive(!full);
        if (!full && clockText != null) clockText.text = lm.NextLifeClock();
    }

    // ── yardımcılar ────────────────────────────────────────────────────────────
    static void HeartIcon(string name, Transform parent, Sprite heart, float size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.sizeDelta = new Vector2(size, size);
        var le = go.GetComponent<LayoutElement>(); le.preferredWidth = size; le.preferredHeight = size;
        var img = go.GetComponent<Image>();
        img.sprite = heart; img.preserveAspect = true; img.raycastTarget = false;
    }

    static TMP_Text Label(string name, Transform parent, float w, float h, float fontSize, string text)
    {
        var t = NewText(name, parent, fontSize, FontStyles.Bold, TextAlignmentOptions.Center);
        t.text = text; t.color = Color.white;
        t.rectTransform.sizeDelta = new Vector2(w, h);
        var le = t.gameObject.AddComponent<LayoutElement>(); le.preferredWidth = w; le.preferredHeight = h;
        return t;
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
