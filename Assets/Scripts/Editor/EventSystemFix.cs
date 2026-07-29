using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// com.unity.inputsystem paketi kaldırılınca GameScene'in EventSystem'indeki InputSystemUIInputModule
/// MISSING SCRIPT oldu → oyun içi HİÇBİR UI butonu çalışmıyordu (pause/success/fail). Bu araç iki sahnede de
/// EventSystem'i onarır: missing component'leri temizler + StandaloneInputModule (eski Input ile çalışır) ekler.
/// Menü: Tools/GET_IT/Fix EventSystems (Standalone Input).
/// </summary>
public static class EventSystemFix
{
    static readonly string[] Scenes = { "Assets/Scenes/GameScene.unity", "Assets/Scenes/MainMenu.unity" };

    [MenuItem("Tools/GET_IT/Fix EventSystems (Standalone Input)")]
    public static void Fix()
    {
        string startScene = EditorSceneManager.GetActiveScene().path;

        foreach (var scenePath in Scenes)
        {
            var scene = EditorSceneManager.GetActiveScene().path == scenePath
                ? EditorSceneManager.GetActiveScene()
                : EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            bool dirty = false;
            var es = Object.FindFirstObjectByType<EventSystem>();
            if (es == null)
            {
                var go = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
                Debug.Log($"[EventSystemFix] {scenePath}: EventSystem YOKTU → oluşturuldu (Standalone).");
                dirty = true;
            }
            else
            {
                // Missing script (kaldırılan InputSystemUIInputModule) bileşenlerini temizle
                int removed = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(es.gameObject);
                if (removed > 0) { Debug.Log($"[EventSystemFix] {scenePath}: {removed} missing script kaldırıldı."); dirty = true; }

                if (es.GetComponent<StandaloneInputModule>() == null)
                {
                    es.gameObject.AddComponent<StandaloneInputModule>();
                    Debug.Log($"[EventSystemFix] {scenePath}: StandaloneInputModule eklendi.");
                    dirty = true;
                }
            }

            if (dirty) EditorSceneManager.SaveScene(scene);
            else Debug.Log($"[EventSystemFix] {scenePath}: zaten sağlıklı.");
        }

        // başlangıç sahnesine dön
        if (!string.IsNullOrEmpty(startScene) && EditorSceneManager.GetActiveScene().path != startScene)
            EditorSceneManager.OpenScene(startScene, OpenSceneMode.Single);

        Debug.Log("[EventSystemFix] BİTTİ — oyun içi butonlar artık eski Input ile çalışır.");
    }
}
