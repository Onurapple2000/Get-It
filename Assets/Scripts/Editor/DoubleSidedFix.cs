using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// ⚠️ MESHY MESH "DELİK" DÜZELTMESİ: Meshy'den gelen + decimate edilen modellerde bazı yüzeylerin normali TERS
/// (winding bozuk) veya ince/açık kabuk var. URP/Lit materyali `_Cull: 2` (arka yüz ELE) olduğundan bu ters
/// yüzler ELENİR → nesnenin İÇİ/arka planı görünür ("farklı renkte delik pixeller"), ışık oradan sızar VE gölge
/// caster pass'ı da aynı yüzü eleyince GÖLGEDE de delik olur. ÇÖZÜM: materyalleri ÇİFT TARAFLI yap (`_Cull: 0`).
/// URP/Lit ShadowCaster pass'ı da `Cull [_Cull]` kullandığından hem yüzey hem GÖLGE deliği kapanır.
///
/// Kapsam: PowerUps (tüm dünyalarda ortak) + Drinks. Beğenilirse diğer Meshy dünyaları (foods/cars/…) da eklenir.
/// Menü: Tools/GET_IT/Fix Double-Sided Materials (Drinks + PowerUps).
/// </summary>
public static class DoubleSidedFix
{
    static readonly string[] Dirs =
    {
        "Assets/Prefabs/PowerUps",
        "Assets/Prefabs/Drinks",
    };

    /// <summary>PERF GERİ ALMA: çift-taraflılığı KAPAT (tek-taraflı = arka yüz elenir → kalabalık sahnede ~yarı üçgen).
    /// Meshler watertight olduğundan görsel bozulmaz; erken "see-through" aslında bias/mip idi (çözüldü).</summary>
    [MenuItem("Tools/GET_IT/Revert Double-Sided (back to single, perf)")]
    public static void Revert()
    {
        int changed = 0;
        foreach (var dir in Dirs)
        {
            if (!Directory.Exists(dir)) continue;
            foreach (var g in AssetDatabase.FindAssets("t:Material", new[] { dir }))
            {
                var mat = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(g));
                if (mat == null || !mat.HasProperty("_Cull")) continue;
                if (Mathf.Approximately(mat.GetFloat("_Cull"), (float)UnityEngine.Rendering.CullMode.Back)) continue;
                mat.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Back);
                mat.doubleSidedGI = false;
                EditorUtility.SetDirty(mat); changed++;
            }
        }
        AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        Debug.Log($"[DoubleSidedFix] Tek-taraflı GERİ ALINDI: {changed} materyal (perf).");
    }

    [MenuItem("Tools/GET_IT/Fix Double-Sided Materials (Drinks + PowerUps)")]
    public static void Run()
    {
        int changed = 0, scanned = 0;
        foreach (var dir in Dirs)
        {
            if (!Directory.Exists(dir)) continue;
            var guids = AssetDatabase.FindAssets("t:Material", new[] { dir });
            foreach (var g in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(g);
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null) continue;
                scanned++;
                if (!mat.HasProperty("_Cull")) continue;
                if (Mathf.Approximately(mat.GetFloat("_Cull"), (float)UnityEngine.Rendering.CullMode.Off)) continue;

                mat.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);   // 0 = çift taraflı (hem forward hem shadow)
                mat.doubleSidedGI = true;
                EditorUtility.SetDirty(mat);
                changed++;
            }
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[DoubleSidedFix] Tarandı {scanned} materyal, çift-taraflı yapıldı {changed}. (Delik/gölge sızıntısı düzelmeli.)");
    }

    /// <summary>
    /// Powerup'ların meshleri "watertight" değil (Meshy delikleri) → gölgeleri delik deşik. Küçük DÖNEN pickup
    /// oldukları için gerçekçi gölgeye ihtiyaçları yok → gölge CAST + RECEIVE kapatılır (temiz görünür, artefakt yok).
    /// </summary>
    [MenuItem("Tools/GET_IT/Disable Shadows on PowerUps")]
    public static void DisablePowerUpShadows()
    {
        int n = 0;
        var guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs/PowerUps" });
        foreach (var g in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(g);
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go == null) continue;
            bool any = false;
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
                any = true;
            }
            if (any) { EditorUtility.SetDirty(go); n++; }
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[DoubleSidedFix] {n} powerup prefabında gölge (cast+receive) KAPATILDI.");
    }

    /// <summary>
    /// TANI TESTİ: Drinks + PowerUps materyallerinde SPEKÜLER highlight + çevre yansımasını KAPAT. Parçalı/kavisli
    /// Meshy geometrisinde speküler kırılıp "fixed-to-mesh beyaz noktalar" yapıyorsa bu onları bitirir (gölgeden
    /// bağımsız). Keyword'ler de set edilir (URP float tek başına yetmez). Geri almak için: SetSpecular(true).
    /// </summary>
    [MenuItem("Tools/GET_IT/Kill Specular on Drinks + PowerUps")]
    public static void KillSpecular() => SetSpecular(false);

    [MenuItem("Tools/GET_IT/Restore Specular on Drinks + PowerUps")]
    public static void RestoreSpecular() => SetSpecular(true);

    static void SetSpecular(bool on)
    {
        int changed = 0;
        foreach (var dir in Dirs)
        {
            if (!Directory.Exists(dir)) continue;
            foreach (var g in AssetDatabase.FindAssets("t:Material", new[] { dir }))
            {
                var mat = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(g));
                if (mat == null) continue;
                if (mat.HasProperty("_SpecularHighlights")) mat.SetFloat("_SpecularHighlights", on ? 1f : 0f);
                if (mat.HasProperty("_EnvironmentReflections")) mat.SetFloat("_EnvironmentReflections", on ? 1f : 0f);
                SetKw(mat, "_SPECULARHIGHLIGHTS_OFF", !on);
                SetKw(mat, "_ENVIRONMENTREFLECTIONS_OFF", !on);
                EditorUtility.SetDirty(mat);
                changed++;
            }
        }
        AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        Debug.Log($"[DoubleSidedFix] Speküler/yansıma {(on ? "AÇILDI" : "KAPATILDI")}: {changed} materyal.");
    }

    static void SetKw(Material m, string kw, bool enable)
    {
        if (enable) m.EnableKeyword(kw); else m.DisableKeyword(kw);
    }

    /// <summary>Powerup gölgelerini GERİ AÇ (cast+receive on). Sorun renk grade'inden çıktıysa gölge kapatmaya gerek yok.</summary>
    [MenuItem("Tools/GET_IT/Enable Shadows on PowerUps")]
    public static void EnablePowerUpShadows()
    {
        int n = 0;
        foreach (var g in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs/PowerUps" }))
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(g));
            if (go == null) continue;
            bool any = false;
            foreach (var rend in go.GetComponentsInChildren<Renderer>(true))
            { rend.shadowCastingMode = ShadowCastingMode.On; rend.receiveShadows = true; any = true; }
            if (any) { EditorUtility.SetDirty(go); n++; }
        }
        AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        Debug.Log($"[DoubleSidedFix] {n} powerup prefabında gölge GERİ AÇILDI (cast+receive on).");
    }

    /// <summary>Drinks receiveShadows GERİ AÇ (doğal gölge). Renk grade fix'i sonrası self-shadow hack'e gerek kalmazsa.</summary>
    [MenuItem("Tools/GET_IT/Enable Receive-Shadows on Drinks")]
    public static void EnableDrinksReceiveShadows()
    {
        int n = 0;
        foreach (var g in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs/Drinks" }))
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(g));
            if (go == null) continue;
            bool any = false;
            foreach (var rend in go.GetComponentsInChildren<Renderer>(true))
            { if (!rend.receiveShadows) { rend.receiveShadows = true; any = true; } }
            if (any) { EditorUtility.SetDirty(go); n++; }
        }
        AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        Debug.Log($"[DoubleSidedFix] {n} drinks prefabında receiveShadows GERİ AÇILDI.");
    }

    /// <summary>
    /// Drinks nesnelerinin ÜZERİNDEKİ beyaz noktalar (bozuk mesh → hatalı self-shadow / z-fighting) için:
    /// receiveShadows KAPAT → nesne kendi üzerine yanlış gölge düşürmez (beyaz noktalar gider). CAST açık kalır →
    /// yapılar (piramit/silindir vb.) yine YERE gölge düşürür (sahne derinliği korunur).
    /// </summary>
    [MenuItem("Tools/GET_IT/Disable Receive-Shadows on Drinks")]
    public static void DisableDrinksReceiveShadows()
    {
        int n = 0, r = 0;
        var guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs/Drinks" });
        foreach (var g in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(g);
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go == null) continue;
            bool any = false;
            foreach (var rend in go.GetComponentsInChildren<Renderer>(true))
            {
                if (rend.receiveShadows) { rend.receiveShadows = false; r++; any = true; }
            }
            if (any) { EditorUtility.SetDirty(go); n++; }
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[DoubleSidedFix] {n} drinks prefabında receiveShadows KAPATILDI ({r} renderer). Cast (yere gölge) açık kaldı.");
    }
}
