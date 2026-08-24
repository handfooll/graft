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

        public static void ConfigureAll()
        {
            ConfigurePlayer();
            RenderPipelineSetup.Configure();
            BootstrapSceneBuilder.Build();
            SwitchToiOS();
            AssetDatabase.SaveAssets();
            Debug.Log($"[GRAFT] ProjectSetup.ConfigureAll complete. Active build target: {EditorUserBuildSettings.activeBuildTarget}, colour space: {PlayerSettings.colorSpace}.");
        }

        [MenuItem("GRAFT/Configure Project")]
        static void ConfigureAllFromMenu() => ConfigureAll();

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

            PlayerSettings.iOS.targetOSVersionString = "13.0";
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
