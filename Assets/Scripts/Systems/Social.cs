using UnityEngine;

/// <summary>
/// Arkadaşa tavsiye (paylaş) + değerlendir. Paylaş: metni panoya kopyalar ve Android'de yerel PAYLAŞ sayfasını
/// (WhatsApp vb. uygulama seçimi telefonun kendisinde) açar. Editör/diğer platformlarda panoya kopyalama yeterli.
/// </summary>
public static class Social
{
    // TODO: gerçek mağaza linki (placeholder).
    public const string GameUrl = "https://play.google.com/store/apps/details?id=com.getit.game";

    public static void ShareGame()
    {
        string msg = $"GET IT oyununu dene! {GameUrl}";
        GUIUtility.systemCopyBuffer = msg;   // her platformda pano yedeği (kopyalanır)
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            var intent = new AndroidJavaObject("android.content.Intent");
            intent.Call<AndroidJavaObject>("setAction", intent.GetStatic<string>("ACTION_SEND"));
            intent.Call<AndroidJavaObject>("setType", "text/plain");
            intent.Call<AndroidJavaObject>("putExtra", intent.GetStatic<string>("EXTRA_TEXT"), msg);
            var chooser = new AndroidJavaClass("android.content.Intent")
                .CallStatic<AndroidJavaObject>("createChooser", intent, "Paylaş");
            var activity = new AndroidJavaClass("com.unity3d.player.UnityPlayer")
                .GetStatic<AndroidJavaObject>("currentActivity");
            activity.Call("startActivity", chooser);
        }
        catch (System.Exception e) { Debug.LogWarning("[Social] paylaş hatası: " + e.Message); }
#else
        Debug.Log("[Social] Paylaşım metni panoya kopyalandı: " + msg);
#endif
    }

    public static void Rate() => Application.OpenURL(GameUrl);
}
