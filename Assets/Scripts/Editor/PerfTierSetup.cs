using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// PerfTier için editör kurulumu (idempotent):
///  1. Assets/Resources/Mobile_Low_RPAsset.asset — Mobile_RPAsset kopyası: gölge/HDR/MSAA kapalı, renderScale 0.7.
///  2. QualitySettings'e "MobileLow" seviyesi (bu asset'i referanslar) → URP shader stripping bu varyantları BUILD'E KOYAR
///     (⚠️ yalnız Resources'tan yüklenen asset'in varyantları stripping'de görülmez → cihazda pembe/kırık materyal).
///  3. Android grafik API sırası: OpenGL ES 3 önce, Vulkan yedek (eski Adreno 6xx Vulkan sürücü takılmaları).
/// Menü: Tools/GET_IT/Release/Setup Perf Tier (Low URP + Quality + GLES3 first)
/// </summary>
public static class PerfTierSetup
{
    const string Src = "Assets/Settings/Mobile_RPAsset.asset";
    const string Dst = "Assets/Resources/Mobile_Low_RPAsset.asset";

    [MenuItem("Tools/GET_IT/Release/Setup Perf Tier (Low URP + Quality + GLES3 first)")]
    public static void Run()
    {
        // 1) Low URP asset
        if (AssetDatabase.LoadAssetAtPath<Object>(Dst) == null)
        {
            if (!AssetDatabase.CopyAsset(Src, Dst)) { Debug.LogError("[PerfTier] Kopyalanamadı: " + Src); return; }
        }
        var low = AssetDatabase.LoadAssetAtPath<Object>(Dst);
        var so = new SerializedObject(low);
        Set(so, "m_MainLightShadowsSupported", 0);
        Set(so, "m_SoftShadowsSupported", 0);
        Set(so, "m_AdditionalLightShadowsSupported", 0);
        Set(so, "m_SupportsHDR", 0);
        Set(so, "m_MSAA", 1);
        Set(so, "m_MainLightShadowmapResolution", 512);
        so.FindProperty("m_RenderScale").floatValue = 0.7f;
        so.FindProperty("m_ShadowDistance").floatValue = 0f;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(low);

        // 2) Quality level "MobileLow"
        var qsObj = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset")[0];
        var qs = new SerializedObject(qsObj);
        var arr = qs.FindProperty("m_QualitySettings");
        int idx = -1;
        for (int i = 0; i < arr.arraySize; i++)
            if (arr.GetArrayElementAtIndex(i).FindPropertyRelative("name").stringValue == "MobileLow") idx = i;
        if (idx < 0)
        {
            // "Mobile" seviyesini bul ve kopyala (InsertArrayElementAtIndex önceki elemanı çoğaltır)
            int mob = 0;
            for (int i = 0; i < arr.arraySize; i++)
                if (arr.GetArrayElementAtIndex(i).FindPropertyRelative("name").stringValue == "Mobile") mob = i;
            arr.InsertArrayElementAtIndex(mob + 1);
            idx = mob + 1;
        }
        var lvl = arr.GetArrayElementAtIndex(idx);
        lvl.FindPropertyRelative("name").stringValue = "MobileLow";
        lvl.FindPropertyRelative("customRenderPipeline").objectReferenceValue = low;
        qs.ApplyModifiedPropertiesWithoutUndo();

        // 3) Grafik API sırası
        PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
        PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.OpenGLES3, GraphicsDeviceType.Vulkan });

        AssetDatabase.SaveAssets();
        Debug.Log("[PerfTier] Kurulum tamam: Mobile_Low_RPAsset + Quality 'MobileLow' + Android API sırası GLES3, Vulkan.");
    }

    static void Set(SerializedObject so, string name, int v)
    {
        var p = so.FindProperty(name);
        if (p == null) { Debug.LogWarning("[PerfTier] alan yok: " + name); return; }
        if (p.propertyType == SerializedPropertyType.Boolean) p.boolValue = v != 0; else p.intValue = v;
    }
}
