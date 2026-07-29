using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Paylaşılan nesne fizik malzemesi: STATİK yüksek (durağanken stabil — kuleler durur) + DİNAMİK düşük
/// (hareket halinde kaygan — deliğe düşerken takılmadan kolay ayrılır). Foods/PowerUps/Deco collider'larına
/// atanır. HoleFloor kendi sürtünmesiz malzemesini kullanır (Minimum combine → nesne-zemin teması 0).
/// </summary>
public static class GripMat
{
    const string PATH = "Assets/Materials/ObjectGrip.physicMaterial";

    public static PhysicsMaterial Get()
    {
        var m = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(PATH);
        if (m == null)
        {
            EnsureDir("Assets/Materials");
            m = new PhysicsMaterial("ObjectGrip");
            AssetDatabase.CreateAsset(m, PATH);
        }
        m.staticFriction = 0.5f;
        m.dynamicFriction = 0.04f;   // düşük dinamik: deliğe düşerken takılmadan ayrılır (kayma nesne damping'iyle çözülür)
        m.frictionCombine = PhysicsMaterialCombine.Average;
        m.bounceCombine = PhysicsMaterialCombine.Minimum;
        m.bounciness = 0f;
        EditorUtility.SetDirty(m);
        return m;
    }

    static void EnsureDir(string assetPath)
    {
        string full = Path.Combine(Application.dataPath, assetPath.Substring("Assets/".Length));
        if (!Directory.Exists(full)) { Directory.CreateDirectory(full); AssetDatabase.Refresh(); }
    }
}
