using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>
/// TANI DÜZENEĞİ — "beyaz nokta + gölge deliği" artefaktını EDİTÖRDE, OYUN MESAFESİNDE (nesne küçük) yeniden üretir
/// ve A/B karşılaştırmalar üretir. Geçici sahne kurar, PNG'leri scratchpad'e yazar, Claude görüntüleri inceler.
/// Sahneyi KAYDETMEZ. Menü: Tools/GET_IT/Capture Artifact Repro.
/// </summary>
public static class DeviceArtifactRepro
{
    const string OUT_DIR = "/private/tmp/claude-501/-Users-onur-Documents-GET-IT-Unity/58da8b83-13e6-421e-99c1-df73ead9823f/scratchpad";

    [MenuItem("Tools/GET_IT/Capture Artifact Repro")]
    public static void Run()
    {
        string prevScene = SceneManager.GetActiveScene().path;
        int prevQuality = QualitySettings.GetQualityLevel();

        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        try
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.transform.localScale = Vector3.one * 6f;
            ground.GetComponent<MeshRenderer>().sharedMaterial =
                new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = new Color(0.62f, 0.60f, 0.55f) };

            var lightGo = new GameObject("Sun");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.5f;
            light.shadows = LightShadows.Soft;
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            var camGo = new GameObject("Cam");
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 45f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.55f, 0.6f, 0.35f);   // drinks zemini gibi yeşilimsi (kontrast)
            // ⚠️ OYUNDAKİ POST-PROCESSING (Bloom!) — asıl fark buydu. GameScene'in global volume profilini ekle.
            var camData = camGo.AddComponent<UniversalAdditionalCameraData>();
            camData.renderPostProcessing = true;
            var volSrc = AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/Settings/SampleSceneProfile.asset");
            Bloom bloom = null;
            if (volSrc != null)
            {
                var volGo = new GameObject("GlobalVolume");
                var vol = volGo.AddComponent<Volume>();
                vol.isGlobal = true; vol.priority = 1f;
                vol.profile = Object.Instantiate(volSrc);   // runtime kopya (asset bozulmaz)
                vol.profile.TryGet(out bloom);
            }

            // ── A/B 1: MIKNATIS oyun mesafesinde. SOL = normal, SAĞ = speküler+yansıma KAPALI (temp materyal) ──
            // ── MIP TESTİ: KÜÇÜK mıknatıs (oyundaki gibi uzak), SOL=mip açık / SAĞ=mip kapalı doku ──
            var magPf0 = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/PowerUps/PowerMagnet.prefab");
            if (magPf0 != null)
            {
                var L = (GameObject)PrefabUtility.InstantiatePrefab(magPf0);
                var R = (GameObject)PrefabUtility.InstantiatePrefab(magPf0);
                Ground(L, new Vector3(-0.7f, 0f, 0f));
                Ground(R, new Vector3(0.7f, 0f, 0f));
                L.transform.rotation = R.transform.rotation = Quaternion.Euler(0f, 30f, 0f);
                NoMips(R);   // sağdakinin dokusunu mipsiz kopyayla değiştir
                // UZAK kamera → mıknatıs KÜÇÜK (oyundaki gibi, coarse mip devrede)
                camGo.transform.position = new Vector3(0f, 11f, -9.5f);
                camGo.transform.LookAt(new Vector3(0f, 0.1f, 0f));
                Capture(cam, 0, "mip_test_mobile.png");
                Object.DestroyImmediate(L); Object.DestroyImmediate(R);
            }

            var magPf = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/PowerUps/PowerMagnet.prefab");
            if (false && magPf != null)
            {
                // Tek mıknatıs, EĞİK açı (yaw 55 — speküler/parlak yüzey kameraya bakar). Kamera yakın-oyun mesafesi.
                var m = (GameObject)PrefabUtility.InstantiatePrefab(magPf);
                Ground(m, Vector3.zero);
                m.transform.rotation = Quaternion.Euler(0f, 55f, 0f);
                Bounds b = RBounds(m);
                float d = b.size.magnitude * 2.2f;
                camGo.transform.position = b.center + new Vector3(0f, d * 0.7f, -d * 0.72f);
                camGo.transform.LookAt(b.center);

                if (bloom != null)
                {
                    bloom.active = true; bloom.threshold.Override(1.0f);           // OYUNDAKİ AYAR
                    Capture(cam, 0, "bloom_t1_mobile.png");
                    Capture(cam, 1, "bloom_t1_pc.png");
                    bloom.threshold.Override(2.0f);                                // EŞİK YÜKSELT (aydınlık yüzey bloom'lamaz)
                    Capture(cam, 0, "bloom_t2_mobile.png");
                    bloom.active = false;                                          // BLOOM KAPALI
                    Capture(cam, 0, "bloom_off_mobile.png");
                    bloom.active = true; bloom.threshold.Override(1.0f);
                }
                // Ayrıca speküler kapalı + bloom açık (t1)
                var m2 = (GameObject)PrefabUtility.InstantiatePrefab(magPf);
                Ground(m2, Vector3.zero); m2.transform.rotation = Quaternion.Euler(0f, 55f, 0f);
                KillSpec(m2); m.SetActive(false);
                Capture(cam, 0, "bloom_t1_specoff_mobile.png");
                m.SetActive(true); Object.DestroyImmediate(m2);
                Object.DestroyImmediate(m);
            }

            // ── CAT: ANİMASYONLU (yürüme posası) düz zeminde, gölge ACNE'si bias testi (düşük vs yüksek) ──
            var catPf = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Cats/Cat.prefab");
            if (catPf != null)
            {
                var cat = (GameObject)PrefabUtility.InstantiatePrefab(catPf);
                // yürüme klibini bul + posala (skinned deformasyon = gerçek oyun koşulu)
                var clip = FindClip("Assets/Art/Meshy_AI_model_Animation_Walking_withSkin.glb");
                if (clip != null) { clip.SampleAnimation(cat, 0.35f); }
                Ground(cat, Vector3.zero);
                Bounds b = RBounds(cat);
                float d = b.size.magnitude * 1.5f;
                camGo.transform.position = b.center + new Vector3(0f, d * 0.9f, -d * 0.7f);
                camGo.transform.LookAt(b.center);

                // Işığı pipeline yerine KENDİ bias'ıyla sür → tek geçişte düşük/yüksek karşılaştır
                var ald = lightGo.GetComponent<UniversalAdditionalLightData>();
                if (ald == null) ald = lightGo.AddComponent<UniversalAdditionalLightData>();
                ald.usePipelineSettings = false;

                light.shadowBias = 0.3f; light.shadowNormalBias = 0.5f;    // eski (birkaç leak dot)
                Capture(cam, 0, "cat_nb05.png");
                light.shadowBias = 0.3f; light.shadowNormalBias = 0.25f;   // YENİ (leak dot azalmalı)
                Capture(cam, 0, "cat_nb025.png");
                Object.DestroyImmediate(cat);
            }

            Debug.Log("[Repro] yazıldı: mip_test + cat_bias_(lo|hi).png");
        }
        finally
        {
            QualitySettings.SetQualityLevel(prevQuality, true);
            if (!string.IsNullOrEmpty(prevScene)) EditorSceneManager.OpenScene(prevScene);
            else EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        }
    }

    static void Ground(GameObject go, Vector3 pos)
    {
        Bounds b = RBounds(go);
        go.transform.position = pos + Vector3.up * (pos.y - b.min.y);
    }

    static void KillSpec(GameObject go)
    {
        foreach (var r in go.GetComponentsInChildren<Renderer>(true))
        {
            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i] == null) continue;
                var m = new Material(mats[i]);
                if (m.HasProperty("_SpecularHighlights")) m.SetFloat("_SpecularHighlights", 0f);
                if (m.HasProperty("_EnvironmentReflections")) m.SetFloat("_EnvironmentReflections", 0f);
                m.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
                m.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
                mats[i] = m;
            }
            r.sharedMaterials = mats;
        }
    }

    // Nesnenin materyal dokularını MİPSİZ kopyayla değiştir (UV dikiş bleed'i mip'ten geliyorsa bu onu bitirir).
    static void NoMips(GameObject go)
    {
        foreach (var r in go.GetComponentsInChildren<Renderer>(true))
        {
            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i] == null) continue;
                var m = new Material(mats[i]);
                var src = m.HasProperty("_BaseMap") ? m.GetTexture("_BaseMap") as Texture2D : m.mainTexture as Texture2D;
                if (src != null)
                {
                    var full = AssetDatabase.GetAssetPath(src);
                    var readable = new Texture2D(src.width, src.height, TextureFormat.RGBA32, false);
                    // asset'ten oku (okunabilir olmayabilir → EncodeToPNG yerine Graphics.Blit ile RT'den al)
                    var rt = RenderTexture.GetTemporary(src.width, src.height);
                    Graphics.Blit(src, rt);
                    var prev = RenderTexture.active; RenderTexture.active = rt;
                    readable.ReadPixels(new Rect(0, 0, src.width, src.height), 0, 0); readable.Apply(false);
                    RenderTexture.active = prev; RenderTexture.ReleaseTemporary(rt);
                    if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", readable);
                    m.mainTexture = readable;
                }
                mats[i] = m;
            }
            r.sharedMaterials = mats;
        }
    }

    static AnimationClip FindClip(string glbPath)
    {
        foreach (var o in AssetDatabase.LoadAllAssetsAtPath(glbPath))
            if (o is AnimationClip c && !c.name.StartsWith("__preview")) return c;
        return null;
    }

    static Bounds RBounds(GameObject go)
    {
        var rs = go.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.one * 0.1f);
        var b = rs[0].bounds;
        for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
        return b;
    }

    static void Capture(Camera cam, int quality, string name)
    {
        QualitySettings.SetQualityLevel(quality, true);
        const int W = 1080, H = 1350;
        var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32);
        cam.targetTexture = rt;
        cam.Render();
        var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
        var prev = RenderTexture.active;
        RenderTexture.active = rt;
        tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
        tex.Apply();
        RenderTexture.active = prev;
        cam.targetTexture = null;
        string path = Path.Combine(OUT_DIR, name);
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(rt);
        Object.DestroyImmediate(tex);
    }
}
