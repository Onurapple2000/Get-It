using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// PLAY MODUNDA gerçek oyun kamerasından yüksek çözünürlüklü PNG yakalar → Claude birebir kullanıcının gördüğünü
/// inceler (sentetik repro gerçek koşulu ıskalayınca). Kullanım: Play'e gir, şüpheli nesneye (mıknatıs/kedi) yaklaş,
/// menüyü çalıştır. Çıktı: scratchpad/game_view.png. Menü: Tools/GET_IT/Capture Game View (Play mode).
/// </summary>
public static class GameViewCapture
{
    const string OUT = "/private/tmp/claude-501/-Users-onur-Documents-GET-IT-Unity/58da8b83-13e6-421e-99c1-df73ead9823f/scratchpad/game_view.png";

    // EDIT-MODE yakalama: Play gerekmez — sahnedeki ana kameradan render (landmark önizlemesi vb. incelemek için).
    const string OUT2 = "/private/tmp/claude-501/-Users-onur-Documents-GET-IT-Unity/ba5ac354-2434-4661-ae2f-83a1ef1213ea/scratchpad/edit_view.png";
    [MenuItem("Tools/GET_IT/Capture Camera (edit mode)")]
    public static void RunEdit() => Capture(OUT2);

    [MenuItem("Tools/GET_IT/Capture Game View (Play mode)")]
    public static void Run()
    {
        if (!Application.isPlaying) { Debug.LogWarning("[GameViewCapture] Önce PLAY moduna gir, nesneye yaklaş, sonra çalıştır."); return; }
        Capture(OUT);
    }

    static void Capture(string outPath)
    {

        var cam = Camera.main;
        if (cam == null)
        {
            var cams = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
            foreach (var c in cams) if (c.isActiveAndEnabled && c.targetTexture == null) { cam = c; break; }
        }
        if (cam == null) { Debug.LogWarning("[GameViewCapture] Aktif kamera bulunamadı."); return; }

        int W = 1080, H = 1920;
        var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 };
        var prevTarget = cam.targetTexture;
        cam.targetTexture = rt;
        cam.Render();
        cam.targetTexture = prevTarget;

        var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
        var prevActive = RenderTexture.active;
        RenderTexture.active = rt;
        tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
        tex.Apply();
        RenderTexture.active = prevActive;

        Directory.CreateDirectory(Path.GetDirectoryName(outPath));
        File.WriteAllBytes(outPath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        Object.DestroyImmediate(rt);
        Debug.Log($"[GameViewCapture] Yakalandı → {outPath} (kamera: {cam.name})");
    }
}
