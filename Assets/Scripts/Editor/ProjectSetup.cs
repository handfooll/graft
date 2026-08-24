using System.IO;
using Graft.Game;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace Graft.Editor
{
    /// <summary>
    /// One-shot, idempotent project configuration, runnable headlessly:
    ///
    /// <code>
    /// Unity -batchmode -nographics -quit -projectPath . \
    ///   -executeMethod Graft.Editor.ProjectSetup.ConfigureAll -logFile setup.log
    /// </code>
    ///
    /// Everything a fresh clone needs is here rather than in someone's editor session, so the
    /// project can be rebuilt from git alone.
    /// </summary>
    public static class ProjectSetup
    {
        public const string BundleIdentifier = "com.handfooll.graft";
        public const string CameraFramingPath = "Assets/Settings/CameraFraming.asset";

        public static void ConfigureAll()
        {
            ConfigurePlayer();
            var framing = EnsureCameraFraming();
            RenderPipelineSetup.Configure(framing);
            BootstrapSceneBuilder.Build(framing);
            SwitchToiOS();
            AssetDatabase.SaveAssets();
            Debug.Log($"[GRAFT] ProjectSetup.ConfigureAll complete. Active build target: {EditorUserBuildSettings.activeBuildTarget}, colour space: {PlayerSettings.colorSpace}.");
        }

        [MenuItem("GRAFT/Configure Project")]
        static void ConfigureAllFromMenu() => ConfigureAll();

        /// <summary>
        /// Loads the camera framing asset, creating it at its defaults if it is missing, so a fresh
        /// clone configures itself without anyone opening the editor.
        /// </summary>
        public static CameraFraming EnsureCameraFraming()
        {
            var existing = AssetDatabase.LoadAssetAtPath<CameraFraming>(CameraFramingPath);
            if (existing != null)
            {
                return existing;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(CameraFramingPath));
            var framing = ScriptableObject.CreateInstance<CameraFraming>();
            AssetDatabase.CreateAsset(framing, CameraFramingPath);
            Debug.Log($"[GRAFT] Created {CameraFramingPath} at defaults.");
            return framing;
        }

        static void ConfigurePlayer()
        {
            PlayerSettings.companyName = "handfooll";
            PlayerSettings.productName = "GRAFT";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, BundleIdentifier);
            PlayerSettings.colorSpace = ColorSpace.Linear;

            // D8: iOS first, touch from the start. Portrait - this is a one-thumb top-down game.
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;

            // 15.0, not lower: this editor version clamps anything below it back up on every
            // run, so asking for 13.0 just makes ProjectSettings.asset dirty every time.
            PlayerSettings.iOS.targetOSVersionString = "15.0";
            PlayerSettings.iOS.appleEnableAutomaticSigning = true;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.iOS, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetIl2CppCompilerConfiguration(NamedBuildTarget.iOS, Il2CppCompilerConfiguration.Release);

            // Note: the active input handler (Input System only, no legacy manager) is set in
            // ProjectSettings.asset directly - changing it through PlayerSettings needs an editor
            // restart to take effect. InputBackendTests guards it.
        }

        static void SwitchToiOS()
        {
            if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.iOS)
            {
                return;
            }

            if (!EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.iOS, BuildTarget.iOS))
            {
                Debug.LogWarning("[GRAFT] Could not switch the active build target to iOS.");
            }
        }
    }
}
