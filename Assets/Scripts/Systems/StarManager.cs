using UnityEngine;

/// <summary>
/// Yıldız (star) derecelendirme + kalıcı kayıt. 1. yıldız GARANTİ (skor ≥ 1 → en az 1★, hiç 0★ olmaz).
/// 2. ve 3. yıldız: alınabilecek MAKS puanın %50'si baz alınır, bunun 2/3'ü 2★, 3/3'ü 3★.
/// Örn: max 1200 → %50 = 600 → 1+ puan 1★, 400 puan 2★, 600+ puan 3★.
/// Level başına EN İYİ yıldız PlayerPrefs'te saklanır; toplam = tüm levellerin en iyilerinin toplamı.
/// </summary>
public static class StarManager
{
    static string Key(int world, int level) => $"Stars_W{world}_L{level}";

    /// <summary>Skora göre yıldız (0-3). maxScore = o levelde alınabilecek toplam puan.</summary>
    public static int Evaluate(int score, int maxScore)
    {
        if (score <= 0) return 0;                  // hiç puan yoksa 0 (galibiyette skor>0 → en az 1★ garanti)
        if (maxScore <= 0) return 3;
        float per = maxScore * 0.5f / 3f;          // 2. ve 3. yıldız eşiği (maks puanın %50'sinin 1/3'ü)
        if (per < 0.001f) return 3;
        int stars = Mathf.FloorToInt(score / per); // eski kural (0..3)
        return Mathf.Clamp(Mathf.Max(1, stars), 1, 3);   // 1. yıldız garanti (skor≥1); 2./3. eski eşiğe bağlı
    }

    public static int Best(int world, int level) => PlayerPrefs.GetInt(Key(world, level), 0);

    /// <summary>Bu level için yıldızı kaydet (yalnızca öncekinden iyiyse). Yeni en iyi değeri döndürür.</summary>
    public static int Record(int world, int level, int stars)
    {
        int best = Best(world, level);
        if (stars > best)
        {
            PlayerPrefs.SetInt(Key(world, level), stars);
            PlayerPrefs.Save();
            best = stars;
        }
        return best;
    }

    /// <summary>Tüm dünyalardaki en iyi yıldızların toplamı (oyuncunun biriktirdiği yıldız).</summary>
    public static int Total()
    {
        int sum = 0;
        for (int w = 0; w < WorldCatalog.Count; w++)
        {
            int n = WorldCatalog.PlayableLevels(w);
            for (int l = 0; l < n; l++) sum += Best(w, l);
        }
        return sum;
    }
}
