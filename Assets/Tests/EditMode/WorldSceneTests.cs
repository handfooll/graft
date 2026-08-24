using Graft.Editor;
using Graft.Game;
using NUnit.Framework;
using Unity.Cinemachine;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Graft.Tests
{
    /// <summary>
    /// Checks the saved World scene is actually wired up.
    ///
    /// Every assertion here has the same silent failure mode: an unassigned <c>[SerializeField]</c>
    /// produces no error, no warning and no log line - just a player that stands still, or a camera
    /// that never moves. That is exactly the class of bug a screenshot cannot distinguish from
    /// "the tour posed it that way".
    ///
    /// Opened additively so running these in an interactive editor does not close whatever the
    /// author had open.
    /// </summary>
    public class WorldSceneTests
    {
        Scene _scene;

        [SetUp]
        public void OpenWorld()
        {
            _scene = EditorSceneManager.OpenScene(BootstrapSceneBuilder.ScenePath, OpenSceneMode.Additive);
        }

        [TearDown]
        public void CloseWorld()
        {
            if (_scene.IsValid())
            {
                EditorSceneManager.CloseScene(_scene, true);
            }
        }

        T FindInWorld<T>() where T : Component
        {
            foreach (var root in _scene.GetRootGameObjects())
            {
                var found = root.GetComponentInChildren<T>(true);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        [Test]
        public void PlayerHasItsMovementTuning()
        {
            var player = FindInWorld<PlayerController>();
            Assert.IsNotNull(player, "No PlayerController in the World scene.");
            Assert.IsNotNull(player.Tuning, "PlayerController.tuning is unassigned - the player will not move.");
        }

        [Test]
        public void VirtualStickHasItsMovementTuning()
        {
            var stick = FindInWorld<VirtualStickInput>();
            Assert.IsNotNull(stick, "No VirtualStickInput in the World scene - there is no touch control.");
            Assert.IsNotNull(stick.Tuning,
                "VirtualStickInput.tuning is unassigned - the stick would silently fall back to a default travel radius.");
        }

        [Test]
        public void PlayerCollidesRatherThanOverlaps()
        {
            var player = FindInWorld<PlayerController>();
            Assert.IsNotNull(player.GetComponent<CharacterController>(),
                "No CharacterController - the player would walk through every wall.");
        }

        [Test]
        public void CameraRigFollowsThePlayerUsingTheFramingAsset()
        {
            var rig = FindInWorld<CameraRig>();
            Assert.IsNotNull(rig, "No CameraRig - the camera is not being driven by Cinemachine.");
            Assert.IsNotNull(rig.Framing, "CameraRig.framing is unassigned; the camera would keep whatever pose it was saved with.");
            Assert.IsNotNull(rig.Target, "CameraRig.target is unassigned - the camera would never follow the player (D2).");
            Assert.IsNotNull(rig.GetComponent<CinemachineFollow>(), "CinemachineCamera has no follow stage.");
        }

        [Test]
        public void MainCameraHasABrainToBeDrivenBy()
        {
            var brain = FindInWorld<CinemachineBrain>();
            Assert.IsNotNull(brain, "No CinemachineBrain - the Cinemachine camera would drive nothing.");
            Assert.AreEqual("MainCamera", brain.tag, "The brain is not on the camera the screenshots capture.");
        }

        [Test]
        public void CameraDoesNotRotateWithThePlayer()
        {
            // D2: fixed pitch, no player-controlled rotation. A rotation stage would re-derive the
            // camera's aim from its damped position every frame and the pitch would visibly breathe.
            var rig = FindInWorld<CameraRig>();
            var camera = rig.GetComponent<CinemachineCamera>();
            Assert.IsNull(camera.Target.LookAtTarget, "A LookAt target reintroduces camera rotation.");
        }
    }
}
