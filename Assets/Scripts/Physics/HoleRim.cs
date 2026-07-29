using UnityEngine;

/// <summary>
/// Deliğin ağzına dekoratif tuğla "kuyu ağzı" halkası. SADECE GÖRSEL — collider yok,
/// üst yüzey zeminle hizada (hafif yOffset z-fight için), nesneler üstünden takılmadan geçer.
/// Deliği takip eder ve büyüdükçe büyür (HoleFloor mantığı). Tuğla görünümü prosedürel doku ile.
/// Kurulum: boş GameObject (origin), bu component eklenir; HoleController otomatik bulunur.
/// </summary>
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class HoleRim : MonoBehaviour
{
    [Tooltip("Delik kaynağı. Boşsa otomatik bulunur.")]
    public HoleController hole;

    [Header("Halka")]
    [Tooltip("Halka bant genişliği (delik kenarından dışa).")]
    public float ringWidth = 0.6f;
    [Tooltip("Zeminden hafif yukarı (z-fight önler). Collider olmadığı için takılma yapmaz.")]
    public float yOffset = 0.02f;
    [Range(24, 256)] public int segments = 96;

    [Header("Tuğla görünümü")]
    [Tooltip("Bir doku tekrarının dünyadaki genişliği (küçük = küçük tuğlalar).")]
    public float texWorldSize = 0.8f;
    public Color brickColor  = new Color(0.70f, 0.32f, 0.22f);
    public Color mortarColor = new Color(0.20f, 0.17f, 0.15f);

    [Header("Güncelleme")]
    [Tooltip("Mesh yalnızca delik yarıçapı bu kadar değişince yeniden inşa edilir (pozisyon her kare takip edilir).")]
    public float radiusThreshold = 0.02f;
    public bool flipWinding = false;

    Mesh mesh;
    Rigidbody rb;
    float lastRadius = -1f;

    void Awake()
    {
        mesh = new Mesh { name = "HoleRimMesh" };
        mesh.MarkDynamic();
        GetComponent<MeshFilter>().sharedMesh = mesh;

        // HoleFloor ile BİREBİR aynı hareket mekanizması: kinematik Rigidbody + Interpolate, FixedUpdate'te
        // MovePosition. Böylece tuğla halka, deliğin carve'landığı HoleFloor ile aynı interpolasyon
        // zamanlamasında render edilir → hareket halinde kayıklık olmaz. (Collider yok; rb sadece
        // transform'u interpolasyonlu taşımak için.)
        rb = GetComponent<Rigidbody>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
        rb.interpolation = RigidbodyInterpolation.None;   // LateUpdate'te transform ile kare-başı sürülüyor (bkz. HoleFloor fix)

        var mr = GetComponent<MeshRenderer>();
        if (mr.sharedMaterial == null)
        {
            var sh = Shader.Find("Universal Render Pipeline/Lit");
            var mat = new Material(sh);
            mat.mainTexture = BuildBrickTexture();
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.08f);
            mr.sharedMaterial = mat;
        }
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    void Start()
    {
        if (hole == null) hole = FindFirstObjectByType<HoleController>();
        if (hole != null)
        {
            Vector3 hp = hole.transform.position;
            transform.position = new Vector3(hp.x, 0f, hp.z);
        }
        Rebuild();
    }

    // 2026-07-25 (yalpalama fix): Delik Update'te (kare-başı) hareket eder. Eskiden tuğla halka FixedUpdate'te
    // MovePosition+Interpolate ile takip ediyordu → render ~1 fizik-adımı geride kalıp deliği ARKADAN gecikmeli
    // takip ediyordu. Artık LateUpdate'te (delik Update'inden SONRA) transform.position ile BİREBİR senkron.
    void LateUpdate()
    {
        if (hole == null) { hole = FindFirstObjectByType<HoleController>(); if (hole == null) return; }
        Vector3 hp = hole.transform.position;
        transform.position = new Vector3(hp.x, 0f, hp.z);   // delikle birebir, gecikmesiz
        // Mesh yalnızca delik büyüyünce yeniden inşa edilir.
        float r = Mathf.Max(0.1f, hole.currentSize * 0.5f);
        if (Mathf.Abs(r - lastRadius) >= radiusThreshold) Rebuild();
    }

    // Mesh YEREL uzayda kurulur (origin = delik merkezi). Pozisyonu transform taşır.
    void Rebuild()
    {
        if (hole == null) return;
        float rIn = Mathf.Max(0.1f, hole.currentSize * 0.5f);
        float rOut = rIn + ringWidth;
        lastRadius = rIn;

        int n = segments;
        var verts = new Vector3[(n + 1) * 2];
        var uvs   = new Vector2[(n + 1) * 2];
        var tris  = new int[n * 6];

        float circ = 2f * Mathf.PI * ((rIn + rOut) * 0.5f);
        float tilesU = Mathf.Max(1f, Mathf.Round(circ / texWorldSize));
        float tilesV = Mathf.Max(1f, Mathf.Round(ringWidth / texWorldSize));

        for (int i = 0; i <= n; i++)
        {
            float t = (float)i / n;
            float a = t * Mathf.PI * 2f;
            Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            // Yerel uzay: merkez = (0,0,0).
            Vector3 inP  = dir * rIn;  inP.y  = yOffset;
            Vector3 outP = dir * rOut; outP.y = yOffset;
            verts[i * 2]     = inP;
            verts[i * 2 + 1] = outP;
            float u = t * tilesU;
            uvs[i * 2]     = new Vector2(u, 0f);
            uvs[i * 2 + 1] = new Vector2(u, tilesV);
        }

        for (int i = 0; i < n; i++)
        {
            int a = i * 2, b = i * 2 + 1, c = (i + 1) * 2, d = (i + 1) * 2 + 1;
            if (!flipWinding)
            {
                tris[i * 6 + 0] = a; tris[i * 6 + 1] = c; tris[i * 6 + 2] = d;
                tris[i * 6 + 3] = a; tris[i * 6 + 4] = d; tris[i * 6 + 5] = b;
            }
            else
            {
                tris[i * 6 + 0] = a; tris[i * 6 + 1] = d; tris[i * 6 + 2] = c;
                tris[i * 6 + 3] = a; tris[i * 6 + 4] = b; tris[i * 6 + 5] = d;
            }
        }

        mesh.Clear();
        mesh.SetVertices(verts);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
    }

    // Running-bond (kaydırmalı) tuğla dokusu: 2 sıra, harç çizgileri
    Texture2D BuildBrickTexture()
    {
        int W = 128, H = 64, mortar = 4, rowH = H / 2, brickW = W / 2;
        var tex = new Texture2D(W, H) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear };
        for (int y = 0; y < H; y++)
        for (int x = 0; x < W; x++)
        {
            int row = y / rowH;
            int offset = (row % 2 == 0) ? 0 : brickW / 2;
            bool mortarH = (y % rowH) < mortar;
            bool mortarV = (((x + offset) % brickW) < mortar);
            bool m = mortarH || mortarV;
            // hafif tuğla renk varyasyonu
            float varns = ((((x / brickW) * 7 + (y / rowH) * 13) % 5) - 2) * 0.025f;
            Color bc = brickColor + new Color(varns, varns * 0.6f, varns * 0.4f, 0f);
            tex.SetPixel(x, y, m ? mortarColor : bc);
        }
        tex.Apply();
        return tex;
    }
}
