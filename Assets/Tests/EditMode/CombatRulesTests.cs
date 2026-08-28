using System.Collections.Generic;
using Graft.Core;
using NUnit.Framework;

namespace Graft.Tests
{
    /// <summary>
    /// The rules of a fight, tested with no engine anywhere near them - which is the whole reason
    /// <see cref="HealthPool"/>, <see cref="AttackClock"/> and <see cref="Targeting"/> live in
    /// <c>Graft.Core</c> rather than inside the MonoBehaviours that use them.
    ///
    /// Every assertion here is about something a screenshot cannot show. A picture of a fight
    /// cannot tell you that the first blow after closing was immediate, or that a target on the
    /// range boundary is not being picked up and dropped on alternate frames - it looks completely
    /// normal either way, which is exactly the kind of failure this project already got burned by.
    /// </summary>
    public class CombatRulesTests
    {
        // -- HealthPool ------------------------------------------------------------------------

        [Test]
        public void DamageFloorsAtZeroRatherThanGoingNegative()
        {
            var pool = HealthPool.Full(20f).Damaged(35f);

            Assert.AreEqual(0f, pool.Current);
            Assert.IsFalse(pool.IsAlive);
            Assert.AreEqual(0f, pool.Fraction);
        }

        [Test]
        public void NegativeDamageIsIgnoredRatherThanHealing()
        {
            // One typo in a graft table would otherwise make something unkillable, and it would
            // present as "that enemy has a lot of health" rather than as a bug.
            var pool = HealthPool.Full(20f).Damaged(5f);
            Assert.AreEqual(15f, pool.Damaged(-100f).Current);
        }

        [Test]
        public void HealingCannotOverfill()
        {
            Assert.AreEqual(20f, HealthPool.Full(20f).Damaged(5f).Healed(999f).Current);
        }

        [Test]
        public void APoolIsImmutable()
        {
            // The reason damage returns a new pool: a caller always has the before and the after in
            // hand, so it cannot report a hit without knowing what the hit did.
            var before = HealthPool.Full(50f);
            var after = before.Damaged(10f);

            Assert.AreEqual(50f, before.Current, "Damaging mutated the pool it was called on.");
            Assert.AreEqual(40f, after.Current);
        }

        [Test]
        public void AMaximumOfZeroIsRefused()
        {
            // Fraction divides by the maximum. A zero maximum is a NaN health bar, which draws as
            // nothing at all and looks exactly like an enemy that has no bar.
            Assert.GreaterOrEqual(HealthPool.Full(0f).Maximum, 1f);
        }

        // -- AttackClock -----------------------------------------------------------------------

        [Test]
        public void TheFirstAttackAfterAcquiringATargetIsImmediate()
        {
            var clock = new AttackClock();

            Assert.IsTrue(clock.Tick(0.016f, true, 1f),
                "Walking into range and then waiting a full interval reads as the game not noticing.");
        }

        [Test]
        public void NoTargetMeansNoAttackAndNoBankedShots()
        {
            var clock = new AttackClock();

            for (var i = 0; i < 600; i++)
            {
                Assert.IsFalse(clock.Tick(0.016f, false, 1f));
            }

            // Ten seconds of chasing buys exactly one hit on arrival, not ten.
            Assert.IsTrue(clock.Tick(0.016f, true, 1f));
            Assert.IsFalse(clock.Tick(0.016f, true, 1f));
        }

        [Test]
        public void AtMostOneAttackPerTickHoweverLongTheFrameWas()
        {
            var clock = new AttackClock();
            clock.Tick(0.016f, true, 0.5f);

            // A five-second hitch covers ten intervals. If it produced ten attacks, an editor
            // stall would be free damage - and on the device, so would a moment of thermal
            // throttling.
            Assert.IsTrue(clock.Tick(5f, true, 0.5f));
            Assert.IsFalse(clock.Tick(0.0001f, true, 0.5f));
        }

        [Test]
        public void TheIntervalIsActuallyWaited()
        {
            var clock = new AttackClock();
            clock.Tick(0.016f, true, 0.5f);

            var attacks = 0;
            for (var i = 0; i < 1000; i++)
            {
                if (clock.Tick(0.01f, true, 0.5f))
                {
                    attacks++;
                }
            }

            // Ten seconds of ticks at a half-second interval is twenty attacks. Checked as a rate
            // over a long run with a tolerance of one, rather than as an exact count over a short
            // one: subtracting a delta a thousand times accumulates enough float error to push an
            // attack over a frame boundary, and a test that fails on that is measuring the
            // arithmetic of the test harness rather than the pacing of the clock.
            Assert.That(attacks, Is.EqualTo(20).Within(1));
        }

        // -- Targeting -------------------------------------------------------------------------

        [Test]
        public void NearestInRangeWins()
        {
            var candidates = new List<Vec2> { new Vec2(5f, 0f), new Vec2(2f, 0f), new Vec2(9f, 0f) };

            Assert.AreEqual(1, Targeting.NearestWithin(Vec2.Zero, candidates, 6f));
        }

        [Test]
        public void NothingInRangeReportsNoTarget()
        {
            var candidates = new List<Vec2> { new Vec2(40f, 0f) };

            Assert.AreEqual(-1, Targeting.NearestWithin(Vec2.Zero, candidates, 6f));
            Assert.AreEqual(-1, Targeting.NearestWithin(Vec2.Zero, new List<Vec2>(), 6f));
            Assert.AreEqual(-1, Targeting.NearestWithin(Vec2.Zero, null, 6f));
        }

        [Test]
        public void TiesGoToTheLowerIndex()
        {
            // Any tie-break would do. Having one at all is what stops the auto-attack flickering
            // between two equidistant enemies and landing nothing on either.
            var candidates = new List<Vec2> { new Vec2(3f, 0f), new Vec2(-3f, 0f) };

            Assert.AreEqual(0, Targeting.NearestWithin(Vec2.Zero, candidates, 6f));
        }

        [Test]
        public void AnAcquiredTargetIsHeldPastTheRangeItWasAcquiredAt()
        {
            const float range = 6f;
            var justOutside = new Vec2(range * 1.05f, 0f);

            Assert.AreEqual(-1, Targeting.NearestWithin(Vec2.Zero, new List<Vec2> { justOutside }, range),
                "This target should be out of acquisition range.");
            Assert.IsTrue(Targeting.StaysAcquired(Vec2.Zero, justOutside, range),
                "Without the margin, a target on the boundary is acquired and dropped on alternate " +
                "frames and no shot ever completes.");
        }

        [Test]
        public void HysteresisIsNotUnlimited()
        {
            Assert.IsFalse(Targeting.StaysAcquired(Vec2.Zero, new Vec2(20f, 0f), 6f));
        }

        // -- EnemyStats ------------------------------------------------------------------------

        [Test]
        public void DepthScalesHealthAndDamageButNeverSpeed()
        {
            // The single most load-bearing rule in the file. DESIGN.md §3 makes the walk home the
            // game; a walk home you cannot outrun is not a decision, it is a death sentence with
            // extra steps.
            var stats = new EnemyStats(40f, 3f, 10f, 1.2f, 2f);
            var deep = stats.Scaled(2.4f);

            Assert.AreEqual(96f, deep.MaxHealth, 0.001f);
            Assert.AreEqual(24f, deep.Damage, 0.001f);
            Assert.AreEqual(stats.MoveSpeed, deep.MoveSpeed, "Depth scaling moved the speed.");
            Assert.AreEqual(stats.AttackInterval, deep.AttackInterval);
        }

        [Test]
        public void RangedIsDecidedByReachRatherThanByFamily()
        {
            Assert.IsFalse(new EnemyStats(10f, 3f, 5f, 1f, EnemyStats.RangedThreshold - 0.1f).IsRanged);
            Assert.IsTrue(new EnemyStats(10f, 3f, 5f, 1f, EnemyStats.RangedThreshold).IsRanged);
        }

        [Test]
        public void FourFamiliesAndTheContractKnowsIt()
        {
            Assert.AreEqual(4, EnemyFamilies.Count);
            Assert.AreEqual(DesignContract.EnemyFamilyCount, EnemyFamilies.Count);
            Assert.AreEqual(EnemyFamilies.Count, System.Enum.GetValues(typeof(EnemyFamily)).Length);

            // The enum value is the index into every weight array in the spawn tables. If these
            // ever disagree, an area's family mix silently becomes a different mix.
            for (var i = 0; i < EnemyFamilies.All.Length; i++)
            {
                Assert.AreEqual(i, EnemyFamilies.IndexOf(EnemyFamilies.All[i]));
            }
        }
    }
}
