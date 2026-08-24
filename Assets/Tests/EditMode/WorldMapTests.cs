using Graft.Core;
using Graft.Editor;
using Graft.Game;
using NUnit.Framework;

namespace Graft.Tests
{
    /// <summary>
    /// The world's rules, checked against the layout the game actually ships with - not a
    /// synthetic one. A generated world has failure modes that no screenshot can catch, because
    /// each of them looks completely normal from wherever you happen to be standing: a biome in
    /// the wrong order, a lobe that drifted clear of the corridor and became an island, a hole in
    /// the boundary at the one corner nobody framed.
    ///
    /// What a screenshot *does* catch - whether the six places look like six places - is the gate,
    /// and it stays a human's job (BUILD_PLAN.md).
    /// </summary>
    public class WorldMapTests
    {
        WorldLayout _layout;
        WorldMap _map;

        [SetUp]
        public void Build()
        {
            _layout = ProjectSetup.EnsureWorldLayout();
            _map = _layout.BuildMap();
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
            Assert.AreEqual((int)Area.Base, _map.AreaAt(_map.Path.Start),
                "The start of the spine is not Base - the player would spawn in a biome.");
            Assert.Less(MeanDistanceFromBase(Area.Base), MeanDistanceFromBase(Area.Verge));
        }

        /// <summary>
        /// D3: one continuous world. A lobe placed slightly too far off the corridor becomes an
        /// island - perfectly good-looking ground that no player can ever reach, and nothing
        /// anywhere reports it.
        /// </summary>
        [Test]
        public void EveryTileIsWalkableFromBase()
        {
            Assert.AreEqual(_map.CountWalkable(), _map.CountReachableFromBase(),
                "Part of the world is cut off from Base - check the lobes overlap the corridor.");
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
        /// The walk home is the game (DESIGN.md §3). If the deepest biome is a short stroll from
        /// Base there is no expedition, and no amount of enemy tuning can put one back.
        /// </summary>
        [Test]
        public void TheWalkHomeFromTheDeepestBiomeIsWorthFearing()
        {
            var tuning = ProjectSetup.EnsureMovementTuning();
            var metres = _map.DistanceFromBase(_map.AnchorFor(Area.EmberScar));
            var seconds = metres / tuning.maxSpeed;

            Assert.Greater(seconds, 40f,
                $"Ember Scar is only {seconds:0} s from Base at a flat run. That is not an expedition.");
            Assert.Less(seconds, 150f,
                $"Ember Scar is {seconds:0} s from Base with nothing in the way. A dull walk is not difficulty.");
        }

        [Test]
        public void BiomeBordersAscendAndCoverTheWholeSpine()
        {
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
