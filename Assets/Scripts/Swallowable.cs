using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class Swallowable : MonoBehaviour
{
    public static readonly List<Swallowable> All = new List<Swallowable>();

    public float objectSize  = 0.4f;   // minimum hole diameter for direct swallow
    public float growAmount  = 0.075f;
    public int   scoreValue  = 10;

    Rigidbody rb;
    Collider  col;
    bool      originallyKinematic;

    [SerializeField] float commitTime         = 0.08f;
    [SerializeField] float quickPassSpeed     = 4f;
    [SerializeField] float tipTorque          = 18f;
    [SerializeField] float tipCommitAngle     = 45f;  // tipped this far → guaranteed fall
    [SerializeField] float tipAbortAngle      = 85f;
    [SerializeField] float tipTriggerFraction = 0.5f; // hole must be >= this fraction of objectSize radius to trigger tip
    [SerializeField] float swallowDuration    = 1.1f; // how long the topple-in takes (higher = slower fall)

    enum State { Idle, Tipping, Swallowing }
    State state      = State.Idle;
    bool  isSnapping = false;

    public bool IsBeingSwallowed => state == State.Swallowing;

    HoleController currentHole;
    float   timeInsideHole = 0f;
    bool    wasInsideHole  = false;
    Vector3 lastHoleVelocity;

    // tipping state
    Vector3    tipSavedPos;
    Quaternion tipSavedRot;

    void Awake()
    {
        rb  = GetComponent<Rigidbody>();
        col = GetComponent<Collider>();
        originallyKinematic = rb != null && rb.isKinematic;
    }
    void OnEnable()  { All.Add(this); }
    void OnDisable() { All.Remove(this); }

    void ClearHoleTracking() { wasInsideHole = false; timeInsideHole = 0f; }

    // ─────────────────────────────────────────────────────────────────────────
    void Update()
    {
        if (state != State.Swallowing && transform.position.y < -12f)
        { GameManager.Instance?.ObjectSwallowed(); Destroy(gameObject); return; }

        if (state == State.Swallowing || isSnapping || currentHole == null) return;

        Vector3 holePos2D = new Vector3(currentHole.transform.position.x, 0f, currentHole.transform.position.z);
        Vector3 myPos2D   = new Vector3(transform.position.x, 0f, transform.position.z);
        float   dist      = Vector3.Distance(holePos2D, myPos2D);
        float   holeR     = currentHole.currentSize * 0.5f;
        bool    isInside  = dist < holeR;

        if (isInside)
        {
            lastHoleVelocity = currentHole.CurrentVelocity;
            timeInsideHole  += Time.deltaTime;
            wasInsideHole    = true;

            if (timeInsideHole >= commitTime)
            {
                if (currentHole.currentSize >= objectSize)
                    BeginSwallow();
                else if (state == State.Idle && holeR >= objectSize * 0.5f * tipTriggerFraction)
                    BeginTipping();
            }
        }
        else if (wasInsideHole)
        {
            bool fast = lastHoleVelocity.magnitude > quickPassSpeed;
            ClearHoleTracking();

            if (state == State.Tipping)
            {
                OnTipInterrupted(fast);
            }
            else
            {
                currentHole = null;
                if (fast)
                {
                    Vector3 launchDir = -new Vector3(lastHoleVelocity.x, 0f, lastHoleVelocity.z).normalized;
                    float   force     = Mathf.Clamp(lastHoleVelocity.magnitude * 0.4f, 0.5f, 3f);
                    if (rb != null && !rb.isKinematic) rb.AddForce(launchDir * force, ForceMode.Impulse);
                }
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    void FixedUpdate()
    {
        if (state != State.Tipping || currentHole == null || isSnapping) return;

        Vector3 holeXZ = new Vector3(currentHole.transform.position.x, 0f, currentHole.transform.position.z);
        Vector3 myXZ   = new Vector3(transform.position.x, 0f, transform.position.z);
        Vector3 toHole = holeXZ - myXZ;
        if (toHole.sqrMagnitude < 0.0001f) return;

        Vector3 axis = Vector3.Cross(Vector3.up, toHole.normalized);
        rb.AddTorque(axis * tipTorque, ForceMode.Acceleration);

        float tilt = Vector3.Angle(transform.up, Vector3.up);

        if (tilt >= tipCommitAngle) { CommitTipFall(); return; }   // point of no return
        if (tilt >= tipAbortAngle)  { OnTipInterrupted(false); }
    }

    // ─────────────────────────────────────────────────────────────────────────
    void BeginTipping()
    {
        state       = State.Tipping;
        tipSavedPos = transform.position;
        tipSavedRot = transform.rotation;

        EnsureNonKinematic();
        if (col != null) col.enabled = false;
        if (rb != null)
        {
            rb.linearVelocity  = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.constraints     = RigidbodyConstraints.FreezePositionX
                               | RigidbodyConstraints.FreezePositionY
                               | RigidbodyConstraints.FreezePositionZ;
        }
    }

    // Hole moved away before 45° commit. Don't snap upright (industry standard).
    // Fast exit = scatter back upright + fly away. Slow exit = freeze where it leaned.
    void OnTipInterrupted(bool fast)
    {
        state       = State.Idle;
        currentHole = null;

        if (rb != null)
        {
            rb.angularVelocity = Vector3.zero;
            rb.linearVelocity  = Vector3.zero;
        }

        if (fast)
        {
            if (rb != null) { rb.position = tipSavedPos; rb.rotation = tipSavedRot; }
            else            { transform.position = tipSavedPos; transform.rotation = tipSavedRot; }
            if (col != null) col.enabled = true;
            if (rb != null)
            {
                rb.constraints = RigidbodyConstraints.FreezeRotation;
                Vector3 launchDir = -new Vector3(lastHoleVelocity.x, 0f, lastHoleVelocity.z).normalized;
                float   force     = Mathf.Clamp(lastHoleVelocity.magnitude * 0.4f, 0.5f, 3f);
                if (launchDir.sqrMagnitude > 0.01f) rb.AddForce(launchDir * force, ForceMode.Impulse);
            }
        }
        else
        {
            // Stays leaning at its current angle until the hole comes back.
            if (rb != null) rb.constraints = RigidbodyConstraints.FreezeAll;
        }
    }

    void CommitTipFall()
    {
        HoleController hole = currentHole;
        BeginSwallowInternal(hole);   // hand off to the suck-in (collider already off)
    }

    // ─────────────────────────────────────────────────────────────────────────
    void BeginSwallow()
    {
        foreach (var c in Physics.OverlapSphere(transform.position, 2f))
        { Rigidbody r = c.GetComponent<Rigidbody>(); if (r != null && !r.isKinematic) r.WakeUp(); }
        BeginSwallowInternal(currentHole);
    }

    void BeginSwallowInternal(HoleController hole)
    {
        state       = State.Swallowing;
        currentHole = null;
        ClearHoleTracking();

        if (col != null) col.enabled = false;
        // Fully scripted, kinematic motion: we never set velocity, so no Unity 6
        // "velocity on kinematic body" errors, and physics never fights the suck-in.
        if (rb != null)
        {
            rb.isKinematic     = true;
            rb.constraints     = RigidbodyConstraints.None;
        }
        StartCoroutine(SwallowRoutine(hole));
    }

    void EnsureNonKinematic()
    {
        if (rb != null && rb.isKinematic) { rb.isKinematic = false; rb.useGravity = true; }
    }

    public void NotifyInsideHole(HoleController hole)
    {
        if (state == State.Swallowing || isSnapping) return;
        currentHole = hole;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Suck-in: the object is continuously pulled toward the hole CENTER while it
    // descends and shrinks. Because its XZ tracks the hole center, it always stays
    // inside the hole disc — so it is NEVER visible sinking into solid ground, no
    // matter where the hole moves. This is how Hole.io / Donut County handle it.
    // ─────────────────────────────────────────────────────────────────────────
    IEnumerator SwallowRoutine(HoleController hole)
    {
        float      pitDepth   = hole != null ? hole.PitDepth : 5f;
        Vector3    startPos    = transform.position;
        Vector3    startScale  = transform.localScale;
        Quaternion startRot    = transform.rotation;
        float      targetDepth = Mathf.Min(pitDepth, 4f);

        // Tip axis: rotate the object so it topples toward the hole center.
        Vector3 holeStart = hole != null ? hole.transform.position : startPos;
        Vector3 toHole    = new Vector3(holeStart.x - startPos.x, 0f, holeStart.z - startPos.z);
        Vector3 tipAxis   = toHole.sqrMagnitude > 0.0001f
            ? Vector3.Cross(Vector3.up, toHole.normalized)
            : transform.right;
        Quaternion tippedRot = Quaternion.AngleAxis(90f, tipAxis) * startRot;

        bool  committed = false;
        float t         = 0f;

        while (t < swallowDuration)
        {
            t += Time.deltaTime;
            float k = t / swallowDuration;            // 0..1 progress

            Vector3 holeCenter = hole != null ? hole.transform.position : startPos;

            // XZ: drift gently toward the hole center (keeps it inside the hole disc
            // so it never sinks into solid ground), but loose enough to feel free.
            float   xzK = Mathf.SmoothStep(0f, 1f, k);
            Vector3 xz  = Vector3.Lerp(startPos, holeCenter, xzK);

            // Y: accelerating fall (k*k) — starts slow, speeds up like real gravity.
            float y = Mathf.Lerp(startPos.y, -targetDepth, k * k);

            transform.position = new Vector3(xz.x, y, xz.z);

            // Topple into the hole as it descends.
            transform.rotation = Quaternion.Slerp(startRot, tippedRot, Mathf.SmoothStep(0f, 1f, k));

            // Keep full size for the first half (free fall feel), shrink only as it
            // disappears into the depths so it doesn't clip the pit walls.
            float shrink = Mathf.Clamp01((k - 0.5f) / 0.5f);
            transform.localScale = startScale * (1f - shrink);

            if (!committed && k >= 0.2f)
            {
                committed = true;
                hole?.Grow(growAmount);
                GameManager.Instance?.AddScore(scoreValue);
                GameManager.Instance?.ObjectSwallowed();
            }

            yield return null;
        }

        if (!committed)
        { hole?.Grow(growAmount); GameManager.Instance?.AddScore(scoreValue); GameManager.Instance?.ObjectSwallowed(); }

        Destroy(gameObject);
    }
}
