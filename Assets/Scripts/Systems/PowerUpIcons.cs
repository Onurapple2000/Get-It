using System.Collections.Generic;
using UnityEngine;

/// <summary>Güç-up PNG ikonlarını (Resources; PowerUpIconMaker üretir) yükleyip önbelleğe alır. HUD + success ekranı ortak kullanır.</summary>
public static class PowerUpIcons
{
    static readonly Dictionary<PowerUpType, Sprite> cache = new();

    public static Sprite Get(PowerUpType t)
    {
        if (cache.TryGetValue(t, out var s) && s) return s;
        string name = t switch
        {
            PowerUpType.Speed     => "power_speed",
            PowerUpType.Magnet    => "power_magnet",
            PowerUpType.SizeBurst => "power_grow",
            PowerUpType.Super     => "super_powerup",
            _ => null,
        };
        if (name == null) return null;
        var tex = Resources.Load<Texture2D>(name);
        s = tex == null ? null : Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
        cache[t] = s;
        return s;
    }
}
