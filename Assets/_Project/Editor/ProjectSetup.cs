using System.IO;
using HandSeals.Game;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HandSeals.EditorTools
{
    /// <summary>
    /// Creates the main scene and applies the player settings the game needs, so the repository does not have
    /// to carry hand-edited scene files. Runs once on the first import and is also available from the menu.
    /// </summary>
    public static class ProjectSetup
    {
        private const string ScenePath = "Assets/_Project/Scenes/Main.unity";

        [InitializeOnLoadMethod]
        private static void ScheduleFirstTimeSetup()
        {
            EditorApplication.delayCall += RunFirstTimeSetup;
        }

        private static void RunFirstTimeSetup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || File.Exists(ScenePath))
            {
                return;
            }

            // Only replace the untouched default scene of a fresh project; never someone's unsaved work.
            Scene active = SceneManager.GetActiveScene();
            if (!string.IsNullOrEmpty(active.path) || active.isDirty)
            {
                Debug.Log("HandSealAI: main scene is missing. Create it with the menu HandSealAI > Create main scene.");
                return;
            }

            CreateMainScene();
        }

        [MenuItem("HandSealAI/Create main scene")]
        public static void CreateMainSceneFromMenu()
        {
            if (File.Exists(ScenePath)
                && !EditorUtility.DisplayDialog("HandSealAI", "Main.unity already exists. Replace it?", "Replace", "Cancel"))
            {
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            CreateMainScene();
        }

        [MenuItem("HandSealAI/Apply player settings")]
        public static void ApplyPlayerSettings()
        {
            PlayerSettings.productName = "HandSealAI";
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;

            // Keep an identifier the developer has already chosen.
            string identifier = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android);
            if (string.IsNullOrEmpty(identifier) || identifier.StartsWith("com.DefaultCompany") || identifier.StartsWith("com.Company."))
            {
                PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.handsealai.game");
            }

            // Phones sold in the last years are 64-bit; many of them refuse to install a 32-bit-only build.
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;

            Debug.Log("HandSealAI: player settings applied (portrait, product name, Android identifier, IL2CPP + ARM64).");
        }

        private static void CreateMainScene()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // The HUD is a screen-space overlay, so the camera only has to clear the screen.
            var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            cameraObject.AddComponent<AudioListener>();

            new GameObject("App").AddComponent<AppRoot>();

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

            ApplyPlayerSettings();
            AssetDatabase.SaveAssets();

            Debug.Log("HandSealAI: created " + ScenePath + " and added it to the build settings. Press Play to try it.");
        }
    }
}
