using System.Linq;
using Graft.Core;
using Graft.Editor;
using Graft.Game;
using NUnit.Framework;

namespace Graft.Tests
{
    /// <summary>
    /// The world's rules, checked against the layouts the game actually ships with - not synthetic
    /// ones, and **both shapes**, because the inactive layout is kept precisely so it can be
    /// switched back to, and a layout that has quietly rotted is not a fallback.
    ///
    /// A generated world has failure modes no screenshot can catch, because each of them looks
    /// completely normal from wherever you happen to be standing: a biome in the wrong order, a
    /// disc that drifted clear of its neighbours and became an island, a hole in the boundary at
    /// the one corner nobody framed.
    ///
    /// What a screenshot *does* catch - whether the six places look like six places - is the gate,
    /// and it stays a human's job (BUILD_PLAN.md).
    /// </summary>
    [TestFixture(WorldShape.Cluster)]
    [TestFixture(WorldShape.Corridor)]
    public class WorldMapTests
    {
        readonly WorldShape _shape;
        WorldLayout _layout;
        WorldMap _map;

        public WorldMapTests(WorldShape shape) => _shape = shape;

        [SetUp]
        public void Build()
        {
            _layout = ProjectSetup.EnsureWorldLayout(_shape);
            _map = _layout.BuildMap();
            Assert.AreEqual(_shape, _map.Shape, "The layout asset is not the shape it was asked for.");
        }

        [Test]
        public void EveryAreaExistsInTheWorld()
        {
            foreach (var area in Areas.All)
            {
                Assert.Greater(CountTiles(area), 0, $"{area} has no ground anywhere in the world.");
            }
        }

        [Test]
        public void ThereAreExactlyTheAreasTheDesignLocksDown()
        {
            Assert.AreEqual(DesignContract.AreaCount, Areas.All.Length);
            Assert.AreEqual(DesignContract.BiomeCount, Areas.Biomes.Length);
            Assert.AreEqual(DesignContract.AreaCount, _layout.palettes.Length,
                "Every area needs a palette, or its ground would render in another area's colour.");
        }

        /// <summary>
        /// The disc array is indexed by distance rank, and each entry also names its area. The two
        /// must agree, or the world is built with Rust Flats' footprint painted Bone Reach's colour
        /// - which looks entirely deliberate in every screenshot.
        /// </summary>
        [Test]
        public void ClusterDiscsAreInDistanceRankOrder()
        {
            if (_shape != WorldShape.Cluster)
            {
                Assert.Pass("Corridor worlds have no discs.");
            }

            for (var i = 0; i < _layout.areaDiscs.Length; i++)
            {
                Assert.AreEqual(i, Areas.DistanceRank(_layout.areaDiscs[i].area),
                    $"Disc {i} names {_layout.areaDiscs[i].area}, whose distance rank is not {i}.");
            }
        }

        /// <summary>
        /// DESIGN.md §7: distance from Base is the difficulty dial. That is only true if the
        /// biomes are actually laid out in ascending order of distance, so measure it rather than
        /// trusting the layout to have been authored carefully.
        /// </summary>
        [Test]
        public void BiomesGetFurtherFromBaseInOrder()
        {
            var previous = 0f;
            foreach (var area in Areas.Biomes)
            {
                var mean = MeanDistanceFromBase(area);
                Assert.Greater(mean, previous,
                    $"{area} sits closer to Base than the biome before it - the difficulty dial runs backwards.");
                previous = mean;
            }
        }

        [Test]
        public void BaseSitsAtTheMouthOfTheWorld()
        {
            Assert.AreEqual((int)Area.Base, _map.AreaAt(_map.BaseCentre),
                "The centre of Base is not Base - the player would spawn in a biome.");
            Assert.Less(MeanDistanceFromBase(Area.Base), MeanDistanceFromBase(Area.Verge));
        }

        /// <summary>
        /// D3: one continuous world. A disc placed slightly too far from its neighbours becomes an
        /// island - perfectly good-looking ground that no player can ever reach, and nothing
        /// anywhere reports it.
        /// </summary>
        [Test]
        public void EveryTileIsWalkableFromBase()
        {
            Assert.AreEqual(_map.CountWalkable(), _map.CountReachableFromBase(),
                "Part of the world is cut off from Base. In a cluster, check discWobble is not " +
                "eating an overlap; in a corridor, check the lobes still meet the spine.");
        }

        /// <summary>
        /// The reason the cluster layout exists. The corridor is one-dimensional - Verge to Bone
        /// Reach means walking through Rust Flats and Glass Mire, every single time - and the
        /// cluster was chosen to end that. So assert the property that was actually bought, not
        /// the disc coordinates that were supposed to buy it.
        /// </summary>
        [Test]
        public void BiomesBorderEachOtherTheWayTheShapeIntends()
        {
            var neighbours = Areas.Biomes.ToDictionary(
                area => area,
                area => _map.NeighboursOf(area).Count(Areas.IsBiome));

            if (_shape == WorldShape.Corridor)
            {
                // Bands in a row: each biome meets the next one out. Nothing more is expected.
                for (var i = 0; i < Areas.Biomes.Length - 1; i++)
                {
                    Assert.IsTrue(_map.AreasTouch(Areas.Biomes[i], Areas.Biomes[i + 1]),
                        $"{Areas.Biomes[i]} does not border {Areas.Biomes[i + 1]} - the bands have a gap.");
                }

                return;
            }

            foreach (var (area, count) in neighbours)
            {
                Assert.GreaterOrEqual(count, 2,
                    $"{area} borders only {count} other biome(s). The cluster exists so that getting " +
                    "between biomes is a hop rather than a traverse.");
            }

            Assert.GreaterOrEqual(neighbours.Values.Max(), 4,
                "No biome borders four others - the cluster has collapsed into a chain, which is " +
                "the corridor's problem with extra steps.");
            Assert.IsTrue(_map.AreasTouch(Area.Base, Area.Verge),
                "Base does not border the Verge, so leaving Base means crossing the void.");
        }

        /// <summary>
        /// The boundary is generated from tiles that have a non-walkable neighbour, so a walkable
        /// tile on the very edge of the grid would have no neighbour to compare against and would
        /// leave a gap in the wall - straight out into the void.
        /// </summary>
        [Test]
        public void TheWorldNeverTouchesTheEdgeOfTheGrid()
        {
            for (var row = 0; row < _map.Rows; row++)
            {
                for (var column = 0; column < _map.Columns; column++)
                {
                    if (!_map.IsWalkable(column, row))
                    {
                        continue;
                    }

                    Assert.That(column, Is.GreaterThan(0).And.LessThan(_map.Columns - 1),
                        $"Walkable tile at the grid edge ({column}, {row}) - the boundary has a hole.");
                    Assert.That(row, Is.GreaterThan(0).And.LessThan(_map.Rows - 1),
                        $"Walkable tile at the grid edge ({column}, {row}) - the boundary has a hole.");
                }
            }
        }

        [Test]
        public void AnchorsAreWalkableAndInTheAreaTheyName()
        {
            foreach (var area in Areas.All)
            {
                var anchor = _map.AnchorFor(area);
                Assert.AreEqual((int)area, _map.AreaAt(anchor),
                    $"{area}'s anchor is not in {area} - the gate would photograph the wrong place.");
            }
        }

        /// <summary>
        /// The walk home is the game (DESIGN.md §3), and §7 makes its length the difficulty dial.
        /// What matters is the *range* of that dial rather than any absolute number - the author
        /// chose a shallower world on 2026-08-24 and may choose again - so assert the gradient, and
        /// keep only a floor on the absolute, below which there is no expedition at all.
        /// </summary>
        [Test]
        public void TheWalkHomeGetsMeaningfullyWorseWithDepth()
        {
            var tuning = ProjectSetup.EnsureMovementTuning();
            var nearest = _map.DistanceFromBase(_map.AnchorFor(Area.Verge)) / tuning.maxSpeed;
            var deepest = _map.DistanceFromBase(_map.AnchorFor(Area.EmberScar)) / tuning.maxSpeed;

            Assert.Greater(deepest, 15f,
                $"Ember Scar is only {deepest:0} s from Base at a flat run. That is not an expedition.");
            Assert.Greater(deepest / nearest, 3f,
                $"The deepest biome costs {deepest / nearest:0.0}x the nearest ({deepest:0} s against " +
                $"{nearest:0} s). Distance is supposed to be the difficulty dial; at that ratio it has " +
                "no range left for Phase 4 to tune against.");
        }

        [Test]
        public void BiomeBordersAscendAndCoverTheWholeSpine()
        {
            if (_shape != WorldShape.Corridor)
            {
                Assert.Pass("Cluster worlds have no spine to divide.");
            }

            var previous = 0f;
            foreach (var fraction in _layout.biomeEndFractions)
            {
                Assert.Greater(fraction, previous, "Biome borders are not in ascending order.");
                previous = fraction;
            }

            Assert.AreEqual(1f, previous, 1e-4f, "The last biome does not reach the end of the spine.");
        }

        int CountTiles(Area area)
        {
            var count = 0;
            for (var row = 0; row < _map.Rows; row++)
            {
                for (var column = 0; column < _map.Columns; column++)
                {
                    if (_map.TileArea(column, row) == (int)area)
                    {
                        count++;
                    }
                }
            }

            return count;
        }

        float MeanDistanceFromBase(Area area)
        {
            var total = 0f;
            var count = 0;
            for (var row = 0; row < _map.Rows; row++)
            {
                for (var column = 0; column < _map.Columns; column++)
                {
                    if (_map.TileArea(column, row) != (int)area)
                    {
                        continue;
                    }

                    total += _map.DistanceFromBase(_map.TileCentre(column, row));
                    count++;
                }
            }

            return count == 0 ? 0f : total / count;
        }
    }
}
