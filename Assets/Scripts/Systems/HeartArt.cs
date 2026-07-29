using UnityEngine;

/// <summary>
/// Can/kalp görselleri (Resources/Hearts'ten yüklenir): dolu, boş ve "+1"li kalp. DALL-E ile üretilen
/// PNG'ler. Yükleme başarısızsa prosedürel kalp'e düşer (oyun bozulmasın). HUD (LivesHud) ve fail
/// rozeti (LivesBadge) kullanır.
/// </summary>
public static class HeartArt
{
    static Sprite _full, _empty, _plus1, _proc;

    public static Sprite Full()  => _full  ? _full  : (_full  = Load("heart_full"));
    public static Sprite Empty() => _empty ? _empty : (_empty = Load("heart_empty"));
    public static Sprite Plus1() => _plus1 ? _plus1 : (_plus1 = Load("heart_full_plus1"));

    static Sprite Load(string name)
    {
        var s = Resources.Load<Sprite>("Hearts/" + name);
        if (s == null) { Debug.LogWarning($"[HeartArt] Resources/Hearts/{name} bulunamadı — prosedürel kalp kullanılıyor."); return Procedural(); }
        return s;
    }

    // ── Yedek: prosedürel 3B kalp (görsel yüklenemezse) ───────────────────────
    static Sprite Procedural()
    {
        if (_proc != null) return _proc;
        const int size = 96;
        var t = new Texture2D(size, size, TextureFormat.RGBA32, false)
        { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        float[] ss = { 0.25f, 0.75f };
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            int hits = 0;
            foreach (float sy in ss) foreach (float sx in ss)
            {
                float u = (x + sx) / size * 2.6f - 1.3f, v = (y + sy) / size * 2.6f - 1.3f;
                float c = u * u + v * v - 1f;
                if (c * c * c - u * u * v * v * v < 0f) hits++;
            }
            t.SetPixel(x, y, new Color(1f, 1f, 1f, hits / 4f));
        }
        t.Apply();
        _proc = Sprite.Create(t, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        return _proc;
    }
}
