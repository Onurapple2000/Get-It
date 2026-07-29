using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Menü görsellerinin (warm maskot + 18 dünya ikonu) BEYAZ arka planını şeffaf yapar. Kenarlardan
/// flood-fill (kenara bağlı beyaz silinir, iç beyazlar korunur) + hafif de-fringe. Kaynak RGB ise yeni
/// RGBA32 texture'a yazar. Menu: Tools/GET_IT/Remove Menu White Backgrounds
/// </summary>
public static class WhiteBgRemover
{
    [MenuItem("Tools/GET_IT/Remove Menu White Backgrounds")]
    public static void Run()
    {
        var paths = new List<string> { "Assets/Art/mole_mascot_warm.png" };
        foreach (var icon in WorldCatalog.Icons)
            paths.Add($"Assets/Art/worlds_icons/{icon}.png");

        foreach (var p in paths) Process(p);
        Debug.Log($"[WhiteBg] {paths.Count} görselin beyaz arka planı temizlendi.");
    }

    [MenuItem("Tools/GET_IT/Remove BG - Surprised Mole")]
    public static void SurprisedMole()
    {
        Process("Assets/Resources/mole_mascot_surprised.png");
        Debug.Log("[WhiteBg] mole_mascot_surprised temizlendi.");
    }

    [MenuItem("Tools/GET_IT/Remove BG - Lock Icon")]
    public static void LockIcon()
    {
        Process("Assets/Resources/burrow_lock_icon.png", global: true);   // iç beyazları da sil
        Debug.Log("[WhiteBg] burrow_lock_icon temizlendi (global).");
    }

    // Yıldız görsellerinin beyaz arka planını şeffaf yap (kenardan flood-fill → yıldız şekli korunur).
    // Runtime Resources kopyalarını + Art kaynaklarını işler.
    [MenuItem("Tools/GET_IT/Remove BG - Stars")]
    public static void Stars()
    {
        Process("Assets/Resources/star_empty.png");
        Process("Assets/Resources/star_full_yellow.png");
        Process("Assets/Art/star_empty.png");
        Process("Assets/Art/star_full_yellow.png");
        Debug.Log("[WhiteBg] yıldız görsellerinin beyaz arka planı temizlendi.");
    }

    static void Process(string path, bool global = false)
    {
        var imp = AssetImporter.GetAtPath(path) as TextureImporter;
        if (imp == null) { Debug.LogWarning($"[WhiteBg] yok: {path}"); return; }

        bool wasReadable = imp.isReadable;
        var prevComp = imp.textureCompression;
        imp.isReadable = true;
        imp.textureCompression = TextureImporterCompression.Uncompressed;
        imp.SaveAndReimport();

        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        int W = tex.width, H = tex.height;
        var px = tex.GetPixels32();

        if (global)
        {
            // Tüm beyaz pikseller (kenara bağlı olmasa da) → şeffaf. Doygun/koyu sanat korunur.
            for (int i = 0; i < px.Length; i++) if (IsWhite(px[i])) px[i].a = 0;
        }
        else
        {
            var visited = new bool[W * H];
            var stack = new Stack<int>();
            void Seed(int x, int y)
            {
                int i = y * W + x;
                if (!visited[i] && IsWhite(px[i])) { visited[i] = true; stack.Push(i); }
            }
            for (int x = 0; x < W; x++) { Seed(x, 0); Seed(x, H - 1); }
            for (int y = 0; y < H; y++) { Seed(0, y); Seed(W - 1, y); }
            while (stack.Count > 0)
            {
                int i = stack.Pop(); px[i].a = 0;
                int x = i % W, y = i / W;
                if (x > 0) Seed(x - 1, y);
                if (x < W - 1) Seed(x + 1, y);
                if (y > 0) Seed(x, y - 1);
                if (y < H - 1) Seed(x, y + 1);
            }
        }

        // de-fringe (beyaz halka azalt)
        var aOut = new byte[W * H];
        for (int i = 0; i < px.Length; i++) aOut[i] = px[i].a;
        for (int y = 0; y < H; y++)
        for (int x = 0; x < W; x++)
        {
            int i = y * W + x;
            if (px[i].a == 0) continue;
            bool nearEmpty = (x > 0 && px[i - 1].a == 0) || (x < W - 1 && px[i + 1].a == 0) ||
                             (y > 0 && px[i - W].a == 0) || (y < H - 1 && px[i + W].a == 0);
            if (!nearEmpty) continue;
            int m = Mathf.Min(px[i].r, Mathf.Min(px[i].g, px[i].b));
            if (m > 200) aOut[i] = (byte)(px[i].a * Mathf.InverseLerp(245f, 200f, m));
        }
        for (int i = 0; i < px.Length; i++) px[i].a = aOut[i];

        var outTex = new Texture2D(W, H, TextureFormat.RGBA32, false);
        outTex.SetPixels32(px); outTex.Apply();
        File.WriteAllBytes(Path.Combine(Application.dataPath, path.Substring("Assets/".Length)), outTex.EncodeToPNG());
        Object.DestroyImmediate(outTex);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

        imp = AssetImporter.GetAtPath(path) as TextureImporter;
        imp.isReadable = wasReadable; imp.textureCompression = prevComp; imp.SaveAndReimport();
    }

    // Arka plan = açık (>210) VE düşük doygunluk (gri/beyaz). Renkli/koyu sanat korunur.
    static bool IsWhite(Color32 c)
    {
        if (c.r <= 210 || c.g <= 210 || c.b <= 210) return false;
        int max = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
        int min = Mathf.Min(c.r, Mathf.Min(c.g, c.b));
        return max - min < 16;
    }
}
