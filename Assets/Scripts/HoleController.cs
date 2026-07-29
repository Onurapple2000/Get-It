using UnityEngine;

public class HoleController : MonoBehaviour
{
    public float moveSpeed  = 6f;
    public float currentSize = 1.5f;
    [Tooltip("Deliğin büyüyebileceği MAKS boyut (en büyük nesneyi alacak kadar). Aşırı büyümeyi önler.")]
    public float maxSize = 3.6f;
    [Tooltip("Deliğin gezebileceği kare sınır (yarı-genişlik). Rim bunu aşmaz → çerçeve içinde kalır.")]
    public float boundaryLimit = 13f;
    [Tooltip("Yutulan nesnenin büyüme katkısı çarpanı. <1 = daha yavaş büyüme (max'a geç ulaşır).")]
    public float growMultiplier = 0.5f;

    [Tooltip("Büyüme yumuşatma süresi (sn). Küçük=hızlı, büyük=daha esnek/yumuşak.")]
    public float growSmoothTime = 0.18f;

    [Tooltip("Eski kendi çukur görselini (_HoleDepth) üretsin mi? HoleFloor varken KAPALI bırak — " +
             "deliğin içini HoleFloor tek kaynaktan çiziyor (çakışma/z-fight önlenir).")]
    public bool createDepthPit = false;
    float targetSize;
    float growVel;

    Rigidbody rb;
    Vector3   previousPos;
    public Vector3 CurrentVelocity { get; private set; }

    // Depth pit — standalone GO (NOT a child) so it keeps clean world-space scale.
    // The hole disc has Y scale 0.01; any child would be squashed.
    GameObject depthPit;
    MeshFilter depthMf;
    const int PIT_SEGS = 32;

    // Pit grows deeper as the hole widens — bigger hole = deeper void
    public float PitDepth => Mathf.Max(5f, currentSize * 3f);

    void Start()
    {
        rb          = GetComponent<Rigidbody>();
        previousPos = rb.position;
        targetSize  = currentSize;
        if (createDepthPit) CreateDepthPit();
    }

    void Update()
    {
        // Büyümeyi hedefe yumuşak yaklaştır (esneyerek büyüme hissi)
        if (!Mathf.Approximately(currentSize, targetSize))
        {
            currentSize = Mathf.SmoothDamp(currentSize, targetSize, ref growVel, growSmoothTime);
            if (Mathf.Abs(currentSize - targetSize) < 0.001f) currentSize = targetSize;
            ApplySize();
        }

        // ── HAREKET: KARE-BAŞI (ekran yenileme hızında, 120/144Hz) → ANINDA/çevik tepki; gecikmesiz ters yön.
        // Eskiden FixedUpdate'teydi (50Hz) → yüksek-Hz ekranda gevşek/gecikmeli hissediliyordu. Delik KİNEMATİK +
        // collider YOK (nesneler mesafeyle algılanır) → transform'u doğrudan sürmek güvenli, fizik çakışması yok.
        if (GameManager.Instance != null && !GameManager.Instance.IsActive)
        {
            CurrentVelocity = Vector3.zero;
            previousPos = transform.position;
            return;
        }

        Vector3 input = Vector3.zero;
        Vector2 j = VirtualJoystick.Direction;   // alt-orta joystick (x=+X sağ, y=+Z ileri; kamera yaw'sız)
        if (j.sqrMagnitude > 0.0004f) { input.x = j.x; input.z = j.y; }
        else { input.x = Input.GetAxis("Horizontal"); input.z = Input.GetAxis("Vertical"); }   // editör klavye yedeği
        if (input.sqrMagnitude > 1f) input = input.normalized;   // 0..1 (kısmi itiş = kısmi hız)

        Vector3 newPos = transform.position + input * moveSpeed * Time.deltaTime;
        newPos.y = 0.05f;
        float halfSize = currentSize * 0.5f;
        float limit = boundaryLimit;
        newPos.x = Mathf.Clamp(newPos.x, -limit + halfSize, limit - halfSize);
        newPos.z = Mathf.Clamp(newPos.z, -limit + halfSize, limit - halfSize);

        transform.position = newPos;
        float dt = Mathf.Max(Time.deltaTime, 1e-5f);
        CurrentVelocity = (newPos - previousPos) / dt;
        previousPos = newPos;
        SyncPitPosition();
    }

    void ApplySize()
    {
        transform.localScale = new Vector3(currentSize, 0.01f, currentSize);
        RefreshPit();
    }

    void CreateDepthPit()
    {
        depthPit = new GameObject("_HoleDepth");
        depthMf  = depthPit.AddComponent<MeshFilter>();
        var mr   = depthPit.AddComponent<MeshRenderer>();

        // Unlit material — always dark, ignores scene lighting.
        // Gradient texture: dark gray at rim → pure black at bottom.
        var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default") ?? Shader.Find("Hidden/InternalErrorShader"));
        mat.SetColor("_BaseColor", Color.white);
        mat.SetFloat("_Cull", 0f); // CullMode.Off — visible from both sides

        // Build a 1×8 vertical gradient texture (dark gray → black)
        var tex = new Texture2D(1, 8, TextureFormat.RGB24, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode   = TextureWrapMode.Clamp;
        for (int p = 0; p < 8; p++)
        {
            float t = p / 7f; // 0 = bottom (black), 1 = top (dark gray)
            float v = Mathf.Lerp(0f, 0.14f, t * t); // quadratic: stays dark, lighter only at rim
            tex.SetPixel(0, p, new Color(v, v, v));
        }
        tex.Apply();
        mat.SetTexture("_BaseMap", tex);

        mr.material          = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows    = false;

        RefreshPit();
        SyncPitPosition();
    }

    void RefreshPit()
    {
        if (depthMf == null) return;
        depthMf.mesh = BuildOpenCylinder(currentSize * 0.5f, PitDepth, PIT_SEGS);
    }

    void SyncPitPosition()
    {
        if (depthPit == null) return;
        Vector3 p = transform.position;
        // Top of cylinder sits just at ground level (Y = 0)
        depthPit.transform.position = new Vector3(p.x, 0f, p.z);
    }

    // Cylinder with UV-gradient walls + solid black bottom cap.
    // UV.y = 1 at rim (top), UV.y = 0 at bottom → gradient texture gives dark-to-black depth feel.
    // Bottom cap closes the pit so there's no see-through to the skybox.
    static Mesh BuildOpenCylinder(float radius, float height, int n)
    {
        // Layout: wall-top[0..n-1], wall-bottom[n..2n-1], cap-edge[2n..3n-1], cap-center[3n]
        int capStart  = n * 2;
        int capCenter = n * 3;
        var verts = new Vector3[n * 3 + 1];
        var uvs   = new Vector2[n * 3 + 1];
        var tris  = new System.Collections.Generic.List<int>(n * 9);

        for (int i = 0; i < n; i++)
        {
            float a = (float)i / n * Mathf.PI * 2f;
            float x = Mathf.Cos(a) * radius;
            float z = Mathf.Sin(a) * radius;
            float u = (float)i / n;

            verts[i]          = new Vector3(x, 0f,      z); // wall top
            verts[i + n]      = new Vector3(x, -height, z); // wall bottom
            verts[capStart+i] = new Vector3(x, -height, z); // cap edge

            uvs[i]          = new Vector2(u, 1f); // top → lighter
            uvs[i + n]      = new Vector2(u, 0f); // bottom → black
            uvs[capStart+i] = new Vector2(u, 0f); // cap edge → black
        }
        verts[capCenter] = new Vector3(0f, -height, 0f);
        uvs[capCenter]   = new Vector2(0.5f, 0f);

        // Wall quads
        for (int i = 0; i < n; i++)
        {
            int next = (i + 1) % n;
            tris.Add(i);     tris.Add(i + n);   tris.Add(next);
            tris.Add(next);  tris.Add(i + n);   tris.Add(next + n);
        }
        // Bottom cap fan (black, closes the pit)
        for (int i = 0; i < n; i++)
        {
            int next = (i + 1) % n;
            tris.Add(capCenter);
            tris.Add(capStart + next);
            tris.Add(capStart + i);
        }

        var mesh = new Mesh();
        mesh.SetVertices(verts);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        return mesh;
    }

    // HAREKET artık Update()'te (kare-başı, çevik). FixedUpdate yalnızca ESKİ Swallowable sistemini tarar
    // (GameScene PhysicsSwallowable kullanıyor → Swallowable.All genelde boş; sadece legacy uyumluluk).
    void FixedUpdate()
    {
        if (GameManager.Instance != null && !GameManager.Instance.IsActive) return;
        if (Swallowable.All.Count == 0) return;

        Vector3 holePos2D = new Vector3(transform.position.x, 0f, transform.position.z);
        foreach (Swallowable s in Swallowable.All)
        {
            if (s == null || s.IsBeingSwallowed) continue;
            Vector3 objPos2D = new Vector3(s.transform.position.x, 0f, s.transform.position.z);
            if (Vector3.Distance(holePos2D, objPos2D) < currentSize * 0.5f)
                s.NotifyInsideHole(this);
        }
    }

    public void Grow(float amount)
    {
        // Anlık değil: hedefi artır, Update yumuşatarak büyütsün. growMultiplier ile yavaşlatılır, maxSize ile sınırlı.
        targetSize = Mathf.Min(maxSize, targetSize + amount * growMultiplier);
    }

    /// <summary>growMultiplier'sız DOĞRUDAN büyüme (güç-up burst'ü için) — amount kadar hedefe eklenir (maxSize sınırlı).</summary>
    public void GrowInstant(float amount)
    {
        targetSize = Mathf.Min(maxSize, targetSize + amount);
    }

    void OnDestroy()
    {
        if (depthPit != null) Destroy(depthPit);
    }
}
