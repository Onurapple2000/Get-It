using System.IO;
using System.Xml;
using UnityEditor.Android;
using UnityEngine;

/// <summary>
/// Android build'inde (gradle projesi üretilince) AndroidManifest'e VIBRATE iznini OTOMATİK ekler.
/// Haptik (AudioManager) doğrudan Android Vibrator API'sini çağırdığından Unity izni kendiliğinden EKLEMEZ;
/// izin olmadan cihazda vibrate() sessizce başarısız olur (SecurityException, try/catch ile çökmez ama titremez).
/// Bu hook sayesinde ekstra manifest yönetmeye gerek yok — normal Build & Run yeterli.
/// </summary>
public class AndroidManifestPermissions : IPostGenerateGradleAndroidProject
{
    public int callbackOrder => 1;

    const string ANDROID_NS = "http://schemas.android.com/apk/res/android";
    const string PERMISSION = "android.permission.VIBRATE";

    public void OnPostGenerateGradleAndroidProject(string path)
    {
        string manifestPath = Path.Combine(path, "src", "main", "AndroidManifest.xml");
        if (!File.Exists(manifestPath))
        {
            Debug.LogWarning("[AndroidManifest] Manifest bulunamadı: " + manifestPath);
            return;
        }

        var doc = new XmlDocument();
        doc.Load(manifestPath);
        var manifest = doc.DocumentElement;   // <manifest>
        if (manifest == null) return;

        // Zaten ekli mi?
        foreach (XmlNode node in manifest.ChildNodes)
            if (node.Name == "uses-permission" && node is XmlElement e &&
                e.GetAttribute("name", ANDROID_NS) == PERMISSION)
                return;

        var perm = doc.CreateElement("uses-permission");
        var nameAttr = doc.CreateAttribute("android", "name", ANDROID_NS);
        nameAttr.Value = PERMISSION;
        perm.Attributes.Append(nameAttr);
        manifest.AppendChild(perm);
        doc.Save(manifestPath);
        Debug.Log("[AndroidManifest] VIBRATE izni eklendi → " + manifestPath);
    }
}
