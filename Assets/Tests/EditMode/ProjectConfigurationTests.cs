using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Graft.Tests
{
    /// <summary>
    /// Phase 0 structural checks. Each of these has a silent failure mode: URP not actually
    /// assigned renders magenta, gamma colour space looks merely "a bit off", and a scene missing
    /// from build settings only fails once you try to load it at runtime.
    /// </summary>
    public class ProjectConfigurationTests
    {
        [Test]
        public void UniversalRenderPipelineIsActive()
        {
            var pipeline = GraphicsSettings.defaultRenderPipeline;
            Assert.IsNotNull(pipeline, "No render pipeline asset assigned - the project is on Built-in.");
            StringAssert.Contains("Universal", pipeline.GetType().Name);
        }

        [Test]
        public void ColourSpaceIsLinear()
        {
            Assert.AreEqual(ColorSpace.Linear, PlayerSettings.colorSpace);
        }

        [Test]
        public void WorldIsTheOnlyBuildScene()
        {
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            Assert.AreEqual(new[] { "Assets/Scenes/World.unity" }, scenes,
                "D3: one continuous gameplay scene. Biomes are content loaded into it, not extra scenes.");
        }

        [Test]
        public void WorldSceneAssetExists()
        {
            Assert.IsNotNull(
                AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/World.unity"),
                "Run Graft.Editor.ProjectSetup.ConfigureAll to build the scene.");
        }
    }
}
