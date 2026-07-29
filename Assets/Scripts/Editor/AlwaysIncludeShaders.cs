using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Runtime'da Shader.Find(...) ile aranan URP shader'larını "Always Included Shaders"a ekler.
/// Aksi halde bu shader'ları hiçbir material ASSET'i kullanmadığından build'de STRIP edilir →
/// cihazda Shader.Find null döner → new Material(null) ArgumentNullException → HoleFloor/OuterFade çöker
/// (zemin ne render olur ne katı; nesneler her yere batar). Editörde tüm shader'lar olduğundan görünmez.
/// Menu: Tools/GET_IT/Fix Always-Included Shaders
/// </summary>
public static class AlwaysIncludeShaders
{
    static readonly string[] Needed =
    {
        "Universal Render Pipeline/Unlit",
        "Universal Render Pipeline/Lit",
        "Sprites/Default",
    };

    [MenuItem("Tools/GET_IT/Fix Always-Included Shaders")]
    public static void Run()
    {
        var gs = GraphicsSettings.GetGraphicsSettings();
        var so = new SerializedObject(gs);
        var arr = so.FindProperty("m_AlwaysIncludedShaders");

        var existing = new HashSet<Object>();
        for (int i = 0; i < arr.arraySize; i++)
            existing.Add(arr.GetArrayElementAtIndex(i).objectReferenceValue);

        int added = 0;
        foreach (var name in Needed)
        {
            var sh = Shader.Find(name);
            if (sh == null) { Debug.LogWarning($"[Shaders] bulunamadı (editörde de yok?): {name}"); continue; }
            if (existing.Contains(sh)) continue;
            int idx = arr.arraySize;
            arr.InsertArrayElementAtIndex(idx);
            arr.GetArrayElementAtIndex(idx).objectReferenceValue = sh;
            existing.Add(sh);
            added++;
            Debug.Log($"[Shaders] Always Included'a eklendi: {name}");
        }

        so.ApplyModifiedProperties();
        AssetDatabase.SaveAssets();
        Debug.Log($"[Shaders] BİTTİ. {added} shader eklendi. Artık build'de strip edilmeyecekler.");
    }
}
