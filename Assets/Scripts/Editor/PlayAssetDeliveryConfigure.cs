using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

/// <summary>
/// ASSET DELIVERY FAZ 2 / Aşama C — "Addressables for Android" (com.unity.addressables.android) paketini DİYALOGSUZ
/// başlatır ve her gruba Play Asset Delivery şeması + teslimat tipini atar:
///   Core, ilk dünya (WorldPacks.IsInstallTime), Default Local Group → Install Time   (ilk indirmede gelir)
///   diğer dünyalar                                                     → On Demand      (oyuncu ilerledikçe iner)
/// Paket API'sine REFLECTION ile erişir → paket yokken de derlenir (menü hata mesajı verir).
/// Menü: Tools/GET_IT/Asset Delivery/Configure Play Asset Delivery (idempotent; Setup Addressable Groups'tan SONRA çalıştır).
/// AAB + Split Application Binary açıkken paket, grupları otomatik asset pack'e çevirir; APK build'de StreamingAssets'e koyar.
/// </summary>
public static class PlayAssetDeliveryConfigure
{
    [MenuItem("Tools/GET_IT/Asset Delivery/Configure Play Asset Delivery")]
    public static void Run()
    {
        var setupType  = Type.GetType("UnityEditor.AddressableAssets.Android.PlayAssetDeliverySetup, Unity.Addressables.Android.Editor");
        var schemaType = Type.GetType("UnityEditor.AddressableAssets.Android.PlayAssetDeliverySchema, Unity.Addressables.Android.Editor");
        var deliveryType = Type.GetType("UnityEngine.AddressableAssets.Android.DeliveryType, Unity.Addressables.Android");
        if (setupType == null || schemaType == null || deliveryType == null)
        { Debug.LogError("[PAD] com.unity.addressables.android paketi bulunamadı (Packages/manifest.json → 1.0.6)."); return; }

        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null) { Debug.LogError("[PAD] Addressables settings yok."); return; }

        // 1) Init (diyalogsuz): ForcePADToExistingAddressablesGroup = true → mevcut gruplara şema otomatik eklenir
        var force = setupType.GetProperty("ForcePADToExistingAddressablesGroup", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        force?.SetValue(null, (bool?)true);
        setupType.GetMethod("InitPlayAssetDelivery", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)?.Invoke(null, null);
        force?.SetValue(null, null);

        // 2) Teslimat tipleri
        object installTime = Enum.Parse(deliveryType, "InstallTime");
        object onDemand    = Enum.Parse(deliveryType, "OnDemand");
        var prop = schemaType.GetProperty("AssetPackDeliveryType");
        int it = 0, od = 0;
        foreach (var g in settings.groups)
        {
            if (g == null) continue;
            var schema = g.GetSchema(schemaType) ?? g.AddSchema(schemaType);
            if (schema == null) continue;
            bool install = g.Name == WorldPacks.Core || g.Name == "Default Local Group";
            foreach (var t in WorldPacks.Table) if (t.pack == g.Name && WorldPacks.IsInstallTime(t.world)) install = true;
            prop.SetValue(schema, install ? installTime : onDemand);
            EditorUtility.SetDirty(schema);
            if (install) it++; else od++;
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"[PAD] Play Asset Delivery yapılandırıldı: {it} install-time grup, {od} on-demand grup. " +
                  "AAB (Split AÇIK) build'de asset pack'ler otomatik üretilir.");
    }
}

/// <summary>Addressables içeriğini (aktif builder ile) derler ve bundle boyutlarını raporlar → Builds/addressables-sizes.txt</summary>
public static class AddressablesSizeReport
{
    [MenuItem("Tools/GET_IT/Asset Delivery/Build Addressables Content + Size Report")]
    public static void Run()
    {
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        var t0 = DateTime.Now;
        AddressableAssetSettings.BuildPlayerContent(out var result);
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"[Addressables build] {(DateTime.Now - t0).TotalSeconds:F0} sn, hata: {(string.IsNullOrEmpty(result.Error) ? "-" : result.Error)}");
        var dir = result.OutputPath != null ? System.IO.Path.GetDirectoryName(result.OutputPath) : null;
        if (dir == null || !System.IO.Directory.Exists(dir)) dir = "Library/com.unity.addressables/aa/Android";
        long total = 0;
        foreach (var f in System.IO.Directory.GetFiles(dir, "*", System.IO.SearchOption.AllDirectories))
        {
            var fi = new System.IO.FileInfo(f); total += fi.Length;
            sb.AppendLine($"{fi.Length / 1048576.0,8:F1} MB  {f.Substring(dir.Length + 1)}");
        }
        sb.AppendLine($"TOPLAM {total / 1048576.0:F1} MB");
        System.IO.Directory.CreateDirectory("Builds");
        System.IO.File.WriteAllText("Builds/addressables-sizes.txt", sb.ToString());
        Debug.Log(sb.ToString());
    }
}
