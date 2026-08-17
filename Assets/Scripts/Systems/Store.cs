using System;
using UnityEngine;

/// <summary>
/// Reklam servisi — STUB (2026-08-06). Gerçek Unity Ads/AdMob buraya bağlanacak. Şimdilik: reklamsız satın
/// alındıysa hiç göstermez; ödüllü reklam anında "izlendi" sayılır (onDone(true)).
/// </summary>
public static class AdsService
{
    public static bool AdsRemoved => PlayerProfile.NoAds;

    /// <summary>Ödüllü reklam göster; bitince onDone(başarı). Stub: anında başarı.</summary>
    public static void ShowRewarded(Action<bool> onDone)
    {
        Debug.Log("[Ads] (stub) ödüllü reklam izlendi.");
        onDone?.Invoke(true);
    }
}

/// <summary>
/// Mağaza / satın alma — STUB (2026-08-06). Gerçek Unity IAP buraya bağlanacak. Şimdilik satın almalar anında
/// başarılı sayılıp yerel olarak verilir (coin ekler / No-Ads açar / güç-up ekler).
/// </summary>
public static class Store
{
    public static void RemoveAds(Action<bool> onDone)
    {
        PlayerProfile.NoAds = true;
        Debug.Log("[Store] (stub) reklamlar kaldırıldı.");
        onDone?.Invoke(true);
    }

    public static void BuyCoins(int amount, Action<bool> onDone)
    {
        PlayerProfile.AddCoins(amount);
        Debug.Log($"[Store] (stub) {amount} coin alındı.");
        onDone?.Invoke(true);
    }

    public static void BuyPowerUp(PowerUpType type, int amount, Action<bool> onDone)
    {
        PowerUpInventory.Add(type, amount);
        Debug.Log($"[Store] (stub) {amount}x {type} alındı.");
        onDone?.Invoke(true);
    }

    /// <summary>Paket satın al: coin + güç-up karışımını verir. Gerçek IAP sonra ödeme akışını buraya bağlayacak.</summary>
    public static void Purchase(int coins, (PowerUpType type, int amount)[] powers, Action<bool> onDone)
    {
        if (coins > 0) PlayerProfile.AddCoins(coins);
        if (powers != null) foreach (var p in powers) PowerUpInventory.Add(p.type, p.amount);
        Debug.Log($"[Store] (stub) paket alındı: {coins} coin + {(powers?.Length ?? 0)} güç-up türü.");
        onDone?.Invoke(true);
    }
}
