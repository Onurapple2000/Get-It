using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// DALL-E kalp görsellerinin BEYAZ arka planını şeffaf yapar. Kenarlardan flood-fill ile yalnızca
/// "kenara bağlı beyaz" bölge silinir → kalbin İÇİNDEKİ beyaz parlamalar korunur. Hafif de-fringe ile
/// beyaz halka azaltılır. Menu: Tools/GET_IT/Remove Heart Backgrounds
/// </summary>
public static class HeartBgRemover
{
    static readonly string[] Hearts = { "heart_full", "heart_empty", "heart_full_plus1" };
    const string DIR = "Assets/Resources/Hearts";

    [MenuItem("Tools/GET_IT/Remove Heart Backgrounds")]
    public static void Run()
    {
        foreach (var name in Hearts)
        {
            string path = $"{DIR}/{name}.png";
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp == null) { Debug.LogWarning($"[HeartBg] yok: {path}"); continue; }

            bool wasReadable = imp.isReadable;
            var prevComp = imp.textureCompression;
            imp.isReadable = true;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.SaveAndReimport();

            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            int W = tex.width, H = tex.height;
            var px = tex.GetPixels32();

            // 1) Kenardan flood-fill: kenara bağlı beyaz pikselleri şeffaf yap
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
                int i = stack.Pop();
                px[i].a = 0;
                int x = i % W, y = i / W;
                if (x > 0)      Seed(x - 1, y);
                if (x < W - 1)  Seed(x + 1, y);
                if (y > 0)      Seed(x, y - 1);
                if (y < H - 1)  Seed(x, y + 1);
            }

            // 2) De-fringe: şeffaf komşusu olan beyazımsı kenar piksellerinin alfasını kırp (beyaz halka azalt)
            var aOut = new byte[W * H];
            for (int i = 0; i < px.Length; i++) aOut[i] = px[i].a;
            for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                int i = y * W + x;
                if (px[i].a == 0) continue;
                bool nearEmpty =
                    (x > 0 && px[i - 1].a == 0) || (x < W - 1 && px[i + 1].a == 0) ||
                    (y > 0 && px[i - W].a == 0) || (y < H - 1 && px[i + W].a == 0);
                if (!nearEmpty) continue;
                int m = Mathf.Min(px[i].r, Mathf.Min(px[i].g, px[i].b));
                if (m > 200) aOut[i] = (byte)(px[i].a * Mathf.InverseLerp(245f, 200f, m)); // beyaza yakınsa sönükleştir
            }
            for (int i = 0; i < px.Length; i++) px[i].a = aOut[i];

            // ÖNEMLİ: kaynak PNG RGB (alfasız). Alfa yazabilmek için YENİ bir RGBA32 texture'a bas.
            var outTex = new Texture2D(W, H, TextureFormat.RGBA32, false);
            outTex.SetPixels32(px);
            outTex.Apply();
            File.WriteAllBytes(Path.Combine(Application.dataPath, path.Substring("Assets/".Length)), outTex.EncodeToPNG());
            Object.DestroyImmediate(outTex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            imp = AssetImporter.GetAtPath(path) as TextureImporter;
            imp.isReadable = wasReadable;
            imp.textureCompression = prevComp;
            imp.SaveAndReimport();
            Debug.Log($"[HeartBg] temizlendi: {name}");
        }
        Debug.Log("[HeartBg] Tüm kalplerin beyaz arka planı temizlendi.");
    }

    static bool IsWhite(Color32 c) => c.r > 232 && c.g > 232 && c.b > 232;
}
