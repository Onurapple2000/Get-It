using UnityEngine;

/// <summary>
/// Uygulama açılışında global çalışma ayarları. Unity mobilde VARSAYILAN 30 fps'e sınırlar; bunu 60'a çekeriz
/// → (a) baseline'da "30 tavan mı CPU limiti mi" ayırt edilir, (b) CPU yeterse akıcı 60 fps.
/// CPU yetmezse zaten kendiliğinden düşer (zarar yok).
/// </summary>
public static class AppBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Init()
    {
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = 60;
    }
}
