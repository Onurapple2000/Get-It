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

        // ⚠️ FAZ 1: Addressables SADECE organizasyon iskeleti. Player build'de Addressables İÇERİĞİ DERLENMESİN
        // (yoksa her build ~1GB modeli LZ sıkıştırır → çok yavaş "archive/compress bundle" adımı + çöp-dosyada
        // patlama). Oyun şu an DOĞRUDAN referansla yükleniyor → build'e assetler zaten girer, oyun çalışır.
        // FAZ 2'de (Sprint 9, yükleme refactor) bu BuildWithPlayer'a çevrilecek.
        settings.BuildAddressablesWithPlayerBuild = AddressableAssetSettings.PlayerBuildOption.DoNotBuildWithPlayer;

        // Çekirdek / paylaşılan → install-time (ilk indirme)
        AssignFolder(settings, "Core", "install-time", "Assets/Prefabs/PowerUps");
        AssignAsset (settings, "Core", "install-time", "Assets/Prefabs/Bomb.prefab");

        // Dünyalar → on-demand (her biri ayrı paket). Prefab klasörü + modelleri (bağımlılık olarak) taşınır.
        AssignFolder(settings, "World0-Park",  "on-demand", "Assets/Prefabs/DecoObjects");
        AssignFolder(settings, "World1-Foods", "on-demand", "Assets/Prefabs/Foods");
        AssignFolder(settings, "World1-Foods", "on-demand", "Assets/Art/worlds/foods");
        AssignFolder(settings, "World2-Cars",  "on-demand", "Assets/Prefabs/Cars");
        AssignFolder(settings, "World2-Cars",  "on-demand", "Assets/Art/worlds/cars");
        AssignFolder(settings, "World3-Buildings", "on-demand", "Assets/Prefabs/Buildings");
        AssignFolder(settings, "World3-Buildings", "on-demand", "Assets/Art/worlds/buildings");
        AssignFolder(settings, "World4-Sweets", "on-demand", "Assets/Prefabs/Sweets");
        AssignFolder(settings, "World4-Sweets", "on-demand", "Assets/Art/worlds/sweets");
        AssignFolder(settings, "World5-Drinks", "on-demand", "Assets/Prefabs/Drinks");
        AssignFolder(settings, "World5-Drinks", "on-demand", "Assets/Art/worlds/drinks");
        AssignFolder(settings, "World6-Gifts", "on-demand", "Assets/Prefabs/Gifts");
        AssignFolder(settings, "World6-Gifts", "on-demand", "Assets/Art/worlds/gifts");
        AssignFolder(settings, "World7-Books", "on-demand", "Assets/Prefabs/Books");
        AssignFolder(settings, "World7-Books", "on-demand", "Assets/Art/worlds/books");
        AssignFolder(settings, "World9-Cats", "on-demand", "Assets/Prefabs/Cats");
        AssignFolder(settings, "World9-Cats", "on-demand", "Assets/Art/worlds/cats");
        AssignFolder(settings, "World10-Dogs", "on-demand", "Assets/Prefabs/Dogs");
        AssignFolder(settings, "World10-Dogs", "on-demand", "Assets/Art/worlds/dogs");
        AssignFolder(settings, "World11-Ships", "on-demand", "Assets/Prefabs/Ships");
        AssignFolder(settings, "World11-Ships", "on-demand", "Assets/Art/worlds/ships");
        AssignFolder(settings, "World12-Planes", "on-demand", "Assets/Prefabs/Planes");
        AssignFolder(settings, "World12-Planes", "on-demand", "Assets/Art/worlds/planes");
        AssignFolder(settings, "World13-Treasure", "on-demand", "Assets/Prefabs/Money");
        AssignFolder(settings, "World13-Treasure", "on-demand", "Assets/Art/worlds/moneys");
        AssignFolder(settings, "World15-Jewelry", "on-demand", "Assets/Prefabs/Jewelry");
        AssignFolder(settings, "World15-Jewelry", "on-demand", "Assets/Art/worlds/jevelary");

        AssetDatabase.SaveAssets();
        Debug.Log("[Addressables] Grup iskeleti kuruldu: Core(install-time) + World0/1/2(on-demand). " +
                  "Not: gerçek on-demand boyut kazancı Sprint 9 yükleme refactor'ü ile gelir.");
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
