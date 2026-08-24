using Graft.Editor;
using Graft.Game;
using NUnit.Framework;
using UnityEditor;

namespace Graft.Tests
{
    /// <summary>
    /// Guards the shipped tuning asset, not a synthetic profile. LocomotionTests proves the model
    /// obeys whatever numbers it is handed; this proves the numbers the game actually loads are
    /// sane ones.
    /// </summary>
    public class MovementTuningTests
    {
        static MovementTuning Load()
        {
            var tuning = AssetDatabase.LoadAssetAtPath<MovementTuning>(ProjectSetup.MovementTuningPath);
            Assert.IsNotNull(tuning, $"Missing {ProjectSetup.MovementTuningPath} - run ProjectSetup.ConfigureAll.");
            return tuning;
        }

        [Test]
        public void StoppingIsSharperThanStarting()
        {
            var tuning = Load();
            Assert.Less(tuning.timeToStop, tuning.timeToMaxSpeed,
                "Coasting past where the thumb lifted reads as a bug, not as weight.");
        }

        [Test]
        public void ADeadzoneExistsButDoesNotEatTheStick()
        {
            var tuning = Load();
            Assert.Greater(tuning.stickDeadzone, 0f, "No deadzone means the character creeps on a resting thumb.");
            Assert.Less(tuning.stickDeadzone, 0.3f, "A deadzone this large throws away most of the stick's travel.");
        }

        [Test]
        public void PlayerCannotOutrunTheCameraSView()
        {
            // The reaction-time sanity check. The player sits centred, so half the visible width is
            // all the warning they get; crossing it in well under a second means threats arrive
            // before they can be seen. Portrait 9:16 is the narrow case and therefore the one to
            // check. This is provisional in both directions - STATUS.md defers the real answer to
            // Phase 3, when there is something to react to.
            var tuning = Load();
            var framing = AssetDatabase.LoadAssetAtPath<CameraFraming>(ProjectSetup.CameraFramingPath);
            Assert.IsNotNull(framing);

            var lead = framing.VisibleWidthOnDevice * 0.5f;
            Assert.Greater(lead / tuning.maxSpeed, 0.8f,
                $"Only {lead / tuning.maxSpeed:0.00}s of warning at full speed. Slow the player or pull the camera back.");
        }
    }
}
