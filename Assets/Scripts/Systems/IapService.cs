using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Purchasing;
using UnityEngine.Purchasing.Extension;
using Unity.Services.Core;

/// <summary>
/// Sprint 10 — Adım 4: Unity IAP (v4) satın alma. 5 coin/güç-up paketi (consumable) + Reklamsız (non-consumable).
/// Ürün→ödül eşlemesi <see cref="Packs"/>'te: satın alma işlenince (ProcessPurchase) ID'ye göre coin/güç-up/noAds
/// verilir + CloudSync.FlushNow ile buluta yazılır (bekleyen/restore satın almalar da tek yerde doğru işlenir).
/// Store facade (Store.Buy/RemoveAds/Restore) buraya yönlenir. Diğer manager'lar gibi oto-bootstrap.
/// </summary>
public class IapService : MonoBehaviour, IDetailedStoreListener
{
    public static IapService Instance { get; private set; }

    public struct Pack
    {
        public string id, locKey;
        public bool noAds;
        public int coins;
        public (PowerUpType type, int amount)[] powers;
        public int priceTier;   // Loc.Price yedeği (0-4); -1 = noAds
    }

    public const string NoAdsId = "getit.noads";
    public static readonly Pack[] Packs =
    {
        new Pack { id = "getit.pack.starter", locKey = "pkStarter", coins = 1000, powers = new[] { (PowerUpType.Speed, 4) }, priceTier = 0 },
        new Pack { id = "getit.pack.coinbag", locKey = "pkCoinBag", coins = 3000, powers = null, priceTier = 1 },
        new Pack { id = "getit.pack.magnet",  locKey = "pkMagnet",  coins = 2000, powers = new[] { (PowerUpType.Magnet, 10) }, priceTier = 2 },
        new Pack { id = "getit.pack.super",   locKey = "pkSuper",   coins = 5000, powers = new[] { (PowerUpType.SizeBurst, 6), (PowerUpType.Super, 2) }, priceTier = 3 },
        new Pack { id = "getit.pack.super3",  locKey = "pkSuper3",  coins = 0, powers = new[] { (PowerUpType.Super, 6) }, priceTier = 4 },
        new Pack { id = NoAdsId, locKey = "removeAds", noAds = true, priceTier = -1 },
    };

    /// <summary>Ürünler mağazadan geldi mi (fiyatlar hazır, satın alma yapılabilir).</summary>
    public bool IsReady { get; private set; }
    /// <summary>Ürünler geldiğinde tetiklenir — mağaza ekranı fiyatları tazelemek için dinleyebilir.</summary>
    public event Action OnReady;

    IStoreController controller;
    IExtensionProvider extensions;
    readonly Dictionary<string, Action<bool>> pending = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        if (Instance == null) new GameObject("IapService").AddComponent<IapService>();
    }

    async void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this; DontDestroyOnLoad(gameObject);
        try
        {
            if (UnityServices.State != ServicesInitializationState.Initialized)
                await UnityServices.InitializeAsync();

            var builder = ConfigurationBuilder.Instance(StandardPurchasingModule.Instance());
            foreach (var p in Packs)
                builder.AddProduct(p.id, p.noAds ? ProductType.NonConsumable : ProductType.Consumable);
            UnityPurchasing.Initialize(this, builder);
        }
        catch (Exception e) { Debug.LogWarning($"[IAP] Init başarısız (çevrimdışı olabilir): {e.Message}"); }
    }

    // ── IStoreListener ──
    public void OnInitialized(IStoreController c, IExtensionProvider e)
    {
        controller = c; extensions = e; IsReady = true;
        if (Owns(NoAdsId)) PlayerProfile.NoAds = true;   // Reklamsız sahibi → geri yükle (Android otomatik)
        Debug.Log($"[IAP] Hazır ({controller.products.all.Length} ürün).");
        OnReady?.Invoke();
    }

    public void OnInitializeFailed(InitializationFailureReason error) => OnInitializeFailed(error, null);
    public void OnInitializeFailed(InitializationFailureReason error, string message)
        => Debug.LogWarning($"[IAP] Init başarısız: {error} {message}");

    public PurchaseProcessingResult ProcessPurchase(PurchaseEventArgs args)
    {
        var id = args.purchasedProduct.definition.id;
        Grant(id);
        if (pending.TryGetValue(id, out var cb)) { pending.Remove(id); cb?.Invoke(true); }
        return PurchaseProcessingResult.Complete;
    }

    public void OnPurchaseFailed(Product product, PurchaseFailureReason reason)
    {
        var id = product?.definition.id;
        Debug.LogWarning($"[IAP] Satın alma başarısız: {id} — {reason}");
        if (id != null && pending.TryGetValue(id, out var cb)) { pending.Remove(id); cb?.Invoke(false); }
    }

    // IDetailedStoreListener — ayrıntılı hata (mesaj dahil); non-deprecated Initialize bu arayüzü ister.
    public void OnPurchaseFailed(Product product, PurchaseFailureDescription failure)
    {
        var id = product?.definition.id;
        Debug.LogWarning($"[IAP] Satın alma başarısız: {id} — {failure.reason} ({failure.message})");
        if (id != null && pending.TryGetValue(id, out var cb)) { pending.Remove(id); cb?.Invoke(false); }
    }

    // ── Public API ──
    /// <summary>Mağazadan yerelleştirilmiş fiyat (ör. "₺9,99"); ürün gelmemişse null.</summary>
    public string GetPrice(string id)
    {
        var p = controller?.products.WithID(id);
        return p != null && p.availableToPurchase ? p.metadata.localizedPriceString : null;
    }

    /// <summary>Ürüne sahip mi (non-consumable makbuzu var).</summary>
    public bool Owns(string id)
    {
        var p = controller?.products.WithID(id);
        return p != null && p.hasReceipt;
    }

    public void Buy(string id, Action<bool> onDone)
    {
        if (controller == null || !IsReady) { Debug.LogWarning("[IAP] Hazır değil — satın alma atlandı."); onDone?.Invoke(false); return; }
        pending[id] = onDone;
        controller.InitiatePurchase(id);
    }

    public void Restore(Action<bool> onDone)
    {
        if (extensions == null) { onDone?.Invoke(false); return; }
#if UNITY_IOS || UNITY_TVOS || UNITY_STANDALONE_OSX
        extensions.GetExtension<IAppleExtensions>().RestoreTransactions((ok, err) =>
        {
            if (ok && Owns(NoAdsId)) PlayerProfile.NoAds = true;
            onDone?.Invoke(ok);
        });
#else
        if (Owns(NoAdsId)) PlayerProfile.NoAds = true;   // Android: non-consumable init'te otomatik restore edilir
        onDone?.Invoke(true);
#endif
    }

    // Ürün ID'ye göre ödülü ver + buluta yaz. Tek yer → pending/restore/relaunch hepsi doğru.
    void Grant(string id)
    {
        Pack pack = default; bool found = false;
        foreach (var p in Packs) if (p.id == id) { pack = p; found = true; break; }
        if (!found) { Debug.LogWarning($"[IAP] Bilinmeyen ürün: {id}"); return; }
        if (pack.noAds) PlayerProfile.NoAds = true;
        if (pack.coins > 0) PlayerProfile.AddCoins(pack.coins);
        if (pack.powers != null) foreach (var (t, a) in pack.powers) PowerUpInventory.Add(t, a);
        CloudSyncService.Instance?.FlushNow();
        Debug.Log($"[IAP] Verildi: {id} (+{pack.coins} coin, noAds={pack.noAds})");
    }
}
