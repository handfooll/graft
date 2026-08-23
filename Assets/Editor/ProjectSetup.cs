using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Graft.EditorTools
{
    /// <summary>
    /// One-shot project configuration, runnable headlessly:
    ///   Unity -batchmode -quit -projectPath . -executeMethod Graft.EditorTools.ProjectSetup.ConfigureAll
    ///
    /// The scene it creates is deliberately empty. GameFlow builds the whole game from code
    /// at startup via [RuntimeInitializeOnLoadMethod], so there is no node tree in a .unity
    /// file that can drift out of sync with the scripts.
    /// </summary>
    public static class ProjectSetup
    {
        const string ScenePath = "Assets/Scenes/Arena.unity";

        [MenuItem("GRAFT/Configure Project")]
        public static void ConfigureAll()
        {
            CreateBootScene();
            ConfigurePlayerSettings();
            AssetDatabase.SaveAssets();
            Debug.Log("[GRAFT] Project configured.");
        }

        static void CreateBootScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, ScenePath);

            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(ScenePath, true)
            };
            Debug.Log("[GRAFT] Boot scene written to " + ScenePath);
        }

        static void ConfigurePlayerSettings()
        {
            PlayerSettings.companyName = "Handfooll";
            PlayerSettings.productName = "GRAFT";
            PlayerSettings.bundleVersion = "0.1.0";

            // Portrait, one-thumb (GDD pillar 3).
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;

            PlayerSettings.SetApplicationIdentifier(
                NamedBuildTarget.iOS, "com.handfooll.graft");
            PlayerSettings.SetApplicationIdentifier(
                NamedBuildTarget.Android, "com.handfooll.graft");

            PlayerSettings.iOS.targetOSVersionString = "15.0";
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24;

            QualitySettings.vSyncCount = 0;
        }
    }
}
