using UnityEditor;
using UnityEngine;

/// <summary>
/// TEST yardımcıları (2026-08-17): yıldız→güç-up ödül animasyonunu tekrar görebilmek için ödül sayaçlarını sıfırlar
/// (bir sonraki başarılı bölümde hak edilen tüm güç-up'lar yeniden verilir → uçuş animasyonu yeniden oynar).
/// </summary>
public static class StarRewardDebug
{
    [MenuItem("Tools/GET_IT/Reset Star Rewards (test)")]
    public static void ResetRewards()
    {
        foreach (PowerUpType t in System.Enum.GetValues(typeof(PowerUpType)))
            PlayerPrefs.DeleteKey($"StarGift_{t}_Granted");
        PlayerPrefs.Save();
        Debug.Log("[StarRewardDebug] Ödül sayaçları sıfırlandı — sonraki başarıda hak edilen güç-up'lar yeniden verilecek (animasyon tekrar oynar).");
    }

    [MenuItem("Tools/GET_IT/Grant Test PowerUps (+3 each)")]
    public static void GrantTest()
    {
        PowerUpInventory.Add(PowerUpType.Speed, 3);
        PowerUpInventory.Add(PowerUpType.Magnet, 3);
        PowerUpInventory.Add(PowerUpType.SizeBurst, 3);
        PowerUpInventory.Add(PowerUpType.Super, 3);
        Debug.Log("[StarRewardDebug] +3 her güç-up envantere eklendi (oyun-içi HUD testi için).");
    }
}
