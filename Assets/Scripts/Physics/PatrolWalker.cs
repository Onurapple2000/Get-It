using UnityEngine;

/// <summary>
/// Nesneyi (kedi vb.) sahne boyunca DÜNYA X ekseninde sağa-sola YÜRÜTÜR ve arena ÇERÇEVESİNDEN döndürür
/// (nesne çerçeve dışına çıkamaz).
///
/// Dönüş noktası otomatik: frameHalf (LevelManager.frameHalf ile aynı) − wallInset − nesnenin yatay
/// yarı-genişliği. Böylece gövde çerçeve iç yüzünü aşmadan tam kenarda döner.
///
/// PhysicsSwallowable.freezeUntilNear ile entegre:
///  - Nesne KİNEMATİK (donuk) haldeyken bu script onu Rigidbody.MovePosition ile ±limit arasında yürütür;
///    uca gelince 180° döner. Child mesh'teki yürüme animasyonu (Animator) yerinde oynar.
///  - Delik yaklaşıp PhysicsSwallowable nesneyi UYANDIRINCA (rb.isKinematic=false) yürüme DURUR ve script
///    devre dışı kalır → nesne normal fizik nesnesi gibi deliğe düşer/yutulur.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class PatrolWalker : MonoBehaviour
{
    [Header("Arena çerçevesi (dönüş sınırı)")]
    [Tooltip("Arena çerçevesi yarı-genişliği — LevelManager.frameHalf ile AYNI olmalı.")]
    public float frameHalf = 15f;
    [Tooltip("Çerçeve iç yüzünden ek boşluk (bar kalınlığı ~0.3 + emniyet payı).")]
    public float wallInset = 0.6f;

    [Header("Yürüyüş")]
    [Tooltip("Yürüme hızı (birim/sn). 'Yavaşça' için ~1.5-2.")]
    public float speed = 2f;
    [Tooltip("Başlangıçta sola (-x) doğru mu yürüsün? (Sağa yerleştirip sola yürütmek için açık.)")]
    public bool startMovingLeft = true;

    [Header("Yönelim")]
    [Tooltip("Modelin hareket yönüne bakması için Y ekseni ofseti (derece). Model +Z'ye bakıyorsa 0.")]
    public float yawOffset = 0f;
    [Tooltip("Uçta dönüş yumuşaklığı (derece/sn). 0 = anında ters çevir.")]
    public float turnSpeed = 720f;

    Rigidbody rb;
    int dir;            // -1 = sola (-x), +1 = sağa (+x)
    float targetYaw;
    float limitX = 10f; // hesaplanan dönüş noktası (±) — Start'ta gövde boyutuna göre

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        dir = startMovingLeft ? -1 : 1;
        targetYaw = YawFor(dir);
        transform.rotation = Quaternion.Euler(0f, targetYaw, 0f);
    }

    void Start()
    {
        // Nesnenin yatay yarı-genişliği (collider dünya AABB'sinden; rotasyon dahil). Max(x,z) → hangi yöne
        // dönerse dönsün güvenli. Dönüş noktasını çerçeve iç yüzünden gövde kadar içeri çek.
        var col = GetComponentInChildren<Collider>();
        float bodyHalf = 0.5f;
        if (col != null) { Vector3 e = col.bounds.extents; bodyHalf = Mathf.Max(e.x, e.z); }
        limitX = Mathf.Max(1f, frameHalf - wallInset - bodyHalf);
    }

    // +X yönü world yaw 90°, -X yönü -90° (Unity forward=+Z=0°). Model +Z'ye bakmıyorsa yawOffset ekle.
    float YawFor(int d) => (d < 0 ? -90f : 90f) + yawOffset;

    void FixedUpdate()
    {
        // Delik uyandırdıysa (kinematik değilse) yürümeyi bırak — fizik devralsın (deliğe düşsün).
        if (!rb.isKinematic) { enabled = false; return; }

        Vector3 p = rb.position;
        p.x += dir * speed * Time.fixedDeltaTime;

        if (dir < 0 && p.x <= -limitX) { p.x = -limitX; dir = 1; targetYaw = YawFor(dir); }
        else if (dir > 0 && p.x >= limitX) { p.x = limitX; dir = -1; targetYaw = YawFor(dir); }

        rb.MovePosition(p);

        Quaternion goal = Quaternion.Euler(0f, targetYaw, 0f);
        rb.MoveRotation(turnSpeed <= 0f
            ? goal
            : Quaternion.RotateTowards(rb.rotation, goal, turnSpeed * Time.fixedDeltaTime));
    }
}
