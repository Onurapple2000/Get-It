using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Güç-up GLB/prefablarından şeffaf PNG ikon üretir (IconRenderer ile 3/4 render) → Assets/Art/PowerUps/ + Resources/.
/// Oyun-içi envanter HUD'ı (PowerUpInventoryHud) bunları Resources'tan yükler. Süper'in PNG'si zaten var.
/// Menü: Tools/GET_IT/Create PowerUp Icons (glb→png).
/// </summary>
public static class PowerUpIconMaker
{
    [MenuItem("Tools/GET_IT/Create PowerUp Icons")]
    public static void Run()
    {
        var jobs = new (string prefab, string name)[]
        {
            ("Assets/Prefabs/PowerUps/PowerSpeedHQ.prefab",  "power_speed"),
            ("Assets/Prefabs/PowerUps/PowerMagnetHQ.prefab", "power_magnet"),
            ("Assets/Prefabs/PowerUps/PowerGrowHQ.prefab",   "power_grow"),
        };
        const string artDir = "Assets/Art/PowerUps", resDir = "Assets/Resources";
        Directory.CreateDirectory(resDir);

        // İkonlar PARLAK çıksın: geçici güçlü yön ışığı + yüksek ambient (sahne ışığı loşsa ikonlar mat kalıyordu).
        var lightGo = new GameObject("~IconLight");
        var light = lightGo.AddComponent<Light>();
        light.type = LightType.Directional; light.intensity = 2.0f; light.color = Color.white;
        lightGo.transform.rotation = Quaternion.Euler(38f, -28f, 0f);
        var pAmbMode = RenderSettings.ambientMode; var pAmb = RenderSettings.ambientLight; var pAmbI = RenderSettings.ambientIntensity;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.75f, 0.75f, 0.75f); RenderSettings.ambientIntensity = 1f;

        int made = 0;
        foreach (var j in jobs)
        {
            var pf = AssetDatabase.LoadAssetAtPath<GameObject>(j.prefab);
            if (pf == null) { Debug.LogWarning($"[PowIcon] prefab yok: {j.prefab}"); continue; }
            var sp = IconRenderer.Render(pf, 256);
            if (sp == null || sp.texture == null) { Debug.LogWarning($"[PowIcon] render başarısız: {j.name}"); continue; }
            byte[] png = sp.texture.EncodeToPNG();
            File.WriteAllBytes($"{artDir}/{j.name}.png", png);
            File.WriteAllBytes($"{resDir}/{j.name}.png", png);
            made++;
        }
        Object.DestroyImmediate(lightGo);
        RenderSettings.ambientMode = pAmbMode; RenderSettings.ambientLight = pAmb; RenderSettings.ambientIntensity = pAmbI;

        AssetDatabase.Refresh();
        Debug.Log($"[PowIcon] {made} güç-up ikonu üretildi (Art/PowerUps + Resources).");
    }
}
