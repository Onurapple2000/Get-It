using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Bir sahne nesnesinin/prefabın runtime'da 3/4 görünüm thumbnail'ını çıkarır → şeffaf arkaplanlı Sprite.
/// HUD hedef ikonları ve ghost'lar için kullanılır (manuel ikon asset'i gerekmez). URP'de
/// senkron çekim için RenderPipeline.SubmitRenderRequest kullanır (Unity 6).
/// </summary>
public static class IconRenderer
{
    // İzole sahnelemek için uzak konum + ayrı layer (ana kamera bunu görmez: frustum dışı + cull).
    const int   IconLayer = 31;
    static readonly Vector3 Stage = new Vector3(7777f, 7777f, 7777f);

    public static Sprite Render(GameObject source, int size = 160)
    {
        if (source == null) return null;

        // Görsel-only kopya
        var copy = Object.Instantiate(source);
        StripNonVisual(copy);
        copy.transform.position = Stage;
        copy.transform.rotation = Quaternion.identity;
        copy.transform.localScale = source.transform.lossyScale;
        SetLayer(copy, IconLayer);

        var rends = copy.GetComponentsInChildren<Renderer>();
        if (rends.Length == 0) { Object.DestroyImmediate(copy); return null; }
        Bounds b = rends[0].bounds;
        for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
        float radius = Mathf.Max(0.05f, b.extents.magnitude);

        // Kamera (ortografik, 3/4 ön-üst-sağ, şeffaf arkaplan)
        var camGo = new GameObject("~IconCam");
        camGo.transform.position = Stage;
        var cam = camGo.AddComponent<Camera>();
        cam.enabled = false;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(1f, 0f, 1f, 0f);   // magenta chroma-key (URP alpha'yı 1'e zorlar → key'lenir)
        cam.cullingMask = 1 << IconLayer;
        cam.orthographic = true;
        cam.orthographicSize = radius * 1.05f;
        // 2026-07-25 (kullanıcı): tabela resmi, nesnenin OYUNDAKİ görünüşüyle BİREBİR aynı olmalı. Oyun kamerası
        // HoleCamera 45° yukarıdan, -Z tarafından bakar → Euler(45,0,0) forward = (0,-sin45,cos45). İkon kamerasını
        // AYNI açıya kur (eski (0.5,0.42,-1) ALTTAN bakıyordu → kafa karışıklığı). Nesne identity yaw'la spawn
        // görünümüyle eşleşir.
        const float camAngle = 45f;   // HoleCamera.angle ile aynı
        Vector3 dir = Quaternion.Euler(camAngle, 0f, 0f) * Vector3.forward;   // yukarıdan-arkadan bakış (oyunla aynı)
        cam.transform.position = b.center - dir * (radius * 4f + 1f);
        cam.transform.rotation = Quaternion.Euler(camAngle, 0f, 0f);          // LookAt yerine oyun kamerasıyla birebir
        cam.nearClipPlane = 0.01f;
        cam.farClipPlane = radius * 10f + 5f;

        // IŞIK: 2026-07-25 (kullanıcı) — ikon, sahnedeki nesneyle AYNI ton/parlaklıkta olmalı. Eskiden 3 ekstra
        // parlak ışık (1.6+0.9+0.6) ekleniyordu → sahnenin tek yön ışığından (1.5) çok daha aydınlık çıkıyordu.
        // Artık EK IŞIK YOK: sahne yön ışığı (cullingMask=Everything → layer 31 dahil) + global ambient ikon
        // kopyasını da aynen aydınlatır → sahne nesneleriyle birebir eşleşir (aynı açı + aynı ışık zaten var).

        // Render (senkron, URP)
        var rt = new RenderTexture(size, size, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
        var req = new RenderPipeline.StandardRequest { destination = rt };
        if (RenderPipeline.SupportsRenderRequest(cam, req))
            RenderPipeline.SubmitRenderRequest(cam, req);
        else { cam.targetTexture = rt; cam.Render(); cam.targetTexture = null; }

        var prev = RenderTexture.active;
        RenderTexture.active = rt;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.ReadPixels(new Rect(0, 0, size, size), 0, 0);
        RenderTexture.active = prev;

        // Chroma-key: SADECE saf magenta arka planı şeffaf yap (nesne piksellerine DOKUNMA → koyulaşma yok).
        // Eşik dar (r,b>185 & g<70) → kırmızı/mor/pembe/mavi yiyecekler etkilenmez.
        var px = tex.GetPixels32();
        for (int i = 0; i < px.Length; i++)
        {
            var c = px[i];
            if (c.r > 185 && c.b > 185 && c.g < 70) px[i] = new Color32(0, 0, 0, 0);
        }
        tex.SetPixels32(px);
        tex.Apply();

        Object.DestroyImmediate(rt);
        Object.DestroyImmediate(camGo);
        Object.DestroyImmediate(copy);

        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    static void StripNonVisual(GameObject go)
    {
        foreach (var c in go.GetComponentsInChildren<MonoBehaviour>(true)) Object.DestroyImmediate(c);
        foreach (var rb in go.GetComponentsInChildren<Rigidbody>(true)) Object.DestroyImmediate(rb);
        foreach (var col in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(col);
    }

    static void SetLayer(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform t in go.transform) SetLayer(t.gameObject, layer);
    }
}
