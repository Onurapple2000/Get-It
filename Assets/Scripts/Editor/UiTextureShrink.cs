using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;

/// <summary>
/// UI/menü texture'larını (Assets/Art, Assets/Resources — modeller HARİÇ) build-dostu yapar:
/// maxTextureSize 1024, crunch compression, mipmap KAPALI (UI'ye gerek yok). Bu texture'lar 81MB'a kadar
/// çıkıyordu (build boyutunun ana suçlusu). Önce mevcut boyut/format'ı loglar (teşhis), sonra düzeltir.
/// Model texture'ları (Assets/Prefabs/*/Tex) zaten pipeline'da 1024 → dokunulmaz.
/// Menu: Tools/GET_IT/Shrink UI Textures
/// </summary>
public static class UiTextureShrink
{
    static readonly string[] Roots = { "Assets/Art", "Assets/Resources" };
    const int MAX = 1024;

    [MenuItem("Tools/GET_IT/Shrink UI Textures")]
    public static void Run()
    {
        var guids = AssetDatabase.FindAssets("t:Texture2D", Roots);
        long before = 0, after = 0;
        int changed = 0;
        var big = new List<string>();

        foreach (var g in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(g);
            if (!path.EndsWith(".png") && !path.EndsWith(".jpg")) continue;
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti == null) continue;

            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            long sz = tex != null ? Profiler.GetRuntimeMemorySizeLong(tex) : 0;
            before += sz;
            if (sz > 8 * 1048576) big.Add($"{sz / 1048576f:0.0}MB {tex.width}x{tex.height} {(tex != null ? tex.format.ToString() : "?")}  {path}");

            bool dirty = false;
            if (ti.mipmapEnabled) { ti.mipmapEnabled = false; dirty = true; }
            if (ti.maxTextureSize > MAX) { ti.maxTextureSize = MAX; dirty = true; }
            if (!ti.crunchedCompression) { ti.crunchedCompression = true; dirty = true; }
            if (ti.textureCompression == TextureImporterCompression.Uncompressed)
            { ti.textureCompression = TextureImporterCompression.Compressed; dirty = true; }
            ti.compressionQuality = 50;

            // Android override → kesinlikle uygulansın (crunch'lı ETC2)
            var a = ti.GetPlatformTextureSettings("Android");
            a.overridden = true; a.maxTextureSize = MAX;
            a.textureCompression = TextureImporterCompression.Compressed;
            a.crunchedCompression = true; a.compressionQuality = 50;
            ti.SetPlatformTextureSettings(a);

            if (dirty || true) { ti.SaveAndReimport(); changed++; }

            var t2 = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            after += t2 != null ? Profiler.GetRuntimeMemorySizeLong(t2) : 0;
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"[UiTexShrink] {changed} texture işlendi.");
        sb.AppendLine($"  Runtime bellek: {before / 1048576f:0.0} MB → {after / 1048576f:0.0} MB");
        sb.AppendLine("  (Not: build/indirme boyutu crunch ile çok daha az; kesin rakam sonraki build report'ta.)");
        sb.AppendLine("  8MB üstü olanlar (önce):");
        foreach (var b in big) sb.AppendLine("   " + b);
        Debug.Log(sb.ToString());
    }

    // Model texture'larını 512'ye indir (bu oyunda nesneler ekranda küçük → algısal KAYIPSIZ). Sıkıştırma/mip
    // dokunulmaz (sadece boyut). Runtime bellek + build ~yarıya iner.
    [MenuItem("Tools/GET_IT/Model Textures to 512")]
    public static void Models512()
    {
        string[] roots = { "Assets/Prefabs/Cars/Tex", "Assets/Prefabs/Foods/Tex",
                           "Assets/Prefabs/PowerUps/Tex", "Assets/Prefabs/DecoObjects/Tex" };
        var exist = new List<string>();
        foreach (var r in roots) if (AssetDatabase.IsValidFolder(r)) exist.Add(r);
        var guids = AssetDatabase.FindAssets("t:Texture2D", exist.ToArray());
        long before = 0, after = 0; int n = 0;
        foreach (var g in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(g);
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti == null) continue;
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            before += tex != null ? Profiler.GetRuntimeMemorySizeLong(tex) : 0;
            if (ti.maxTextureSize > 512) { ti.maxTextureSize = 512; ti.SaveAndReimport(); n++; }
            var t2 = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            after += t2 != null ? Profiler.GetRuntimeMemorySizeLong(t2) : 0;
        }
        Debug.Log($"[Models512] {n} model texture 512'ye indi. Runtime: {before / 1048576f:0.0} MB → {after / 1048576f:0.0} MB");
    }

    // Model crunch'ı GERİ AL: crunch kapat + Android override kaldır (pipeline default'una dön). Crunch bunları
    // büyüttü (RGB→RGBA ETC2 zorladı). Model texture'ları zaten iyi durumdaydı.
    [MenuItem("Tools/GET_IT/Revert Model Textures")]
    public static void RevertModels()
    {
        string[] roots = { "Assets/Prefabs/Cars/Tex", "Assets/Prefabs/Foods/Tex",
                           "Assets/Prefabs/PowerUps/Tex", "Assets/Prefabs/DecoObjects/Tex" };
        var exist = new List<string>();
        foreach (var r in roots) if (AssetDatabase.IsValidFolder(r)) exist.Add(r);
        var guids = AssetDatabase.FindAssets("t:Texture2D", exist.ToArray());
        int n = 0;
        foreach (var g in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(g);
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti == null) continue;
            ti.crunchedCompression = false;
            var a = ti.GetPlatformTextureSettings("Android");
            a.overridden = false; a.crunchedCompression = false;
            ti.SetPlatformTextureSettings(a);
            ti.SaveAndReimport(); n++;
        }
        Debug.Log($"[Revert] {n} model texture eski haline döndürüldü (crunch kapalı).");
    }

    // Model texture'ları (Assets/Prefabs/*/Tex): crunch ekle, 1024 kalsın, MIPMAP KORUNUR (3D uzaklık için).
    [MenuItem("Tools/GET_IT/Crunch Model Textures")]
    public static void CrunchModels()
    {
        string[] roots = { "Assets/Prefabs/Cars/Tex", "Assets/Prefabs/Foods/Tex",
                           "Assets/Prefabs/PowerUps/Tex", "Assets/Prefabs/DecoObjects/Tex" };
        var exist = new List<string>();
        foreach (var r in roots) if (AssetDatabase.IsValidFolder(r)) exist.Add(r);
        if (exist.Count == 0) { Debug.LogWarning("[Crunch] model Tex klasörü yok."); return; }

        var guids = AssetDatabase.FindAssets("t:Texture2D", exist.ToArray());
        long before = 0, after = 0; int n = 0;
        foreach (var g in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(g);
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti == null) continue;
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            before += tex != null ? Profiler.GetRuntimeMemorySizeLong(tex) : 0;

            if (ti.maxTextureSize > 1024) ti.maxTextureSize = 1024;
            ti.textureCompression = TextureImporterCompression.Compressed;
            ti.crunchedCompression = true;
            ti.compressionQuality = 50;
            var a = ti.GetPlatformTextureSettings("Android");
            a.overridden = true; a.maxTextureSize = 1024;
            a.textureCompression = TextureImporterCompression.Compressed;
            a.crunchedCompression = true; a.compressionQuality = 50;
            ti.SetPlatformTextureSettings(a);
            ti.SaveAndReimport(); n++;

            var t2 = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            after += t2 != null ? Profiler.GetRuntimeMemorySizeLong(t2) : 0;
        }
        Debug.Log($"[Crunch] {n} model texture crunch'landı. Runtime: {before / 1048576f:0.0} MB → {after / 1048576f:0.0} MB");
    }
}
