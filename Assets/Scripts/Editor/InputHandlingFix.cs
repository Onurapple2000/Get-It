using UnityEditor;
using UnityEngine;

/// <summary>
/// Active Input Handling'i "Input Manager (Old)"a çeker (0). Kod sadece eski Input API kullanıyor (VirtualJoystick/
/// HoleController → Input.touches/GetAxis), yeni InputSystem YOK → "Both" gereksiz + Android'de uyarı/yük.
/// ⚠️ Dosyayı elle düzenlemek YETMEZ (çalışan Unity bellekteki değeri geri yazar) → SerializedObject ile bellekte
/// değiştirilir. Değişiklikten sonra Unity YENİDEN BAŞLATILMALI. Menü: Tools/GET_IT/Set Input Handling to Old.
/// </summary>
public static class InputHandlingFix
{
    [MenuItem("Tools/GET_IT/Set Input Handling to Old")]
    public static void SetOld()
    {
        var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
        if (assets == null || assets.Length == 0) { Debug.LogError("[Input] ProjectSettings.asset yüklenemedi."); return; }

        var so = new SerializedObject(assets[0]);
        var prop = so.FindProperty("activeInputHandler");
        if (prop == null) { Debug.LogError("[Input] 'activeInputHandler' property bulunamadı."); return; }

        prop.intValue = 0;   // 0 = Input Manager (Old), 1 = Input System (New), 2 = Both
        so.ApplyModifiedProperties();
        AssetDatabase.SaveAssets();
        Debug.Log("[Input] activeInputHandler = 0 (Input Manager Old) ayarlandı. ⚠️ Unity'yi YENİDEN BAŞLAT (uyarı gitsin).");
    }
}
