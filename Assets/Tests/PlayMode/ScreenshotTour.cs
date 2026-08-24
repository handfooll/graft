using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Graft.Tests
{
    /// <summary>
    /// The screenshot gate, as a test. BUILD_PLAN.md: a phase is done when a human has looked at a
    /// PNG and said yes - the previous build passed 50/50 tests while being the wrong game.
    /// Every phase adds its named captures here.
    ///
    /// <code>
    /// Unity -batchmode -projectPath . -runTests -testPlatform PlayMode \
    ///   -testFilter ScreenshotTour -testResults tour.xml -logFile tour.log
    /// </code>
    ///
    /// No <c>-nographics</c>. With it the captures are blank.
    /// </summary>
    public class ScreenshotTour
    {
        /// <summary>Frames to let URP finish its first-frame setup before capturing.</summary>
        const int SettleFrames = 5;

        [UnityTest]
        public IEnumerator Phase0_GreyBoxOnGround()
        {
            yield return LoadWorld();

            var path = Screenshot.Capture("phase0_grey_box");

            Assert.IsTrue(File.Exists(path), "Capture wrote no file.");
            Assert.Greater(new FileInfo(path).Length, 2048,
                "Capture is suspiciously small - this is what an empty render looks like. " +
                "Check the tour was run without -nographics.");
        }

        static IEnumerator LoadWorld()
        {
            Screenshot.UseDeviceQualityLevel();
            yield return SceneManager.LoadSceneAsync("World", LoadSceneMode.Single);

            for (var i = 0; i < SettleFrames; i++)
            {
                yield return null;
            }

            Assert.IsNotNull(Camera.main, "World scene has no camera tagged MainCamera.");
        }
    }
}
