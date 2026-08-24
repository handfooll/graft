using System.Collections;
using System.IO;
using Graft.Core;
using Graft.Game;
using Graft.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
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

            player.Teleport(OpenGround(), 205f);
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

            player.Teleport(OpenGround() + new Vector3(-1.5f, 0f, -5f), 0f);
            yield return Frames(2);

            input.Value = new Vec2(0.55f, 0.83f);
            yield return Seconds(1f);

            Assert.Greater(player.Speed, player.Tuning.maxSpeed * 0.95f,
                "Not actually at speed - this would be an at-rest picture with a different name.");
            Capture("phase1_moving");
        }

        /// <summary>
        /// Image 3 of 3: hard against a wall, still pushing into it. Driven into it rather than
        /// placed there, so what the picture shows is the CharacterController's real behaviour -
        /// sliding along the face instead of sticking to it.
        ///
        /// The wall is now the bank monolith in Base. Phase 1's two scaffolding blocks were
        /// deleted in Phase 2 as STATUS.md said they would be, and the monolith is the nearest
        /// thing the real world has to the flat vertical face this capture is about.
        /// </summary>
        [UnityTest]
        public IEnumerator Phase1_NearWall()
        {
            yield return LoadWorld();
            var player = FindPlayer();
            var input = Drive(player, Vec2.Zero);

            // Posed off the monolith's own bounds rather than against copied coordinates: the
            // scene builder lives in an editor-only assembly this test cannot see, and a
            // duplicated constant would go stale silently the first time Base is rearranged.
            var monolith = GameObject.Find("Base_Monolith");
            Assert.IsNotNull(monolith, "Base has no monolith - there is nothing in the world to be against.");
            var wall = monolith.GetComponent<Collider>().bounds;

            player.Teleport(new Vector3(wall.center.x - 0.6f, 0f, wall.min.z - 4.5f), 0f);
            yield return Frames(2);

            // Almost straight into the face, with a slight drift along it. Straight on would show
            // nothing but a stop; too much drift and the player rounds the corner before the
            // shutter opens.
            input.Value = new Vec2(0.14f, 0.99f);
            yield return Seconds(2.5f);

            var position = player.transform.position;
            var gap = wall.min.z - position.z;

            Assert.Greater(gap, 0f, "Walked into the wall - the CharacterController is not colliding.");
            Assert.Less(gap, 1.2f, "Never reached the wall - the capture would show empty ground.");
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

            player.Teleport(OpenGround() + new Vector3(0f, 0f, -1.5f), 0f);
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

        // ---------------------------------------------------------------------------------------
        // Phase 2. BUILD_PLAN.md: "one PNG per area, six total. Each must be identifiable as a
        // different place from the picture alone. If two biomes are indistinguishable, the biome
        // design has failed, and no amount of later code fixes it."
        //
        // Numbered in the order you meet them walking out from Base, so a directory listing reads
        // as the journey - the escalation is part of what is being judged, not just the six
        // palettes on their own.
        // ---------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator Phase2_Base() => CaptureArea(Area.Base, "phase2_0_base");

        [UnityTest]
        public IEnumerator Phase2_Verge() => CaptureArea(Area.Verge, "phase2_1_verge");

        [UnityTest]
        public IEnumerator Phase2_RustFlats() => CaptureArea(Area.RustFlats, "phase2_2_rust_flats");

        [UnityTest]
        public IEnumerator Phase2_GlassMire() => CaptureArea(Area.GlassMire, "phase2_3_glass_mire");

        [UnityTest]
        public IEnumerator Phase2_BoneReach() => CaptureArea(Area.BoneReach, "phase2_4_bone_reach");

        [UnityTest]
        public IEnumerator Phase2_EmberScar() => CaptureArea(Area.EmberScar, "phase2_5_ember_scar");

        /// <summary>
        /// The seventh image, for Phase 2's other promise: "boundaries so you cannot walk into the
        /// void". The player is driven into the edge of the world at full speed and does not cross
        /// it - which is a claim about collision that a picture of a wall cannot make on its own,
        /// so the assertion carries it and the picture shows what the edge looks like.
        /// </summary>
        [UnityTest]
        public IEnumerator Phase2_WorldEdge()
        {
            yield return LoadWorld();
            var player = FindPlayer();
            var input = Drive(player, Vec2.Zero);

            var regions = Object.FindAnyObjectByType<WorldRegions>();
            Assert.IsNotNull(regions);

            // March sideways out of the Verge until the ground stops, then step back off it.
            var anchor = regions.AnchorFor(Area.Verge);
            var outward = Vector3.right;
            var edge = anchor;
            for (var step = 0; step < 400 && regions.TryAreaAt(edge + outward, out _); step++)
            {
                edge += outward;
            }

            Assert.Greater((edge - anchor).magnitude, 5f, "The world ends within 5 m of the Verge anchor.");

            player.Teleport(edge - outward * 6f, 90f);
            yield return Frames(2);

            input.Value = new Vec2(1f, 0f);
            yield return Seconds(3f);

            Assert.IsTrue(regions.TryAreaAt(player.transform.position, out _),
                "Walked straight off the edge of the world - the boundary is not stopping anything.");
            Assert.Greater(player.Speed, 0.1f, "Not still pushing into the boundary when captured.");

            Capture("phase2_6_world_edge");
        }

        /// <summary>
        /// Stands the player at an area's anchor, facing the way you would be walking, and
        /// captures.
        ///
        /// The two assertions are the ones a picture cannot make for itself. That the player is
        /// really in the area the file is named after - a gate of six images is worthless if two
        /// of them are the same place under different names. And that the NavMesh reaches here -
        /// an unbaked patch looks exactly like a baked one until Phase 3 puts an enemy on it.
        /// </summary>
        static IEnumerator CaptureArea(Area area, string name)
        {
            yield return LoadWorld();
            var player = FindPlayer();
            Drive(player, Vec2.Zero);

            var regions = Object.FindAnyObjectByType<WorldRegions>();
            Assert.IsNotNull(regions, "No WorldRegions in the World scene - there is no world to photograph.");

            var anchor = regions.AnchorFor(area);
            player.Teleport(anchor, HeadingAt(regions, area, anchor));
            yield return Seconds(0.4f);

            Assert.AreEqual(area, regions.AreaAt(player.transform.position),
                $"Standing at {area}'s anchor puts the player in " +
                $"{regions.AreaAt(player.transform.position)} - the gate would photograph the wrong place.");
            Assert.IsTrue(NavMesh.SamplePosition(player.transform.position, out _, 2f, NavMesh.AllAreas),
                $"No NavMesh within 2 m of {area}'s anchor - enemies could never reach this biome.");

            Capture(name);
        }

        /// <summary>
        /// Facing the way the world goes. Base faces its own monument; a biome faces further out,
        /// which is the direction the player is looking when they choose to keep going.
        /// </summary>
        static float HeadingAt(WorldRegions regions, Area area, Vector3 anchor)
        {
            if (area == Area.Base)
            {
                return 0f;
            }

            var along = regions.Map.DistanceFromBase(new Vec2(anchor.x, anchor.z));
            return regions.Map.Path.DirectionAt(along).Heading;
        }

        /// <summary>
        /// Where the Phase 1 captures are posed, now that the world exists: the clear ground at the
        /// Verge's anchor, rather than the origin.
        ///
        /// The origin is inside the Base plaza these days, and those three images ask one question -
        /// can you tell which way the character is facing - which a monument and a ring of pillars
        /// crowding the frame does not help anyone answer.
        /// </summary>
        static Vector3 OpenGround()
        {
            var regions = Object.FindAnyObjectByType<WorldRegions>();
            Assert.IsNotNull(regions, "No WorldRegions in the World scene.");
            return regions.AnchorFor(Area.Verge);
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
