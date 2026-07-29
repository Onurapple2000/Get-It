#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Can (Lives) sistemini test etmek için editör yardımcıları. Yıldız hediyesi "sınırsız can" (UnlimitedUntil)
/// veya birikmiş canlar test'i maskeleyebilir → bu menülerle sıfırla. (PlayerPrefs anahtarları LivesManager ile aynı.)
/// </summary>
public static class LivesDebug
{
    const string KEY_LIVES  = "Lives_Count";
    const string KEY_ANCHOR = "Lives_AnchorTicks";
    const string KEY_UNLIMITED = "Lives_UnlimitedUntilTicks";
    const string KEY_STARGIFTS = "StarGiftsClaimed";

    [MenuItem("Tools/GET_IT/Lives — Sınırsız Can'ı Temizle")]
    public static void ClearUnlimited()
    {
        PlayerPrefs.DeleteKey(KEY_UNLIMITED);
        PlayerPrefs.Save();
        Debug.Log("[LivesDebug] Sınırsız can temizlendi. (Artık fail'de can eksilir + bitince engellenir.)");
    }

    [MenuItem("Tools/GET_IT/Lives — Canı 1'e Ayarla (bitmeye yakın test)")]
    public static void SetOneLife()
    {
        PlayerPrefs.DeleteKey(KEY_UNLIMITED);
        PlayerPrefs.SetInt(KEY_LIVES, 1);
        PlayerPrefs.SetString(KEY_ANCHOR, System.DateTime.UtcNow.Ticks.ToString());
        PlayerPrefs.Save();
        Debug.Log("[LivesDebug] Can=1, sınırsız temizlendi. 1 kez fail → 0 → engellenmeli.");
    }

    [MenuItem("Tools/GET_IT/Lives — Tam Sıfırla (5 can, hediye sayacı 0)")]
    public static void ResetFull()
    {
        PlayerPrefs.DeleteKey(KEY_UNLIMITED);
        PlayerPrefs.SetInt(KEY_LIVES, 5);
        PlayerPrefs.SetString(KEY_ANCHOR, System.DateTime.UtcNow.Ticks.ToString());
        PlayerPrefs.SetInt(KEY_STARGIFTS, 0);
        PlayerPrefs.Save();
        Debug.Log("[LivesDebug] Canlar 5'e sıfırlandı, sınırsız temizlendi, yıldız-hediye sayacı 0.");
    }
}
#endif
