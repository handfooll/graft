using System;

namespace Graft.Core
{
    /// <summary>
    /// The spine of the world: a polyline running from Base outward, parameterised by arc length.
    ///
    /// Everything about the world's difficulty gradient is expressed as a distance along this
    /// path - which biome you are standing in, how far you are from Base, how wide the world is
    /// where you are. DESIGN.md §7 makes distance from Base the difficulty dial, so making the
    /// distance a real measured quantity rather than a set of hand-placed region centres is what
    /// keeps that promise honest.
    ///
    /// Pure C# (BUILD_PLAN.md rule 4). Distances are metres and X/Y map to Unity's X/Z.
    /// </summary>
    public sealed class WorldPath
    {
        readonly Vec2[] _points;
        readonly float[] _cumulative;

        public WorldPath(Vec2[] points)
        {
            if (points == null || points.Length < 2)
            {
                throw new ArgumentException("A world path needs at least two points.", nameof(points));
            }

            _points = points;
            _cumulative = new float[points.Length];
            for (var i = 1; i < points.Length; i++)
            {
                _cumulative[i] = _cumulative[i - 1] + (points[i] - points[i - 1]).Magnitude;
            }

            if (Length <= 0f)
            {
                throw new ArgumentException("A world path needs a non-zero length.", nameof(points));
            }
        }

        /// <summary>Total arc length, in metres. This is the depth of the world.</summary>
        public float Length => _cumulative[_cumulative.Length - 1];

        public Vec2 Start => _points[0];

        public Vec2 End => _points[_points.Length - 1];

        /// <summary>Where the path is, <paramref name="along"/> metres from Base. Clamped at both ends.</summary>
        public Vec2 PointAt(float along)
        {
            along = Clamp(along, 0f, Length);
            var segment = SegmentContaining(along);
            var span = _cumulative[segment + 1] - _cumulative[segment];
            var t = span > 1e-5f ? (along - _cumulative[segment]) / span : 0f;
            return _points[segment] + (_points[segment + 1] - _points[segment]) * t;
        }

        /// <summary>Unit heading of the path at a distance out. Clamped at both ends.</summary>
        public Vec2 DirectionAt(float along)
        {
            var segment = SegmentContaining(Clamp(along, 0f, Length));
            return (_points[segment + 1] - _points[segment]).Normalised;
        }

        /// <summary>
        /// Nearest point on the path to <paramref name="point"/>.
        ///
        /// <see cref="Projection.Along"/> is clamped to the path, so a point past either end
        /// projects onto the endpoint and <see cref="Projection.Offset"/> becomes its straight-line
        /// distance to that endpoint. That is deliberate: it gives the world rounded caps for free
        /// instead of the square cut a clamp-free parameterisation would leave.
        /// </summary>
        public Projection Project(Vec2 point)
        {
            var bestOffset = float.MaxValue;
            var bestAlong = 0f;
            var bestSide = 0f;

            for (var i = 0; i < _points.Length - 1; i++)
            {
                var a = _points[i];
                var delta = _points[i + 1] - a;
                var lengthSquared = delta.SqrMagnitude;
                var toPoint = point - a;
                var t = lengthSquared > 1e-6f
                    ? Clamp((toPoint.X * delta.X + toPoint.Y * delta.Y) / lengthSquared, 0f, 1f)
                    : 0f;

                var closest = a + delta * t;
                var away = point - closest;
                var offset = away.Magnitude;
                if (offset >= bestOffset)
                {
                    continue;
                }

                bestOffset = offset;
                bestAlong = _cumulative[i] + (delta.Magnitude * t);
                // Positive to the left of the direction of travel. Only the sign is used - for
                // waving the biome borders - but a signed value keeps the two sides distinguishable.
                var direction = delta.Normalised;
                bestSide = direction.X * away.Y - direction.Y * away.X;
            }

            return new Projection(bestAlong, bestOffset, bestSide >= 0f ? 1f : -1f);
        }

        int SegmentContaining(float along)
        {
            for (var i = 0; i < _points.Length - 1; i++)
            {
                if (along <= _cumulative[i + 1])
                {
                    return i;
                }
            }

            return _points.Length - 2;
        }

        static float Clamp(float value, float min, float max) =>
            value < min ? min : value > max ? max : value;

        public readonly struct Projection
        {
            /// <summary>Metres from Base along the path. Never negative, never past the end.</summary>
            public readonly float Along;

            /// <summary>Metres sideways from the path, always positive.</summary>
            public readonly float Offset;

            /// <summary>+1 left of the direction of travel, -1 right.</summary>
            public readonly float Side;

            public Projection(float along, float offset, float side)
            {
                Along = along;
                Offset = offset;
                Side = side;
            }
        }
    }
}
