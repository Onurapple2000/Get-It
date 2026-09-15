using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Unity.Services.CloudSave;

/// <summary>
/// Sprint 10 — Yerel ilerlemeyi UGS Cloud Save ile senkronlar. Tek bir "save" anahtarında JSON tutar.
///
/// Akış: <see cref="AccountManager"/> giriş yapınca → buluttan çek → yerelle BİRLEŞTİR (SaveState.Merge,
/// ilerleme kaybetmez) → yerele uygula → buluta geri yaz. Bundan sonra kayıt noktalarında (level tamam/fail,
/// satın alma) <see cref="FlushNow"/> ile bulut güncellenir.
///
/// GÜVENLİK: Merge tamamlanmadan (<see cref="synced"/>) buluta YAZILMAZ — yeni kurulumda boş yerelin
/// buluttaki ilerlemeyi ezmesini önler. Çevrimdışıysa sessizce atlar, oyun yerelle çalışır.
/// </summary>
public class CloudSyncService : MonoBehaviour
{
    public static CloudSyncService Instance { get; private set; }
    const string KEY = "save";

    bool synced;       // ilk pull+merge tamamlandı mı (buluta yazmak için ön koşul)
    bool uploading;
    bool dirty;

    /// <summary>Buluttaki veri yerele uygulandığında tetiklenir — HUD/menü değerlerini yenilemek için.</summary>
    public event Action OnSynced;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        if (Instance == null)
            new GameObject("CloudSyncService").AddComponent<CloudSyncService>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // Aboneliği Start'ta yap: bu ana kadar TÜM Awake'ler (AccountManager dahil) çalışmış olur →
    // bootstrap sıra yarışı yok (Awake'te AccountManager.Instance null olabilirdi → senkron hiç çalışmazdı).
    void Start()
    {
        var am = AccountManager.Instance;
        if (am == null) { Debug.LogWarning("[CloudSync] AccountManager yok — senkron atlandı."); return; }
        am.OnSignedIn += HandleSignedIn;
        if (am.IsSignedIn) HandleSignedIn();   // zaten giriş yaptıysa olayı kaçırmayalım
    }

    void HandleSignedIn() => _ = PullMergePush();

    async Task PullMergePush()
    {
        synced = false;   // hesap değişiminde (link/restore) merge bitene kadar buluta YAZMA — boş yerelin eski kaydı ezmesini önler
        try
        {
            var cloud = await LoadCloud();
            var local = SaveState.CaptureLocal();
            var merged = SaveState.Merge(local, cloud);
            merged.ApplyLocal();
            await SaveCloud(merged);
            synced = true;
            Debug.Log("[CloudSync] Pull+merge+push tamam.");
            OnSynced?.Invoke();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[CloudSync] İlk senkron başarısız (çevrimdışı olabilir): {e.Message}");
        }
    }

    async Task<SaveState> LoadCloud()
    {
        var q = await CloudSaveService.Instance.Data.Player.LoadAsync(new HashSet<string> { KEY });
        if (q.TryGetValue(KEY, out var item) && item != null)
            return SaveState.FromJson(item.Value.GetAs<string>());
        return null;
    }

    async Task SaveCloud(SaveState s)
    {
        await CloudSaveService.Instance.Data.Player.SaveAsync(
            new Dictionary<string, object> { { KEY, s.ToJson() } });
    }

    /// <summary>Değişiklik oldu işaretle (arka plana geçişte/çıkışta yazılır).</summary>
    public void MarkDirty() => dirty = true;

    /// <summary>Yereli hemen buluta yaz (level tamam/fail, satın alma sonrası). Senkron değilse/çevrimdışıysa atlar.</summary>
    public async void FlushNow()
    {
        var am = AccountManager.Instance;
        if (!synced || uploading || am == null || !am.IsSignedIn) { dirty = true; return; }
        uploading = true; dirty = false;
        try { await SaveCloud(SaveState.CaptureLocal()); Debug.Log("[CloudSync] FlushNow yazdı."); }
        catch (Exception e) { Debug.LogWarning($"[CloudSync] Yükleme başarısız: {e.Message}"); dirty = true; }
        finally { uploading = false; }
    }

    void OnApplicationPause(bool paused)
    {
        if (paused && dirty) FlushNow();   // arka plana geçince bekleyen değişikliği kaydet
    }

    void OnApplicationQuit()
    {
        if (dirty) FlushNow();
    }
}
