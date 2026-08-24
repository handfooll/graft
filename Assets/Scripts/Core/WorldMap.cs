using System;
using System.Collections.Generic;

namespace Graft.Core
{
    /// <summary>
    /// Turns a <see cref="WorldMapSpec"/> into a grid of tiles, each belonging to one
    /// <see cref="Area"/> or to the void outside the world.
    ///
    /// This is the whole of Phase 2's world *design* - the shape of the world, which biome is
    /// where, and how far each one is from Base - and it lives in <c>Graft.Core</c> so those are
    /// rules a test can check rather than something only a screenshot can see. The editor-side
    /// builder does nothing but turn these tiles into meshes.
    ///
    /// The world is a corridor, not five arenas: biomes are transverse bands along one path, so
    /// walking out crosses them in ascending order and the walk home from the far end crosses all
    /// of them again. That is DESIGN.md §7's "distance from Base is the difficulty dial", made
    /// geometric instead of asserted.
    /// </summary>
    public sealed class WorldMap
    {
        readonly WorldMapSpec _spec;
        readonly sbyte[] _tiles;
        readonly float[] _biomeEnds;

        public WorldMap(WorldMapSpec spec)
        {
            _spec = spec ?? throw new ArgumentNullException(nameof(spec));
            Path = new WorldPath(spec.PathPoints);

            if (spec.BiomeEndFractions.Length != Areas.Biomes.Length)
            {
                throw new ArgumentException(
                    $"Expected {Areas.Biomes.Length} biome borders, got {spec.BiomeEndFractions.Length}.");
            }

            _biomeEnds = new float[Areas.Biomes.Length];
            for (var i = 0; i < _biomeEnds.Length; i++)
            {
                _biomeEnds[i] = spec.BiomeEndFractions[i] * Path.Length;
            }

            var reach = MaxHalfWidth + spec.EdgeWaveAmplitude;
            var min = new Vec2(float.MaxValue, float.MaxValue);
            var max = new Vec2(float.MinValue, float.MinValue);
            foreach (var point in spec.PathPoints)
            {
                min = new Vec2(MathF.Min(min.X, point.X - reach), MathF.Min(min.Y, point.Y - reach));
                max = new Vec2(MathF.Max(max.X, point.X + reach), MathF.Max(max.Y, point.Y + reach));
            }

            foreach (var lobe in spec.Lobes)
            {
                min = new Vec2(MathF.Min(min.X, lobe.Centre.X - lobe.Radius), MathF.Min(min.Y, lobe.Centre.Y - lobe.Radius));
                max = new Vec2(MathF.Max(max.X, lobe.Centre.X + lobe.Radius), MathF.Max(max.Y, lobe.Centre.Y + lobe.Radius));
            }

            // The skirt is generated, not merely allowed for: the camera looks outward past the
            // boundary wall, and without dead ground out there the frame shows the clear colour,
            // which reads as a hole in the world rather than as an edge of it.
            var pad = spec.TileSize * (spec.VoidSkirtTiles + 1);
            Origin = new Vec2(
                MathF.Floor((min.X - pad) / spec.TileSize) * spec.TileSize,
                MathF.Floor((min.Y - pad) / spec.TileSize) * spec.TileSize);
            Columns = (int)MathF.Ceiling((max.X + pad - Origin.X) / spec.TileSize);
            Rows = (int)MathF.Ceiling((max.Y + pad - Origin.Y) / spec.TileSize);

            _tiles = new sbyte[Columns * Rows];
            for (var row = 0; row < Rows; row++)
            {
                for (var column = 0; column < Columns; column++)
                {
                    _tiles[Index(column, row)] = (sbyte)Classify(TileCentre(column, row));
                }
            }
        }

        /// <summary>Unity's C# has MathF.Tau only on newer runtimes; one turn in radians, spelled out.</summary>
        const float Tau = 6.28318530718f;

        public WorldPath Path { get; }

        public int Columns { get; }
        public int Rows { get; }

        public float TileSize => _spec.TileSize;

        /// <summary>World position of the outer corner of tile (0, 0).</summary>
        public Vec2 Origin { get; }

        public Vec2 TileCentre(int column, int row) => new Vec2(
            Origin.X + (column + 0.5f) * TileSize,
            Origin.Y + (row + 0.5f) * TileSize);

        public bool InBounds(int column, int row) =>
            column >= 0 && row >= 0 && column < Columns && row < Rows;

        /// <summary>The area of a tile, or <see cref="Areas.None"/> for the void.</summary>
        public int TileArea(int column, int row) =>
            InBounds(column, row) ? _tiles[Index(column, row)] : Areas.None;

        public bool IsWalkable(int column, int row) => TileArea(column, row) != Areas.None;

        /// <summary>The area at a world position, or <see cref="Areas.None"/> outside the world.</summary>
        public int AreaAt(Vec2 point)
        {
            var column = (int)MathF.Floor((point.X - Origin.X) / TileSize);
            var row = (int)MathF.Floor((point.Y - Origin.Y) / TileSize);
            return TileArea(column, row);
        }

        /// <summary>Metres from Base along the spine - the number difficulty is a function of.</summary>
        public float DistanceFromBase(Vec2 point) => Path.Project(point).Along;

        public float BiomeStart(Area area) =>
            area == Area.Base ? 0f : _biomeEnds[Areas.DistanceRank(area) - 1] - BandDepth(area);

        public float BiomeEnd(Area area) =>
            area == Area.Base ? _spec.BaseRadius : _biomeEnds[Areas.DistanceRank(area) - 1];

        /// <summary>
        /// A representative spot to stand in an area - the middle of its band, on the spine, so it
        /// is walkable, roughly equidistant from both borders, and away from the boundary wall.
        /// The Phase 2 gate captures from these, and Phase 3 will spawn from them.
        /// </summary>
        public Vec2 AnchorFor(Area area) =>
            area == Area.Base
                ? Path.Start
                : Path.PointAt((BiomeStart(area) + BiomeEnd(area)) * 0.5f);

        /// <summary>
        /// How many mottle patches fit across one tile. The tile is the unit the world is
        /// *designed* in - biome borders, prop placement, the boundary - but it is far too coarse
        /// to be the unit the ground is *drawn* in: at 4 m under a camera that sees 9.5 m, a
        /// two-shade scatter of whole tiles reads as a checkerboard rather than as ground.
        /// </summary>
        public const int MottlePerTile = 2;

        public float MottleSize => TileSize / MottlePerTile;

        public Vec2 MottleCentre(int column, int row, int subColumn, int subRow) => new Vec2(
            Origin.X + (column * MottlePerTile + subColumn + 0.5f) * MottleSize,
            Origin.Y + (row * MottlePerTile + subRow + 0.5f) * MottleSize);

        /// <summary>
        /// Whether a patch takes its area's second ground colour. Two close shades, scattered, is
        /// what stops a flat-coloured biome floor reading as a solid fill.
        /// </summary>
        public bool IsMottled(int column, int row, int subColumn, int subRow) =>
            Hash(column * MottlePerTile + subColumn, row * MottlePerTile + subRow, _spec.Seed) < 0.3f;

        /// <summary>Void tiles close enough to the world to be visible past the boundary wall.</summary>
        public bool IsSkirt(int column, int row)
        {
            if (IsWalkable(column, row))
            {
                return false;
            }

            var reach = _spec.VoidSkirtTiles;
            for (var dy = -reach; dy <= reach; dy++)
            {
                for (var dx = -reach; dx <= reach; dx++)
                {
                    if (IsWalkable(column + dx, row + dy))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Every tile edge where the world stops. The boundary wall is built from exactly these,
        /// so it can never disagree with the ground it is supposed to be enclosing (BUILD_PLAN.md
        /// Phase 2: "boundaries so you cannot walk into the void").
        /// </summary>
        public IEnumerable<Edge> BoundaryEdges()
        {
            for (var row = 0; row < Rows; row++)
            {
                for (var column = 0; column < Columns; column++)
                {
                    if (!IsWalkable(column, row))
                    {
                        continue;
                    }

                    if (!IsWalkable(column + 1, row)) yield return new Edge(column, row, 1, 0);
                    if (!IsWalkable(column - 1, row)) yield return new Edge(column, row, -1, 0);
                    if (!IsWalkable(column, row + 1)) yield return new Edge(column, row, 0, 1);
                    if (!IsWalkable(column, row - 1)) yield return new Edge(column, row, 0, -1);
                }
            }
        }

        /// <summary>
        /// How many tiles are reachable on foot from Base, by flood fill. The world is only
        /// continuous (D3) if this equals the total walkable count - a lobe that drifts clear of
        /// the corridor is otherwise an unreachable island that nothing would report.
        /// </summary>
        public int CountReachableFromBase()
        {
            var seen = new bool[Columns * Rows];
            var frontier = new Stack<int>();
            var start = Index(
                (int)MathF.Floor((Path.Start.X - Origin.X) / TileSize),
                (int)MathF.Floor((Path.Start.Y - Origin.Y) / TileSize));
            frontier.Push(start);
            seen[start] = true;
            var count = 0;

            while (frontier.Count > 0)
            {
                var index = frontier.Pop();
                count++;
                var column = index % Columns;
                var row = index / Columns;
                Visit(column + 1, row);
                Visit(column - 1, row);
                Visit(column, row + 1);
                Visit(column, row - 1);
            }

            return count;

            void Visit(int column, int row)
            {
                if (!IsWalkable(column, row))
                {
                    return;
                }

                var index = Index(column, row);
                if (seen[index])
                {
                    return;
                }

                seen[index] = true;
                frontier.Push(index);
            }
        }

        public int CountWalkable()
        {
            var count = 0;
            foreach (var tile in _tiles)
            {
                if (tile != Areas.None)
                {
                    count++;
                }
            }

            return count;
        }

        int Classify(Vec2 point)
        {
            var projection = Path.Project(point);
            var inside = projection.Offset <= HalfWidthAt(projection);

            if (!inside)
            {
                foreach (var lobe in _spec.Lobes)
                {
                    if ((point - lobe.Centre).Magnitude > lobe.Radius)
                    {
                        continue;
                    }

                    inside = true;
                    break;
                }
            }

            if (!inside)
            {
                return Areas.None;
            }

            if ((point - Path.Start).Magnitude <= _spec.BaseRadius)
            {
                return (int)Area.Base;
            }

            // The biome border is pushed back and forth across the corridor so it is a wandering
            // line rather than a clean transverse cut. Waving by the lateral offset (not by the
            // distance out) keeps every point on a given border at the same distance rank.
            var lateral = projection.Offset * projection.Side;
            var waved = projection.Along +
                        MathF.Sin(lateral / _spec.BorderWaveLength * Tau) * _spec.BorderWaveAmplitude;

            for (var i = 0; i < _biomeEnds.Length; i++)
            {
                if (waved <= _biomeEnds[i])
                {
                    return (int)Areas.Biomes[i];
                }
            }

            return (int)Areas.Biomes[Areas.Biomes.Length - 1];
        }

        float HalfWidthAt(WorldPath.Projection projection)
        {
            var t = projection.Along / Path.Length;
            var width = _spec.HalfWidthNear + (_spec.HalfWidthFar - _spec.HalfWidthNear) * t;
            // Two waves at different rates so the edge does not repeat visibly; the second is
            // driven by side as well, so the two banks of the corridor pinch independently.
            var wave = MathF.Sin(projection.Along / _spec.EdgeWaveLength * Tau) * 0.6f
                       + MathF.Sin(projection.Along / (_spec.EdgeWaveLength * 0.37f) * Tau + projection.Side) * 0.4f;
            return width + wave * _spec.EdgeWaveAmplitude;
        }

        float MaxHalfWidth => MathF.Max(_spec.HalfWidthNear, _spec.HalfWidthFar);

        float BandDepth(Area area)
        {
            var rank = Areas.DistanceRank(area);
            var start = rank <= 1 ? 0f : _biomeEnds[rank - 2];
            return _biomeEnds[rank - 1] - start;
        }

        int Index(int column, int row) => row * Columns + column;

        /// <summary>Deterministic 0..1 hash. A seeded RNG would depend on iteration order; this does not.</summary>
        static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                var h = (uint)(x * 374761393 + y * 668265263 + seed * 1274126177);
                h = (h ^ (h >> 13)) * 1274126177u;
                return (h ^ (h >> 16)) / (float)uint.MaxValue;
            }
        }

        /// <summary>One side of one walkable tile, facing the void.</summary>
        public readonly struct Edge
        {
            public readonly int Column;
            public readonly int Row;
            public readonly int DirectionX;
            public readonly int DirectionY;

            public Edge(int column, int row, int directionX, int directionY)
            {
                Column = column;
                Row = row;
                DirectionX = directionX;
                DirectionY = directionY;
            }
        }
    }
}
