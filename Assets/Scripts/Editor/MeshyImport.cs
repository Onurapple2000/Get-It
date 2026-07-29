using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// GLB (Meshy) modellerinin Base Color texture'larını küçültür + malzemeyi GÜVENİLİR URP/Lit'e taşır.
/// Magenta tuzağı: gltfast malzemesini `new Material(src)` ile kopyalayıp prefab'a gömmek runtime/editor'da
/// magenta verebiliyor. ÇÖZÜM (bu projede kanıtlı): çalışan bir URP .mat'i KOPYALA, texture/rengi ona bas.
/// Graphics.Blit ile texture okunur (kaynak CPU-okunabilir olmak zorunda değil).
/// </summary>
public static class MeshyImport
{
    const string MAT_TEMPLATE = "Assets/Materials/DecoObjects/Clay_Red.mat";   // sorunsuz URP/Lit

    /// <summary>Renderer malzemelerini: texture'ı maxSize'a indir + URP/Lit template'ine taşı.</summary>
    public static void DownscaleTextures(GameObject instance, string outDir, string prefix, int maxSize)
    {
        EnsureDir(outDir);
        var texCache = new Dictionary<Texture, Texture2D>();
        var matCache = new Dictionary<Material, Material>();
        int matIdx = 0;

        foreach (var rend in instance.GetComponentsInChildren<Renderer>())
        {
            var mats = rend.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                var src = mats[i];
                if (src == null) continue;

                if (!matCache.TryGetValue(src, out var nm))
                {
                    var tex = BaseTextureOf(src);
                    Texture2D finalTex = null;
                    if (tex != null)
                    {
                        if (tex.width > maxSize || tex.height > maxSize)
                        {
                            if (!texCache.TryGetValue(tex, out finalTex))
                            {
                                finalTex = Downscale(tex, maxSize, $"{outDir}/{prefix}_{Clean(tex.name)}.png");
                                texCache[tex] = finalTex;
                            }
                        }
                        else finalTex = tex as Texture2D;
                    }

                    nm = MakeMat($"{outDir}/{prefix}_mat{matIdx++}.mat", finalTex, BaseColorOf(src));
                    matCache[src] = nm;
                }
                mats[i] = nm;
            }
            rend.sharedMaterials = mats;
        }
    }

    // Çalışan URP/Lit .mat'i kopyala (magenta önlenir) → texture + renk bas.
    static Material MakeMat(string path, Texture2D tex, Color baseColor)
    {
        AssetDatabase.DeleteAsset(path);
        if (!AssetDatabase.CopyAsset(MAT_TEMPLATE, path))
        {
            // template yoksa son çare: URP/Lit
            var m2 = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(m2, path);
        }
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", baseColor);
        m.color = baseColor;
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.25f);
        if (tex != null) { m.SetTexture("_BaseMap", tex); m.mainTexture = tex; }
        m.enableInstancing = true;   // aynı türden çok nesne → tek çizim çağrısı (mobil perf)
        EditorUtility.SetDirty(m);
        return m;
    }

    // gltfast Shader Graph property adları farklı (baseColorTexture/baseColorFactor). HasProperty ile güvenli oku.
    static readonly string[] TexProps = { "baseColorTexture", "_BaseMap", "_BaseColorMap", "_MainTex" };
    static readonly string[] ColProps = { "baseColorFactor", "_BaseColor", "_Color" };

    static Texture BaseTextureOf(Material src)
    {
        foreach (var p in TexProps)
            if (src.HasProperty(p)) { var t = src.GetTexture(p); if (t != null) return t; }
        return src.mainTexture;
    }

    static Color BaseColorOf(Material src)
    {
        foreach (var p in ColProps)
            if (src.HasProperty(p)) return src.GetColor(p);
        return Color.white;   // texture rengini olduğu gibi göster
    }

    static Texture2D Downscale(Texture src, int max, string assetPath)
    {
        int w = src.width, h = src.height;
        float k = (float)max / Mathf.Max(w, h);
        int nw = Mathf.Max(1, Mathf.RoundToInt(w * k)), nh = Mathf.Max(1, Mathf.RoundToInt(h * k));

        var rt = RenderTexture.GetTemporary(nw, nh, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        Graphics.Blit(src, rt);
        var prev = RenderTexture.active;
        RenderTexture.active = rt;
        var tex = new Texture2D(nw, nh, TextureFormat.RGBA32, false);
        tex.ReadPixels(new Rect(0, 0, nw, nh), 0, 0);
        tex.Apply();
        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);

        string full = Path.Combine(Application.dataPath, assetPath.Substring("Assets/".Length));
        File.WriteAllBytes(full, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);

        var imp = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (imp != null)
        {
            imp.maxTextureSize = max;
            imp.textureCompression = TextureImporterCompression.Compressed;
            imp.mipmapEnabled = true;
            imp.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
    }

    static string Clean(string n)
    {
        foreach (var c in Path.GetInvalidFileNameChars()) n = n.Replace(c, '_');
        return string.IsNullOrEmpty(n) ? "tex" : n;
    }

    static void EnsureDir(string assetPath)
    {
        string full = Path.Combine(Application.dataPath, assetPath.Substring("Assets/".Length));
        if (!Directory.Exists(full)) { Directory.CreateDirectory(full); AssetDatabase.Refresh(); }
    }
}
