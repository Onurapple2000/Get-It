using System.Text;
using UnityEngine;

/// <summary>
/// Cihazda/editörde performans ölçümü için ekran-üstü HUD. Otomatik başlar (sahne düzenlemeye gerek yok),
/// tüm sahnelerde görünür, sağ-üstteki "PERF" düğmesiyle aç/kapa.
///
/// Gösterir:
///  - FPS + ortalama frame süresi (ms)
///  - worst(3s): son 3 sn'nin en kötü frame'i (takılma/stutter göstergesi)
///  - CPU/GPU ms (FrameTimingManager — Player Settings'te "Frame Timing Stats" açıksa; GPU-bound mı CPU-bound mı?)
///  - Bellek (GC yönetilen + toplam ayrılmış — dev build)
///  - swallowables: sahnedeki aktif yutulabilir nesne sayısı (sahne yoğunluğu)
///  - EDİTÖRDE ayrıca: üçgen, vertex, draw call, batch, setPass (UnityStats — cihazda Profiler'dan okunur)
///
/// Baseline notu: OnGUI'nin küçük bir maliyeti var (tutarlı). Kesin release FPS için düğmeden kapatıp ölçebilirsin.
/// </summary>
public class PerfHud : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        var go = new GameObject("PerfHud");
        go.AddComponent<PerfHud>();
        DontDestroyOnLoad(go);
    }

    static bool show = true;
    float dt;                 // yumuşatılmış frame süresi (s)
    float fps, accum; int frames;
    float worst, worstTimer;  // 3 sn penceresinde en kötü frame
    GUIStyle label, btn;
    readonly FrameTiming[] timings = new FrameTiming[1];

    void Update()
    {
        float d = Time.unscaledDeltaTime;
        dt += (d - dt) * 0.1f;
        accum += d; frames++;
        if (accum >= 0.5f) { fps = frames / accum; frames = 0; accum = 0f; }
        if (d > worst) worst = d;
        worstTimer += d;
        if (worstTimer >= 3f) { worst = d; worstTimer = 0f; }
    }

    void OnGUI()
    {
        if (label == null)
        {
            int fs = Mathf.Max(12, Mathf.RoundToInt(Screen.height * 0.026f));
            label = new GUIStyle(GUI.skin.label) { fontSize = fs, fontStyle = FontStyle.Bold, richText = false };
            label.normal.textColor = Color.white;
            btn = new GUIStyle(GUI.skin.button) { fontSize = fs };
        }

        float w = Screen.width, h = Screen.height;
        float bw = Mathf.Max(90f, w * 0.13f), bh = Mathf.Max(44f, h * 0.05f);
        if (GUI.Button(new Rect(w - bw - 10f, 10f, bw, bh), show ? "PERF ✕" : "PERF", btn)) show = !show;
        if (!show) return;

        var sb = new StringBuilder();
        sb.AppendLine($"FPS {fps:0}    {dt * 1000f:0.0} ms");
        sb.AppendLine($"worst 3s: {worst * 1000f:0.0} ms  (~{(worst > 0 ? 1f / worst : 0f):0} fps dip)");

        // CPU/GPU ms (Frame Timing Stats açıksa) → GPU-bound mu CPU-bound mu anlamak için EN değerli metrik
        FrameTimingManager.CaptureFrameTimings();
        uint n = FrameTimingManager.GetLatestTimings(1, timings);
        if (n > 0 && (timings[0].cpuFrameTime > 0 || timings[0].gpuFrameTime > 0))
            sb.AppendLine($"CPU {timings[0].cpuFrameTime:0.0}  GPU {timings[0].gpuFrameTime:0.0} ms");

        sb.AppendLine($"GC mem {System.GC.GetTotalMemory(false) / 1048576f:0.0} MB");
        long tot = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong();
        if (tot > 0) sb.AppendLine($"total mem {tot / 1048576f:0.0} MB");

        int objs = PhysicsSwallowable.All != null ? PhysicsSwallowable.All.Count : 0;
        sb.AppendLine($"swallowables: {objs}");

#if UNITY_EDITOR
        sb.AppendLine($"tris {UnityEditor.UnityStats.triangles:n0}   verts {UnityEditor.UnityStats.vertices:n0}");
        sb.AppendLine($"drawCalls {UnityEditor.UnityStats.drawCalls}   setPass {UnityEditor.UnityStats.setPassCalls}");
        sb.AppendLine($"instancedBatched {UnityEditor.UnityStats.instancedBatchedDrawCalls}");
#endif

        var content = new GUIContent(sb.ToString());
        Vector2 sz = label.CalcSize(content);
        GUI.color = new Color(0f, 0f, 0f, 0.55f);
        GUI.DrawTexture(new Rect(6f, 6f, sz.x + 20f, sz.y + 14f), Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.Label(new Rect(16f, 12f, sz.x, sz.y), content, label);
    }
}
