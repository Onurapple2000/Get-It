using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;

/// <summary>
/// Bir level'ın veri tanımı (Sprint 1 ikinci faz). LevelManager bunu okur:
/// süreyi belirler, spawn listesinden nesneleri üretir, hedefleri ObjectiveTracker'a verir,
/// (varsa) bombaları kurar. Asset'ler Tools/GET_IT/Create Sample Levels ile üretilir.
/// </summary>
[CreateAssetMenu(fileName = "Level", menuName = "GET_IT/Level Data")]
public class LevelData : ScriptableObject
{
    [Header("Kimlik")]
    public int worldId = 0;
    public int levelIndex = 0;

    [Header("Süre")]
    public float levelTime = 60f;

    [Header("Spawn — sahneye üretilecek nesneler")]
    public List<SpawnEntry> spawns = new List<SpawnEntry>();

    [Header("Hedefler — kazanmak için yutulması gerekenler")]
    public List<ObjectiveEntry> objectives = new List<ObjectiveEntry>();

    [Header("Bomba (opsiyonel)")]
    public bool bombsEnabled = false;
    public GameObject bombPrefab;
    public int bombCount = 1;

    /// <summary>Zor level mi? Her 5 levelda 1 (5., 10., 15.). Süre yarıya iner, bombalar gizli yerleşir,
    /// başlangıçta "HARD LEVEL" intro'su çıkar. (2026-07-24: %10→%5, dünya başına 15 level.)</summary>
    public bool IsHard => (levelIndex + 1) % 5 == 0;

    [System.Serializable]
    public class SpawnEntry
    {
        // ── ASSET DELIVERY (FAZ 2, 2026-09-15) ──
        // Prefab artık DOĞRUDAN referans DEĞİL: AssetReference (GUID) → build'e sert bağ girmez, dünya içerikleri
        // Play Asset Delivery paketlerinden on-demand iner. `prefab` PROPERTY olarak KALDI ki 3600 satırlık üretim
        // kodu (LevelManager/LandmarkBuilder) ve editör araçları (`s.prefab = x`) HİÇ DEĞİŞMEDEN çalışsın.
        //  • Runtime: WorldContentLoader sahne yüklenmeden ÖNCE tüm ref'leri yükler ve cache'i doldurur (senkron Awake
        //    zinciri bozulmaz). Cache boşsa null döner → LevelManager o girdiyi atlar (ve hata loglar).
        //  • Editör: cache boşsa AssetDatabase'den çözer → preview/creator araçları aynen çalışır.
        //  • Setter (editör araçları): cache'i doldurur + GUID'i prefabRef'e yazar (asset'e kaydedilir).
        [SerializeField] AssetReferenceGameObject prefabRef;
        [System.NonSerialized] GameObject _prefab;

        public GameObject prefab
        {
            get
            {
                if (_prefab != null) return _prefab;
                _prefab = WorldContentLoader.Resolve(PrefabGuid);   // runtime: yüklenmiş global cache (aynı GUID, başka instance)
                if (_prefab != null) return _prefab;
#if UNITY_EDITOR
                if (prefabRef != null && !string.IsNullOrEmpty(prefabRef.AssetGUID))
                    _prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(UnityEditor.AssetDatabase.GUIDToAssetPath(prefabRef.AssetGUID));
#endif
                return _prefab;
            }
            set
            {
                _prefab = value;
#if UNITY_EDITOR
                string guid = value != null ? UnityEditor.AssetDatabase.AssetPathToGUID(UnityEditor.AssetDatabase.GetAssetPath(value)) : "";
                prefabRef = string.IsNullOrEmpty(guid) ? null : new AssetReferenceGameObject(guid);
#endif
            }
        }

        /// <summary>Addressables referansı (GUID). Boşsa girdi geçersiz.</summary>
        public AssetReferenceGameObject PrefabRef => prefabRef;
        public string PrefabGuid => prefabRef != null ? prefabRef.AssetGUID : null;
        public bool HasRef => !string.IsNullOrEmpty(PrefabGuid);
        /// <summary>WorldContentLoader: yüklenen prefab'ı cache'e koyar (asset'i değiştirmez).</summary>
        public void SetResolved(GameObject go) { _prefab = go; }

        [Tooltip("Kaç yerleşim noktası (yığın). Toplam adet = count × stack.")]
        public int count = 4;
        [Tooltip("Her noktada üst üste kaç adet (görsel yığın). 1 = tekli.")]
        public int stack = 1;
        [Tooltip("Boyut çarpanı (aynı nesnenin küçük/büyük varyantı için). 1 = orijinal.")]
        public float scale = 1f;
    }

    [System.Serializable]
    public class ObjectiveEntry
    {
        [Tooltip("PhysicsSwallowable.ResolvedType ile eşleşen tür (ör. FlowerPot).")]
        public string objectType = "FlowerPot";
        public int required = 3;
        [Tooltip("Opsiyonel HUD ikonu. Boşsa runtime'da nesne thumbnail'ı üretilir.")]
        public Sprite icon;
    }
}
