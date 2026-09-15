/// <summary>
/// Sprint 10 / Coin ekonomisi — merkezi fiyat & ödül sabitleri (2026-08-18 kullanıcı onaylı tablo).
/// Coin yumuşak paradır: KAYNAK = ödüllü reklam + IAP; harcama yerleri (sink) coin VEYA reklam ile.
/// </summary>
public static class Economy
{
    public const int FreeCoinsPerAd = 50;    // "free coins" reklamı başına

    // Coin ile powerup fiyatları
    public static int PowerupCost(PowerUpType t) => t switch
    {
        PowerUpType.Speed     => 150,
        PowerUpType.Magnet    => 200,
        PowerUpType.SizeBurst => 250,
        PowerUpType.Super     => 500,
        _ => int.MaxValue,
    };

    public const int ContinueCost   = 100;   // fail'de +süre / bombada devam
    public const int RefillLifeCost  = 80;   // +1 can
    public const int SkipAdCost      = 30;   // tek geçiş reklamını atla (coin-only)
    public const float ContinueBonusSeconds = 40f;   // süre-fail'inde "devam"da eklenen süre
}
