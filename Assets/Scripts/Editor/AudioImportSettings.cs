using UnityEditor;
using UnityEngine;

/// <summary>
/// Sprint 6 — `Assets/Resources/Audio/` altındaki tüm ses dosyalarına otomatik import ayarı (build boyutu + RAM):
///  • Müzik (`/Audio/Music/`): uzun döngü klipleri → **Streaming** (2dk klip RAM'e açılmaz), loadInBackground, Vorbis q0.5.
///  • SFX (`/Audio/SFX/`): kısa tek-atımlar → **DecompressOnLoad** (anında çalsın, gecikmesiz), Vorbis q0.6.
/// Kaynak MP3'ler zaten küçük; bu ayar build'i Vorbis'e sıkıştırır ve loop'ların bellek maliyetini keser.
/// Reimport: `Tools/GET_IT/Reimport Audio` (ayarların mevcut dosyalara uygulanması için).
/// </summary>
public class AudioImportSettings : AssetPostprocessor
{
    void OnPreprocessAudio()
    {
        if (!assetPath.Replace('\\', '/').Contains("/Resources/Audio/")) return;

        var importer = (AudioImporter)assetImporter;
        bool isMusic = assetPath.Replace('\\', '/').Contains("/Audio/Music/");

        var s = importer.defaultSampleSettings;
        s.compressionFormat = AudioCompressionFormat.Vorbis;
        s.quality = isMusic ? 0.5f : 0.6f;
        s.loadType = isMusic ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
        s.preloadAudioData = !isMusic;   // müzik gecikmeli/streaming; SFX önden hazır
        importer.defaultSampleSettings = s;

        importer.loadInBackground = isMusic;
        importer.forceToMono = false;
    }

    [MenuItem("Tools/GET_IT/Reimport Audio")]
    static void ReimportAudio()
    {
        AssetDatabase.ImportAsset("Assets/Resources/Audio", ImportAssetOptions.ImportRecursive);
        Debug.Log("[AudioImportSettings] Resources/Audio yeniden import edildi (Streaming/Vorbis ayarları uygulandı).");
    }
}
