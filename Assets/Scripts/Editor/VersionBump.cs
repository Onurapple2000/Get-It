using UnityEditor;
using UnityEngine;

/// <summary>
/// Sürüm/sürüm kodunu Unity API'siyle ayarlar (ProjectSettings.asset'i dışarıdan düzenlemek Unity açıkken
/// üzerine yazılıyor — 2026-09-15 tuzağı). Play'e bir kez yüklenen sürüm kodu bir daha kullanılamaz.
/// </summary>
public static class VersionBump
{
    [MenuItem("Tools/GET_IT/Release/Bump Version Code (+1)")]
    public static void Bump()
    {
        int code = PlayerSettings.Android.bundleVersionCode + 1;
        PlayerSettings.Android.bundleVersionCode = code;
        AssetDatabase.SaveAssets();
        Debug.Log($"[Version] {PlayerSettings.bundleVersion} / sürüm kodu {code}");
    }

    [MenuItem("Tools/GET_IT/Release/Set Version 0.3.1 + Code 4")]
    public static void Set031()
    {
        PlayerSettings.bundleVersion = "0.3.1";
        PlayerSettings.Android.bundleVersionCode = 4;
        AssetDatabase.SaveAssets();
        Debug.Log("[Version] 0.3.1 / sürüm kodu 4 ayarlandı");
    }
}
