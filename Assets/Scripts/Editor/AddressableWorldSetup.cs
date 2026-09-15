using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

/// <summary>
/// Sprint 5/9 — KÜÇÜK İLK İNDİRME için Addressables grup İSKELETİ (Faz 1). Her dünyayı ayrı gruba/etikete koyar:
///  • Core (install-time): paylaşılan + çekirdek (PowerUps, Bomb) — ilk indirmede.
///  • World{N}-... (on-demand): o dünyanın ağır prefab/model/texture'ları — çalışırken indirilecek.
/// Böylece ilk indirme = çekirdek + Dünya 0; sonrakiler oyuncu ilerledikçe (bir dünya önden prefetch) inecek.
///
/// ⚠️ Bu SADECE grup organizasyonu (iskelet). GERÇEK on-demand/boyut kazancı, Sprint 9'daki YÜKLEME REFACTOR'ü ile
/// gelir: LevelData → AssetReference, LevelManager async yükleme + dünyaya girince sonrakini prefetch + PAD build.
/// O zamana kadar LevelData doğrudan referansla çalışır (runtime aynen çalışır; sadece dev build'de geçici çift
/// içerme olabilir — sürümde önemli değil). İçerik bu gruplara yazıldıkça sonradan reorganizasyon derdi olmaz.
///
/// Menü: Tools/GET_IT/Setup Addressable Groups (idempotent — yeni dünya klasörü ekleyince tekrar çalıştır).
/// </summary>
public static class AddressableWorldSetup
{
    [MenuItem("Tools/GET_IT/Setup Addressable Groups")]
    public static void Setup()
    {
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
            settings = AddressableAssetSettingsDefaultObject.GetSettings(true);   // ilk kez: default ayar+grup üret
        if (settings == null) { Debug.LogError("[Addressables] Settings oluşturulamadı."); return; }

        settings.AddLabel("install-time");
        settings.AddLabel("on-demand");

        // FAZ 2 (2026-09-15): içerik ARTIK Addressables'tan yükleniyor (LevelData → AssetReference, WorldContentLoader)
        // → Addressables içeriği player build ile birlikte derlenir (PAD paketi bunu asset pack'lere böler).
        settings.BuildAddressablesWithPlayerBuild = AddressableAssetSettings.PlayerBuildOption.BuildWithPlayer;

        // Eski tireli grup adları (World1-Foods) Google asset pack kuralına uymaz (harf/rakam/alt çizgi) → yeniden adlandır.
        foreach (var t in WorldPacks.Table)
        {
            var old = settings.FindGroup(t.pack.Replace('_', '-'));
            if (old != null && old.Name != t.pack) { old.Name = t.pack; Debug.Log($"[Addressables] Grup adı düzeltildi: {old.Name}"); }
        }

        // Core (install-time): PowerUp prefab'ları — sahnede DEĞİL, yalnız LevelData spawn listelerinde → Addressables'tan
        // yüklenmek ZORUNDA. Bomb.prefab BURADA YOK: LevelData.bombPrefab doğrudan ref → zaten base'de (çift kopya olmasın).
        AssignFolder(settings, WorldPacks.Core, "install-time", "Assets/Prefabs/PowerUps");
        var coreG = settings.FindGroup(WorldPacks.Core);
        if (coreG != null)
        {
            var bombGuid = AssetDatabase.AssetPathToGUID("Assets/Prefabs/Bomb.prefab");
            if (!string.IsNullOrEmpty(bombGuid) && coreG.GetAssetEntry(bombGuid) != null) settings.RemoveAssetEntry(bombGuid, false);
        }

        // Dünyalar: YALNIZ prefab klasörü girdi olur; modeller/dokular prefab bağımlılığı olarak AYNI bundle'a girer.
        // (Art klasörünü de girdi yapmak, kullanılmayan GLB/doku'ları da pakete sokup şişirir.)
        foreach (var t in WorldPacks.Table)
            AssignFolder(settings, t.pack, WorldPacks.IsInstallTime(t.world) ? "install-time" : "on-demand", t.prefabFolder);

        // Eski Art klasörü girdilerini kaldır (varsa)
        foreach (var t in WorldPacks.Table)
        {
            var g = settings.FindGroup(t.pack);
            if (g == null) continue;
            var toRemove = new System.Collections.Generic.List<AddressableAssetEntry>();
            foreach (var e in g.entries) if (e.AssetPath.StartsWith("Assets/Art/")) toRemove.Add(e);
            foreach (var e in toRemove) settings.RemoveAssetEntry(e.guid, false);
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[Addressables] Gruplar kuruldu: Core + {WorldPacks.Table.Length} dünya (install-time: ilk dünya; gerisi on-demand). BuildWithPlayer açık.");
    }

    static AddressableAssetGroup GetOrCreateGroup(AddressableAssetSettings s, string name)
    {
        var g = s.FindGroup(name);
        if (g == null)
            g = s.CreateGroup(name, false, false, false, null,
                              typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
        return g;
    }

    static void AssignFolder(AddressableAssetSettings s, string group, string label, string folderPath)
    {
        if (!AssetDatabase.IsValidFolder(folderPath)) { Debug.LogWarning("[Addressables] Klasör yok, atlandı: " + folderPath); return; }
        AssignGuid(s, group, label, AssetDatabase.AssetPathToGUID(folderPath), folderPath);
    }

    static void AssignAsset(AddressableAssetSettings s, string group, string label, string assetPath)
    {
        string guid = AssetDatabase.AssetPathToGUID(assetPath);
        if (string.IsNullOrEmpty(guid)) { Debug.LogWarning("[Addressables] Asset yok, atlandı: " + assetPath); return; }
        AssignGuid(s, group, label, guid, assetPath);
    }

    static void AssignGuid(AddressableAssetSettings s, string group, string label, string guid, string address)
    {
        if (string.IsNullOrEmpty(guid)) return;
        var g = GetOrCreateGroup(s, group);
        var entry = s.CreateOrMoveEntry(guid, g, false, false);
        if (entry == null) return;
        entry.SetAddress(address, false);
        entry.SetLabel(label, true, true, false);
    }
}
