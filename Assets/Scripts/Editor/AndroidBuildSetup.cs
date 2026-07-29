using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

/// <summary>
/// Android'de (telefonda) test için gerekli Player/Build ayarlarını tek tıkla yapar.
/// Menu: Tools/GET_IT/Configure Android for Testing
///
/// NOT: Gerçek build almak için Unity Hub'dan "Android Build Support" (OpenJDK + SDK + NDK) modülü kurulu OLMALI.
/// Bu araç ayarları yapar; build'i File > Build Settings > Android > Switch Platform + Build And Run ile alırsın.
/// </summary>
public static class AndroidBuildSetup
{
    const string BUNDLE_ID = "com.getit.game";

    [MenuItem("Tools/GET_IT/Configure Android for Testing")]
    public static void Configure()
    {
        // 1) Bundle ID (boşsa build başarısız olur)
        PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, BUNDLE_ID);

        // 2) Frame Timing Stats → HUD'daki CPU/GPU ms için (GPU-bound mu CPU-bound mu?)
        PlayerSettings.enableFrameTimingStats = true;

        // 3) IL2CPP + ARM64 (modern cihaz/Play uyumu; release-benzeri perf ölçümü)
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;

        // 4) Development Build + Autoconnect Profiler (Unity Profiler'ı USB/Wi-Fi üstünden bağlar)
        EditorUserBuildSettings.development = true;
        EditorUserBuildSettings.connectProfiler = true;
        EditorUserBuildSettings.buildAppBundle = false;   // test için APK (AAB değil)

        AssetDatabase.SaveAssets();

        bool moduleInstalled = BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android);

        Debug.Log(
            "[AndroidSetup] Ayarlandı ✓\n" +
            $"  • Bundle ID: {BUNDLE_ID}\n" +
            "  • Frame Timing Stats: AÇIK (HUD'da CPU/GPU ms)\n" +
            "  • Scripting: IL2CPP, Arch: ARM64\n" +
            "  • Development Build + Autoconnect Profiler: AÇIK, çıktı: APK\n" +
            $"  • Android Build Support modülü: {(moduleInstalled ? "KURULU ✓" : "❌ KURULU DEĞİL — Unity Hub > sürüm > Add Modules > Android Build Support (+OpenJDK,SDK,NDK)")}\n" +
            "  SONRAKİ: File > Build Settings > Android > Switch Platform → telefonu USB ile bağla → Build And Run.");
    }
}
