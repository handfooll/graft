using Graft.Game;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Graft.Tests
{
    /// <summary>
    /// The camera framing asset is the single source of the numbers D2 locks down, and several
    /// other settings are sized off it. These guard the couplings that fail silently.
    /// </summary>
    public class CameraFramingTests
    {
        CameraFraming Framing =>
            AssetDatabase.LoadAssetAtPath<CameraFraming>("Assets/Settings/CameraFraming.asset");

        [Test]
        public void FramingAssetExists()
        {
            Assert.IsNotNull(Framing, "Run Graft.Editor.ProjectSetup.ConfigureAll.");
        }

        [Test]
        public void CameraLooksDownAtTheGround()
        {
            // Shallow enough and the top of the frame is sky rather than ground, which is not a
            // top-down camera any more (D2) and makes FarGroundDistance infinite.
            Assert.Greater(Framing.pitchDegrees - Framing.fieldOfView * 0.5f, 5f,
                "Pitch is too shallow for the field of view - the frame no longer meets the ground.");
            Assert.IsFalse(float.IsInfinity(Framing.FarGroundDistance));
        }

        /// <summary>
        /// The bug this exists for: shadow distance was a flat 25 m while the camera was free to
        /// move. Pull back past it and every shadow in the frame vanishes, with no error anywhere.
        /// </summary>
        [Test]
        public void ShadowDistanceCoversTheWholeFrame()
        {
            foreach (var path in new[] { RenderPipelineAssetPathMobile, RenderPipelineAssetPathPC })
            {
                var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                Assert.IsNotNull(asset, $"Missing render pipeline asset: {path}");

                var shadowDistance = new SerializedObject(asset).FindProperty("m_ShadowDistance");
                Assert.IsNotNull(shadowDistance, "URP renamed m_ShadowDistance.");
                Assert.GreaterOrEqual(shadowDistance.floatValue, Framing.FarGroundDistance,
                    $"{path}: shadows stop at {shadowDistance.floatValue:0.0} m but the frame " +
                    $"reaches {Framing.FarGroundDistance:0.0} m, so the far part of the screen has none.");
            }
        }

        const string RenderPipelineAssetPathMobile = "Assets/Settings/Mobile_RPAsset.asset";
        const string RenderPipelineAssetPathPC = "Assets/Settings/PC_RPAsset.asset";

        [Test]
        public void PortraitWidthIsNarrowerThanItSounds()
        {
            // Guards against reasoning about the vertical FOV and forgetting 9:16 squeezes the
            // horizontal field to well under half of it.
            var width = Framing.VisibleWidthOnDevice;
            Assert.Greater(width, 6f, "Less than 6 m of world on screen leaves no room to react.");
            Assert.Less(width, 30f, "Over 30 m and the player is a speck.");
        }
    }
}
