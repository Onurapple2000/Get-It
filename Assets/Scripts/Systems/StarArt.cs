using UnityEngine;

/// <summary>
/// Yıldız görsellerini (Resources) runtime'da Sprite olarak sağlar (önbellekli). Kaynak PNG'ler:
/// Assets/Resources/star_empty.png, star_full_yellow.png. Import tipi ne olursa olsun Texture2D'den
/// Sprite üretilir (Sprite modu şart değil).
/// </summary>
public static class StarArt
{
    static Sprite _empty, _full;

    public static Sprite Empty() => Get("star_empty", ref _empty);
    public static Sprite Full()  => Get("star_full_yellow", ref _full);

    static Sprite Get(string res, ref Sprite cache)
    {
        if (cache != null) return cache;
        var tex = Resources.Load<Texture2D>(res);
        if (tex == null) { Debug.LogWarning($"[StarArt] Resources/{res} bulunamadı."); return null; }
        cache = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
        return cache;
    }
}
