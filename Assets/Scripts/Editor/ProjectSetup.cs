using System.IO;
using Graft.Core;
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
        public const string MovementTuningPath = "Assets/Settings/MovementTuning.asset";
        public const string CharacterProportionsPath = "Assets/Settings/CharacterProportions.asset";
        /// <summary>
        /// **Which world gets built.** One line to switch, then re-run <see cref="ConfigureAll"/> -
        /// both layouts are kept as assets precisely so the two can be compared on a device instead
        /// of on paper. The corridor was the Phase 2 gate's first world; the cluster replaced it on
        /// the author's call, 2026-08-24, because a one-dimensional world made every trip between
        /// two biomes a traverse of everything between them.
        /// </summary>
        public const WorldShape ActiveWorldShape = WorldShape.Cluster;

        public const string ClusterLayoutPath = "Assets/Settings/WorldLayout_Cluster.asset";
        public const string CorridorLayoutPath = "Assets/Settings/WorldLayout_Corridor.asset";

        public static void ConfigureAll()
        {
            ConfigurePlayer();
            var framing = EnsureCameraFraming();
            var movement = EnsureMovementTuning();
            var shape = EnsureCharacterProportions();
            // Both are ensured, so the one that is not being built is still there to switch to.
            EnsureWorldLayout(WorldShape.Corridor);
            EnsureWorldLayout(WorldShape.Cluster);
            var layout = EnsureWorldLayout();
            RenderPipelineSetup.Configure(framing);
            BootstrapSceneBuilder.Build(framing, movement, shape, layout);
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
        public static CameraFraming EnsureCameraFraming() =>
            EnsureAsset<CameraFraming>(CameraFramingPath);

        /// <summary>
        /// Loads the movement tuning asset, creating it at its defaults if missing. Same reasoning
        /// as <see cref="EnsureCameraFraming"/>: a fresh clone configures itself from git alone.
        /// </summary>
        public static MovementTuning EnsureMovementTuning() =>
            EnsureAsset<MovementTuning>(MovementTuningPath);

        /// <summary>
        /// Loads the character silhouette, creating it at its defaults if missing. Phase 6 replaces
        /// the primitives with real meshes, but the proportions chosen here are what the camera and
        /// the graft sockets were built against, so they outlive the primitives.
        /// </summary>
        public static CharacterProportions EnsureCharacterProportions() =>
            EnsureAsset<CharacterProportions>(CharacterProportionsPath);

        /// <summary>
        /// The layout the scene is built from. Its defaults *are* the world - the shape of it, the
        /// borders and the palettes - so this is the one asset whose defaults are a design decision
        /// rather than a starting point.
        /// </summary>
        public static WorldLayout EnsureWorldLayout() => EnsureWorldLayout(ActiveWorldShape);

        /// <summary>
        /// Loads one shape's layout, creating it at that shape's defaults if missing. Both are
        /// created either way, so the inactive one is always there to switch to.
        /// </summary>
        public static WorldLayout EnsureWorldLayout(WorldShape shape)
        {
            var path = shape == WorldShape.Cluster ? ClusterLayoutPath : CorridorLayoutPath;
            var existing = AssetDatabase.LoadAssetAtPath<WorldLayout>(path);
            if (existing != null)
            {
                return existing;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var created = ScriptableObject.CreateInstance<WorldLayout>();
            // Both layouts are the same class, so CreateInstance cannot produce two different
            // worlds on its own - the defaults have to be stamped on after the fact.
            created.ApplyDefaults(shape);
            AssetDatabase.CreateAsset(created, path);
            Debug.Log($"[GRAFT] Created {path} at its {shape} defaults.");
            return created;
        }

        static T EnsureAsset<T>(string path) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null)
            {
                return existing;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var created = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(created, path);
            Debug.Log($"[GRAFT] Created {path} at defaults.");
            return created;
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
