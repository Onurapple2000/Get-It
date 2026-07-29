using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tepside servis grubu: tepsi uyandığında üstündeki nesneleri birlikte uyandırır (önce tepsi hareketlenir,
/// sonra üzerindekiler). Tepsiye eklenir; üstteki nesnelerin PhysicsSwallowable.group buna işaret eder.
/// </summary>
public class TrayGroup : MonoBehaviour
{
    public List<PhysicsSwallowable> items = new List<PhysicsSwallowable>();
    bool done;

    public void WakeAll()
    {
        if (done) return;
        done = true;
        for (int i = 0; i < items.Count; i++)
            if (items[i] != null) items[i].WakeNow();
    }
}
