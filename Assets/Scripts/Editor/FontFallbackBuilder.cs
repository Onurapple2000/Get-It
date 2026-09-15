using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using TMPro;

/// <summary>
/// Eksik glifleri (para birimi sembolleri, Korece/Arapça/Kiril) render etmek için DİNAMİK TMP font asset'leri üretir
/// (atlas runtime'da dolar) ve TMP'nin GLOBAL fallback listesine SIRAYLA ekler.
/// Menü: Tools/GET_IT/Build Font Fallbacks
///
/// SIRA ÖNEMLİ — TMP fallback listesini baştan sona tarar, ilk bulduğu glifi kullanır:
///   1) NotoSans  → ₺ ₽ ₹ ₩ ₱ gibi MODERN para birimleri + Latin/Kiril  (SIL OFL, yayına uygun)
///   2) ArialUnicode → Korece/Arapça/CJK  (Noto'da olmayanlar buradan gelir)
///
/// ⚠️ 2026-08-22 TESPİT: Play mağazasından gelen fiyat metni (localizedPriceString) cihazın para birimini
/// içeriyor. ArialUnicode 1990'ların fontu; ₺(2012) ₽(2014) ₹(2010) Unicode'a SONRADAN eklendiği için o fontta
/// YOK → Türkiye/Rusya/Hindistan'da fiyatlar KUTU görünüyordu. Noto bunları içeriyor (doğrulandı).
/// ⚠️ ArialUnicode LİSANSLI (dev/test). Yayında ideali: Korece/Arapça için de Noto Sans KR / Noto Sans Arabic.
/// </summary>
public static class FontFallbackBuilder
{
    // Sıra = fallback önceliği. Önce Noto (para birimleri), sonra ArialUnicode (CJK/Arapça).
    static readonly (string src, string outPath, string name)[] Fonts =
    {
        ("Assets/Fonts/NotoSans-Regular.ttf",
         "Assets/TextMesh Pro/Resources/Fonts & Materials/NotoSans SDF.asset", "NotoSans SDF"),
        ("Assets/Fonts/ArialUnicode.ttf",
         "Assets/TextMesh Pro/Resources/Fonts & Materials/ArialUnicode SDF.asset", "ArialUnicode SDF"),
    };

    [MenuItem("Tools/GET_IT/Build Font Fallbacks")]
    public static void Build()
    {
        // Yeni eklenen .ttf dosyaları Unity dışından kopyalanmış olabilir → önce içe aktar,
        // yoksa LoadAssetAtPath null döner ve font sessizce atlanır.
        AssetDatabase.Refresh();

        var assets = new List<TMP_FontAsset>();

        foreach (var (src, outPath, name) in Fonts)
        {
            var font = AssetDatabase.LoadAssetAtPath<Font>(src);
            if (font == null) { Debug.LogWarning($"[Font] kaynak font yok, atlanıyor: {src}"); continue; }

            var fa = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(outPath);
            if (fa == null)
            {
                fa = TMP_FontAsset.CreateFontAsset(font, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024,
                                                   AtlasPopulationMode.Dynamic, true);
                fa.name = name;
                AssetDatabase.CreateAsset(fa, outPath);
                // Atlas dokusu + materyali alt-asset olarak sakla (yoksa kaybolur).
                if (fa.atlasTextures != null)
                    foreach (var tex in fa.atlasTextures)
                        if (tex != null && !AssetDatabase.Contains(tex)) { tex.name = name + " Atlas"; AssetDatabase.AddObjectToAsset(tex, fa); }
                if (fa.material != null && !AssetDatabase.Contains(fa.material)) { fa.material.name = name + " Material"; AssetDatabase.AddObjectToAsset(fa.material, fa); }
                EditorUtility.SetDirty(fa);
                Debug.Log($"[Font] {name} (dinamik) oluşturuldu.");
            }
            assets.Add(fa);
        }

        if (assets.Count == 0) { Debug.LogError("[Font] Hiç font asset'i üretilemedi."); return; }
        AssetDatabase.SaveAssets();

        // TMP Settings global fallback listesini İSTENEN SIRAYLA yeniden kur.
        var guids = AssetDatabase.FindAssets("t:TMP_Settings");
        if (guids.Length == 0) { Debug.LogError("[Font] TMP Settings bulunamadı (TMP kurulu mu?)."); return; }
        var settings = AssetDatabase.LoadAssetAtPath<TMP_Settings>(AssetDatabase.GUIDToAssetPath(guids[0]));
        var so = new SerializedObject(settings);
        var list = so.FindProperty("m_fallbackFontAssets");
        if (list == null) { Debug.LogError("[Font] m_fallbackFontAssets alanı yok."); return; }

        // Bizim asset'lerimizi listeden çıkar (sıra bozulmasın), sonra başa doğru sırayla ekle.
        for (int i = list.arraySize - 1; i >= 0; i--)
            if (assets.Contains(list.GetArrayElementAtIndex(i).objectReferenceValue as TMP_FontAsset))
                list.DeleteArrayElementAtIndex(i);

        for (int i = 0; i < assets.Count; i++)
        {
            list.InsertArrayElementAtIndex(i);
            list.GetArrayElementAtIndex(i).objectReferenceValue = assets[i];
        }

        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();

        var order = string.Join(" → ", assets.ConvertAll(a => a.name));
        Debug.Log($"[Font] TMP global fallback sırası: {order}  (₺ ₽ ₹ artık Noto'dan gelir)");
    }
}
