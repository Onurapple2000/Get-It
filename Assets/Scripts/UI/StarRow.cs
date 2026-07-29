using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Başarı ekranında 3 yıldız gösterimi: önce 3 BOŞ yıldız görünür, sonra oyuncunun kazandığı (earned)
/// yıldızlar sırayla dolu hâlde POP ederek belirir. Kendi kendine animasyon yapar.
/// </summary>
public class StarRow : MonoBehaviour
{
    int earned;
    readonly RectTransform[] full = new RectTransform[3];

    public static void Build(Transform parent, int earned, Vector2 pos)
    {
        var go = new GameObject("StarRow", typeof(RectTransform));
        var rt = (RectTransform)go.transform; rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f); rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos; rt.sizeDelta = new Vector2(1020, 330);
        go.AddComponent<StarRow>().earned = Mathf.Clamp(earned, 0, 3);
    }

    void Start()
    {
        float[] xs = { -330f, 0f, 330f };
        const float size = 300f;   // ~2.3x büyütülmüş yıldız
        for (int i = 0; i < 3; i++)
        {
            MakeStar(StarArt.Empty(), xs[i], size);                 // boş (altta, hep görünür)
            var f = MakeStar(StarArt.Full(), xs[i], size);          // dolu (üstte, animasyonla gelir)
            f.localScale = Vector3.zero;
            full[i] = f;
        }
        StartCoroutine(Animate());
    }

    RectTransform MakeStar(Sprite sprite, float x, float size)
    {
        var go = new GameObject("Star", typeof(RectTransform), typeof(Image));
        var rt = (RectTransform)go.transform; rt.SetParent(transform, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f); rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(x, 0f); rt.sizeDelta = new Vector2(size, size);
        var img = go.GetComponent<Image>();
        img.sprite = sprite; img.preserveAspect = true; img.raycastTarget = false;
        if (sprite == null) img.color = new Color(1f, 1f, 1f, 0.15f);   // görsel yoksa hafif iz
        return rt;
    }

    IEnumerator Animate()
    {
        yield return new WaitForSeconds(0.35f);   // önce boş yıldızlar görünsün
        for (int i = 0; i < earned; i++)
        {
            yield return PopIn(full[i]);
            yield return new WaitForSeconds(0.12f);
        }
    }

    IEnumerator PopIn(RectTransform rt)
    {
        const float dur = 0.30f;
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / dur);
            // overshoot: 0 → 1.2 → 1
            float sc = k < 0.7f ? Mathf.Lerp(0f, 1.2f, k / 0.7f)
                                : Mathf.Lerp(1.2f, 1f, (k - 0.7f) / 0.3f);
            rt.localScale = Vector3.one * sc;
            yield return null;
        }
        rt.localScale = Vector3.one;
    }
}
