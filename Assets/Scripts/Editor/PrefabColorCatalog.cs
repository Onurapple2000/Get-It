using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// LANDMARK sistemi için prefab RENK KATALOĞU üretir: tüm dünya prefab klasörlerini tarar, her prefabın ana
/// texture'ının (veya düz baseColor'ının) ORTALAMA RENGİNİ hesaplar → Assets/Resources/prefab_avg_colors.txt
/// ("Ad r g b" satırları, 0-1 float). Runtime'da LandmarkBuilder bunu okuyup renk-hedefli nesne seçer
/// (örn. köprü = kırmızı nesne). Compressed texture'lar RenderTexture blit + ReadPixels ile okunur (isReadable
/// gerekmez). Menü: Tools/GET_IT/Build Prefab Color Catalog
/// </summary>
public static class PrefabColorCatalog
{
    static readonly string[] DIRS =
    {
        "Assets/Prefabs/DecoObjects", "Assets/Prefabs/Foods", "Assets/Prefabs/Cars", "Assets/Prefabs/Buildings",
        "Assets/Prefabs/Sweets", "Assets/Prefabs/Drinks", "Assets/Prefabs/Gifts", "Assets/Prefabs/Books",
        "Assets/Prefabs/Cats", "Assets/Prefabs/Dogs", "Assets/Prefabs/Ships", "Assets/Prefabs/Planes",
        "Assets/Prefabs/Money", "Assets/Prefabs/Jewelry",
    };
    const string OUT = "Assets/Resources/prefab_avg_colors.txt";

    [MenuItem("Tools/GET_IT/Build Prefab Color Catalog")]
    public static void Run()
    {
        var sb = new StringBuilder();
        int count = 0;
        foreach (var dir in DIRS)
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { dir }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.StartsWith(dir + "/") || !path.EndsWith(".prefab")) continue;
                var pf = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (pf == null) continue;
                Color c = AverageColor(pf);
                sb.AppendLine($"{pf.name} {c.r:F3} {c.g:F3} {c.b:F3}");
                count++;
            }
        }
        Directory.CreateDirectory(Path.Combine(Application.dataPath, "Resources"));
        File.WriteAllText(Path.Combine(Application.dataPath, "Resources/prefab_avg_colors.txt"), sb.ToString());
        AssetDatabase.Refresh();
        Debug.Log($"[ColorCatalog] {count} prefab rengi → {OUT}");
    }

    // Prefabın tüm renderer materyallerinin ağırlıksız ortalaması: texture varsa 32x32 downsample ortalaması
    // (× baseColor tint), yoksa düz baseColor.
    static Color AverageColor(GameObject pf)
    {
        var rends = pf.GetComponentsInChildren<Renderer>();
        Vector3 sum = Vector3.zero; int n = 0;
        foreach (var r in rends)
        {
            foreach (var m in r.sharedMaterials)
            {
                if (m == null) continue;
                Color baseC = Color.white;
                if (m.HasProperty("_BaseColor")) baseC = m.GetColor("_BaseColor");
                else if (m.HasProperty("_Color")) baseC = m.color;
                Texture tex = m.HasProperty("_BaseMap") ? m.GetTexture("_BaseMap") : null;
                if (tex == null && m.HasProperty("baseColorTexture")) tex = m.GetTexture("baseColorTexture");
                if (tex == null && m.HasProperty("_MainTex")) tex = m.GetTexture("_MainTex");

                Color c = baseC;
                if (tex != null) c = TexAverage(tex) * baseC;
                sum += new Vector3(c.r, c.g, c.b); n++;
            }
        }
        if (n == 0) return Color.gray;
        return new Color(sum.x / n, sum.y / n, sum.z / n);
    }

    static Color TexAverage(Texture tex)
    {
        var rt = RenderTexture.GetTemporary(32, 32, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        var prev = RenderTexture.active;
        Graphics.Blit(tex, rt);
        RenderTexture.active = rt;
        var t2 = new Texture2D(32, 32, TextureFormat.RGBA32, false);
        t2.ReadPixels(new Rect(0, 0, 32, 32), 0, 0);
        t2.Apply();
        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);

        var px = t2.GetPixels32();
        Object.DestroyImmediate(t2);
        long r = 0, g = 0, b = 0;
        foreach (var p in px) { r += p.r; g += p.g; b += p.b; }
        float inv = 1f / (px.Length * 255f);
        return new Color(r * inv, g * inv, b * inv);
    }
}
