using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// ⚠️ BEYAZ BENEK KÖK NEDEN FIX'İ — UV DİKİŞ SIZINTISI (mip bleed).
/// Meshy atlas dokularında UV parçaları BEMBEYAZ arka plan üstünde ve kenar payı (dilation) YOK. Mip üretimi +
/// bilinear küçültme + ASTC, parça kenar piksellerini arka planla ORTALAR → nesne yüzeyinde UV dikişleri boyunca
/// mesh'e sabit BEYAZ benekler ("delikmiş, ışık geçiyormuş gibi"). Cihazda daha kötü (RenderScale 0.8 = 1 mip
/// derin). 1024→512 küçültmemiz sızmayı ayrıca artırdı — "sonradan ortaya çıktı" bunun için.
///
/// ÇÖZÜM: her prefabın mesh UV üçgenleri doku boyutunda RASTERİZE edilir (kaplama maskesi), arka plan pikselleri
/// en yakın kaplanan pikselin rengiyle DOLDURULUR (BFS dilation, tüm arka plan dolar) → mip/ASTC artık parça
/// rengiyle ortalar, beyaz sızmaz. Görsel değişiklik YOK (UV içi piksellere dokunulmaz). İdempotent.
/// Menü: Tools/GET_IT/Dilate Model Textures (fix white seams).
/// </summary>
public static class TextureDilate
{
    static readonly string[] PrefabDirs =
    {
        "Assets/Prefabs/PowerUps", "Assets/Prefabs/Drinks", "Assets/Prefabs/Cats",
        "Assets/Prefabs/Foods", "Assets/Prefabs/Cars", "Assets/Prefabs/Buildings",
        "Assets/Prefabs/Sweets",
    };

    [MenuItem("Tools/GET_IT/Dilate Model Textures (fix white seams)")]
    public static void Run()
    {
        // doku yolu → o dokuyu kullanan (mesh, submesh) listesi
        var texMeshes = new Dictionary<string, List<(Mesh mesh, int sub)>>();

        foreach (var dir in PrefabDirs)
        {
            if (!AssetDatabase.IsValidFolder(dir)) continue;
            foreach (var g in AssetDatabase.FindAssets("t:Prefab", new[] { dir }))
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(g));
                if (go == null) continue;
                foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                {
                    Mesh mesh = r is SkinnedMeshRenderer smr ? smr.sharedMesh
                              : r.TryGetComponent(out MeshFilter mf) ? mf.sharedMesh : null;
                    if (mesh == null) continue;
                    var mats = r.sharedMaterials;
                    for (int s = 0; s < mesh.subMeshCount && s < mats.Length; s++)
                    {
                        if (mats[s] == null) continue;
                        var tex = mats[s].HasProperty("_BaseMap") ? mats[s].GetTexture("_BaseMap") : mats[s].mainTexture;
                        if (tex == null) continue;
                        string tp = AssetDatabase.GetAssetPath(tex);
                        if (string.IsNullOrEmpty(tp) || !tp.EndsWith(".png")) continue;
                        if (!texMeshes.TryGetValue(tp, out var list)) texMeshes[tp] = list = new List<(Mesh, int)>();
                        list.Add((mesh, s));
                    }
                }
            }
        }

        int done = 0, skipped = 0;
        foreach (var kv in texMeshes)
        {
            try
            {
                if (Dilate(kv.Key, kv.Value)) done++; else skipped++;
            }
            catch (System.Exception e) { Debug.LogWarning($"[TexDilate] HATA {kv.Key}: {e.Message}"); }
            if ((done + skipped) % 25 == 0) Debug.Log($"[TexDilate] ilerleme {done + skipped}/{texMeshes.Count}");
        }
        AssetDatabase.Refresh();
        Debug.Log($"[TexDilate] BİTTİ: {done} doku dolduruldu, {skipped} atlandı (kaplama zaten tam). Toplam {texMeshes.Count}.");
    }

    static bool Dilate(string path, List<(Mesh mesh, int sub)> users)
    {
        string full = Path.GetFullPath(path);
        var raw = File.ReadAllBytes(full);
        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!tex.LoadImage(raw)) { Debug.LogWarning($"[TexDilate] okunamadı: {path}"); return false; }
        int w = tex.width, h = tex.height;
        var px = tex.GetPixels32();

        // 1) UV üçgenlerini rasterize et → kaplama maskesi
        var covered = new bool[w * h];
        foreach (var (mesh, sub) in users)
        {
            var uv = mesh.uv;
            if (uv == null || uv.Length == 0) continue;
            var tris = mesh.GetTriangles(Mathf.Min(sub, mesh.subMeshCount - 1));
            for (int t = 0; t < tris.Length; t += 3)
                RasterTri(covered, w, h, uv[tris[t]], uv[tris[t + 1]], uv[tris[t + 2]]);
        }

        int covCount = 0;
        foreach (var c in covered) if (c) covCount++;
        if (covCount == 0) { Debug.LogWarning($"[TexDilate] UV kaplaması çıkarılamadı: {path}"); return false; }
        if (covCount >= w * h) return false;   // arka plan yok → gerek yok

        // 2) BFS dilation: kaplanan piksellerden dışa doğru renk yay (tüm arka plan dolana dek)
        var queue = new Queue<int>(covCount / 2);
        for (int i = 0; i < covered.Length; i++) if (covered[i]) queue.Enqueue(i);
        int[] dx = { 1, -1, 0, 0, 1, 1, -1, -1 };
        int[] dy = { 0, 0, 1, -1, 1, -1, 1, -1 };
        while (queue.Count > 0)
        {
            int i = queue.Dequeue();
            int x = i % w, y = i / w;
            for (int n = 0; n < 8; n++)
            {
                int nx = x + dx[n], ny = y + dy[n];
                if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                int j = ny * w + nx;
                if (covered[j]) continue;
                px[j] = px[i];
                covered[j] = true;
                queue.Enqueue(j);
            }
        }

        tex.SetPixels32(px);
        File.WriteAllBytes(full, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path);   // mip'ler dolgulu dokudan yeniden üretilir
        return true;
    }

    // Basit üçgen rasterizasyonu (bbox + barycentric). UV'ler frac ile 0-1'e sarılır; kenar payı için pikselin
    // merkezi yerine 4 köşesinden biri bile içindeyse kapla (konservatif — dikiş pikselleri kesin dahil olsun).
    static void RasterTri(bool[] covered, int w, int h, Vector2 a, Vector2 b, Vector2 c)
    {
        a = Frac(a); b = Frac(b); c = Frac(c);
        float minX = Mathf.Min(a.x, Mathf.Min(b.x, c.x)) * w - 1f;
        float maxX = Mathf.Max(a.x, Mathf.Max(b.x, c.x)) * w + 1f;
        float minY = Mathf.Min(a.y, Mathf.Min(b.y, c.y)) * h - 1f;
        float maxY = Mathf.Max(a.y, Mathf.Max(b.y, c.y)) * h + 1f;
        int x0 = Mathf.Max(0, (int)minX), x1 = Mathf.Min(w - 1, (int)maxX);
        int y0 = Mathf.Max(0, (int)minY), y1 = Mathf.Min(h - 1, (int)maxY);

        Vector2 A = new Vector2(a.x * w, a.y * h), B = new Vector2(b.x * w, b.y * h), C = new Vector2(c.x * w, c.y * h);
        float det = (B.y - C.y) * (A.x - C.x) + (C.x - B.x) * (A.y - C.y);
        if (Mathf.Abs(det) < 1e-8f) return;   // dejenere
        float inv = 1f / det;

        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                float l0 = ((B.y - C.y) * (p.x - C.x) + (C.x - B.x) * (p.y - C.y)) * inv;
                float l1 = ((C.y - A.y) * (p.x - C.x) + (A.x - C.x) * (p.y - C.y)) * inv;
                float l2 = 1f - l0 - l1;
                const float eps = -0.02f;   // hafif konservatif (kenar piksellerini dahil et)
                if (l0 >= eps && l1 >= eps && l2 >= eps) covered[y * w + x] = true;
            }
    }

    static Vector2 Frac(Vector2 v) => new Vector2(v.x - Mathf.Floor(v.x), v.y - Mathf.Floor(v.y));
}
