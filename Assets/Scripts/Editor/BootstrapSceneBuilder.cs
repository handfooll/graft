using System.IO;
using Graft.Game;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Graft.Editor
{
    /// <summary>
    /// Builds <c>Assets/Scenes/World.unity</c> from scratch.
    ///
    /// There is exactly one gameplay scene (D3, and REFERENCE_XPHERO.md - the shipped game does
    /// not use a scene per world). Biomes are content inside this scene, never siblings of it. The
    /// scene is a real saved asset rather than something assembled at runtime, because the NavMesh
    /// is baked across it and baking needs authored geometry.
    ///
    /// This file now owns only the things that are not the world: the player, the camera, the HUD
    /// and the lighting. <see cref="WorldBuilder"/> owns the ground, the boundary, Base and the
    /// props, and it is driven entirely by the <c>WorldLayout</c> asset.
    ///
    /// Phase 1's two scaffolding walls are gone, as STATUS.md said they would be. Nothing was
    /// built on them.
    /// </summary>
    public static class BootstrapSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/World.unity";

        [MenuItem("GRAFT/Rebuild World Scene")]
        static void BuildFromMenu() =>
            Build(ProjectSetup.EnsureCameraFraming(), ProjectSetup.EnsureMovementTuning(),
                ProjectSetup.EnsureCharacterProportions(), ProjectSetup.EnsureWorldLayout(),
                ProjectSetup.EnsureCombatTuning(), ProjectSetup.EnsureEnemyRoster(),
                ProjectSetup.EnsureSpawnTables(), ProjectSetup.EnsureEssenceTuning());

        public static void Build(CameraFraming framing, MovementTuning tuning, CharacterProportions shape,
            WorldLayout layout, CombatTuning combat, EnemyRoster roster, SpawnTables tables,
            EssenceTuning essence)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            var world = WorldBuilder.Build(layout);
            var regions = world.GetComponent<WorldRegions>();
            var player = BuildPlayer(tuning, shape, regions);
            // After the player and the world, because every combat component needs one or the
            // other - and before the camera, so the very first frame already has a target.
            CombatBuilder.Build(player, regions, combat, roster, tables, essence);
            // After combat, not before: Phase 4's HUD draws the player's cargo ledger, and
            // PlayerCargo is one of the components CombatBuilder adds. Wiring the HUD first would
            // hand it a null and the bar would sit empty all game with nothing to report.
            HudBuilder.Build(player.GetComponent<VirtualStickInput>(), player.GetComponent<PlayerCargo>());
            ConfigureLight();
            ConfigureCamera(framing, player.transform);

            // Kept deliberately dim. Bright ambient flattens everything into one value, which on a
            // top-down camera means you cannot tell a box from a painted square.
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.28f, 0.32f, 0.38f);
            RenderSettings.ambientEquatorColor = new Color(0.19f, 0.20f, 0.22f);
            RenderSettings.ambientGroundColor = new Color(0.10f, 0.10f, 0.11f);

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();

            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            Debug.Log($"[GRAFT] World scene rebuilt at {ScenePath}.");
        }

        /// <summary>
        /// The player. The silhouette comes from <see cref="CharacterProportions"/> and the parts
        /// are built by <see cref="CharacterRig"/>, so the shape can be changed without touching
        /// this file - and so the proportion sweep can rebuild it at runtime.
        /// </summary>
        static GameObject BuildPlayer(MovementTuning tuning, CharacterProportions shape, WorldRegions regions)
        {
            var player = new GameObject("Player");
            // Standing in Base, which is where an expedition starts and where death returns you
            // (§3). Read from the world rather than assumed to be the origin, so re-routing the
            // spine cannot leave the player spawning in the void.
            player.transform.position = regions.AnchorFor(Graft.Core.Area.Base);

            var controller = player.AddComponent<CharacterController>();
            controller.slopeLimit = 50f;
            controller.stepOffset = 0.3f;

            CharacterRig.Build(player, shape,
                MaterialLibrary.GetOrCreate("M_Player", new Color(0.60f, 0.65f, 0.73f)),
                MaterialLibrary.GetOrCreate("M_PlayerAccent", new Color(0.93f, 0.51f, 0.18f)));

            player.AddComponent<VirtualStickInput>().Tuning = tuning;
            player.AddComponent<PlayerController>().Tuning = tuning;
            return player;
        }

        static void ConfigureLight()
        {
            var light = Object.FindAnyObjectByType<Light>();
            light.name = "Sun";
            light.type = LightType.Directional;
            // Low sun elevation on purpose. A long cast shadow is the strongest depth cue a
            // top-down camera has - without it a lit box on flat ground reads as a flat square.
            light.transform.rotation = Quaternion.Euler(38f, -35f, 0f);
            light.color = new Color(1f, 0.95f, 0.87f);
            light.intensity = 1.9f;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.75f;
            light.shadowBias = 0.02f;
            light.shadowNormalBias = 0.2f;
        }

        /// <summary>
        /// Cinemachine drives the camera from Phase 1 on (REFERENCE_XPHERO.md: the shipped game
        /// uses it; do not hand-roll a follow camera). The Unity camera keeps its position and
        /// rotation set here as well as having a brain, so the very first rendered frame is already
        /// framed correctly rather than blending in from wherever the camera happened to be.
        /// </summary>
        static void ConfigureCamera(CameraFraming framing, Transform target)
        {
            var camera = Camera.main;
            camera.name = "Main Camera";
            camera.orthographic = false;
            camera.fieldOfView = framing.fieldOfView;
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = framing.FarGroundDistance * 2f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.09f, 0.10f, 0.12f);
            camera.transform.SetPositionAndRotation(
                target.position + framing.OffsetFromTarget, framing.Rotation);

            var brain = camera.gameObject.AddComponent<CinemachineBrain>();
            // There is one camera and there will only ever be one (D2). A default blend would only
            // ever have the chance to fire on the first frame, as a slide in from nowhere.
            brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut, 0f);

            var rigObject = new GameObject("PlayerCamera");
            rigObject.AddComponent<CinemachineCamera>();
            rigObject.AddComponent<CinemachineFollow>();

            var rig = rigObject.AddComponent<CameraRig>();
            rig.Framing = framing;
            rig.Target = target;
            rig.Apply();
        }

    }
}
