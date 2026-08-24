using System.Collections;
using Graft.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Graft.Tests
{
    /// <summary>
    /// A tuning aid, not a gate. Captures the same scene at several framings so the camera can be
    /// chosen against images instead of against somebody's description of one.
    ///
    /// <code>
    /// Unity -batchmode -projectPath . -runTests -testPlatform PlayMode \
    ///   -testFilter CameraFramingSweep -testResults sweep.xml -logFile sweep.log
    /// </code>
    ///
    /// Every sweep image contains three things, because the framing has to answer three questions
    /// at once and optimising for any one of them alone gives the wrong answer:
    ///
    /// 1. **The player**, at its real size - is the character big enough to be a character?
    /// 2. **Approach rings** of enemy-sized capsules at 6 m and 11 m - how much warning is there?
    /// 3. **Stand-in graft props** on the player's sockets - can a mutation be read at all? Phase 5's
    ///    gate is four pictures of the same character at rising corruption, and that gate is
    ///    unwinnable at a framing where the props are three pixels wide. This is the constraint that
    ///    Phase 0 could not see, because Phase 0 had no character to put props on.
    /// </summary>
    public class CameraFramingSweep
    {
        /// <summary>Camera pitches to compare, in degrees below horizontal.</summary>
        static readonly float[] Pitches = { 42f, 47.5f, 53f, 58f };

        /// <summary>
        /// Metres of world across the screen's short axis. Labelled in metres rather than in
        /// distances, because metres-on-screen is the thing being judged; the distance needed to
        /// produce it is arithmetic.
        /// </summary>
        static readonly float[] VisibleWidths = { 9f, 11f, 12f, 14f };

        /// <summary>Vertical fields of view to compare, all framed to the same visible width.</summary>
        static readonly float[] Lenses = { 45f, 34f, 26f };

        /// <summary>The width the lens sweep holds constant while the lens changes.</summary>
        const float ChosenWidth = 12f;

        /// <summary>Roughly "already on you" and "you have a moment".</summary>
        static readonly float[] RingRadii = { 6f, 11f };

        const int MarkersPerRing = 8;

        [UnityTest]
        public IEnumerator SweepVisibleWidth()
        {
            yield return Setup();
            var camera = Camera.main;
            var pitch = camera.transform.eulerAngles.x;
            foreach (var width in VisibleWidths)
            {
                Place(camera.transform, pitch, DistanceFor(camera, width));
                yield return null;
                Screenshot.Capture($"width_{width:00}m");
                Debug.Log($"[GRAFT] {width:0} m visible width -> distance {DistanceFor(camera, width):0.0} m");
            }
        }

        /// <summary>
        /// Same visible width, different lens. Unity's field of view is vertical, and portrait 9:16
        /// makes a wide one very tall - so objects near the top and bottom of frame lean outward
        /// hard. Trading field of view for distance keeps the same metres on screen while flattening
        /// that lean, which is what makes a top-down view read as top-down at the frame edges rather
        /// than only in the middle.
        /// </summary>
        [UnityTest]
        public IEnumerator SweepLens()
        {
            yield return Setup();
            var camera = Camera.main;
            var pitch = camera.transform.eulerAngles.x;

            foreach (var fov in Lenses)
            {
                camera.fieldOfView = fov;
                Place(camera.transform, pitch, DistanceFor(camera, ChosenWidth));
                yield return null;
                Screenshot.Capture($"lens_{fov:00}deg");
                Debug.Log($"[GRAFT] {fov:0} deg lens -> distance {DistanceFor(camera, ChosenWidth):0.0} m for {ChosenWidth:0} m width");
            }
        }

        [UnityTest]
        public IEnumerator SweepPitch()
        {
            yield return Setup();
            var camera = Camera.main;

            // Visible width held constant, so the player stays the same size and the only thing
            // changing is the angle. Vary the distance instead and you are comparing zoom levels
            // while believing you are comparing angles.
            var distance = DistanceFor(camera, ChosenWidth);
            foreach (var pitch in Pitches)
            {
                Place(camera.transform, pitch, distance);
                yield return null;
                Screenshot.Capture($"pitch_{pitch:00}");
            }
        }

        /// <summary>Loads the world, poses the player and builds the throwaway sweep props.</summary>
        static IEnumerator Setup()
        {
            Screenshot.UseDeviceQualityLevel();
            yield return SceneManager.LoadSceneAsync("World", LoadSceneMode.Single);
            for (var i = 0; i < 5; i++)
            {
                yield return null;
            }

            Assert.IsNotNull(Camera.main, "World scene has no camera tagged MainCamera.");

            var player = Object.FindAnyObjectByType<PlayerController>();
            Assert.IsNotNull(player, "World scene has no player to judge the framing against.");
            player.Teleport(Vector3.zero, 205f);

            // The camera is repositioned by hand here, so Cinemachine must stop overwriting it.
            var rig = Object.FindAnyObjectByType<CameraRig>();
            if (rig != null)
            {
                rig.gameObject.SetActive(false);
            }

            var shape = ProportionsOf(player);
            SweepProps.BuildApproachMarkers(player.transform, shape);
            SweepProps.BuildGraftStandIns(player.transform, shape);
            yield return null;
        }

        /// <summary>
        /// Reads the silhouette back off the built character, so the sweep's markers and props stay
        /// sized to whatever proportions the scene was built with.
        /// </summary>
        static CharacterProportions ProportionsOf(PlayerController player)
        {
            var controller = player.GetComponent<CharacterController>();
            var head = player.transform.Find(CharacterRig.HeadName).localScale.y;
            var shape = ScriptableObject.CreateInstance<CharacterProportions>();
            shape.height = controller.height;
            shape.width = controller.radius * 2f;
            shape.headFraction = head / controller.height;
            return shape;
        }

        static float DistanceFor(Camera camera, float visibleWidth)
        {
            // The capture's own aspect, never camera.aspect: under the test runner the camera
            // carries the editor window's aspect until a target texture is assigned, so reading it
            // answers for the wrong screen shape. Portrait is exactly where that bites.
            var captureAspect = (float)Screenshot.Width / Screenshot.Height;
            var verticalHalf = Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            return visibleWidth / (2f * verticalHalf * captureAspect);
        }

        static void Place(Transform camera, float pitch, float distance)
        {
            var radians = pitch * Mathf.Deg2Rad;
            camera.position = new Vector3(0f, distance * Mathf.Sin(radians), -distance * Mathf.Cos(radians));
            camera.rotation = Quaternion.Euler(pitch, 0f, 0f);
        }

    }
}
