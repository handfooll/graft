using Graft.Core;
using NUnit.Framework;

namespace Graft.Tests
{
    /// <summary>
    /// What gets sent at the player, where it appears, and when. All of it decided without an
    /// engine, so the answers can be checked rather than watched.
    ///
    /// The two that matter most are the two a screenshot cannot show: that Base never spawns
    /// anything, and that nothing ever appears inside the frame. Both look completely normal in
    /// any single picture and are ruinous in play.
    /// </summary>
    public class SpawnDirectorTests
    {
        static SpawnSpec Spec(int cap = 4, float pace = 1f)
        {
            return new SpawnSpec
            {
                RingInnerRadius = 13f,
                RingOuterRadius = 22f,
                DespawnRadius = 46f,
                Areas = new[]
                {
                    AreaSpawn.Empty(Area.Base),
                    new AreaSpawn(Area.Verge, cap, pace, 1f, new[] { 0.7f, 0.3f, 0f, 0f }),
                    new AreaSpawn(Area.EmberScar, cap, pace, 2.4f, new[] { 0.25f, 0.25f, 0.25f, 0.25f }),
                },
            };
        }

        static SpawnDirector Director(int cap = 4, float pace = 1f, int seed = 1) =>
            new SpawnDirector(Spec(cap, pace), seed);

        [Test]
        public void BaseNeverSpawnsAnythingWhateverTheTableSays()
        {
            // Enforced in the director rather than by leaving Base's row empty, because §7 makes
            // the safe hub a rule. A table is a tuning value and tuning values get edited.
            var spec = Spec();
            spec.Areas[0] = new AreaSpawn(Area.Base, 99, 0.1f, 1f, new[] { 1f, 1f, 1f, 1f });
            var director = new SpawnDirector(spec, 1);

            for (var i = 0; i < 1000; i++)
            {
                Assert.IsFalse(director.TryProduce(Area.Base, 0, 0.1f, Vec2.Zero, out _),
                    "Something spawned in Base.");
            }
        }

        [Test]
        public void StandingInBaseAlsoResetsThePacing()
        {
            // Otherwise a player who shelters in Base for a minute walks back out into a backlog
            // dumped on the plaza edge all at once.
            var director = Director(pace: 2f);

            Assert.IsTrue(director.TryProduce(Area.Verge, 0, 0.1f, Vec2.Zero, out _));
            Assert.Greater(director.Cooldown, 0f);

            director.TryProduce(Area.Base, 0, 0.1f, Vec2.Zero, out _);
            Assert.AreEqual(0f, director.Cooldown);
        }

        [Test]
        public void ThePopulationCapIsRespected()
        {
            var director = Director(cap: 3);
            var live = 0;

            for (var i = 0; i < 500 && live < 50; i++)
            {
                if (director.TryProduce(Area.Verge, live, 0.5f, Vec2.Zero, out _))
                {
                    live++;
                }
            }

            Assert.AreEqual(3, live, "The director spawned past the area's population cap.");
        }

        [Test]
        public void SpawnsArePacedRatherThanArrivingAllAtOnce()
        {
            var director = Director(cap: 99, pace: 1f);
            var spawned = 0;

            // Ten seconds of tenth-of-a-second ticks at one per second.
            for (var i = 0; i < 100; i++)
            {
                if (director.TryProduce(Area.Verge, 0, 0.1f, Vec2.Zero, out _))
                {
                    spawned++;
                }
            }

            Assert.AreEqual(10, spawned);
        }

        [Test]
        public void NothingEverAppearsInsideTheFrame()
        {
            // The camera shows about 9.5 m across. Anything appearing inside that reads as a bug
            // rather than as something that walked up to you, and no amount of later polish undoes
            // a first impression of enemies materialising in your face.
            var director = Director(cap: 99, pace: 0.05f);
            var player = new Vec2(17f, -32f);
            var produced = 0;

            for (var i = 0; i < 4000; i++)
            {
                if (!director.TryProduce(Area.EmberScar, 0, 0.1f, player, out var order))
                {
                    continue;
                }

                produced++;
                var distance = (order.Position - player).Magnitude;
                Assert.GreaterOrEqual(distance, 13f - 0.001f, "Spawned inside the ring's inner radius.");
                Assert.LessOrEqual(distance, 22f + 0.001f, "Spawned outside the ring's outer radius.");
            }

            Assert.Greater(produced, 100, "The test never actually produced anything to check.");
        }

        [Test]
        public void SpawnPointsAreSpreadAroundThePlayerRatherThanAllOnOneSide()
        {
            // A ring sampled badly is still a ring. This catches the bearing being constant, which
            // would put every fight in the game in the same direction.
            var director = Director(cap: 99, pace: 0.05f);
            var quadrants = new bool[4];

            for (var i = 0; i < 2000; i++)
            {
                if (!director.TryProduce(Area.EmberScar, 0, 0.1f, Vec2.Zero, out var order))
                {
                    continue;
                }

                var q = (order.Position.X >= 0f ? 1 : 0) + (order.Position.Y >= 0f ? 2 : 0);
                quadrants[q] = true;
            }

            CollectionAssert.DoesNotContain(quadrants, false, "Spawns never reached one of the quadrants.");
        }

        [Test]
        public void OnlyTheFamiliesAnAreaWeightsTurnUpInIt()
        {
            var director = Director(cap: 99, pace: 0.05f);
            var seen = 0;

            for (var i = 0; i < 2000; i++)
            {
                if (director.TryProduce(Area.Verge, 0, 0.1f, Vec2.Zero, out var order))
                {
                    seen |= 1 << (int)order.Family;
                }
            }

            // The Verge weights Venom and Impact only. Hide or Spark appearing there would mean
            // the weight roll had drifted off its own table.
            Assert.AreNotEqual(0, seen & (1 << (int)EnemyFamily.Venom), "No Venom in the Verge.");
            Assert.AreNotEqual(0, seen & (1 << (int)EnemyFamily.Impact), "No Impact in the Verge.");
            Assert.AreEqual(0, seen & (1 << (int)EnemyFamily.Hide), "Hide turned up in the Verge.");
            Assert.AreEqual(0, seen & (1 << (int)EnemyFamily.Spark), "Spark turned up in the Verge.");
        }

        [Test]
        public void WeightsDecideProportionsAndNotJustPresence()
        {
            var director = Director(cap: 99, pace: 0.05f);
            var venom = 0;
            var impact = 0;

            for (var i = 0; i < 4000; i++)
            {
                if (!director.TryProduce(Area.Verge, 0, 0.1f, Vec2.Zero, out var order))
                {
                    continue;
                }

                if (order.Family == EnemyFamily.Venom)
                {
                    venom++;
                }
                else if (order.Family == EnemyFamily.Impact)
                {
                    impact++;
                }
            }

            // Weighted 0.70 / 0.30. Loose bounds - this is checking the roll reads the table at
            // all, not that a pseudo-random sequence is perfectly distributed.
            var share = venom / (float)(venom + impact);
            Assert.That(share, Is.EqualTo(0.70f).Within(0.06f));
        }

        [Test]
        public void AnAreaWithACapButNoWeightsProducesNothingRatherThanSpinning()
        {
            var spec = Spec();
            spec.Areas[1] = new AreaSpawn(Area.Verge, 8, 0.5f, 1f, new float[4]);
            var director = new SpawnDirector(spec, 1);

            for (var i = 0; i < 200; i++)
            {
                Assert.IsFalse(director.TryProduce(Area.Verge, 0, 0.1f, Vec2.Zero, out _));
            }
        }

        [Test]
        public void TheSameSeedRebuildsTheSameFight()
        {
            // This is what makes a gate screenshot re-shootable. A fight posed by a director that
            // rolls differently every run is a different picture every run.
            var a = Director(cap: 99, pace: 0.2f, seed: 4242);
            var b = Director(cap: 99, pace: 0.2f, seed: 4242);

            for (var i = 0; i < 300; i++)
            {
                var producedA = a.TryProduce(Area.EmberScar, 0, 0.1f, Vec2.Zero, out var orderA);
                var producedB = b.TryProduce(Area.EmberScar, 0, 0.1f, Vec2.Zero, out var orderB);

                Assert.AreEqual(producedA, producedB);
                if (!producedA)
                {
                    continue;
                }

                Assert.AreEqual(orderA.Family, orderB.Family);
                Assert.AreEqual(orderA.Position.X, orderB.Position.X, 0.0001f);
                Assert.AreEqual(orderA.Position.Y, orderB.Position.Y, 0.0001f);
            }
        }

        [Test]
        public void RecyclingHappensWellOutsideTheSpawnRing()
        {
            // If the despawn radius fell inside the ring, enemies would be deleted as fast as they
            // were made and the world would be permanently empty for no visible reason.
            var director = Director();

            Assert.IsFalse(director.ShouldRecycle(new Vec2(22f, 0f), Vec2.Zero),
                "Something spawned at the outer ring is already being recycled.");
            Assert.IsTrue(director.ShouldRecycle(new Vec2(60f, 0f), Vec2.Zero));
            Assert.Greater(director.Spec.DespawnRadius, director.Spec.RingOuterRadius);
        }

        [Test]
        public void AnAreaMissingFromTheTableIsEmptyRatherThanAnException()
        {
            var director = Director();

            Assert.IsFalse(director.TryProduce(Area.BoneReach, 0, 1f, Vec2.Zero, out _));
        }
    }
}
