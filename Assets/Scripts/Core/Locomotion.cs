using System;

namespace Graft.Core
{
    /// <summary>
    /// How the player accelerates, brakes and turns. Pure C#, so the numbers that decide whether
    /// moving around is pleasant can be asserted on directly rather than eyeballed.
    ///
    /// BUILD_PLAN.md Phase 1: "the single most important phase. If moving around is not pleasant,
    /// nothing else matters."
    /// </summary>
    public readonly struct MovementProfile
    {
        /// <summary>Metres per second at full stick.</summary>
        public readonly float MaxSpeed;

        /// <summary>
        /// Seconds from a standstill to <see cref="MaxSpeed"/> at full stick. Expressed as a time
        /// rather than an acceleration because a time is a thing you can feel and therefore a
        /// thing you can tune; m/s² is not.
        /// </summary>
        public readonly float TimeToMaxSpeed;

        /// <summary>Seconds from <see cref="MaxSpeed"/> to a standstill on releasing the stick.</summary>
        public readonly float TimeToStop;

        /// <summary>
        /// How fast the character may rotate. Finite on purpose: an instant snap to the stick
        /// direction is what makes twin-stick characters read as weightless.
        /// </summary>
        public readonly float TurnDegreesPerSecond;

        public MovementProfile(float maxSpeed, float timeToMaxSpeed, float timeToStop, float turnDegreesPerSecond)
        {
            MaxSpeed = MathF.Max(0f, maxSpeed);
            TimeToMaxSpeed = MathF.Max(1e-4f, timeToMaxSpeed);
            TimeToStop = MathF.Max(1e-4f, timeToStop);
            TurnDegreesPerSecond = MathF.Max(1f, turnDegreesPerSecond);
        }

        public float Acceleration => MaxSpeed / TimeToMaxSpeed;

        public float Braking => MaxSpeed / TimeToStop;
    }

    /// <summary>Everything locomotion carries between frames.</summary>
    public readonly struct LocomotionState
    {
        public readonly Vec2 Velocity;

        /// <summary>Degrees, Unity Y-rotation convention. Held - never reset - when input stops.</summary>
        public readonly float Heading;

        public LocomotionState(Vec2 velocity, float heading)
        {
            Velocity = velocity;
            Heading = heading;
        }

        public float Speed => Velocity.Magnitude;

        public static LocomotionState AtRest(float heading = 0f) => new LocomotionState(Vec2.Zero, heading);
    }

    public static class Locomotion
    {
        /// <summary>
        /// Shapes a raw stick reading into an intent vector: anything inside the deadzone is
        /// nothing, and everything outside it is rescaled so the very first movement past the
        /// deadzone is a *small* one rather than a jump to <paramref name="deadzone"/> of full
        /// speed. Without the rescale the character lurches the instant the thumb moves.
        /// </summary>
        public static Vec2 ShapeStick(Vec2 raw, float deadzone)
        {
            deadzone = Math.Clamp(deadzone, 0f, 0.95f);
            var magnitude = raw.Magnitude;
            if (magnitude <= deadzone)
            {
                return Vec2.Zero;
            }

            var scaled = MathF.Min(1f, (magnitude - deadzone) / (1f - deadzone));
            return raw.Normalised * scaled;
        }

        /// <summary>
        /// Advances one frame. <paramref name="intent"/> is an already-shaped stick vector with a
        /// magnitude in [0,1]; its magnitude is the fraction of <see cref="MovementProfile.MaxSpeed"/>
        /// being asked for, so a half-pressed stick really does walk.
        /// </summary>
        public static LocomotionState Step(LocomotionState state, Vec2 intent, MovementProfile profile, float deltaTime)
        {
            if (deltaTime <= 0f)
            {
                return state;
            }

            var intentMagnitude = MathF.Min(1f, intent.Magnitude);
            var target = intentMagnitude > 0f ? intent.Normalised * (profile.MaxSpeed * intentMagnitude) : Vec2.Zero;

            // Braking is a separate, faster rate than acceleration. Sharing one rate makes a
            // character that both launches sluggishly and coasts too far; splitting them is what
            // buys "responsive but not weightless".
            var rate = intentMagnitude > 0f ? profile.Acceleration : profile.Braking;
            var velocity = Vec2.MoveTowards(state.Velocity, target, rate * deltaTime);

            return new LocomotionState(velocity, StepHeading(state.Heading, intent, velocity, profile, deltaTime));
        }

        /// <summary>
        /// Turns toward where the player is *steering*, not where they are drifting, so the
        /// character has already committed to a direction while it is still building up speed.
        /// With no input the heading is held: a stopped character keeps facing where it was going.
        /// </summary>
        static float StepHeading(float heading, Vec2 intent, Vec2 velocity, MovementProfile profile, float deltaTime)
        {
            var reference = intent.SqrMagnitude > 1e-6f ? intent : velocity;
            if (reference.SqrMagnitude < 1e-6f)
            {
                return heading;
            }

            var delta = Vec2.ShortestAngle(heading, reference.Heading);
            var maxStep = profile.TurnDegreesPerSecond * deltaTime;
            if (MathF.Abs(delta) <= maxStep)
            {
                return reference.Heading;
            }

            return heading + MathF.Sign(delta) * maxStep;
        }
    }
}
