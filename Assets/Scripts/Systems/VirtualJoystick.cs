using UnityEngine;

/// <summary>
/// Ekranın alt-ortasında sabit, yarı-saydam sanal joystick. Oto-başlar (sahne düzenlemeye gerek yok),
/// sadece oynanışta (GameManager aktifken) görünür. HoleController `VirtualJoystick.Direction` okur.
///
/// Kullanım: alt-ortadaki halka içine/civarına başparmakla bas + sürükle → knob takip eder, Direction (-1..1)
/// üretir (x=sağ, y=ileri/+Z). Bırakınca sıfırlanır.
/// </summary>
public class VirtualJoystick : MonoBehaviour
{
    public static Vector2 Direction;   // -1..1  (x = dünya +X, y = dünya +Z)

    static Texture2D ringTex, discTex;
    Vector2 baseC, knobC;   // ekran koordinatı (y YUKARI)
    float baseR, knobR;
    int finger = -1;        // aktif dokunuş id; -2 = mouse
    bool active;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Boot()
    {
        var go = new GameObject("VirtualJoystick");
        go.AddComponent<VirtualJoystick>();
        DontDestroyOnLoad(go);
    }

    // Tutorial (FoodsL1) joystick adımı: oyun donmuşken (timeScale=0) joystick RESPONSIVE olsun ki oyuncu deneyip
    // öğrensin. TutorialActive true iken Playable (GameManager aktif olmasa da). Delik yine hareket etmez (dt=0).
    public static bool TutorialActive;

    // Pause/X overlay açıkken joystick GİZLİ + İŞLEVSİZ (OnGUI hep uGUI üstünde çizerdi → arkada kalması için bastır).
    static bool Playable => !PauseMenu.OverlayOpen && (TutorialActive || (GameManager.Instance != null && GameManager.Instance.IsActive));

    // Tutorial'ın joystick tabanını işaret etmesi için (Layout ile aynı formül).
    public static Vector2 BaseScreenPos()
    {
        float r = Mathf.Clamp(Screen.height * 0.12f, 60f, 220f);
        float by = Mathf.Max(Screen.height * 0.17f, Screen.safeArea.yMin + r + 24f);
        return new Vector2(Screen.width * 0.5f, by);
    }

    void Layout()
    {
        baseR = Mathf.Clamp(Screen.height * 0.12f, 60f, 220f);
        knobR = baseR * 0.46f;
        // alt-orta, en alta yapışık değil (yukarıda ~%17); safe-area alt payını (gesture/home barı) da geç.
        float by = Mathf.Max(Screen.height * 0.17f, Screen.safeArea.yMin + baseR + 24f);
        baseC = new Vector2(Screen.width * 0.5f, by);
    }

    void Update()
    {
        if (!Playable) { active = false; finger = -1; Direction = Vector2.zero; return; }
        Layout();
        if (!active) knobC = baseC;

        GetPointer(out Vector2 p, out bool down, out bool held, out bool up, out int id);

        float actR = baseR * 2.4f;   // cömert etkinleşme alanı (tam halkaya basmaya gerek yok)
        if (!active && down && (p - baseC).sqrMagnitude <= actR * actR)
        { active = true; finger = id; }

        if (active && id == finger && (held || down))
        {
            Vector2 off = p - baseC;
            if (off.magnitude > baseR) off = off.normalized * baseR;
            knobC = baseC + off;
            Direction = off / baseR;   // -1..1
        }
        if (active && id == finger && up)
        { active = false; finger = -1; knobC = baseC; Direction = Vector2.zero; }
    }

    // Aktif finger'ı takip et; yoksa ilk dokunuşu/mouse'u döndür.
    void GetPointer(out Vector2 pos, out bool down, out bool held, out bool up, out int id)
    {
        pos = Vector2.zero; down = held = up = false; id = -1;

        if (Input.touchCount > 0)
        {
            if (active)
                for (int i = 0; i < Input.touchCount; i++)
                {
                    var t = Input.GetTouch(i);
                    if (t.fingerId != finger) continue;
                    pos = t.position; id = t.fingerId;
                    down = t.phase == TouchPhase.Began;
                    held = t.phase == TouchPhase.Moved || t.phase == TouchPhase.Stationary;
                    up   = t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled;
                    return;
                }
            var f = Input.GetTouch(0);
            pos = f.position; id = f.fingerId;
            down = f.phase == TouchPhase.Began;
            held = f.phase == TouchPhase.Moved || f.phase == TouchPhase.Stationary;
            up   = f.phase == TouchPhase.Ended || f.phase == TouchPhase.Canceled;
            return;
        }

        // Editörde mouse
        if (Input.GetMouseButton(0) || Input.GetMouseButtonUp(0))
        {
            pos = Input.mousePosition; id = -2;
            down = Input.GetMouseButtonDown(0);
            up   = Input.GetMouseButtonUp(0);
            held = Input.GetMouseButton(0) && !down;
        }
    }

    void OnGUI()
    {
        if (!Playable) return;
        Layout();
        if (ringTex == null) ringTex = MakeRing(128, 0.80f);
        if (discTex == null) discTex = MakeDisc(96);

        float by = Screen.height - baseC.y;   // OnGUI y AŞAĞI
        float ky = Screen.height - knobC.y;

        Color prev = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, 0.22f);
        GUI.DrawTexture(new Rect(baseC.x - baseR, by - baseR, baseR * 2f, baseR * 2f), ringTex);
        GUI.color = new Color(1f, 1f, 1f, active ? 0.55f : 0.40f);
        GUI.DrawTexture(new Rect(knobC.x - knobR, ky - knobR, knobR * 2f, knobR * 2f), discTex);
        GUI.color = prev;
    }

    // Yumuşak kenarlı dolu daire (knob).
    static Texture2D MakeDisc(int s)
    {
        var t = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        float c = (s - 1) * 0.5f, rad = c;
        var px = new Color32[s * s];
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                float a = Mathf.Clamp01((rad - d) / 2f);
                px[y * s + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
        t.SetPixels32(px); t.Apply();
        return t;
    }

    // Yumuşak kenarlı halka (taban).
    static Texture2D MakeRing(int s, float innerFrac)
    {
        var t = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        float c = (s - 1) * 0.5f, rad = c, inner = rad * innerFrac;
        var px = new Color32[s * s];
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                float aOut = Mathf.Clamp01((rad - d) / 2f);
                float aIn  = Mathf.Clamp01((d - inner) / 2f);
                float a = Mathf.Min(aOut, aIn);
                px[y * s + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
        t.SetPixels32(px); t.Apply();
        return t;
    }
}
