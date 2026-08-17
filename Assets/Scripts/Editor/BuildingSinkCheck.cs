using UnityEditor;
using UnityEngine;

/// <summary>TANI (geçici): bina prefablarında CONVEX collider mesh'in DİBİ ile RENDERER dibi farkını ölçer
/// (dünya-Y). Fark > ~0.03 ise bina uyanınca o kadar BATAR. Menü: Tools/GET_IT/Check Building Collider Bottom.</summary>
public static class BuildingSinkCheck
{
    [MenuItem("Tools/GET_IT/Check Building Collider Bottom")]
    public static void Run()
    {
        string[] samples = { "Barracks", "NeonCityCorner", "AnimeCommoner", "TownHall", "Blue", "AncientEpheusLibrar" };
        var sb = new System.Text.StringBuilder("[SinkCheck] collider dibi - renderer dibi (dünya-Y); + = collider yukarıda → batar\n");
        int bad = 0;
        foreach (var nm in samples)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/Buildings/{nm}.prefab");
            if (prefab == null) { sb.AppendLine($"  {nm}: YOK"); continue; }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            try
            {
                var rends = go.GetComponentsInChildren<Renderer>();
                var mc = go.GetComponentInChildren<MeshCollider>();
                if (rends.Length == 0 || mc == null || mc.sharedMesh == null) { sb.AppendLine($"  {nm}: renderer/collider yok"); continue; }
                Bounds rb = rends[0].bounds; for (int i = 1; i < rends.Length; i++) rb.Encapsulate(rends[i].bounds);
                // collider mesh dünya-bounds: yerel bounds köşelerini transform et
                var lb = mc.sharedMesh.bounds; var m = mc.transform.localToWorldMatrix;
                float cMinY = float.PositiveInfinity;
                for (int c = 0; c < 8; c++)
                {
                    var corner = lb.center + Vector3.Scale(lb.extents, new Vector3((c&1)==0?-1:1,(c&2)==0?-1:1,(c&4)==0?-1:1));
                    float wy = m.MultiplyPoint3x4(corner).y;
                    if (wy < cMinY) cMinY = wy;
                }
                float gap = cMinY - rb.min.y;
                if (gap > 0.03f) bad++;
                sb.AppendLine($"  {nm}: rendMinY={rb.min.y:F3} colMinY={cMinY:F3} gap={gap:F3}{(gap>0.03f?"  ⚠️BATAR":"")}");
            }
            finally { Object.DestroyImmediate(go); }
        }
        sb.AppendLine($"BATAN örnek: {bad}/{samples.Length}");
        Debug.Log(sb.ToString());
    }
}
