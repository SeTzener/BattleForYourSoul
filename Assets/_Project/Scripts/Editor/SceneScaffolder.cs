using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using BattleForYourSoul.Core;
using BattleForYourSoul.UI;

namespace BattleForYourSoul.EditorTools
{
    /// <summary>
    /// Creates the Bootstrap and MainMenu scenes and keeps the Build Settings scene list in order.
    /// The Game scene is owned by <see cref="GameSceneBuilder"/>.
    /// </summary>
    public static class SceneScaffolder
    {
        public const string BootstrapScenePath = GameSceneBuilder.ScenesFolder + "/Bootstrap.unity";
        public const string MainMenuScenePath = GameSceneBuilder.ScenesFolder + "/MainMenu.unity";

        private const string TemplateScenePath = "Assets/Scenes/SampleScene.unity";
        private const string TemplateSceneFolder = "Assets/Scenes";

        [MenuItem("Tools/BattleForYourSoul/Build All Scenes")]
        public static void BuildAllScenes()
        {
            BuildBootstrapScene();
            BuildMainMenuScene();
            GameSceneBuilder.BuildGameScene();
            UpdateBuildSettings();
            RemoveTemplateScene();
            AssetDatabase.SaveAssets();
            Debug.Log("[SceneScaffolder] All scenes built and Build Settings updated.");
        }

        [MenuItem("Tools/BattleForYourSoul/Build Bootstrap Scene")]
        public static void BuildBootstrapScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            CreateSimpleCamera();

            var bootstrapGo = new GameObject("Bootstrap");
            bootstrapGo.AddComponent<GameBootstrap>();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, BootstrapScenePath);
        }

        [MenuItem("Tools/BattleForYourSoul/Build Main Menu Scene")]
        public static void BuildMainMenuScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            CreateSimpleCamera();

            var controllerGo = new GameObject("MainMenu");
            var controller = controllerGo.AddComponent<MainMenuController>();

            var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            var buttonGo = new GameObject("PlayButton", typeof(RectTransform), typeof(Image), typeof(Button));
            buttonGo.transform.SetParent(canvasGo.transform, false);
            var buttonRect = buttonGo.GetComponent<RectTransform>();
            buttonRect.anchorMin = new Vector2(0.5f, 0.5f);
            buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
            buttonRect.pivot = new Vector2(0.5f, 0.5f);
            buttonRect.sizeDelta = new Vector2(320f, 96f);
            buttonRect.anchoredPosition = Vector2.zero;

            var image = buttonGo.GetComponent<Image>();
            image.color = new Color32(70, 116, 206, 255);

            var button = buttonGo.GetComponent<Button>();
            button.targetGraphic = image;

            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            labelGo.transform.SetParent(buttonGo.transform, false);
            var labelRect = labelGo.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            var label = labelGo.GetComponent<Text>();
            label.text = "PLAY";
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            label.fontSize = 48;
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            UnityEventTools.AddPersistentListener(button.onClick, controller.PlayGame);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, MainMenuScenePath);
        }

        private static void CreateSimpleCamera()
        {
            var cameraGo = new GameObject("Main Camera");
            cameraGo.tag = "MainCamera";
            cameraGo.transform.position = new Vector3(0f, 0f, -10f);

            var camera = cameraGo.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color32(24, 22, 32, 255);
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
        }

        private static void UpdateBuildSettings()
        {
            var scenes = new List<EditorBuildSettingsScene>
            {
                new EditorBuildSettingsScene(BootstrapScenePath, true),
                new EditorBuildSettingsScene(MainMenuScenePath, true),
                new EditorBuildSettingsScene(GameSceneBuilder.GameScenePath, true),
            };

            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static void RemoveTemplateScene()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(TemplateScenePath) != null)
            {
                AssetDatabase.DeleteAsset(TemplateScenePath);
            }

            // Remove the now-empty template folder if nothing else lives in it.
            if (AssetDatabase.IsValidFolder(TemplateSceneFolder) &&
                AssetDatabase.FindAssets(string.Empty, new[] { TemplateSceneFolder }).Length == 0)
            {
                AssetDatabase.DeleteAsset(TemplateSceneFolder);
            }
        }
    }
}
