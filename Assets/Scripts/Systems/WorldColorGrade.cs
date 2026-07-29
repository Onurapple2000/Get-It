using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Dünya-bazlı RENK CANLILIĞI (post-processing). Bazı dünyalarda (özellikle İçecekler) modeller/zemin SOLUK
/// görünüyor; tek tek malzeme düzeltmek yerine EKRAN GENELİNDE ucuz bir Color Adjustments (doygunluk + hafif
/// kontrast/pozlama) uygulanır → tüm nesneler + zemin bir arada canlanır.
///
/// - Global bir Volume runtime'da oluşturulur (yüksek öncelik). Profil koda gömülüdür (sahne asset'i bozulmaz).
/// - ColorAdjustments varyantı `Settings/DefaultVolumeProfile` (URP global) içinde ZATEN referanslı → shader
///   build'de STRIP EDİLMEZ (cihazda güvenli; geçmişteki strip-crash riski yok).
/// - Ayarlar dünya-bazlı: yalnız İSTENEN dünyada (şu an İçecekler=5) uygulanır; diğerleri DEĞİŞMEZ. Beğenilirse
///   `Grade` tablosuna başka dünyalar eklenerek yaygınlaştırılır.
///
/// Kurulum: sahneye eklemeye gerek yok — LevelManager.Start çağırır (Apply). İdempotent (tek örnek).
/// </summary>
public static class WorldColorGrade
{
    struct G { public float sat, con, exp; public G(float s, float c, float e) { sat = s; con = c; exp = e; } }

    // Dünya → (doygunluk, kontrast, pozlama). Listede olmayan dünya = değişiklik yok (nötr).
    static G GradeFor(int world)
    {
        switch (world)
        {
            // İçecekler: soluk → canlı. ⚠️ SADECE DOYGUNLUK. contrast/exposure KULLANMA: sahnede tonemapping
            // olmadığından bunlar parlak speküler pikselleri beyaza CLIP eder → nesnelerde "beyaz nokta" artefaktı
            // (kullanıcı tanısı). Doygunluk highlight'ı parlatmaz → güvenli canlılık.
            case 5: return new G(18f, 0f, 0f);
            default: return new G(0f, 0f, 0f);      // diğer dünyalar dokunulmaz
        }
    }

    static GameObject go;

    public static void Apply(int world)
    {
        var g = GradeFor(world);
        bool neutral = Mathf.Approximately(g.sat, 0f) && Mathf.Approximately(g.con, 0f) && Mathf.Approximately(g.exp, 0f);

        if (go == null)
        {
            if (neutral) return;                       // gerekmiyorsa hiç kurma
            go = new GameObject("_WorldColorGrade");
            Object.DontDestroyOnLoad(go);
        }

        var vol = go.GetComponent<Volume>();
        if (vol == null) { vol = go.AddComponent<Volume>(); vol.isGlobal = true; vol.priority = 100f; }
        vol.enabled = !neutral;
        if (neutral) return;

        if (vol.profile == null) vol.profile = ScriptableObject.CreateInstance<VolumeProfile>();
        if (!vol.profile.TryGet(out ColorAdjustments ca)) ca = vol.profile.Add<ColorAdjustments>(true);
        ca.saturation.Override(g.sat);
        ca.contrast.Override(g.con);
        ca.postExposure.Override(g.exp);
    }
}
