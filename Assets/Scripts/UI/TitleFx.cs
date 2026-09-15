using UnityEngine;
using TMPro;

/// <summary>
/// "GET IT" logosu için çizgi-film stili yazı efekti (2026-09-15 görsel cila): dikey renk geçişi (sarı→turuncu),
/// kalın koyu kontur, alt gölge, harf başına hafif eğim/boyut farkı ve yumuşak "zıplama" (idle) animasyonu.
/// Ayrı görsel gerektirmez; TMP mesh'ini her karede günceller (6 harf → maliyeti önemsiz).
/// İstenirse yerine Meshy/AI logo PNG'si konabilir (bkz. MainMenuController.RefreshHome).
/// </summary>
[RequireComponent(typeof(TMP_Text))]
public class TitleFx : MonoBehaviour
{
    [Tooltip("Harf başına maksimum eğim (derece). Sırayla +/- uygulanır.")]
    public float tiltDeg = 6f;
    [Tooltip("Zıplama genliği (piksel) ve hızı.")]
    public float bobAmp = 6f, bobSpeed = 2.2f;
    [Tooltip("Harfler arası faz farkı (dalga hissi).")]
    public float phaseStep = 0.55f;
    [Tooltip("Kalp gibi atsın: tüm yazı 'tum-tum' ritmiyle büyüyüp küçülür (TEBRİKLER!).")]
    public bool heartbeat = false;
    public float heartbeatBpm = 72f, heartbeatAmp = 0.08f;
    [Tooltip("Kontur kalınlığı (0-0.5). Büyük değer = daha kalın/ağır yazı.")]
    public float outlineWidth = 0.28f;
    [Tooltip("Harf gövdesini şişirir (kalınlaştırır), 0-0.3.")]
    public float faceDilate = 0.12f;

    TMP_Text t;

    // Materyal/gradient ayarı Start'ta: AddComponent sonrası dışarıdan atanan alanlar (outlineWidth, heartbeat…)
    // Awake'te henüz set edilmemiş olurdu.
    void Awake() { t = GetComponent<TMP_Text>(); }

    void Start()
    {

        // Renk geçişi: üst açık sarı → alt turuncu (köstebek/ahşap paletiyle uyumlu)
        t.enableVertexGradient = true;
        t.colorGradient = new VertexGradient(
            new Color(1.00f, 0.96f, 0.62f), new Color(1.00f, 0.96f, 0.62f),
            new Color(1.00f, 0.62f, 0.18f), new Color(1.00f, 0.62f, 0.18f));
        t.color = Color.white;
        t.characterSpacing = 6f;

        // Kontur + alt gölge (materyal örneği → paylaşılan font materyali bozulmaz)
        var m = t.fontMaterial;
        m.EnableKeyword("OUTLINE_ON");
        m.SetFloat(ShaderUtilities.ID_OutlineWidth, outlineWidth);
        m.SetFloat(ShaderUtilities.ID_FaceDilate, faceDilate);   // gövde şişirme → daha kalın harf
        m.SetColor(ShaderUtilities.ID_OutlineColor, new Color(0.30f, 0.14f, 0.04f));
        m.EnableKeyword("UNDERLAY_ON");
        m.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(0f, 0f, 0f, 0.55f));
        m.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0.6f);
        m.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -0.8f);
        m.SetFloat(ShaderUtilities.ID_UnderlayDilate, 0.15f);
        m.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0.25f);
        t.fontMaterial = m;
        t.UpdateMeshPadding();   // kontur/gölge kırpılmasın
    }

    void LateUpdate()
    {
        if (t == null) return;
        t.ForceMeshUpdate();
        var info = t.textInfo;
        if (info == null || info.characterCount == 0) return;

        float time = Time.unscaledTime;
        // Kalp atışı: bir vuruşta iki tepe ("tum-tum"), sonra dinlenme — gerçek nabız eğrisine benzer.
        float beat = 1f;
        if (heartbeat)
        {
            float ph = (time * heartbeatBpm / 60f) % 1f;
            float p1 = Mathf.Exp(-Mathf.Pow((ph - 0.08f) / 0.06f, 2f));
            float p2 = Mathf.Exp(-Mathf.Pow((ph - 0.28f) / 0.07f, 2f)) * 0.7f;
            beat = 1f + heartbeatAmp * (p1 + p2);
        }
        int visible = 0;
        for (int i = 0; i < info.characterCount; i++)
        {
            var ch = info.characterInfo[i];
            if (!ch.isVisible) continue;
            int mi = ch.materialReferenceIndex, vi = ch.vertexIndex;
            var verts = info.meshInfo[mi].vertices;

            // Harfin merkezi etrafında: sırayla ± eğim, hafif boyut farkı, dalga şeklinde zıplama
            Vector3 c = (verts[vi] + verts[vi + 2]) * 0.5f;
            float tilt = (visible % 2 == 0 ? 1f : -1f) * tiltDeg;
            float bob = Mathf.Sin(time * bobSpeed + visible * phaseStep) * bobAmp;
            float scale = (1f + 0.04f * Mathf.Sin(time * bobSpeed * 0.7f + visible * phaseStep)) * beat;
            var mtx = Matrix4x4.TRS(new Vector3(0f, bob, 0f), Quaternion.Euler(0f, 0f, tilt), Vector3.one * scale);
            for (int k = 0; k < 4; k++) verts[vi + k] = mtx.MultiplyPoint3x4(verts[vi + k] - c) + c;
            visible++;
        }
        for (int m = 0; m < info.meshInfo.Length; m++)
        {
            info.meshInfo[m].mesh.vertices = info.meshInfo[m].vertices;
            t.UpdateGeometry(info.meshInfo[m].mesh, m);
        }
    }
}
