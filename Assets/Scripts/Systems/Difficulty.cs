using UnityEngine;

/// <summary>Oyun zorluk seviyesi. Normal = oyunun mevcut hali.</summary>
public enum Difficulty { Easy = 0, Normal = 1, Hard = 2 }

/// <summary>
/// Zorluk seviyesi ayarı (PlayerPrefs) + oynanışa uygulanan çarpanlar (2026-08-06 kullanıcı):
///  • Easy : tüm level SÜRELERİ ×2, delik BÜYÜMESİ (yutulan başına) ×3.
///  • Normal: mevcut oyun (çarpan yok).
///  • Hard : sahnede HİÇ güç-up doğmaz (oyuncu yalnız kendi envanterini kullanır).
/// </summary>
public static class DifficultySettings
{
    const string KEY = "Difficulty";

    public static Difficulty Current
    {
        get => (Difficulty)Mathf.Clamp(PlayerPrefs.GetInt(KEY, (int)Difficulty.Normal), 0, 2);
        set { PlayerPrefs.SetInt(KEY, (int)value); PlayerPrefs.Save(); }
    }

    /// <summary>Level süresi çarpanı (Easy ×2).</summary>
    public static float TimeMultiplier => Current == Difficulty.Easy ? 2f : 1f;

    /// <summary>Delik büyüme çarpanı — yutulan nesne başına (Easy ×3).</summary>
    public static float GrowthMultiplier => Current == Difficulty.Easy ? 3f : 1f;

    /// <summary>Sahnede güç-up nesneleri doğsun mu? (Hard'da HAYIR.)</summary>
    public static bool PowerUpsSpawnInScene => Current != Difficulty.Hard;

    public static string Label(Difficulty d) => d switch
    {
        Difficulty.Easy => "Kolay",
        Difficulty.Hard => "Zor",
        _ => "Normal",
    };
}
