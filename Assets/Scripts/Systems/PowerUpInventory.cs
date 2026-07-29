using UnityEngine;

/// <summary>
/// Kalıcı güç-up envanteri (PlayerPrefs). Yıldız hediyeleriyle (StarRewards) kazanılan güç-up'lar burada
/// birikir; ileride level içinde harcanabilir. Altyapı — kullanım (harcama) UI'ı sonra bağlanır.
/// </summary>
public static class PowerUpInventory
{
    static string Key(PowerUpType t) => $"PowerInv_{t}";

    public static int Count(PowerUpType t) => PlayerPrefs.GetInt(Key(t), 0);

    public static void Add(PowerUpType t, int n = 1)
    {
        if (t == PowerUpType.None || n <= 0) return;
        PlayerPrefs.SetInt(Key(t), Count(t) + n);
        PlayerPrefs.Save();
    }

    /// <summary>Bir adet harca (varsa). Başarılıysa true.</summary>
    public static bool TryConsume(PowerUpType t)
    {
        int c = Count(t);
        if (c <= 0) return false;
        PlayerPrefs.SetInt(Key(t), c - 1);
        PlayerPrefs.Save();
        return true;
    }
}
