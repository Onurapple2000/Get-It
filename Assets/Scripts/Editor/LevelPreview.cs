using UnityEditor;
using UnityEngine;

/// <summary>
/// Edit modunda level kompozisyonunu sahneye kurup incelemeyi sağlar (oynamadan). Yapılar _LevelObjects /
/// _PlayArena altına gelir. İncelemeyi bitirince "Clear Preview" ile temizle (sahneyi BU haliyle KAYDETME).
/// Menu: Tools/GET_IT/Preview ...
/// </summary>
public static class LevelPreview
{
    [MenuItem("Tools/GET_IT/Preview - ONLY Eiffel")]
    public static void PreviewOnlyEiffel()
    {
        var lm = Object.FindAnyObjectByType<LevelManager>();
        if (lm == null) { Debug.LogError("[Preview] Sahnede LevelManager yok (GameScene açık olmalı)."); return; }
        var kebab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Foods/Kebab.prefab");
        if (kebab == null) { Debug.LogError("[Preview] Kebab.prefab yok."); return; }
        var macs = new[]
        {
            Load("MacaronPurple"), Load("MacaronBlue"), Load("MacaronGreen"), Load("MacaronYellow"), Load("MacaronPink"),
        };
        bool allMacs = System.Array.TrueForAll(macs, m => m != null);
        Clear();
        lm.BuildEiffelPreview(kebab, allMacs ? macs : null, Vector2.zero);
        Debug.Log("[Preview] SADECE Eyfel kuruldu (merkezde). İncele; bitince Clear Preview. (Kaydetme.)");
    }

    [MenuItem("Tools/GET_IT/Preview - Foods Level 5 (Eiffel)")]
    public static void PreviewFoodsL5() => Build(1, 4);

    [MenuItem("Tools/GET_IT/Preview - Foods Level 2 (Atomium)")]
    public static void PreviewFoodsL2() => Build(1, 1);

    [MenuItem("Tools/GET_IT/Preview - Foods Level 3 (Taj Mahal)")]
    public static void PreviewFoodsL3() => Build(1, 2);

    [MenuItem("Tools/GET_IT/Preview - Foods Level 1")]
    public static void PreviewFoodsL1() => Build(1, 0);

    [MenuItem("Tools/GET_IT/Preview - Park Level 1")]
    public static void PreviewParkL1() => Build(0, 0);

    [MenuItem("Tools/GET_IT/Preview - Cars Level 1")]
    public static void PreviewCarsL1() => Build(2, 0);
    [MenuItem("Tools/GET_IT/Preview - Cars Level 2")]
    public static void PreviewCarsL2() => Build(2, 1);
    [MenuItem("Tools/GET_IT/Preview - Cars Level 4")]
    public static void PreviewCarsL4() => Build(2, 3);
    [MenuItem("Tools/GET_IT/Preview - Cars Level 7")]
    public static void PreviewCarsL7() => Build(2, 6);
    [MenuItem("Tools/GET_IT/Preview - Cars Level 5")]
    public static void PreviewCarsL5() => Build(2, 4);
    [MenuItem("Tools/GET_IT/Preview - Cars Level 11")]
    public static void PreviewCarsL11() => Build(2, 10);
    [MenuItem("Tools/GET_IT/Preview - Cars Level 12")]
    public static void PreviewCarsL12() => Build(2, 11);
    [MenuItem("Tools/GET_IT/Preview - Cars Level 13")]
    public static void PreviewCarsL13() => Build(2, 12);
    [MenuItem("Tools/GET_IT/Preview - Cars Level 15")]
    public static void PreviewCarsL15() => Build(2, 14);

    // ── BİNALAR (dünya 3) önizlemeleri: bina sayısı %25 azaltma + delik büyüme kontrolü (2026-07-28) ──
    [MenuItem("Tools/GET_IT/Preview - Buildings Level 1")]
    public static void PreviewBldL1() => Build(3, 0);
    [MenuItem("Tools/GET_IT/Preview - Buildings Level 7")]
    public static void PreviewBldL7() => Build(3, 6);
    [MenuItem("Tools/GET_IT/Preview - Buildings Level 5")]
    public static void PreviewBldL5() => Build(3, 4);
    [MenuItem("Tools/GET_IT/Preview - Buildings Level 9")]
    public static void PreviewBldL9() => Build(3, 8);
    [MenuItem("Tools/GET_IT/Preview - Buildings Level 10")]
    public static void PreviewBldL10() => Build(3, 9);
    [MenuItem("Tools/GET_IT/Preview - Buildings Level 11")]
    public static void PreviewBldL11() => Build(3, 10);
    [MenuItem("Tools/GET_IT/Preview - Buildings Level 15")]
    public static void PreviewBldL15() => Build(3, 14);

    // ── Level-pass yardımcıları (2026-07-25): herhangi bir Foods levelını kur + sahnedeki tür sayımını logla ──
    [MenuItem("Tools/GET_IT/Preview - Foods Level 6")]
    public static void PreviewFoodsL6() => Build(1, 5);
    [MenuItem("Tools/GET_IT/Preview - Foods Level 7")]
    public static void PreviewFoodsL7() => Build(1, 6);
    [MenuItem("Tools/GET_IT/Preview - Foods Level 8")]
    public static void PreviewFoodsL8() => Build(1, 7);
    [MenuItem("Tools/GET_IT/Preview - Foods Level 9")]
    public static void PreviewFoodsL9() => Build(1, 8);
    [MenuItem("Tools/GET_IT/Preview - Foods Level 10")]
    public static void PreviewFoodsL10() => Build(1, 9);
    [MenuItem("Tools/GET_IT/Preview - Foods Level 11")]
    public static void PreviewFoodsL11() => Build(1, 10);
    [MenuItem("Tools/GET_IT/Preview - Foods Level 12")]
    public static void PreviewFoodsL12() => Build(1, 11);
    [MenuItem("Tools/GET_IT/Preview - Foods Level 13")]
    public static void PreviewFoodsL13() => Build(1, 12);
    [MenuItem("Tools/GET_IT/Preview - Foods Level 14")]
    public static void PreviewFoodsL14() => Build(1, 13);
    [MenuItem("Tools/GET_IT/Preview - Foods Level 15")]
    public static void PreviewFoodsL15() => Build(1, 14);

    // Kurulu önizlemedeki her nesne türünden KAÇ adet olduğunu loglar (hedef "sahne miktarı"nı bilmek için).
    [MenuItem("Tools/GET_IT/Count Scene Object Types")]
    public static void CountTypes()
    {
        var root = GameObject.Find("_LevelObjects");
        if (root == null) { Debug.LogError("[Count] _LevelObjects yok — önce bir Preview kur."); return; }
        var dict = new System.Collections.Generic.Dictionary<string, int>();
        foreach (var sw in root.GetComponentsInChildren<PhysicsSwallowable>(true))
        {
            string k = string.IsNullOrEmpty(sw.objectType) ? sw.name : sw.objectType;
            dict.TryGetValue(k, out int c); dict[k] = c + 1;
        }
        var sorted = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, int>>(dict);
        sorted.Sort((a, b) => b.Value - a.Value);
        var sb = new System.Text.StringBuilder($"[Count] Sahne nesne türleri ({sorted.Count} tür):\n");
        foreach (var kv in sorted) sb.AppendLine($"  {kv.Key}: {kv.Value}");
        Debug.Log(sb.ToString());
    }

    // ── DÜNYA HARİKALARI önizlemeleri (L3/6/9/12 = landmark levelları; LandmarkBuilder.cs) ──
    [MenuItem("Tools/GET_IT/Preview Landmark - Cars L3 (Tower Bridge)")]
    public static void PvCarsL3() => Build(2, 2);
    [MenuItem("Tools/GET_IT/Preview Landmark - Cars L6 (Big Ben)")]
    public static void PvCarsL6() => Build(2, 5);
    [MenuItem("Tools/GET_IT/Preview Landmark - Buildings L3 (Great Pyramid)")]
    public static void PvBldL3() => Build(3, 2);
    [MenuItem("Tools/GET_IT/Preview Landmark - Sweets L3 (Ferris Wheel)")]
    public static void PvSwtL3() => Build(4, 2);
    [MenuItem("Tools/GET_IT/Preview Landmark - Sweets L6 (Cappadocia)")]
    public static void PvSwtL6() => Build(4, 5);
    [MenuItem("Tools/GET_IT/Preview Landmark - Drinks L6 (Lighthouse)")]
    public static void PvDrkL6() => Build(5, 5);
    [MenuItem("Tools/GET_IT/Preview Landmark - Cats L3 (Torii Gate)")]
    public static void PvCatL3() => Build(9, 2);
    [MenuItem("Tools/GET_IT/Preview Landmark - Dogs L3 (Great Pyramid)")]
    public static void PvDogL3() => Build(10, 2);
    [MenuItem("Tools/GET_IT/Preview Landmark - Books L12 (Stonehenge)")]
    public static void PvBookL12() => Build(7, 11);
    [MenuItem("Tools/GET_IT/Preview Landmark - Ships L3 (Lighthouse)")]
    public static void PvShipL3() => Build(11, 2);
    [MenuItem("Tools/GET_IT/Preview Landmark - Books L3 (Big Ben)")]
    public static void PvBookL3() => Build(7, 2);
    [MenuItem("Tools/GET_IT/Preview Landmark - Money L3 (Gold Pyramid)")]
    public static void PvMoneyL3() => Build(13, 2);
    [MenuItem("Tools/GET_IT/Preview Landmark - Park L3 (Ferris Wheel)")]
    public static void PvParkL3() => Build(0, 2);
    [MenuItem("Tools/GET_IT/Preview Landmark - Gifts L9 (Basilica)")]
    public static void PvGiftL9() => Build(6, 8);
    [MenuItem("Tools/GET_IT/Preview Landmark - Planes L9 (Ferris Wheel)")]
    public static void PvPlaneL9() => Build(12, 8);
    [MenuItem("Tools/GET_IT/Preview Landmark - Jewelry L3 (Basilica)")]
    public static void PvJewL3() => Build(15, 2);
    [MenuItem("Tools/GET_IT/Preview Landmark - Park L9 (Windmill)")]
    public static void PvParkL9() => Build(0, 8);

    [MenuItem("Tools/GET_IT/Clear Preview")]
    public static void Clear()
    {
        DestroyByName("_LevelObjects");
        DestroyByName("_PlayArena");
        Debug.Log("[Preview] Temizlendi.");
    }

    static void Build(int world, int level)
    {
        var lm = Object.FindAnyObjectByType<LevelManager>();
        if (lm == null) { Debug.LogError("[Preview] Sahnede LevelManager yok (GameScene açık olmalı)."); return; }
        Clear();
        lm.BuildPreview(world, level);
        Debug.Log($"[Preview] Dünya {world} Level {level + 1} sahneye kuruldu. Scene'de incele; bitince Clear Preview. " +
                  "(Bu önizlemeyi sahneye KAYDETME.)");
    }

    static GameObject Load(string name) => AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/Foods/{name}.prefab");

    static void DestroyByName(string n)
    {
        var go = GameObject.Find(n);
        if (go != null) Object.DestroyImmediate(go);
    }
}
