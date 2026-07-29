using System.Collections.Generic;
using UnityEngine;

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
        public GameObject prefab;
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
