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
    /// Two shapes, both real, chosen by <see cref="WorldShape"/> - the project keeps a layout asset
    /// for each so they can be compared on a phone rather than argued about on paper:
    ///
    /// <list type="bullet">
    /// <item><b>Corridor</b> - biomes as transverse bands along one spine. The strongest possible
    /// difficulty gradient, because the walk home from the deepest biome crosses every other one.
    /// Its cost is that the world is one-dimensional: any biome to any other means traversing
    /// everything in between.</item>
    /// <item><b>Cluster</b> - biomes as overlapping discs packed around Base, each bordering two to
    /// four others. Travel between biomes becomes a hop. The gradient survives because the discs
    /// sit at ascending distances from Base, but its range is narrower.</item>
    /// </list>
    ///
    /// Either way DESIGN.md §7's "distance from Base is the difficulty dial" is geometry rather
    /// than an assertion, and <c>WorldMapTests</c> measures it on whichever shape is built.
    /// </summary>
    public sealed class WorldMap
    {
        readonly WorldMapSpec _spec;
        readonly sbyte[] _tiles;
        readonly float[] _biomeEnds;
        HashSet<long> _adjacency;

        public WorldMap(WorldMapSpec spec)
        {
            _spec = spec ?? throw new ArgumentNullException(nameof(spec));

            var min = new Vec2(float.MaxValue, float.MaxValue);
            var max = new Vec2(float.MinValue, float.MinValue);
            void Include(Vec2 centre, float reach)
            {
                min = new Vec2(MathF.Min(min.X, centre.X - reach), MathF.Min(min.Y, centre.Y - reach));
                max = new Vec2(MathF.Max(max.X, centre.X + reach), MathF.Max(max.Y, centre.Y + reach));
            }

            if (Shape == WorldShape.Corridor)
            {
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
                foreach (var point in spec.PathPoints)
                {
                    Include(point, reach);
                }

                foreach (var lobe in spec.Lobes)
                {
                    Include(lobe.Centre, lobe.Radius);
                }
            }
            else
            {
                if (spec.AreaDiscs.Length != Areas.All.Length)
                {
                    throw new ArgumentException(
                        $"A cluster needs one disc per area: expected {Areas.All.Length}, got {spec.AreaDiscs.Length}.");
                }

                _biomeEnds = Array.Empty<float>();
                foreach (var disc in spec.AreaDiscs)
                {
                    Include(disc.Centre, disc.Radius * (1f + spec.DiscWobble));
                }
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

        public WorldShape Shape => _spec.Shape;

        /// <summary>The spine. Corridor only - <c>null</c> for a cluster, which has no spine.</summary>
        public WorldPath Path { get; }

        /// <summary>
        /// Where Base is. Everything the difficulty gradient means is measured from this point, so
        /// it is read from the layout rather than assumed to be the world origin.
        /// </summary>
        public Vec2 BaseCentre => Shape == WorldShape.Corridor
            ? Path.Start
            : _spec.AreaDiscs[Areas.DistanceRank(Area.Base)].Centre;

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

        /// <summary>
        /// Metres from Base - the number difficulty is a function of (DESIGN.md §7).
        ///
        /// Along the spine for a corridor, where the spine *is* the route home. Straight-line for a
        /// cluster, where it genuinely is: every biome disc reaches Base directly or through one
        /// neighbour, so there is no detour to account for.
        /// </summary>
        public float DistanceFromBase(Vec2 point) => Shape == WorldShape.Corridor
            ? Path.Project(point).Along
            : (point - BaseCentre).Magnitude;

        /// <summary>
        /// The way "further out" points from here - what the player is facing when they choose to
        /// keep going. Along the spine for a corridor, away from Base for a cluster.
        /// </summary>
        public Vec2 OutwardHeading(Vec2 point)
        {
            if (Shape == WorldShape.Corridor)
            {
                return Path.DirectionAt(Path.Project(point).Along);
            }

            var away = point - BaseCentre;
            return away.SqrMagnitude > 1e-4f ? away.Normalised : new Vec2(0f, 1f);
        }

        /// <summary>Where a biome's band begins, in metres along the spine. Corridor only.</summary>
        public float BiomeStart(Area area)
        {
            RequireCorridor();
            return area == Area.Base ? 0f : _biomeEnds[Areas.DistanceRank(area) - 1] - BandDepth(area);
        }

        /// <summary>Where a biome's band ends, in metres along the spine. Corridor only.</summary>
        public float BiomeEnd(Area area)
        {
            RequireCorridor();
            return area == Area.Base ? _spec.BaseRadius : _biomeEnds[Areas.DistanceRank(area) - 1];
        }

        void RequireCorridor()
        {
            if (Shape != WorldShape.Corridor)
            {
                throw new InvalidOperationException(
                    $"Biome bands are a corridor idea; this world is a {Shape}. Use AnchorFor or DistanceFromBase.");
            }
        }

        /// <summary>
        /// A representative spot to stand in an area: the middle of its band for a corridor, the
        /// centre of its disc for a cluster. Either way it is walkable, roughly equidistant from
        /// the area's borders, and well clear of the boundary wall.
        /// The Phase 2 gate captures from these, and Phase 3 will spawn from them.
        /// </summary>
        public Vec2 AnchorFor(Area area)
        {
            if (Shape == WorldShape.Cluster)
            {
                return _spec.AreaDiscs[Areas.DistanceRank(area)].Centre;
            }

            return area == Area.Base
                ? Path.Start
                : Path.PointAt((BiomeStart(area) + BiomeEnd(area)) * 0.5f);
        }

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
                (int)MathF.Floor((BaseCentre.X - Origin.X) / TileSize),
                (int)MathF.Floor((BaseCentre.Y - Origin.Y) / TileSize));
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

        /// <summary>
        /// The areas this one shares a border with.
        ///
        /// Measured off the finished tiles rather than off the layout's discs, so it reports what
        /// the world *is* and not what it was meant to be - a wobble that pulls two discs apart at
        /// their only contact point is exactly the kind of thing that looks fine in the numbers.
        ///
        /// This is what decides whether getting from one biome to another is a hop or a traverse,
        /// so <c>WorldMapTests</c> asserts on it, and Phase 4's way home will read it.
        /// </summary>
        public IEnumerable<Area> NeighboursOf(Area area)
        {
            foreach (var other in Areas.All)
            {
                if (other != area && AreasTouch(area, other))
                {
                    yield return other;
                }
            }
        }

        public bool AreasTouch(Area a, Area b) => Adjacency.Contains(Pair((int)a, (int)b));

        HashSet<long> Adjacency
        {
            get
            {
                if (_adjacency != null)
                {
                    return _adjacency;
                }

                _adjacency = new HashSet<long>();
                for (var row = 0; row < Rows; row++)
                {
                    for (var column = 0; column < Columns; column++)
                    {
                        var here = TileArea(column, row);
                        if (here == Areas.None)
                        {
                            continue;
                        }

                        Meet(here, TileArea(column + 1, row));
                        Meet(here, TileArea(column, row + 1));
                    }
                }

                return _adjacency;

                void Meet(int here, int there)
                {
                    if (there != Areas.None && there != here)
                    {
                        _adjacency.Add(Pair(here, there));
                    }
                }
            }
        }

        static long Pair(int a, int b) => a < b ? a * 16L + b : b * 16L + a;

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

        int Classify(Vec2 point) => Shape == WorldShape.Corridor
            ? ClassifyCorridor(point)
            : ClassifyCluster(point);

        /// <summary>
        /// Which disc a point is furthest *inside*, or the void if it is inside none.
        ///
        /// Comparing <c>distance - radius</c> rather than plain distance is what makes overlapping
        /// discs of different sizes share a sensible border: the point goes to whichever biome it
        /// is deeper into, so a big disc keeps its interior instead of being sliced in half by a
        /// small neighbour's midline. The same expression decides walkability, so the outside edge
        /// of the world and the borders between biomes are one function and cannot disagree.
        ///
        /// Base is a crisp circle rather than a competitor in that comparison - it is the one place
        /// in the world that is built rather than grown, and it should have an edge you can see.
        /// </summary>
        int ClassifyCluster(Vec2 point)
        {
            if ((point - BaseCentre).Magnitude <= _spec.BaseRadius)
            {
                return (int)Area.Base;
            }

            var best = Areas.None;
            var bestDepth = float.MaxValue;
            var shallowest = float.MaxValue;
            var dither = Dither(point);

            foreach (var area in Areas.Biomes)
            {
                var rank = Areas.DistanceRank(area);
                var disc = _spec.AreaDiscs[rank];
                var away = point - disc.Centre;
                var depth = away.Magnitude - WobbledRadius(disc.Radius, rank, away);

                // Walkability is decided on the true depth and the winner on the jittered one, so
                // the jitter roughens the border between two biomes without fraying the edge of
                // the world into loose tiles.
                shallowest = MathF.Min(shallowest, depth);

                var contest = depth + dither * (rank % 2 == 0 ? 1f : -1f);
                if (contest >= bestDepth)
                {
                    continue;
                }

                bestDepth = contest;
                best = (int)area;
            }

            return shallowest <= 0f ? best : Areas.None;
        }

        /// <summary>
        /// A disc's radius in one direction, wandering with bearing so the world is not a diagram
        /// of circles. Two harmonics, phase-shifted per disc, so no two edges repeat each other.
        /// </summary>
        float WobbledRadius(float radius, int rank, Vec2 away)
        {
            var bearing = MathF.Atan2(away.Y, away.X);
            var wobble = MathF.Sin(bearing * 3f + rank * 1.7f) * 0.62f
                         + MathF.Sin(bearing * 5f - rank * 2.3f) * 0.38f;
            return radius * (1f + wobble * _spec.DiscWobble);
        }

        int ClassifyCorridor(Vec2 point)
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

            if ((point - BaseCentre).Magnitude <= _spec.BaseRadius)
            {
                return (int)Area.Base;
            }

            // The biome border is pushed back and forth across the corridor so it is a wandering
            // line rather than a clean transverse cut. Waving by the lateral offset (not by the
            // distance out) keeps every point on a given border at the same distance rank.
            var lateral = projection.Offset * projection.Side;
            var waved = projection.Along
                        + MathF.Sin(lateral / _spec.BorderWaveLength * Tau) * _spec.BorderWaveAmplitude
                        + Dither(point);

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

        /// <summary>
        /// Per-tile jitter in metres, symmetric about zero. Quantised to the tile so every point
        /// within one tile gets the same answer - jittering continuously would dither the border at
        /// sub-tile resolution and simply blur it back into a straight line.
        /// </summary>
        float Dither(Vec2 point)
        {
            if (_spec.BorderDither <= 0f)
            {
                return 0f;
            }

            var column = (int)MathF.Floor((point.X - Origin.X) / TileSize);
            var row = (int)MathF.Floor((point.Y - Origin.Y) / TileSize);
            return (Hash(column, row, _spec.Seed + 9176) - 0.5f) * 2f * _spec.BorderDither;
        }

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
