using System.Collections;
using System.IO;
using Graft.Core;
using Graft.Game;
using Graft.UI;
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
    ///
    /// Phase 0's grey-box capture is gone: the box it was named after is now the player capsule,
    /// and a picture called <c>phase0_grey_box</c> with no grey box in it is worse than no picture.
    /// It was approved on 2026-08-24 and is reproducible from commit <c>37c93bf</c>.
    /// </summary>
    public class ScreenshotTour
    {
        /// <summary>Frames to let URP finish its first-frame setup before capturing.</summary>
        const int SettleFrames = 5;

        /// <summary>
        /// BUILD_PLAN.md Phase 1, image 1 of 3: at rest. Standing still on an off-axis heading, so
        /// the question the gate actually asks - can you tell which way this thing is pointing -
        /// is not accidentally answered by the character happening to align with the screen.
        /// </summary>
        [UnityTest]
        public IEnumerator Phase1_AtRest()
        {
            yield return LoadWorld();
            var player = FindPlayer();
            Drive(player, Vec2.Zero);

            player.Teleport(new Vector3(0f, 0f, 0f), 205f);
            yield return Seconds(0.5f);

            Assert.Less(player.Speed, 0.01f, "Player has not come to rest.");
            Capture("phase1_at_rest");
        }

        /// <summary>
        /// Image 2 of 3: mid-move. Captured while genuinely at speed, not merely after a nudge -
        /// the assertion is what stops this quietly degrading into a second at-rest picture.
        /// </summary>
        [UnityTest]
        public IEnumerator Phase1_Moving()
        {
            yield return LoadWorld();
            var player = FindPlayer();
            var input = Drive(player, Vec2.Zero);

            player.Teleport(new Vector3(-2.5f, 0f, -6f), 0f);
            yield return Frames(2);

            input.Value = new Vec2(0.55f, 0.83f);
            yield return Seconds(1f);

            Assert.Greater(player.Speed, player.Tuning.maxSpeed * 0.95f,
                "Not actually at speed - this would be an at-rest picture with a different name.");
            Capture("phase1_moving");
        }

        /// <summary>
        /// Image 3 of 3: hard against a wall, still pushing into it. Driven into the corner rather
        /// than placed there, so what the picture shows is the CharacterController's real
        /// behaviour - sliding along one face, stopping dead against the other.
        /// </summary>
        [UnityTest]
        public IEnumerator Phase1_NearWall()
        {
            yield return LoadWorld();
            var player = FindPlayer();
            var input = Drive(player, Vec2.Zero);

            // Posed off the wall's own bounds rather than against copied coordinates: the scene
            // builder lives in an editor-only assembly this test cannot see, and a duplicated
            // constant would go stale silently the first time the walls move.
            var wall = GameObject.Find("Wall_East").GetComponent<Collider>().bounds;

            player.Teleport(new Vector3(wall.min.x - 6f, 0f, wall.min.z + 4f), 0f);
            yield return Frames(2);

            input.Value = new Vec2(0.78f, 0.63f);
            yield return Seconds(3f);

            var position = player.transform.position;
            var gap = wall.min.x - position.x;

            Assert.Greater(gap, 0f, "Walked into the wall - the CharacterController is not colliding.");
            Assert.Less(gap, 1f, "Never reached the wall - the capture would show empty ground.");
            Assert.Greater(player.Speed, 0.1f, "Not still pushing into the wall when captured.");
            Capture("phase1_near_wall");
        }

        /// <summary>
        /// Image 4: the floating stick, drawn where a thumb would be. Driven by hand because
        /// <c>-batchmode</c> has no pointer to press - the ring and knob are placed at the same
        /// coordinates the input layer would have produced.
        /// </summary>
        [UnityTest]
        public IEnumerator Phase1_StickEngaged()
        {
            yield return LoadWorld();
            var player = FindPlayer();
            var input = Drive(player, Vec2.Zero);

            var view = Object.FindAnyObjectByType<VirtualStickView>();
            Assert.IsNotNull(view, "No VirtualStickView - the HUD is missing from the World scene.");
            var stick = Object.FindAnyObjectByType<VirtualStickInput>();
            Assert.IsNotNull(stick);

            player.Teleport(new Vector3(0f, 0f, -1.5f), 0f);
            input.Value = new Vec2(0.62f, 0.78f);
            yield return Seconds(1f);

            // Hand control of the view over from the (absent) pointer to this test, and place the
            // ring where a thumb would sit: low and left, pushed up-right, matching the stick above.
            view.Source = null;
            var radius = stick.TravelRadiusPixels;
            var origin = new Vector2(Screen.width * 0.34f, Screen.height * 0.24f);
            view.Show(origin, origin + new Vector2(0.62f, 0.78f) * radius, radius);
            yield return null;

            Capture("phase1_stick");
        }

        static void Capture(string name)
        {
            var path = Screenshot.Capture(name);
            Assert.IsTrue(File.Exists(path), "Capture wrote no file.");
            Assert.Greater(new FileInfo(path).Length, 2048,
                "Capture is suspiciously small - this is what an empty render looks like. " +
                "Check the tour was run without -nographics.");
        }

        static PlayerController FindPlayer()
        {
            var player = Object.FindAnyObjectByType<PlayerController>();
            Assert.IsNotNull(player, "World scene has no PlayerController.");
            Assert.IsNotNull(player.Tuning,
                "PlayerController has no MovementTuning - it would stand still and report no error.");
            return player;
        }

        static ScriptedMoveInput Drive(PlayerController player, Vec2 initial)
        {
            var input = new ScriptedMoveInput { Value = initial };
            player.Input = input;
            return input;
        }

        static IEnumerator Frames(int count)
        {
            for (var i = 0; i < count; i++)
            {
                yield return null;
            }
        }

        /// <summary>
        /// Drives for a span of simulated time rather than a count of frames. Headless play mode
        /// runs uncapped, so a frame count buys an unpredictable amount of movement - a capture
        /// posed by frame count would drift the moment this machine got faster.
        /// </summary>
        static IEnumerator Seconds(float seconds)
        {
            var deadline = Time.time + seconds;
            var guard = 0;
            while (Time.time < deadline && guard++ < 100000)
            {
                yield return null;
            }
        }

        static IEnumerator LoadWorld()
        {
            Screenshot.UseDeviceQualityLevel();
            yield return SceneManager.LoadSceneAsync("World", LoadSceneMode.Single);
            yield return Frames(SettleFrames);
            Assert.IsNotNull(Camera.main, "World scene has no camera tagged MainCamera.");
        }
    }
}
