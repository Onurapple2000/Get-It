using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ASSET DELIVERY FAZ 2 — Tüm LevelData asset'lerinin runtime listesi (Resources/LevelCatalog.asset).
/// Ana menü, sahne YÜKLENMEDEN ÖNCE seçilen level'ın prefab ref'lerini bilmek zorunda (indirme + bellek yükleme
/// LevelManager.Awake'ten önce bitmeli); LevelManager sahnede olduğu için menüden erişilemiyordu → bu katalog.
/// Editör aracı Tools/GET_IT/Asset Delivery/Rebuild Level Catalog üretir; her player build'de otomatik yenilenir.
/// LevelData asset'leri KÜÇÜKTÜR (prefab'lara sert bağ yok) → Resources'ta durmaları boyut yaratmaz.
/// </summary>
public class LevelCatalog : ScriptableObject
{
    public const string ResourceName = "LevelCatalog";
    public List<LevelData> all = new List<LevelData>();

    static LevelCatalog _inst; static bool _tried;
    public static LevelCatalog Instance
    {
        get
        {
            if (_inst == null && !_tried) { _tried = true; _inst = Resources.Load<LevelCatalog>(ResourceName); }
            return _inst;
        }
    }

    /// <summary>worldId + levelIndex → LevelData (yoksa null). LevelManager ile aynı asset nesnesi.</summary>
    public static LevelData Get(int worldId, int levelIndex)
    {
        var c = Instance;
        if (c == null) return null;
        for (int i = 0; i < c.all.Count; i++)
        {
            var l = c.all[i];
            if (l != null && l.worldId == worldId && l.levelIndex == levelIndex) return l;
        }
        return null;
    }

    /// <summary>Bir dünyanın tüm levelları (sıralı değil; boş liste olabilir).</summary>
    public static List<LevelData> World(int worldId)
    {
        var res = new List<LevelData>();
        var c = Instance;
        if (c == null) return res;
        foreach (var l in c.all) if (l != null && l.worldId == worldId) res.Add(l);
        return res;
    }
}
