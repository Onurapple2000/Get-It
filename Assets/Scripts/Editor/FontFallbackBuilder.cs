using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using TMPro;

/// <summary>
/// Korece/Arapça (ve Kiril vb.) gliflerini render etmek için: Arial Unicode'dan DİNAMİK bir TMP font asset üretir
/// (atlas runtime'da dolar) ve TMP'nin GLOBAL fallback listesine ekler → tüm TMP metinleri eksik glifleri buradan alır
/// (kutu yerine harf). Menü: Tools/GET_IT/Build Font Fallbacks. ⚠️ Arial Unicode lisanslı (dev/test); yayında Noto (OFL) tercih et.
/// </summary>
public static class FontFallbackBuilder
{
    const string SrcFont = "Assets/Fonts/ArialUnicode.ttf";
    const string OutPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/ArialUnicode SDF.asset";

    [MenuItem("Tools/GET_IT/Build Font Fallbacks")]
    public static void Build()
    {
        var font = AssetDatabase.LoadAssetAtPath<Font>(SrcFont);
        if (font == null) { Debug.LogError($"[Font] kaynak font yok: {SrcFont}"); return; }

        var fa = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(OutPath);
        if (fa == null)
        {
            fa = TMP_FontAsset.CreateFontAsset(font, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024,
                                               AtlasPopulationMode.Dynamic, true);
            fa.name = "ArialUnicode SDF";
            AssetDatabase.CreateAsset(fa, OutPath);
            // Atlas dokusu + materyali alt-asset olarak sakla (yoksa kaybolur).
            if (fa.atlasTextures != null)
                foreach (var tex in fa.atlasTextures)
                    if (tex != null && !AssetDatabase.Contains(tex)) { tex.name = "ArialUnicode Atlas"; AssetDatabase.AddObjectToAsset(tex, fa); }
            if (fa.material != null && !AssetDatabase.Contains(fa.material)) { fa.material.name = "ArialUnicode Material"; AssetDatabase.AddObjectToAsset(fa.material, fa); }
            EditorUtility.SetDirty(fa);
            AssetDatabase.SaveAssets();
            Debug.Log("[Font] ArialUnicode SDF (dinamik) oluşturuldu.");
        }

        // TMP Settings global fallback listesine ekle.
        var guids = AssetDatabase.FindAssets("t:TMP_Settings");
        if (guids.Length == 0) { Debug.LogError("[Font] TMP Settings bulunamadı (TMP kurulu mu?)."); return; }
        var settings = AssetDatabase.LoadAssetAtPath<TMP_Settings>(AssetDatabase.GUIDToAssetPath(guids[0]));
        var so = new SerializedObject(settings);
        var list = so.FindProperty("m_fallbackFontAssets");
        if (list == null) { Debug.LogError("[Font] m_fallbackFontAssets alanı yok."); return; }

        bool already = false;
        for (int i = 0; i < list.arraySize; i++)
            if (list.GetArrayElementAtIndex(i).objectReferenceValue == fa) { already = true; break; }
        if (!already)
        {
            list.arraySize++;
            list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = fa;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            Debug.Log("[Font] ArialUnicode SDF → TMP Settings global fallback'e eklendi. Artık KR/AR/Kiril render olur.");
        }
        else Debug.Log("[Font] Fallback zaten ekli.");
    }
}
