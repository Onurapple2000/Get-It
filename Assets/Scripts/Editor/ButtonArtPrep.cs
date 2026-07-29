using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Buton görsellerini UI için hazırlar: beyaz arka planı şeffaf yapar (kenardan flood-fill), opak alana
/// kırpar, Sprite'a çevirir; dikdörtgen butona 9-slice border verir (değişen boyutlarda bozulmadan uzasın).
/// Menu: Tools/GET_IT/Prep Button Art
/// </summary>
public static class ButtonArtPrep
{
    [MenuItem("Tools/GET_IT/Prep Button Art")]
    public static void Run()
    {
        Prep("Assets/Resources/burrow_button_empty_rect.png", true);
        Prep("Assets/Resources/burrow_button_empty_circle.png", false);
        Debug.Log("[ButtonArt] Butonlar hazırlandı (bg şeffaf + kırpıldı + 9-slice).");
    }

    static void Prep(string path, bool nineSlice)
    {
        var imp = AssetImporter.GetAtPath(path) as TextureImporter;
        if (imp == null) { Debug.LogWarning($"[ButtonArt] yok: {path}"); return; }
        imp.isReadable = true; imp.textureCompression = TextureImporterCompression.Uncompressed;
        imp.textureType = TextureImporterType.Default; imp.SaveAndReimport();

        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        int W = tex.width, H = tex.height;
        var px = tex.GetPixels32();

        // beyaz bg → şeffaf (kenardan flood-fill)
        var visited = new bool[W * H];
        var stack = new System.Collections.Generic.Stack<int>();
        void Seed(int x, int y) { int i = y * W + x; if (!visited[i] && IsWhite(px[i])) { visited[i] = true; stack.Push(i); } }
        for (int x = 0; x < W; x++) { Seed(x, 0); Seed(x, H - 1); }
        for (int y = 0; y < H; y++) { Seed(0, y); Seed(W - 1, y); }
        while (stack.Count > 0)
        {
            int i = stack.Pop(); px[i].a = 0;
            int x = i % W, y = i / W;
            if (x > 0) Seed(x - 1, y); if (x < W - 1) Seed(x + 1, y);
            if (y > 0) Seed(x, y - 1); if (y < H - 1) Seed(x, y + 1);
        }

        // opak sınır kutusu
        int minX = W, minY = H, maxX = -1, maxY = -1;
        for (int y = 0; y < H; y++)
        for (int x = 0; x < W; x++)
            if (px[y * W + x].a > 10)
            { if (x < minX) minX = x; if (x > maxX) maxX = x; if (y < minY) minY = y; if (y > maxY) maxY = y; }
        if (maxX < 0) { Debug.LogWarning($"[ButtonArt] opak alan yok: {path}"); return; }
        int pad = 4;
        minX = Mathf.Max(0, minX - pad); minY = Mathf.Max(0, minY - pad);
        maxX = Mathf.Min(W - 1, maxX + pad); maxY = Mathf.Min(H - 1, maxY + pad);
        int cw = maxX - minX + 1, ch = maxY - minY + 1;

        var outTex = new Texture2D(cw, ch, TextureFormat.RGBA32, false);
        var cpx = new Color32[cw * ch];
        for (int y = 0; y < ch; y++)
        for (int x = 0; x < cw; x++)
            cpx[y * cw + x] = px[(minY + y) * W + (minX + x)];
        outTex.SetPixels32(cpx); outTex.Apply();
        File.WriteAllBytes(Path.Combine(Application.dataPath, path.Substring("Assets/".Length)), outTex.EncodeToPNG());
        Object.DestroyImmediate(outTex);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

        // Sprite + (gerekirse) 9-slice border
        imp = AssetImporter.GetAtPath(path) as TextureImporter;
        imp.textureType = TextureImporterType.Sprite;
        imp.spriteImportMode = SpriteImportMode.Single;
        imp.isReadable = false;
        imp.textureCompression = TextureImporterCompression.Compressed;
        if (nineSlice)
        {
            int b = Mathf.RoundToInt(Mathf.Min(cw, ch) * 0.4f);   // köşe koruma payı
            imp.spriteBorder = new Vector4(b, b, b, b);
        }
        imp.SaveAndReimport();
        Debug.Log($"[ButtonArt] {Path.GetFileName(path)} → {cw}x{ch}" + (nineSlice ? $" border {Mathf.RoundToInt(Mathf.Min(cw,ch)*0.4f)}" : ""));
    }

    static bool IsWhite(Color32 c)
    {
        if (c.r <= 210 || c.g <= 210 || c.b <= 210) return false;
        int max = Mathf.Max(c.r, Mathf.Max(c.g, c.b)), min = Mathf.Min(c.r, Mathf.Min(c.g, c.b));
        return max - min < 16;
    }
}
