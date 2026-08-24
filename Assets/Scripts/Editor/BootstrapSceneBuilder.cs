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
    /// not use a scene per world). Biomes become content loaded into this scene, never siblings
    /// of it. The scene is a real saved asset rather than something assembled at runtime, because
    /// Phase 2 has to bake a NavMesh across it and baking needs authored geometry.
    ///
    /// Phase 1 contents: ground, the player capsule and its camera rig, and two wall blocks -
    /// the walls exist only so the gate's "near a wall" capture has something to be near. Phase 2
    /// builds the real world; nothing here is meant to survive it.
    /// </summary>
    public static class BootstrapSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/World.unity";

        /// <summary>
        /// Ground is far wider than the camera can see. Sized off the framing rather than picked,
        /// because at 40x40 the original slab ran out of the top of frame as soon as the camera
        /// pulled back, leaving a black band that looks like a rendering fault.
        /// </summary>
        const float GroundMargin = 2.5f;

        /// <summary>
        /// Where the gate's "near a wall" capture drives into. Far enough from the origin that the
        /// walls are outside the frame when the player is stood at the origin - the at-rest and
        /// mid-move captures are about the character, and a wall filling a third of those images
        /// buries the thing they are asking you to look at.
        /// </summary>
        public static readonly Vector3 WallCorner = new Vector3(24f, 0f, 4f);

        [MenuItem("GRAFT/Rebuild World Scene")]
        static void BuildFromMenu() =>
            Build(ProjectSetup.EnsureCameraFraming(), ProjectSetup.EnsureMovementTuning(),
                ProjectSetup.EnsureCharacterProportions());

        public static void Build(CameraFraming framing, MovementTuning tuning, CharacterProportions shape)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            BuildGround(framing);
            BuildWalls();
            var player = BuildPlayer(tuning, shape);
            HudBuilder.Build(player.GetComponent<VirtualStickInput>());
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

        static void BuildGround(CameraFraming framing)
        {
            var extent = framing.FarGroundDistance * GroundMargin;
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "Ground";
            ground.transform.position = new Vector3(0f, -0.5f, 0f);
            ground.transform.localScale = new Vector3(extent, 1f, extent);
            ground.isStatic = true;

            // The ground receives shadows but never casts one. A flat slab has nothing to cast
            // onto, and including it as a caster only adds depth-bias artefacts - a faint diagonal
            // band across otherwise uniform ground, which later reads as a fake biome edge.
            ground.GetComponent<MeshRenderer>().shadowCastingMode =
                UnityEngine.Rendering.ShadowCastingMode.Off;

            SetMaterial(ground, MaterialLibrary.GetOrCreate("M_Ground", new Color(0.21f, 0.25f, 0.22f)));
        }

        /// <summary>
        /// Two blocks meeting at <see cref="WallCorner"/>. A corner rather than a single slab so
        /// the capture shows the CharacterController sliding along one face and stopping dead
        /// against the other - the two behaviours worth seeing before committing to the movement.
        /// </summary>
        static void BuildWalls()
        {
            var parent = new GameObject("Walls");
            var material = MaterialLibrary.GetOrCreate("M_Wall", new Color(0.33f, 0.32f, 0.36f));

            AddWall(parent, "Wall_East", new Vector3(WallCorner.x + 0.4f, 1.1f, WallCorner.z - 5f),
                new Vector3(0.8f, 2.2f, 11f), material);
            AddWall(parent, "Wall_North", new Vector3(WallCorner.x - 4.6f, 1.1f, WallCorner.z + 0.4f),
                new Vector3(10f, 2.2f, 0.8f), material);
        }

        static void AddWall(GameObject parent, string name, Vector3 position, Vector3 scale, Material material)
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = name;
            wall.transform.SetParent(parent.transform);
            wall.transform.position = position;
            wall.transform.localScale = scale;
            wall.isStatic = true;
            SetMaterial(wall, material);
        }

        /// <summary>
        /// The player. The silhouette comes from <see cref="CharacterProportions"/> and the parts
        /// are built by <see cref="CharacterRig"/>, so the shape can be changed without touching
        /// this file - and so the proportion sweep can rebuild it at runtime.
        /// </summary>
        static GameObject BuildPlayer(MovementTuning tuning, CharacterProportions shape)
        {
            var player = new GameObject("Player");
            player.transform.position = Vector3.zero;

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

        static void SetMaterial(GameObject target, Material material)
        {
            target.GetComponent<MeshRenderer>().sharedMaterial = material;
        }
    }
}
