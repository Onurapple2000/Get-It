using UnityEditor;
using UnityEngine;

/// <summary>
/// Sürüm/sürüm kodunu Unity API'siyle ayarlar (ProjectSettings.asset'i dışarıdan düzenlemek Unity açıkken
/// üzerine yazılıyor — 2026-09-15 tuzağı). Play'e bir kez yüklenen sürüm kodu bir daha kullanılamaz.
/// Bump: sürüm kodu +1 VE sürüm adının son basamağı +1 (0.3.1 → 0.3.2).
/// </summary>
public static class VersionBump
{
    [MenuItem("Tools/GET_IT/Release/Bump Version (code +1, patch +1)")]
    public static void Bump()
    {
        int code = PlayerSettings.Android.bundleVersionCode + 1;
        PlayerSettings.Android.bundleVersionCode = code;
        var parts = PlayerSettings.bundleVersion.Split('.');
        if (parts.Length >= 3 && int.TryParse(parts[parts.Length - 1], out int patch))
        {
            parts[parts.Length - 1] = (patch + 1).ToString();
            PlayerSettings.bundleVersion = string.Join(".", parts);
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"[Version] {PlayerSettings.bundleVersion} / sürüm kodu {code}");
    }

    [MenuItem("Tools/GET_IT/Release/Set Version 0.3.2 + Code 5")]
    public static void Set032()
    {
        PlayerSettings.bundleVersion = "0.3.2";
        PlayerSettings.Android.bundleVersionCode = 5;
        AssetDatabase.SaveAssets();
        Debug.Log("[Version] 0.3.2 / sürüm kodu 5 ayarlandı");
    }
}
