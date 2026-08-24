using System.IO;
using Graft.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

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
    /// Phase 0 contents, deliberately nothing more: ground, one grey box, a light, a top-down camera.
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

        [MenuItem("GRAFT/Rebuild World Scene")]
        static void BuildFromMenu() => Build(ProjectSetup.EnsureCameraFraming());

        public static void Build(CameraFraming framing)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            var ground = BuildGround(framing);
            BuildLandmarkBox();
            ConfigureLight();
            ConfigureCamera(framing);

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
            Debug.Log($"[GRAFT] World scene rebuilt at {ScenePath} (ground {ground.name}).");
        }

        static GameObject BuildGround(CameraFraming framing)
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
            return ground;
        }

        /// <summary>
        /// The Phase 0 screenshot gate: one grey box on the ground plane. It stands where the
        /// player capsule goes in Phase 1, so the camera framing carries over unchanged.
        /// </summary>
        static void BuildLandmarkBox()
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = "GreyBox";
            // Player-sized: roughly the footprint and height the Phase 1 capsule will have, so the
            // camera framing established here is the framing the game keeps.
            box.transform.localScale = new Vector3(1f, 1.8f, 1f);
            box.transform.position = new Vector3(0f, 0.9f, 0f);
            SetMaterial(box, MaterialLibrary.GetOrCreate("M_GreyBox", new Color(0.78f, 0.78f, 0.80f)));
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

        static void ConfigureCamera(CameraFraming framing)
        {
            var camera = Camera.main;
            camera.name = "Main Camera";
            camera.orthographic = false;
            camera.fieldOfView = framing.fieldOfView;
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = framing.FarGroundDistance * 2f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.09f, 0.10f, 0.12f);

            // Phase 1 replaces this with a Cinemachine rig reading the same asset, so the framing
            // established here is the framing that survives.
            camera.transform.position = framing.OffsetFromTarget;
            camera.transform.rotation = framing.Rotation;
        }

        static void SetMaterial(GameObject target, Material material)
        {
            target.GetComponent<MeshRenderer>().sharedMaterial = material;
        }
    }
}
