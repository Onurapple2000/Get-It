using UnityEditor;
using UnityEngine;

/// <summary>
/// MIPMAP kontrolü. Beyaz-nokta (UV dikiş bleed) mip'ten geliyordu → mip kapatınca temizleniyor AMA mip PERFORMANSI
/// İYİLEŞTİRİR (uzakta küçük doku örneklenir); kapatmak kalabalık sahnede bant genişliğini artırır. Bu yüzden:
///   - PowerUps (3 doku, az nesne, aşırı kontrast): mip KAPALI kalsın (mıknatıs temiz, perf maliyeti önemsiz).
///   - Drinks (82 doku, YÜZLERCE nesne): mip AÇIK (perf) — hafif dikiş bleed'i tolere.
/// Menüler ayrı grup kontrol eder.
/// </summary>
public static class TextureMipFix
{
    const string POW = "Assets/Prefabs/PowerUps/Tex";
    const string DRK = "Assets/Prefabs/Drinks/Tex";

    [MenuItem("Tools/GET_IT/Mipmaps: PowerUps OFF")]      public static void PowOff()  => Set(POW, false);
    [MenuItem("Tools/GET_IT/Mipmaps: PowerUps ON")]       public static void PowOn()   => Set(POW, true);
    [MenuItem("Tools/GET_IT/Mipmaps: Drinks OFF")]        public static void DrkOff()  => Set(DRK, false);
    [MenuItem("Tools/GET_IT/Mipmaps: Drinks ON (perf)")]  public static void DrkOn()   => Set(DRK, true);

    static void Set(string dir, bool mips)
    {
        if (!AssetDatabase.IsValidFolder(dir)) { Debug.LogWarning($"[MipFix] klasör yok: {dir}"); return; }
        int n = 0;
        foreach (var g in AssetDatabase.FindAssets("t:Texture2D", new[] { dir }))
        {
            string p = AssetDatabase.GUIDToAssetPath(g);
            var imp = AssetImporter.GetAtPath(p) as TextureImporter;
            if (imp == null || imp.mipmapEnabled == mips) continue;
            imp.mipmapEnabled = mips;
            EditorUtility.SetDirty(imp);
            imp.SaveAndReimport();
            n++;
        }
        Debug.Log($"[MipFix] {dir}: mipmap {(mips ? "AÇILDI" : "KAPATILDI")} ({n} doku).");
    }
}
