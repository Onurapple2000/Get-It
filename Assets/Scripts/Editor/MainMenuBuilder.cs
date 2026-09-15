using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// MainMenu sahnesini kurar (Sprint 3 Faz A): Kamera + Canvas + EventSystem + MainMenuController + LivesHud,
/// sprite referansları atanır, Build Settings'e MainMenu(0)+GameScene eklenir. Menu: Tools/GET_IT/Build Main Menu
/// </summary>
public static class MainMenuBuilder
{
    const string SCENE_PATH = "Assets/Scenes/MainMenu.unity";

    [MenuItem("Tools/GET_IT/Build Main Menu")]
    public static void Build()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // Kamera
        var camGo = new GameObject("Main Camera");
        var cam = camGo.AddComponent<Camera>();
        cam.tag = "MainCamera";
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.18f, 0.14f, 0.11f);
        cam.orthographic = true;
        camGo.AddComponent<AudioListener>();

        // Canvas
        var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight = 0.5f;

        // EventSystem
        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

        // Menu controller + can göstergesi (LivesHud)
        var menu = new GameObject("MainMenu", typeof(MainMenuController), typeof(LivesHud));
        var mc = menu.GetComponent<MainMenuController>();
        mc.gameScene = "GameScene";
        mc.bgSprite     = LoadSprite("Assets/Art/burrow_bg_warm.png");
        mc.moleSprite   = LoadSprite("Assets/Art/mole_mascot_warm.png");
        mc.proudMoleSprite = LoadSprite("Assets/Art/mole_mascot_proud.png");   // ana sayfa büyük maskotu
        mc.buttonSprite = LoadSprite("Assets/Resources/burrow_button_empty_rect.png");

        // 18 dünya ikonu — WorldCatalog sırasıyla
        mc.worldIcons = new Sprite[WorldCatalog.Count];
        for (int i = 0; i < WorldCatalog.Count; i++)
            mc.worldIcons[i] = LoadSprite($"Assets/Art/worlds_icons/{WorldCatalog.Icons[i]}.png");

        EditorSceneManager.SaveScene(scene, SCENE_PATH);
        AddToBuildSettings();

        Debug.Log("[MainMenu] MainMenu sahnesi kuruldu + Build Settings güncellendi (MainMenu=0, GameScene=1).");
    }

    static Sprite LoadSprite(string path)
    {
        var s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (s == null) Debug.LogWarning($"[MainMenu] sprite yok/Sprite tipinde değil: {path}");
        return s;
    }

    static void AddToBuildSettings()
    {
        var list = new List<EditorBuildSettingsScene>
        {
            new EditorBuildSettingsScene(SCENE_PATH, true),
            new EditorBuildSettingsScene("Assets/Scenes/GameScene.unity", true),
        };
        EditorBuildSettings.scenes = list.ToArray();
    }
}
