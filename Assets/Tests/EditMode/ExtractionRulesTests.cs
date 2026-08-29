using Graft.Core;
using NUnit.Framework;
using UnityEngine;

namespace Graft.Tests
{
    /// <summary>
    /// The rules of the extraction loop, with no engine anywhere near them - which is why
    /// <see cref="Cargo"/>, <see cref="ExtractionLedger"/> and <see cref="EssenceRules"/> live in
    /// <c>Graft.Core</c> rather than inside the MonoBehaviours that use them.
    ///
    /// Every assertion here is about something no screenshot can show. A picture of a full cargo
    /// bar cannot tell you that dying leaves the banked total untouched, that banking twice does
    /// not double-count, or that a mote refused for want of room was refused rather than silently
    /// clamped away. All three look completely normal, which is exactly the kind of failure this
    /// project has already been burned by once.
    ///
    /// The numbers the *shipped* assets produce are checked separately, in
    /// <see cref="ExtractionAssetTests"/> - the same split as <c>CombatRulesTests</c> against
    /// <c>CombatAssetTests</c>.
    /// </summary>
    public class ExtractionRulesTests
    {
        // -- Cargo -------------------------------------------------------------------------------

        [Test]
        public void CargoRefusesAMoteThatWouldOverflowRatherThanClampingIt()
        {
            // Clamping silently would make a refused mote indistinguishable from an absorbed one,
            // and the refusal is a mechanic: it is what leaves essence visibly lying on the ground
            // when the hold is full (DESIGN.md §4).
            var cargo = new Cargo(100f, 96f);

            Assert.IsFalse(cargo.TryAdd(8f, out var after), "An overflowing mote was accepted.");
            Assert.AreEqual(96f, after.Carried, "A refused mote still changed the cargo.");
        }

        [Test]
        public void CargoIsFullAtExactlyCapacityAndNotBefore()
        {
            Assert.IsFalse(new Cargo(100f, 99.9f).IsFull);
            Assert.IsTrue(new Cargo(100f, 100f).IsFull);
        }

        [Test]
        public void AMoteThatExactlyFillsTheHoldIsAccepted()
        {
            // The boundary that makes the full-cargo state reachable at all. If the last mote were
            // refused for being one epsilon too big, the bar would sit forever a mote short and
            // §4's prompt to head home would never fire.
            var cargo = new Cargo(100f, 98f);

            Assert.IsTrue(cargo.TryAdd(2f, out var after));
            Assert.IsTrue(after.IsFull);
        }

        [Test]
        public void GrowingTheHoldKeepsWhatIsInIt()
        {
            // §4 makes capacity a Base upgrade. An upgrade that spilled the cargo you were holding
            // would be a punishment.
            var grown = new Cargo(100f, 60f).WithCapacity(200f);

            Assert.AreEqual(60f, grown.Carried);
            Assert.AreEqual(200f, grown.Capacity);
        }

        // -- Banking -----------------------------------------------------------------------------

        [Test]
        public void BankingMovesCarriedToBankedEmptiesTheHoldAndRaisesTheEventOnce()
        {
            var ledger = Ledger(out var values);
            var mote = new EssenceMote(EnemyFamily.Venom, 10f, Area.EmberScar);
            ledger.Absorb(mote);

            var raised = 0;
            var last = default(BankReceipt);
            ledger.Banked += receipt =>
            {
                raised++;
                last = receipt;
            };

            ledger.Bank();

            Assert.AreEqual(1, raised, "Banking raised its event a number of times that is not one.");
            Assert.AreEqual(0f, ledger.Carried.Carried, "The hold was not emptied by banking.");
            Assert.AreEqual(0f, ledger.CarriedValue);
            Assert.AreEqual(values.ValueOf(mote), ledger.BankedTotal, 1e-3f);
            Assert.AreEqual(ledger.BankedTotal, last.Total, 1e-3f, "The receipt disagrees with the ledger.");
        }

        [Test]
        public void BankingAnEmptyHoldStillRaisesTheEvent()
        {
            // D5 makes banking what resets corruption, and a player who fled home with nothing has
            // still ended their expedition - very possibly at 90% corruption, which is exactly the
            // case where Phase 5's reset matters most. Firing only on a non-empty haul would skip it.
            var ledger = Ledger(out _);
            var raised = 0;
            ledger.Banked += _ => raised++;

            var receipt = ledger.Bank();

            Assert.AreEqual(1, raised);
            Assert.IsTrue(receipt.WasEmpty);
            Assert.AreEqual(0f, ledger.BankedTotal);
        }

        // -- Death -------------------------------------------------------------------------------

        [Test]
        public void DyingEmptiesTheHoldAndLeavesTheBankedTotalBitIdentical()
        {
            // **This is D4 and it must never be softened.** "Death = lose unbanked cargo only" -
            // the "only" limits which categories are at risk, not what fraction of them, and the
            // banked total is not merely preserved but untouched: not clamped, not recomputed, not
            // rounded. Compared bit-for-bit rather than with a tolerance for that reason.
            var ledger = Ledger(out _);
            ledger.Absorb(new EssenceMote(EnemyFamily.Hide, 40f, Area.BoneReach));
            ledger.Bank();
            var banked = ledger.BankedTotal;

            ledger.Absorb(new EssenceMote(EnemyFamily.Spark, 25f, Area.EmberScar));
            var lost = ledger.LoseCarried();

            Assert.AreEqual(banked, ledger.BankedTotal,
                "Dying changed the banked total. D4 puts only unbanked cargo at risk.");
            Assert.AreEqual(0f, ledger.Carried.Carried, "Dying left cargo in the hold.");
            Assert.Greater(lost, 0f, "Dying with a full hold reported no loss.");
        }

        [Test]
        public void DyingWithNothingCarriedCostsNothingAndReportsNothing()
        {
            var ledger = Ledger(out _);
            ledger.Absorb(new EssenceMote(EnemyFamily.Venom, 12f, Area.Verge));
            ledger.Bank();
            var banked = ledger.BankedTotal;

            Assert.AreEqual(0f, ledger.LoseCarried());
            Assert.AreEqual(banked, ledger.BankedTotal);
        }

        [Test]
        public void BankThenDieAndDieThenBankBothLeaveTheBankedTotalCorrect()
        {
            // The ordering test. Neither sequence may double-count the haul, and neither may lose
            // one that was already made permanent.
            var mote = new EssenceMote(EnemyFamily.Impact, 30f, Area.GlassMire);

            var bankFirst = Ledger(out var values);
            bankFirst.Absorb(mote);
            bankFirst.Bank();
            bankFirst.Absorb(mote);
            bankFirst.LoseCarried();

            var dieFirst = Ledger(out _);
            dieFirst.Absorb(mote);
            dieFirst.LoseCarried();
            dieFirst.Absorb(mote);
            dieFirst.Bank();

            var one = values.ValueOf(mote);
            Assert.AreEqual(one, bankFirst.BankedTotal, 1e-3f, "Bank-then-die banked the wrong amount.");
            Assert.AreEqual(one, dieFirst.BankedTotal, 1e-3f, "Die-then-bank banked the wrong amount.");
        }

        [Test]
        public void BankingTwiceInARowDoesNotBankTheSameHaulTwice()
        {
            var ledger = Ledger(out var values);
            var mote = new EssenceMote(EnemyFamily.Venom, 10f, Area.Verge);
            ledger.Absorb(mote);

            ledger.Bank();
            ledger.Bank();

            Assert.AreEqual(values.ValueOf(mote), ledger.BankedTotal, 1e-3f);
        }

        // -- What a haul is worth ----------------------------------------------------------------

        [Test]
        public void ADeepMoteBanksForMoreThanAShallowOneOfTheSameSize()
        {
            var ledger = Ledger(out var values);
            var shallow = new EssenceMote(EnemyFamily.Venom, 10f, Area.Verge);
            var deep = new EssenceMote(EnemyFamily.Venom, 10f, Area.EmberScar);

            Assert.Greater(values.ValueOf(deep), values.ValueOf(shallow));
            Assert.IsNotNull(ledger);
        }

        [Test]
        public void AHaulIsWorthWhereItWasKilledNotWhereItWasCarriedTo()
        {
            // A player who kills on the Ember Scar border and steps into Bone Reach to collect has
            // not made their haul cheaper. The mote carries its origin for exactly this.
            var ledger = Ledger(out var values);
            ledger.Absorb(new EssenceMote(EnemyFamily.Spark, 10f, Area.EmberScar));

            Assert.AreEqual(values.PerUnit(Area.EmberScar) * 10f, ledger.CarriedValue, 1e-3f);
        }

        [Test]
        public void EssenceValueIsMonotonicInDistanceFromBase()
        {
            // No biome may ever be worth less than a shallower one, or the difficulty gradient of
            // §7 and the payout gradient point in different directions and the deepest biome
            // becomes a harder fight for less money.
            var values = new EssenceValues(Map());
            var previous = 0f;

            foreach (var area in Areas.Biomes)
            {
                var here = values.PerUnit(area);
                Assert.GreaterOrEqual(here, previous,
                    $"{area} is worth less per unit than the biome before it.");
                previous = here;
            }
        }

        [Test]
        public void TheDepthRatioIsDerivedFromTheWorldRatherThanWrittenDown()
        {
            // Around 4.1 for the shipped cluster - but asserted as a *ratio of two distances the
            // map reports*, so re-tuning the world's depth re-tunes the payout instead of making
            // this test wrong. The same trick, and the same reason, as the Phase 2 depth test.
            var map = Map();
            var values = new EssenceValues(map);

            var expected = map.DistanceFromBase(map.AnchorFor(Area.EmberScar))
                           / map.DistanceFromBase(map.AnchorFor(Area.Verge));

            Assert.AreEqual(expected, values.DepthRatio, 1e-3f);
            Assert.Greater(values.DepthRatio, 2f,
                "The deepest biome pays less than twice the nearest. There is no reason to go out there.");
        }

        [Test]
        public void AFullBarFromTheDeepestBiomeBanksTheDepthRatioTimesAFullBarFromTheNearest()
        {
            var values = new EssenceValues(Map());
            const float Capacity = 400f;

            var deep = values.FullBarValue(Area.EmberScar, Capacity);
            var shallow = values.FullBarValue(Area.Verge, Capacity);

            Assert.AreEqual(values.DepthRatio, deep / shallow, 1e-3f);
        }

        // -- What a kill is worth ----------------------------------------------------------------

        [Test]
        public void AMoteIsWorthWhatItCostToKill()
        {
            // The choice the whole fill-rate design rests on. A four-times-tougher enemy pays four
            // times as much, which is what makes the bar fill at a rate set by the player's damage
            // output rather than by whatever depth happens to spawn.
            //
            // Sizes chosen to land on whole motes, so this measures the exchange rate rather than
            // the rounding - which gets an assertion of its own below.
            var rules = new EssenceRules(2f, 0.1f);

            Assert.AreEqual(4f, rules.CargoFor(40f), 1e-3f);
            Assert.AreEqual(16f, rules.CargoFor(160f), 1e-3f);
        }

        [Test]
        public void FillTimeDoesNotDependOnDepth()
        {
            // The identity in EssenceRules, stated as a test: effective health cancels out of
            // (kills per second x cargo per kill), so seconds-to-fill is a function of the player's
            // damage and the exchange rate alone. D-P4.4: depth changes payout, not fill rate.
            //
            // Asserted on the ideal rate, because that is where the identity is exact. What
            // quantising does to it is the next test's business.
            var rules = new EssenceRules(2f, 0.1f);
            const float Capacity = 400f;

            Assert.AreEqual(150f, rules.SecondsToFill(Capacity, 26.667f), 0.1f);

            foreach (var health in new[] { 22f, 52.8f, 118f, 228f })
            {
                var ideal = health * rules.CargoPerEnemyHealth;
                var killsPerSecond = 26.667f / health;
                Assert.AreEqual(150f, Capacity / (killsPerSecond * ideal), 0.1f,
                    $"An enemy of {health} health fills the bar at a different rate from the rest. " +
                    "The whole point of pricing a mote by what it cost is that this cannot happen.");
            }
        }

        [Test]
        public void QuantisingAKillIntoWholeMotesCostsAtMostHalfAMote()
        {
            // Motes are whole objects, so what a kill actually pays is the ideal rounded to the
            // nearest mote. That is a real error and it is largest on the weakest enemies, where
            // one mote is a big fraction of the whole drop - a 30-health enemy worth 3 units is
            // paid 4, which is a third too much.
            //
            // It averages out across a biome's mix rather than compounding (the shipped tables land
            // within 11% of the target, checked in ExtractionAssetTests), and the alternative -
            // fractional motes - buys accuracy nobody can see at the cost of the thing the player
            // can: a big kill showering essence.
            var rules = new EssenceRules(2f, 0.1f);

            for (var health = 5f; health <= 400f; health += 5f)
            {
                var ideal = health * rules.CargoPerEnemyHealth;
                if (ideal < rules.CargoPerMote * 0.5f)
                {
                    // Below half a mote everything is rounded up to the one-mote floor, which is
                    // deliberate: no kill in the game pays nothing.
                    continue;
                }

                Assert.LessOrEqual(Mathf.Abs(rules.CargoFor(health) - ideal), rules.CargoPerMote * 0.5f + 1e-3f,
                    $"An enemy of {health} health is paid {rules.CargoFor(health)} against an ideal " +
                    $"of {ideal}, which is more than half a mote out.");
            }
        }

        [Test]
        public void EveryKillDropsAtLeastOneMote()
        {
            // A kill that pays nothing teaches the player to walk past that family, and there is no
            // family in the game they should be walking past.
            var rules = new EssenceRules(2f, 0.1f);

            Assert.GreaterOrEqual(rules.MotesFor(1f), 1);
            Assert.GreaterOrEqual(rules.MotesFor(0f), 1);
        }

        [Test]
        public void AnAbsurdlyToughEnemyIsCappedRatherThanShoweringUnboundedMotes()
        {
            var rules = new EssenceRules(2f, 0.1f, maxMotesPerKill: 12);

            Assert.AreEqual(12, rules.MotesFor(1_000_000f));
        }

        [Test]
        public void CapacityDerivedForAFillTimeIsAWholeNumberOfMotes()
        {
            // Otherwise Cargo.IsFull is unreachable and the full-cargo state never happens.
            var rules = new EssenceRules(2f, 0.1f);
            var capacity = rules.CapacityForFillTime(150f, 26.667f);

            Assert.AreEqual(0f, capacity % 2f, 1e-4f);
            Assert.AreEqual(150f, rules.SecondsToFill(capacity, 26.667f), 2f);
        }

        // -- Helpers -----------------------------------------------------------------------------

        /// <summary>
        /// The shipped cluster. Built here rather than mocked, because every value assertion above
        /// is about distances the real world produces - a synthetic map would let the payout
        /// gradient invert without anything noticing.
        /// </summary>
        static WorldMap Map()
        {
            var layout = UnityEditor.AssetDatabase.LoadAssetAtPath<Graft.Game.WorldLayout>(
                Graft.Editor.ProjectSetup.ClusterLayoutPath);
            Assert.IsNotNull(layout,
                $"Missing {Graft.Editor.ProjectSetup.ClusterLayoutPath} - run ProjectSetup.ConfigureAll.");
            return layout.BuildMap();
        }

        static ExtractionLedger Ledger(out EssenceValues values)
        {
            values = new EssenceValues(Map());
            return new ExtractionLedger(values, 400f);
        }
    }
}
