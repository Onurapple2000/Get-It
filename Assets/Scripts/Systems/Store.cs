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
/// Mağaza / satın alma facade — gerçek akış <see cref="IapService"/> (Unity IAP v5). Ürün→ödül eşlemesi ve
/// coin/güç-up/noAds verme IapService'te; UI buraya yönlenir. IAP hazır değilse (çevrimdışı) onDone(false).
/// </summary>
public static class Store
{
    /// <summary>Ürün ID ile paket satın al. Başarıda ödül IapService.Grant'ta verilir + buluta yazılır.</summary>
    public static void Buy(string productId, Action<bool> onDone)
    {
        if (IapService.Instance == null) { Debug.LogWarning("[Store] IAP servisi yok."); onDone?.Invoke(false); return; }
        IapService.Instance.Buy(productId, onDone);
    }

    /// <summary>Reklamsız (non-consumable) satın al.</summary>
    public static void RemoveAds(Action<bool> onDone) => Buy(IapService.NoAdsId, onDone);

    /// <summary>Önceki satın almaları geri yükle (özellikle iOS Reklamsız; Android otomatik).</summary>
    public static void Restore(Action<bool> onDone)
    {
        if (IapService.Instance == null) { onDone?.Invoke(false); return; }
        IapService.Instance.Restore(onDone);
    }
}
