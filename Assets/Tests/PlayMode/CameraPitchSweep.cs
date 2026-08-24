using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Graft.Tests
{
    /// <summary>
    /// A tuning aid, not a gate. Captures the same scene at several camera pitches so the angle can
    /// be chosen against a reference image instead of against somebody's description of one.
    ///
    /// <code>
    /// Unity -batchmode -projectPath . -runTests -testPlatform PlayMode \
    ///   -testFilter CameraPitchSweep -testResults sweep.xml -logFile sweep.log
    /// </code>
    ///
    /// Slant distance from the player is held constant across every pitch, so the player box stays
    /// the same size on screen and the only thing changing is the viewing angle. Vary the height
    /// instead and you are comparing zoom levels while thinking you are comparing angles.
    ///
    /// The marker rings are built at runtime and never saved - World.unity stays exactly as the
    /// Phase 0 gate describes it. They are here because the real question a top-down action camera
    /// has to answer is "how much warning do I get before something reaches me", and an empty field
    /// cannot show that.
    /// </summary>
    public class CameraPitchSweep
    {
        static readonly float[] Pitches = { 45f, 55f, 62f, 70f };


        /// <summary>Approach rings, in metres. Roughly "already on you" and "you have a moment".</summary>
        static readonly float[] RingRadii = { 6f, 11f };

        const int MarkersPerRing = 8;

        /// <summary>Slant distances for the zoom sweep. Multiples of the framing asset's own value,
        /// so the sweep still brackets the current choice after that value is retuned.</summary>
        static readonly float[] DistanceScales = { 0.75f, 1f, 1.3f, 1.6f };

        [UnityTest]
        public IEnumerator SweepPitch()
        {
            Screenshot.UseDeviceQualityLevel();
            yield return SceneManager.LoadSceneAsync("World", LoadSceneMode.Single);
            for (var i = 0; i < 5; i++)
            {
                yield return null;
            }

            BuildApproachMarkers();
            yield return null;

            var camera = Camera.main;
            Assert.IsNotNull(camera, "World scene has no camera tagged MainCamera.");

            var baseline = camera.transform.position.magnitude;
            foreach (var pitch in Pitches)
            {
                Place(camera.transform, pitch, baseline);
                yield return null;
                Screenshot.Capture($"pitch_{pitch:00}");
            }
        }

        /// <summary>
        /// The knob that turned out to matter more than pitch: how much world is on screen.
        /// Pitch is held at the scene's own value and only the distance changes.
        /// </summary>
        [UnityTest]
        public IEnumerator SweepDistance()
        {
            Screenshot.UseDeviceQualityLevel();
            yield return SceneManager.LoadSceneAsync("World", LoadSceneMode.Single);
            for (var i = 0; i < 5; i++)
            {
                yield return null;
            }

            BuildApproachMarkers();
            yield return null;

            var camera = Camera.main;
            Assert.IsNotNull(camera, "World scene has no camera tagged MainCamera.");

            var baseline = camera.transform.position.magnitude;
            var pitch = camera.transform.eulerAngles.x;
            foreach (var scale in DistanceScales)
            {
                var distance = baseline * scale;
                Place(camera.transform, pitch, distance);
                yield return null;
                Screenshot.Capture($"width_{VisibleWidth(camera, distance):00}m");
                Debug.Log($"[GRAFT] distance {distance:0.0} m -> {VisibleWidth(camera, distance):0.0} m visible width");
            }
        }

        /// <summary>
        /// Metres of world visible across the full screen width at the player's distance.
        ///
        /// Uses the capture's own aspect rather than <c>camera.aspect</c>. Under the test runner the
        /// camera still carries the editor's window aspect until a target texture is assigned, so
        /// reading it here answers for the wrong screen shape - and portrait is exactly where that
        /// matters, because 9:16 makes the horizontal field far narrower than the vertical FOV
        /// suggests.
        /// </summary>
        public static float VisibleWidth(Camera camera, float distance)
        {
            var verticalHalf = Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            var captureAspect = (float)Screenshot.Width / Screenshot.Height;
            return 2f * distance * verticalHalf * captureAspect;
        }

        static void Place(Transform camera, float pitch, float distance)
        {
            var radians = pitch * Mathf.Deg2Rad;
            camera.position = new Vector3(
                0f,
                distance * Mathf.Sin(radians),
                -distance * Mathf.Cos(radians));
            camera.rotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        static void BuildApproachMarkers()
        {
            var material = GameObject.Find("GreyBox").GetComponent<MeshRenderer>().sharedMaterial;

            foreach (var radius in RingRadii)
            {
                for (var i = 0; i < MarkersPerRing; i++)
                {
                    var angle = i * Mathf.PI * 2f / MarkersPerRing;
                    var marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    marker.name = $"Marker_{radius}_{i}";
                    marker.transform.localScale = new Vector3(0.8f, 1.4f, 0.8f);
                    marker.transform.position = new Vector3(
                        Mathf.Sin(angle) * radius,
                        0.7f,
                        Mathf.Cos(angle) * radius);
                    marker.GetComponent<MeshRenderer>().sharedMaterial = material;
                }
            }
        }
    }
}
