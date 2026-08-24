using System;

namespace Graft.Core
{
    /// <summary>
    /// A 2D vector on the ground plane. Exists because <c>Graft.Core</c> may not reference
    /// UnityEngine (BUILD_PLAN.md sequencing rule 4) and the locomotion model is worth keeping
    /// on that side of the line - "how does moving feel" is a rule, and rules stay testable
    /// without an engine.
    ///
    /// <c>X</c> maps to Unity's world X and <c>Y</c> to world **Z**. There is no vertical
    /// component anywhere in the movement model: the player walks on a plane, and height is the
    /// CharacterController's problem, not locomotion's.
    /// </summary>
    public readonly struct Vec2 : IEquatable<Vec2>
    {
        public readonly float X;
        public readonly float Y;

        public Vec2(float x, float y)
        {
            X = x;
            Y = y;
        }

        public static Vec2 Zero => new Vec2(0f, 0f);

        public float Magnitude => MathF.Sqrt(X * X + Y * Y);

        public float SqrMagnitude => X * X + Y * Y;

        /// <summary>Unit length, or zero if the vector has no direction to preserve.</summary>
        public Vec2 Normalised
        {
            get
            {
                var length = Magnitude;
                return length > 1e-6f ? new Vec2(X / length, Y / length) : Zero;
            }
        }

        /// <summary>
        /// Heading in degrees, measured the way Unity measures a Y rotation: 0 is +Z (screen "up"
        /// under this camera), 90 is +X. Zero-length vectors have no heading and return 0.
        /// </summary>
        public float Heading => MathF.Atan2(X, Y) * (180f / MathF.PI);

        public static Vec2 FromHeading(float degrees, float length = 1f)
        {
            var radians = degrees * (MathF.PI / 180f);
            return new Vec2(MathF.Sin(radians) * length, MathF.Cos(radians) * length);
        }

        public static Vec2 operator +(Vec2 a, Vec2 b) => new Vec2(a.X + b.X, a.Y + b.Y);
        public static Vec2 operator -(Vec2 a, Vec2 b) => new Vec2(a.X - b.X, a.Y - b.Y);
        public static Vec2 operator *(Vec2 v, float k) => new Vec2(v.X * k, v.Y * k);
        public static Vec2 operator *(float k, Vec2 v) => v * k;

        /// <summary>
        /// Steps <paramref name="from"/> toward <paramref name="to"/> by at most
        /// <paramref name="maxDelta"/>, landing exactly on the target rather than approaching it
        /// asymptotically. Acceleration and braking both need to actually *arrive*.
        /// </summary>
        public static Vec2 MoveTowards(Vec2 from, Vec2 to, float maxDelta)
        {
            var delta = to - from;
            var distance = delta.Magnitude;
            if (distance <= maxDelta || distance < 1e-6f)
            {
                return to;
            }

            return from + delta * (maxDelta / distance);
        }

        /// <summary>Signed shortest rotation from one heading to another, in (-180, 180].</summary>
        public static float ShortestAngle(float fromDegrees, float toDegrees)
        {
            var delta = (toDegrees - fromDegrees) % 360f;
            if (delta > 180f)
            {
                delta -= 360f;
            }
            else if (delta <= -180f)
            {
                delta += 360f;
            }

            return delta;
        }

        public bool Equals(Vec2 other) => X.Equals(other.X) && Y.Equals(other.Y);
        public override bool Equals(object obj) => obj is Vec2 other && Equals(other);
        public override int GetHashCode() => (X, Y).GetHashCode();
        public override string ToString() => $"({X:0.###}, {Y:0.###})";
    }
}
