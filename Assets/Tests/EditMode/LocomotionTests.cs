using System;
using Graft.Core;
using NUnit.Framework;

namespace Graft.Tests
{
    /// <summary>
    /// The movement model, asserted on directly. These are the numbers that decide whether Phase 1
    /// feels right; the screenshots still get the final say, but "reaches full speed in the time it
    /// says it does" is a rule, and rules are cheaper to check here than in a picture.
    /// </summary>
    public class LocomotionTests
    {
        const float Dt = 1f / 60f;

        static MovementProfile Profile(float maxSpeed = 5.5f, float toMax = 0.16f,
            float toStop = 0.10f, float turn = 1080f) =>
            new MovementProfile(maxSpeed, toMax, toStop, turn);

        static LocomotionState Run(LocomotionState state, Vec2 intent, MovementProfile profile, float seconds)
        {
            var steps = (int)MathF.Round(seconds / Dt);
            for (var i = 0; i < steps; i++)
            {
                state = Locomotion.Step(state, intent, profile, Dt);
            }

            return state;
        }

        [Test]
        public void FullStickReachesMaxSpeedInTheAdvertisedTime()
        {
            var profile = Profile(toMax: 0.16f);
            var state = Run(LocomotionState.AtRest(), new Vec2(0f, 1f), profile, 0.16f);
            Assert.AreEqual(profile.MaxSpeed, state.Speed, 0.05f);
        }

        [Test]
        public void ReleasingTheStickStopsInTheAdvertisedTime()
        {
            var profile = Profile(toStop: 0.10f);
            var moving = new LocomotionState(new Vec2(0f, profile.MaxSpeed), 0f);
            var state = Run(moving, Vec2.Zero, profile, 0.10f);
            Assert.AreEqual(0f, state.Speed, 0.02f);
        }

        [Test]
        public void BrakingIsFasterThanAccelerating()
        {
            // Coasting past where you let go reads as a bug, so stopping is deliberately the
            // sharper of the two. If the tuning ever inverts this, movement will feel sloppy.
            var profile = Profile();
            Assert.Greater(profile.Braking, profile.Acceleration);
        }

        [Test]
        public void HalfStickWalksAtHalfSpeed()
        {
            var profile = Profile();
            var state = Run(LocomotionState.AtRest(), new Vec2(0f, 0.5f), profile, 1f);
            Assert.AreEqual(profile.MaxSpeed * 0.5f, state.Speed, 0.01f);
        }

        [Test]
        public void SpeedNeverExceedsMaximumOnADiagonal()
        {
            // A raw (1,1) stick has magnitude 1.41. Uncorrected, diagonal movement is 41% faster
            // than cardinal movement, which is the classic top-down bug.
            var profile = Profile();
            var intent = Locomotion.ShapeStick(new Vec2(1f, 1f), 0.12f);
            var state = Run(LocomotionState.AtRest(), intent, profile, 1f);
            Assert.LessOrEqual(state.Speed, profile.MaxSpeed + 1e-3f);
            Assert.AreEqual(profile.MaxSpeed, state.Speed, 0.01f);
        }

        [Test]
        public void StickInsideTheDeadzoneIsNoInput()
        {
            Assert.AreEqual(Vec2.Zero, Locomotion.ShapeStick(new Vec2(0.08f, 0.05f), 0.12f));
        }

        [Test]
        public void CrossingTheDeadzoneStartsFromZeroRatherThanJumping()
        {
            // Without rescaling, the first reading past a 0.12 deadzone would already ask for 12%
            // of max speed - a visible lurch the instant the thumb moves.
            var justOutside = Locomotion.ShapeStick(new Vec2(0f, 0.13f), 0.12f);
            Assert.Less(justOutside.Magnitude, 0.02f);
            Assert.Greater(justOutside.Magnitude, 0f);
        }

        [Test]
        public void FullStickStillMeansFullSpeedAfterDeadzoneRescaling()
        {
            var shaped = Locomotion.ShapeStick(new Vec2(0f, 1f), 0.12f);
            Assert.AreEqual(1f, shaped.Magnitude, 1e-4f);
        }

        [Test]
        public void HeadingFollowsTheStickAndIsCappedByTheTurnRate()
        {
            var profile = Profile(turn: 1080f);
            var state = LocomotionState.AtRest();
            // One frame at 1080 deg/s can cover 18 degrees, so a 90-degree demand cannot be met.
            state = Locomotion.Step(state, new Vec2(1f, 0f), profile, Dt);
            Assert.AreEqual(profile.TurnDegreesPerSecond * Dt, state.Heading, 0.01f);
        }

        [Test]
        public void HeadingArrivesExactlyRatherThanApproaching()
        {
            var profile = Profile();
            var state = Run(LocomotionState.AtRest(), new Vec2(1f, 0f), profile, 0.5f);
            Assert.AreEqual(90f, state.Heading, 1e-3f);
        }

        [Test]
        public void AReversalTurnsTheShortWayRoundNotThroughZero()
        {
            var profile = Profile();
            // Facing 350; asked for 10. The short way is +20 degrees, not -340.
            var state = Locomotion.Step(new LocomotionState(Vec2.Zero, 350f),
                Vec2.FromHeading(10f), profile, Dt);
            Assert.Greater(state.Heading, 350f);
        }

        [Test]
        public void HeadingIsHeldWhenTheStickIsReleased()
        {
            // A stopped character keeps facing where it was going. Snapping back to a default
            // heading on release is the thing that makes idle characters look broken.
            var profile = Profile();
            var facing = Run(LocomotionState.AtRest(), Vec2.FromHeading(217f), profile, 0.5f).Heading;
            var rested = Run(new LocomotionState(Vec2.Zero, facing), Vec2.Zero, profile, 1f);
            Assert.AreEqual(facing, rested.Heading, 1e-3f);
        }

        [Test]
        public void ZeroDeltaTimeChangesNothing()
        {
            var profile = Profile();
            var state = new LocomotionState(new Vec2(1f, 2f), 45f);
            var stepped = Locomotion.Step(state, new Vec2(1f, 0f), profile, 0f);
            Assert.AreEqual(state.Velocity, stepped.Velocity);
            Assert.AreEqual(state.Heading, stepped.Heading);
        }

        [Test]
        public void HeadingUsesUnityYRotationConvention()
        {
            // +Z is 0 and +X is 90, so a heading can be handed to Quaternion.Euler unchanged.
            Assert.AreEqual(0f, new Vec2(0f, 1f).Heading, 1e-4f);
            Assert.AreEqual(90f, new Vec2(1f, 0f).Heading, 1e-4f);
            Assert.AreEqual(180f, MathF.Abs(new Vec2(0f, -1f).Heading), 1e-4f);
        }
    }
}
