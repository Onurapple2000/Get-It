using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Çalışan Unity içinden Android APK build alır (Development build → DEVELOPMENT_BUILD define → tüm leveller açık,
/// test için). Çıktı: {proje}/Builds/GET_IT.apk. Menü: Tools/GET_IT/Build Android APK.
/// NOT: cihaza kurulum ayrı (adb). Xiaomi/MIUI USB tuzağı için gerekirse APK'yı /sdcard/Download'a push'la.
/// </summary>
public static class AndroidApkBuild
{
    [MenuItem("Tools/GET_IT/Build Android APK")]
    public static void Build()
    {
        string root = Directory.GetParent(Application.dataPath).FullName;
        string dir = Path.Combine(root, "Builds");
        Directory.CreateDirectory(dir);
        string apk = Path.Combine(dir, "GET_IT.apk");

        var scenes = new List<string>();
        foreach (var s in EditorBuildSettings.scenes)
            if (s.enabled) scenes.Add(s.path);
        if (scenes.Count == 0) { Debug.LogError("[APK] Build Settings'te etkin sahne yok!"); return; }

        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
        {
            Debug.LogWarning("[APK] Aktif platform Android değil → Android'e geçiliyor. Geçiş bitince menüyü TEKRAR çalıştır.");
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
            return;
        }

        EditorUserBuildSettings.buildAppBundle = false;   // AAB değil, kurulabilir APK

        var opts = new BuildPlayerOptions
        {
            scenes = scenes.ToArray(),
            locationPathName = apk,
            target = BuildTarget.Android,
            targetGroup = BuildTargetGroup.Android,
            options = BuildOptions.Development | BuildOptions.AllowDebugging,
        };

        Debug.Log($"[APK] Build BAŞLADI → {apk} ({scenes.Count} sahne). Birkaç dakika sürebilir…");
        var report = BuildPipeline.BuildPlayer(opts);
        var sum = report.summary;
        Debug.Log($"[APK] Build {sum.result}: {sum.totalSize / (1024 * 1024)} MB, {sum.totalErrors} hata, çıktı: {apk}");
    }
}
