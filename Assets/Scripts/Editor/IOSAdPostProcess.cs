#if UNITY_IOS
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
using System.IO;

/// <summary>
/// Sprint 8 — iOS build sonrası Info.plist'e AdMob/ATT anahtarlarını ekler.
/// - <c>NSUserTrackingUsageDescription</c>: App Tracking Transparency (ATT) izin metni.
///   (GADApplicationIdentifier ve SKAdNetworkItems'ı Google Mobile Ads plugin'i kendi
///    post-processor'ı ile ekler; App ID'yi Assets → Google Mobile Ads → Settings'ten gir.)
/// SDK gerektirmez — yalnız iOS Xcode API'si kullanır.
/// </summary>
public static class IOSAdPostProcess
{
    [PostProcessBuild(100)]
    public static void OnPostprocessBuild(BuildTarget target, string pathToBuiltProject)
    {
        if (target != BuildTarget.iOS) return;

        string plistPath = Path.Combine(pathToBuiltProject, "Info.plist");
        var plist = new PlistDocument();
        plist.ReadFromFile(plistPath);

        // ATT izin açıklaması — reklamların ilgi alanına göre kişiselleştirilmesi için.
        plist.root.SetString("NSUserTrackingUsageDescription",
            "Bu izin, sana daha uygun reklamlar göstermek için kullanılır.");

        plist.WriteToFile(plistPath);
    }
}
#endif
