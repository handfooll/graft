using System.Collections;
using Graft.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Graft.Tests
{
    /// <summary>
    /// Renders the same scene with the same camera and only the character's silhouette changing.
    ///
    /// <code>
    /// Unity -batchmode -projectPath . -runTests -testPlatform PlayMode \
    ///   -testFilter CharacterProportionSweep -testResults sweep.xml -logFile sweep.log
    /// </code>
    ///
    /// The point of this sweep is that "which model pack" and "what shape is the character" are
    /// different decisions, and only the second one is blocking. The camera is framed against the
    /// silhouette, Phase 5's sockets hang off it, and how legibly a graft reads at 12 m depends on
    /// it entirely. A pack can be chosen in Phase 6 from inside whichever bracket wins here.
    /// </summary>
    public class CharacterProportionSweep
    {
        /// <summary>
        /// Named by how many head-heights tall each is - the measure illustrators actually use.
        /// The range deliberately spans from anatomical to the reference screenshot's chibi.
        /// </summary>
        static readonly (string name, float height, float width, float headFraction)[] Candidates =
        {
            ("a_realistic", 1.80f, 0.90f, 0.13f),   // ~7.7 heads - human proportions
            ("b_stylised",  1.70f, 0.85f, 0.25f),   // ~4.0 heads - the KayKit/Quaternius bracket
            ("c_chunky",    1.50f, 0.95f, 0.32f),   // ~3.1 heads
            ("d_chibi",     1.20f, 1.00f, 0.42f),   // ~2.4 heads - XP Hero's bracket
        };

        [UnityTest]
        public IEnumerator SweepSilhouette()
        {
            Screenshot.UseDeviceQualityLevel();
            yield return SceneManager.LoadSceneAsync("World", LoadSceneMode.Single);
            for (var i = 0; i < 5; i++)
            {
                yield return null;
            }

            var player = Object.FindAnyObjectByType<PlayerController>();
            Assert.IsNotNull(player, "World scene has no player.");
            player.Teleport(Vector3.zero, 205f);

            var body = player.transform.Find(CharacterRig.BodyName).GetComponent<MeshRenderer>().sharedMaterial;
            var accent = player.transform.Find(CharacterRig.ChestName).GetComponent<MeshRenderer>().sharedMaterial;

            var shape = ScriptableObject.CreateInstance<CharacterProportions>();

            foreach (var candidate in Candidates)
            {
                shape.height = candidate.height;
                shape.width = candidate.width;
                shape.headFraction = candidate.headFraction;

                SweepProps.DestroyByPrefix("Marker_");
                SweepProps.DestroyByPrefix("Graft_");
                CharacterRig.Build(player.gameObject, shape, body, accent);
                SweepProps.BuildApproachMarkers(player.transform, shape);
                SweepProps.BuildGraftStandIns(player.transform, shape);

                // The controller was resized under the player's feet; put them back on the floor.
                player.Teleport(Vector3.zero, 205f);
                yield return null;
                yield return null;

                Screenshot.Capture($"shape_{candidate.name}");
                Debug.Log($"[GRAFT] {candidate.name}: {candidate.height:0.00} m tall, " +
                          $"{candidate.width:0.00} m wide, {shape.HeadsTall:0.0} heads tall.");
            }

            Object.DestroyImmediate(shape);
        }
    }
}
