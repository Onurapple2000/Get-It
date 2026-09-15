using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
#if UNITY_ANDROID
using UnityEngine.Android;
#endif

/// <summary>
/// ASSET DELIVERY FAZ 2 (2026-09-15) — Dünya içeriklerini (prefab'lar) Play Asset Delivery paketlerinden indirir ve
/// SAHNE YÜKLENMEDEN ÖNCE belleğe alır. Amaç: LevelManager.Awake'in SENKRON prefab zinciri hiç değişmesin —
/// sahne açıldığında `SpawnEntry.prefab` cache'ten anında döner.
///
/// Akış (her sahne yükleme noktası bunu çağırır — MainMenu Play, GameManager Next/Retry, PauseMenu Retry):
///   Prepare(world, index) → [Android] gereken asset pack(ler) inik değilse indir (ilerleme + Wi-Fi bekleme/mobil veri izni)
///                         → Addressables ile o level'ın benzersiz prefab'larını yükle (GUID anahtarı) → cache
///                         → artık gerekmeyen önceki handle'ları serbest bırak (bellek)
///   PrefetchAhead(world)  → sıradaki PrefetchAhead dünyanın paketlerini arka planda indir (oyuncu beklemesin).
///
/// Paket adı = Addressables grup adı (Google kuralı: harf/rakam/alt çizgi) — WorldPacks tablosu tek kaynak;
/// AddressableWorldSetup (editör) da aynı tabloyu kullanır. Karma (17) tüm dünyaları kullanır → tüm paketler.
/// Editörde/Android dışında paket adımı atlanır; Addressables "Use Asset Database" modunda yükler.
/// </summary>
public static class WorldContentLoader
{
    /// <summary>Oynanan dünyadan sonra kaç dünya önden indirilsin (kullanıcı 2026-09-15: "3'er 3'er" hissi → 2 önde).</summary>
    public const int PrefetchAheadCount = 2;

    // ── durum ──
    static readonly Dictionary<string, AsyncOperationHandle<GameObject>> handles = new Dictionary<string, AsyncOperationHandle<GameObject>>();
    static readonly Dictionary<string, GameObject> resolved = new Dictionary<string, GameObject>();
    static readonly HashSet<string> packsKnownInstalled = new HashSet<string>();
    static readonly HashSet<string> packsDownloading = new HashSet<string>();
    public static string LastError { get; private set; }
    /// <summary>Asset pack API'si yalnız GERÇEK Android cihazda (editörde Android platformu seçiliyken derlenir ama çağrılmaz).</summary>
    static bool OnDevice => Application.platform == RuntimePlatform.Android;

    /// <summary>LevelData getter'ı için: GUID → yüklü prefab (yoksa null).</summary>
    public static GameObject Resolve(string guid)
    {
        if (string.IsNullOrEmpty(guid)) return null;
        return resolved.TryGetValue(guid, out var go) ? go : null;
    }

    /// <summary>Bu level'ın tüm prefab'ları bellekte mi? (LevelManager.Awake güvenlik kontrolü.)</summary>
    public static bool IsReady(LevelData ld)
    {
        if (ld == null) return false;
        foreach (var s in ld.spawns)
            if (s.HasRef && s.prefab == null) return false;
        return true;
    }

    // ═══════════════ PUBLIC: hazırla + devam et ═══════════════

    /// <summary>
    /// world/index level'ını hazırlar, bitince onDone(ok). İlerleme: progress(label, 0..1). Kalıcı koşucuda çalışır
    /// (çağıranın StopAllCoroutines'i kesmez). Hata → LastError dolu, onDone(false).
    /// </summary>
    public static void Prepare(int world, int index, Action<string, float> progress, Action<bool> onDone)
    {
        Runner.Instance.StartCoroutine(PrepareRoutine(world, index, progress, onDone));
    }

    /// <summary>Sıradaki PrefetchAhead dünyanın paketlerini arka planda indirir (Android dışı: no-op).</summary>
    public static void PrefetchAhead(int world)
    {
#if UNITY_ANDROID
        if (!OnDevice) return;
        var names = new List<string>();
        int w = world;
        for (int k = 0; k < PrefetchAheadCount; k++)
        {
            w = WorldCatalog.NextWorld(w);
            if (w < 0) break;
            foreach (var p in WorldPacks.PacksFor(w)) if (!IsInstalled(p) && !packsDownloading.Contains(p)) names.Add(p);
        }
        if (names.Count == 0) return;
        Runner.Instance.StartCoroutine(PrefetchRoutine(names));
#endif
    }

    // ═══════════════ iç akış ═══════════════

#if UNITY_ANDROID
    static IEnumerator PrefetchRoutine(List<string> names)
    {
        List<string> need = null;
        yield return QueryNeedsDownload(names, r => need = r);
        if (need == null || need.Count == 0) yield break;
        foreach (var n in need) packsDownloading.Add(n);
        Debug.Log("[WorldContent] Ön-indirme: " + string.Join(", ", need));
        try
        {
            AndroidAssetPacks.DownloadAssetPackAsync(need.ToArray(), info =>
            {
                if (info == null) return;
                if (info.status == AndroidAssetPackStatus.Completed) { packsKnownInstalled.Add(info.name); packsDownloading.Remove(info.name); }
                else if (info.status == AndroidAssetPackStatus.Failed || info.status == AndroidAssetPackStatus.Canceled) packsDownloading.Remove(info.name);
                // WaitingForWifi: sessiz ön-indirmede mobil veri SORMAYIZ; oyuncu dünyaya gelince Prepare sorar.
            });
        }
        catch (Exception e) { Debug.LogWarning("[WorldContent] Ön-indirme başlatılamadı: " + e.Message); foreach (var n in need) packsDownloading.Remove(n); }
    }
#endif

    /// <summary>Test/manuel sürüş için açık; normalde Prepare() kullan.</summary>
    public static IEnumerator PrepareRoutine(int world, int index, Action<string, float> progress, Action<bool> onDone)
    {
        LastError = null;
        var ld = LevelCatalog.Get(world, index);
        if (ld == null)
        {
            // Katalogda yoksa (eski davranış): sahne kendi referanslarıyla açılır; editörde AssetDatabase fallback var.
            Debug.LogWarning($"[WorldContent] Katalogda level yok: dünya {world} / {index} — hazırlık atlandı.");
            onDone?.Invoke(true); yield break;
        }

        // 1) Gereken paketler (Android). Durum sorgusu: paket bu build'de YOKSA (APK/USB testi → Unknown/Failed) ya da
        //    PlayCore yoksa indirme adımı ATLANIR; yükleme Addressables'a bırakılır (StreamingAssets'ten gelir).
#if UNITY_ANDROID
        var maybe = new List<string>();
        if (OnDevice) foreach (var p in WorldPacks.PacksFor(world)) if (!IsInstalled(p)) maybe.Add(p);
        if (maybe.Count > 0)
        {
            List<string> need = null;
            yield return QueryNeedsDownload(maybe, r => need = r);
            if (need != null && need.Count > 0)
            {
                bool ok = false;
                yield return DownloadPacks(need, progress, r => ok = r);
                if (!ok) { onDone?.Invoke(false); yield break; }
            }
        }
#endif

        // 2) Prefab'ları belleğe al (benzersiz GUID'ler)
        var guids = new List<string>();
        foreach (var s in ld.spawns) if (s.HasRef && !guids.Contains(s.PrefabGuid)) guids.Add(s.PrefabGuid);

        // ⚠️ BUG FIX (2026-09-16): önceki bir Prepare yarıda başarısız olunca (paket hatası) aynı anda yüklenmekte olan
        // handle'lar (ör. Core PowerUp'lar) `handles`'ta kalıyor ama `resolved`'a hiç yazılmıyordu; sonraki dünyada "zaten
        // var" diye atlanıp NULL kalıyordu (Binalar L1 "YÜKLÜ DEĞİL"). Artık mevcut handle'ın sonucu alınır / beklenir.
        var pending = new List<(string guid, AsyncOperationHandle<GameObject> h)>();
        foreach (var g in guids)
        {
            if (resolved.TryGetValue(g, out var have) && have != null) continue;   // gerçekten hazır
            if (handles.TryGetValue(g, out var old))
            {
                if (old.IsValid() && (!old.IsDone || old.Status == AsyncOperationStatus.Succeeded)) { pending.Add((g, old)); continue; }
                if (old.IsValid()) Addressables.Release(old);   // başarısız/geçersiz kalıntı → temizle ve yeniden yükle
                handles.Remove(g);
            }
            AsyncOperationHandle<GameObject> h;
            try { h = Addressables.LoadAssetAsync<GameObject>(g); }
            catch (Exception e) { LastError = e.Message; Debug.LogError("[WorldContent] LoadAssetAsync hata: " + e); onDone?.Invoke(false); yield break; }
            handles[g] = h; pending.Add((g, h));
        }
        string lbl = Loc.T("loading");
        bool anyFail = false; string failMsg = null;
        for (int i = 0; i < pending.Count; i++)
        {
            var (g, h) = pending[i];
            while (h.IsValid() && !h.IsDone) { progress?.Invoke(lbl, (i + h.PercentComplete) / Mathf.Max(1, pending.Count)); yield return null; }
            if (!h.IsValid() || h.Status != AsyncOperationStatus.Succeeded || h.Result == null)
            {
                if (!anyFail) { anyFail = true; failMsg = h.IsValid() ? h.OperationException?.Message : "invalid handle"; }
                if (h.IsValid()) Addressables.Release(h);
                handles.Remove(g); resolved.Remove(g);
                continue;   // diğerlerini de bekle → temiz durum bırak, sonraki denemede yalnız eksikler yüklenir
            }
            resolved[g] = h.Result;
        }
        if (anyFail)
        {
            LastError = "load";
            Debug.LogError($"[WorldContent] Prefab yüklenemedi (dünya {world} L{index + 1}): {failMsg}");
            onDone?.Invoke(false); yield break;
        }
        foreach (var s in ld.spawns) if (s.HasRef && resolved.TryGetValue(s.PrefabGuid, out var go)) s.SetResolved(go);
        if (!IsReady(ld))
        {
            LastError = "notready";
            Debug.LogError($"[WorldContent] Hazırlık bitti ama level hazır değil (dünya {world} L{index + 1}) — eksik ref var.");
            onDone?.Invoke(false); yield break;
        }
        Debug.Log($"[WorldContent] Hazır: dünya {world} L{index + 1}, {guids.Count} prefab ({pending.Count} yeni yüklendi)");

        // 3) Artık gerekmeyenleri bırak (aynı level'ı tekrar → hepsi gerekli, hiçbiri bırakılmaz)
        var drop = new List<string>();
        foreach (var kv in handles) if (!guids.Contains(kv.Key)) drop.Add(kv.Key);
        foreach (var g in drop) { Addressables.Release(handles[g]); handles.Remove(g); resolved.Remove(g); }

        progress?.Invoke(lbl, 1f);
        onDone?.Invoke(true);
    }

#if UNITY_ANDROID
    static bool IsInstalled(string pack)
    {
        if (packsKnownInstalled.Contains(pack)) return true;
        string path = null;
        try { path = AndroidAssetPacks.GetAssetPackPath(pack); } catch (Exception e) { Debug.LogWarning("[WorldContent] GetAssetPackPath: " + e.Message); }
        Debug.Log($"[WorldContent] Paket {pack}: yol='{path}'");
        if (!string.IsNullOrEmpty(path)) { packsKnownInstalled.Add(pack); return true; }
        return false;
    }

    /// <summary>
    /// Paket durumlarını sorgular; yalnız gerçekten İNDİRİLEBİLİR olanları (NotInstalled/Pending/Downloading/WaitingForWifi)
    /// döndürür. Unknown/Failed (paket bu build'de yok — APK) ve istisna (PlayCore yok) → boş liste (indirme atlanır).
    /// </summary>
    static IEnumerator QueryNeedsDownload(List<string> names, Action<List<string>> done)
    {
        var res = new List<string>();
        bool fin = false;
        try
        {
            AndroidAssetPacks.GetAssetPackStateAsync(names.ToArray(), (size, states) =>
            {
                if (states != null)
                    foreach (var st in states)
                    {
                        if (st == null) continue;
                        Debug.Log($"[WorldContent] Paket {st.name} durumu: {st.status} (hata: {st.error})");
                        switch (st.status)
                        {
                            case AndroidAssetPackStatus.Completed: packsKnownInstalled.Add(st.name); break;
                            case AndroidAssetPackStatus.NotInstalled:
                            case AndroidAssetPackStatus.Pending:
                            case AndroidAssetPackStatus.Downloading:
                            case AndroidAssetPackStatus.Transferring:
                            case AndroidAssetPackStatus.WaitingForWifi: res.Add(st.name); break;
                            default: Debug.LogWarning($"[WorldContent] Paket '{st.name}' durumu {st.status} — indirme atlandı (APK build?)"); break;
                        }
                    }
                fin = true;
            });
        }
        catch (Exception e) { Debug.LogWarning("[WorldContent] Asset pack API yok (PlayCore?): " + e.Message); fin = true; }
        float t0 = Time.realtimeSinceStartup;
        while (!fin && Time.realtimeSinceStartup - t0 < 20f) yield return null;
        done(res);
    }

    static IEnumerator DownloadPacks(List<string> names, Action<string, float> progress, Action<bool> done)
    {
        string lbl = Loc.T("downloading");
        var status = new Dictionary<string, AndroidAssetPackStatus>();
        var prog = new Dictionary<string, float>();
        bool askedMobile = false, mobileDenied = false, failed = false;
        foreach (var n in names) { status[n] = AndroidAssetPackStatus.Pending; prog[n] = 0f; packsDownloading.Add(n); }
        Debug.Log("[WorldContent] İndiriliyor: " + string.Join(", ", names));

        try { AndroidAssetPacks.DownloadAssetPackAsync(names.ToArray(), info =>
        {
            if (info == null) return;
            status[info.name] = info.status;
            if (info.size > 0) prog[info.name] = Mathf.Clamp01((float)info.bytesDownloaded / info.size);
            if (info.status == AndroidAssetPackStatus.Completed) { prog[info.name] = 1f; packsKnownInstalled.Add(info.name); packsDownloading.Remove(info.name); }
            if (info.status == AndroidAssetPackStatus.Failed || info.status == AndroidAssetPackStatus.Canceled) { failed = true; packsDownloading.Remove(info.name); }
        }); }
        catch (Exception e) { LastError = e.Message; Debug.LogError("[WorldContent] DownloadAssetPackAsync: " + e); done(false); yield break; }

        float t0 = Time.realtimeSinceStartup;
        while (true)
        {
            bool all = true, waitingWifi = false;
            float sum = 0f;
            foreach (var n in names)
            {
                sum += prog[n];
                if (status[n] != AndroidAssetPackStatus.Completed) all = false;
                if (status[n] == AndroidAssetPackStatus.WaitingForWifi) waitingWifi = true;
            }
            progress?.Invoke(lbl, sum / names.Count);
            if (all) break;
            if (failed) { LastError = "download"; done(false); yield break; }
            if (waitingWifi && !askedMobile)
            {
                askedMobile = true;   // Wi-Fi yok → mobil veri izni sor (sistem diyaloğu); reddederse hata
                AndroidAssetPacks.RequestToUseMobileDataAsync(r => { if (r == null || !r.allowed) mobileDenied = true; });
            }
            if (mobileDenied) { LastError = "wifi"; done(false); yield break; }
            if (Time.realtimeSinceStartup - t0 > 900f) { LastError = "timeout"; done(false); yield break; }   // 15 dk güvenlik
            yield return null;
        }
        done(true);
    }
#endif

    // Kalıcı coroutine koşucusu (sahne geçişlerinde yaşar; GameManager.StopAllCoroutines etkilemez).
    class Runner : MonoBehaviour
    {
        static Runner _i;
        public static Runner Instance
        {
            get
            {
                if (_i == null)
                {
                    var go = new GameObject("_WorldContentLoader");
                    if (Application.isPlaying) DontDestroyOnLoad(go); else go.hideFlags = HideFlags.HideAndDontSave;   // EditMode test
                    _i = go.AddComponent<Runner>();
                }
                return _i;
            }
        }
    }
}

/// <summary>worldId → asset pack (Addressables grup) adları. Editör kurulumu (AddressableWorldSetup) ile TEK kaynak.</summary>
public static class WorldPacks
{
    public const string Core = "Core";

    public static readonly (int world, string pack, string prefabFolder)[] Table =
    {
        (0,  "World0_Park",      "Assets/Prefabs/DecoObjects"),
        (1,  "World1_Foods",     "Assets/Prefabs/Foods"),
        (2,  "World2_Cars",      "Assets/Prefabs/Cars"),
        (3,  "World3_Buildings", "Assets/Prefabs/Buildings"),
        (4,  "World4_Sweets",    "Assets/Prefabs/Sweets"),
        (5,  "World5_Drinks",    "Assets/Prefabs/Drinks"),
        (6,  "World6_Gifts",     "Assets/Prefabs/Gifts"),
        (7,  "World7_Books",     "Assets/Prefabs/Books"),
        (9,  "World9_Cats",      "Assets/Prefabs/Cats"),
        (10, "World10_Dogs",     "Assets/Prefabs/Dogs"),
        (11, "World11_Ships",    "Assets/Prefabs/Ships"),
        (12, "World12_Planes",   "Assets/Prefabs/Planes"),
        (13, "World13_Treasure", "Assets/Prefabs/Money"),
        (15, "World15_Jewelry",  "Assets/Prefabs/Jewelry"),
    };

    /// <summary>İlk indirmede (install-time) gelecek dünyalar: sıradaki ilk dünya. Gerisi on-demand.</summary>
    public static bool IsInstallTime(int world) => world == WorldCatalog.FirstWorld;

    /// <summary>
    /// Bu dünyanın İNDİRİLMESİ gereken (on-demand) paketleri. Karma(17) = tüm on-demand paketler. Install-time
    /// dünyalar (ilk dünya) burada YOK: PAD paketi tüm install-time grupları tek "AddressablesAssetPack"e koyar,
    /// ayrı paket adı olmaz → indirme sorgusu yapılmaz (zaten kuruludur).
    /// </summary>
    public static List<string> PacksFor(int world)
    {
        var res = new List<string>();
        foreach (var t in Table)
            if ((world == 17 || t.world == world) && !IsInstallTime(t.world)) res.Add(t.pack);
        return res;
    }
}
