#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using TMPro;

/// <summary>
/// GET_IT sonuç ekranlarını köstebek temalı görsellerle kurar.
///  - Success: köstebek deliği arka plan + sevinen köstebek + Next Level butonu.
///  - Fail: arka plan SAYDAM (oyun sahnesi görünmeye devam eder) + üzgün köstebek
///          + Retry / Cancel butonları.
/// Menü: Tools/GET_IT/...  Tekrar çalıştırılabilir.
/// </summary>
public static class SuccessScreenBuilder
{
    const string BurrowPath = "Assets/Art/burrow_bg.png";
    const string MolePath   = "Assets/Art/mole_mascot.png";
    const string ButtonPath = "Assets/Art/button.png";
    const string SadMolePath = "Assets/Art/mole_mascot_sad.png";
    const string RetryPath   = "Assets/Art/retry_button.png";
    const string CancelPath  = "Assets/Art/cancel_button.png";
    const string FontGuid   = "8f586378b4e144a9851e7b34d9b748ee";

    [MenuItem("Tools/GET_IT/Build Success Screen")]
    public static void BuildSuccess()
    {
        if (!Resolve(out var gm, out _)) return;
        var panel = gm.successPanel;
        if (panel == null) { Warn("GameManager.successPanel atanmamış."); return; }

        var font = LoadFont();
        var panelRT = ClearPanel(panel);

        // Karartma overlay
        SetBackdrop(panel, new Color(0f, 0f, 0f, 0.55f));

        // Arka plan: köstebek deliği (tam ekran)
        var bg = CreateImage("Background", panelRT, LoadSprite(BurrowPath));
        bg.preserveAspect = true;
        Stretch(bg.rectTransform);

        // TÜM alt küme MERKEZ-hizalı tek yığın (sabit aralık → hiçbir en-boy oranında çakışma yok). 2026-07-24 yeniden
        // dağıtıldı (büyük buton+maskot için gevşek aralık): title(-320) → maskot(+160,460) → skor(-175) →
        // [yıldızlar -310, GameManager] → buton(-640,375) → [hediye -880, GameManager].
        var title = CreateText("Title", panelRT, "TEBRİKLER!", 84, font);
        AnchorTop(title.rectTransform, new Vector2(0, -320), new Vector2(900, 140));   // biraz aşağı (kullanıcı)

        var moleImg = CreateImage("MoleMascot", panelRT, LoadSprite(MolePath));
        moleImg.preserveAspect = true;
        Center(moleImg.rectTransform, new Vector2(0, 195), new Vector2(500, 500));     // büyütüldü 460→500 + yukarı (kullanıcı)

        var score = CreateText("SuccessScoreText", panelRT, "Skor: 0", 56, font);
        Center(score.rectTransform, new Vector2(0, -140), new Vector2(700, 84));       // yukarı -175→-140 (yıldızlarla çakışmasın)

        // Next Level butonu — MERKEZ-hizalı, yıldızların BELİRGİN ALTINDA (kullanıcı: yıldızlara çok yakındı → aşağı).
        var btn = CreateButton("NextLevelButton", panelRT, LoadSprite(ButtonPath));
        Center(btn.GetComponent<RectTransform>(), new Vector2(0, -640), new Vector2(440, 375));
        var label = CreateText("Label", btn.transform, "Next Level", 42, font);
        Stretch(label.rectTransform);
        UnityEventTools.AddPersistentListener(btn.onClick, gm.NextLevel);

        WireRef(gm, "successScoreText", score);
        Save(gm, "Success ekranı kuruldu.");
    }

    [MenuItem("Tools/GET_IT/Build Fail Screen")]
    public static void BuildFail()
    {
        if (!Resolve(out var gm, out _)) return;
        var panel = gm.failPanel;
        if (panel == null) { Warn("GameManager.failPanel atanmamış."); return; }

        var font = LoadFont();
        var panelRT = ClearPanel(panel);

        // ÇOK hafif karartma — oyun sahnesi arkada net görünsün
        // (kullanıcı bitirmeye ne kadar yakın olduğunu görebilsin).
        SetBackdrop(panel, new Color(0f, 0f, 0f, 0.20f));
        // Arka plan görseli YOK -> oyun sahnesi görünür kalır.

        // MERKEZ-hizalı tek yığın (sabit aralık → çakışma yok): maskot(+190) → can(-40) →
        // [fail sebebi -180, GameManager] → butonlar(-320).
        // ~2x BÜYÜK fail ekranı (kullanıcı). Butonlar alt alta (yan yana 2x taşardı). Dikey stack, ekrana sığar.
        var moleImg = CreateImage("SadMole", panelRT, LoadSprite(SadMolePath));
        moleImg.preserveAspect = true;
        Center(moleImg.rectTransform, new Vector2(0, 460), new Vector2(480, 480));   // büyük maskot (200px aşağı)

        var title = CreateText("Title", panelRT, "OLMADI!", 130, font);              // 80→130
        Center(title.rectTransform, new Vector2(0, 70), new Vector2(1000, 200));     // maskotun altında (200px aşağı)

        // Can sayacı — FailPanel'in çocuğuydu, panel temizlenince silindi. Yeniden oluşturup livesText'e bağla.
        var lives = CreateText("LivesText", panelRT, "Can: 5 / 5", 44, font);
        Center(lives.rectTransform, new Vector2(0, -40), new Vector2(600, 72));
        WireRef(gm, "livesText", lives);

        // İki buton (yazıları görselde basılı) — MERKEZ-hizalı, fail sebebinin altında (sabit boşluk → çakışmaz)
        var retry = CreateButton("RetryButton", panelRT, LoadSprite(RetryPath));
        Center(retry.GetComponent<RectTransform>(), new Vector2(-258, -600), new Vector2(510, 182));   // yan yana (sol), 200px aşağı
        retry.GetComponent<Image>().preserveAspect = true;
        UnityEventTools.AddPersistentListener(retry.onClick, gm.RetryLevel);

        var cancel = CreateButton("CancelButton", panelRT, LoadSprite(CancelPath));
        Center(cancel.GetComponent<RectTransform>(), new Vector2(258, -600), new Vector2(510, 182));   // yan yana (sağ), 200px aşağı
        cancel.GetComponent<Image>().preserveAspect = true;
        UnityEventTools.AddPersistentListener(cancel.onClick, gm.ExitToMainMenu);

        Save(gm, "Fail ekranı kuruldu.");
    }

    // ─────────────────────── ortak yardımcılar ───────────────────────

    static bool Resolve(out GameManager gm, out GameObject panel)
    {
        panel = null;
        gm = Object.FindFirstObjectByType<GameManager>();
        if (gm == null) { Warn("GameManager bulunamadı. GameScene açık mı?"); return false; }
        return true;
    }

    static RectTransform ClearPanel(GameObject panel)
    {
        var rt = panel.GetComponent<RectTransform>();
        for (int i = rt.childCount - 1; i >= 0; i--)
            Object.DestroyImmediate(rt.GetChild(i).gameObject);
        return rt;
    }

    static void SetBackdrop(GameObject panel, Color c)
    {
        var img = panel.GetComponent<Image>();
        if (img != null) { img.sprite = null; img.color = c; img.raycastTarget = true; }
    }

    static void WireRef(GameManager gm, string propName, Object value)
    {
        var so = new SerializedObject(gm);
        var prop = so.FindProperty(propName);
        if (prop != null) prop.objectReferenceValue = value;
        so.ApplyModifiedProperties();
    }

    static void Save(GameManager gm, string msg)
    {
        EditorUtility.SetDirty(gm);
        EditorSceneManager.MarkSceneDirty(gm.gameObject.scene);
        EditorSceneManager.SaveScene(gm.gameObject.scene);
        Debug.Log("[ScreenBuilder] " + msg + " Sahne kaydedildi.");
    }

    static TMP_FontAsset LoadFont() =>
        AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(FontGuid));

    static Sprite LoadSprite(string path)
    {
        var s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (s == null) Debug.LogError($"[ScreenBuilder] Sprite yüklenemedi: {path}");
        return s;
    }

    static Image CreateImage(string name, Transform parent, Sprite sprite)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        go.layer = parent.gameObject.layer;
        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.raycastTarget = false;
        return img;
    }

    static Button CreateButton(string name, Transform parent, Sprite sprite)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        go.layer = parent.gameObject.layer;
        go.GetComponent<Image>().sprite = sprite;
        return go.GetComponent<Button>();
    }

    static TextMeshProUGUI CreateText(string name, Transform parent, string text, float size, TMP_FontAsset font)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
        go.transform.SetParent(parent, false);
        go.layer = parent.gameObject.layer;
        var t = go.AddComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.text = text;
        t.fontSize = size;
        t.alignment = TextAlignmentOptions.Center;
        t.color = Color.white;
        t.raycastTarget = false;
        return t;
    }

    static void Warn(string msg) => EditorUtility.DisplayDialog("GET_IT Screen Builder", msg, "Tamam");

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }
    static void Center(RectTransform rt, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f); rt.anchoredPosition = pos; rt.sizeDelta = size;
    }
    static void AnchorTop(RectTransform rt, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f); rt.anchoredPosition = pos; rt.sizeDelta = size;
    }
    static void AnchorBottom(RectTransform rt, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f); rt.anchoredPosition = pos; rt.sizeDelta = size;
    }
}
#endif
