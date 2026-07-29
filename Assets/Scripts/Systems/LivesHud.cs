using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Oynanışta sürekli görünen can göstergesi (sol-üst): 5 adet 3B kalp (dolu=kırmızı, kayıp=soluk).
/// Dolu değilken altında küçük "+1 kalp" ikonu + yenilenme geri sayımı (mm:ss). LivesManager'a bağlı.
/// Kalp sprite'ı HeartArt'tan (prosedürel, 3B). Sahnede bir GameObject'e ekle; Canvas otomatik bulunur.
/// </summary>
public class LivesHud : MonoBehaviour
{
    [Header("Yerleşim (sol-üst)")]
    public Canvas canvas;
    public Vector2 offset = new Vector2(18f, -14f);
    public float heartSize = 40f;
    public float spacing = 5f;

    Image[] hearts;
    GameObject regenGroup;
    TMP_Text regenClock;

    void Awake() { if (canvas == null) canvas = FindAnyObjectByType<Canvas>(); }

    void Start()
    {
        Build();
        if (LivesManager.Instance != null) LivesManager.Instance.OnChanged += Refresh;
        Refresh();
    }

    void OnDestroy() { if (LivesManager.Instance != null) LivesManager.Instance.OnChanged -= Refresh; }

    void Update() => Refresh();

    void Build()
    {
        if (canvas == null) { Debug.LogWarning("[LivesHud] Canvas yok."); return; }

        var row = NewRect("LivesHud", canvas.transform);
        row.anchorMin = row.anchorMax = new Vector2(0f, 1f);
        row.pivot = new Vector2(0f, 1f);
        row.anchoredPosition = offset;

        hearts = new Image[LivesManager.MaxLives];
        for (int i = 0; i < hearts.Length; i++)
        {
            var rt = NewRect("Heart" + i, row);
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(i * (heartSize + spacing), 0f);
            rt.sizeDelta = new Vector2(heartSize, heartSize);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = HeartArt.Full(); img.preserveAspect = true; img.raycastTarget = false;
            hearts[i] = img;
        }

        // "+1'li kalp" görseli + geri sayım (dolu değilken). Görselde zaten "+1" var → yazı eklemiyoruz.
        regenGroup = new GameObject("Regen", typeof(RectTransform));
        var gr = (RectTransform)regenGroup.transform;
        gr.SetParent(row, false);
        gr.anchorMin = gr.anchorMax = new Vector2(0f, 1f);
        gr.pivot = new Vector2(0f, 1f);
        gr.anchoredPosition = new Vector2(0f, -(heartSize + 4f));

        float mini = heartSize * 0.95f;
        var iconRt = NewRect("PlusHeart", gr);
        iconRt.anchorMin = iconRt.anchorMax = new Vector2(0f, 1f);
        iconRt.pivot = new Vector2(0f, 1f);
        iconRt.anchoredPosition = Vector2.zero;
        iconRt.sizeDelta = new Vector2(mini, mini);
        var iconImg = iconRt.gameObject.AddComponent<Image>();
        iconImg.sprite = HeartArt.Plus1(); iconImg.preserveAspect = true; iconImg.raycastTarget = false;

        regenClock = NewText("Clock", gr, 20, FontStyles.Bold, TextAlignmentOptions.Left);
        var cr = regenClock.rectTransform;
        cr.anchorMin = cr.anchorMax = new Vector2(0f, 1f); cr.pivot = new Vector2(0f, 1f);
        cr.anchoredPosition = new Vector2(mini + 6f, -(mini - mini * 0.78f) * 0.5f - 2f);
        cr.sizeDelta = new Vector2(120f, mini);
        regenClock.color = new Color(1f, 1f, 1f, 0.92f);
    }

    void Refresh()
    {
        var lm = LivesManager.Instance;
        if (lm == null || hearts == null) return;
        int l = lm.Lives;
        for (int i = 0; i < hearts.Length; i++)
            hearts[i].sprite = i < l ? HeartArt.Full() : HeartArt.Empty();
        bool full = lm.IsFull;
        if (regenGroup != null) regenGroup.SetActive(!full);
        if (!full && regenClock != null) regenClock.text = lm.NextLifeClock();
    }

    // ── küçük UI yardımcıları ──────────────────────────────────────────────────
    static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        return rt;
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
