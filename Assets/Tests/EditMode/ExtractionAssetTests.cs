using System.Text;
using Graft.Core;
using Graft.Editor;
using Graft.Game;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Graft.Tests
{
    /// <summary>
    /// Guards the shipped extraction numbers rather than synthetic ones.
    /// <see cref="ExtractionRulesTests"/> proves the rules obey whatever they are handed; this
    /// proves the numbers the game actually loads are worth obeying.
    ///
    /// **This file exists because the phase's headline number is invisible.** "About 150 seconds of
    /// sustained fighting to fill a bar, in any biome" is not something a screenshot can show, and
    /// it is not something anyone can read off an inspector: it emerges from four assets tuned in
    /// three different phases for three unrelated reasons - <c>CombatTuning</c>'s damage and
    /// interval, <c>EnemyRoster</c>'s health, <c>SpawnTables</c>' pacing and depth multipliers, and
    /// <c>EssenceTuning</c>'s exchange rate. Change any one and the expedition silently becomes a
    /// different length.
    ///
    /// Phase 3 has already been bitten by exactly this shape of thing once. A player who died in
    /// 0.47 s looked completely correct in every picture of every fight.
    /// </summary>
    public class ExtractionAssetTests
    {
        /// <summary>
        /// The target from the Phase 4 spec (D-P4.4), and the band it is judged in.
        ///
        /// Deliberately generous at ±25 s. The model below is idealised - it assumes the player is
        /// always shooting and never walking between targets - so the point is to catch a biome
        /// that has drifted into a *grind*, not to pin a number that real play will not reproduce
        /// anyway.
        /// </summary>
        const float TargetFillSeconds = 150f;
        const float FillTolerance = 25f;

        [Test]
        public void EveryBiomeFillsACargoBarInAboutTheSpecifiedTime()
        {
            var model = Model();
            var report = new StringBuilder();
            var worst = 0f;

            foreach (var area in Areas.Biomes)
            {
                var run = model.For(area);
                Assert.IsTrue(run.SpawnsAnything, $"{area} spawns nothing - it can never be farmed.");

                report.AppendLine($"  {run}");
                worst = Mathf.Max(worst, Mathf.Abs(run.SecondsToFill - TargetFillSeconds));
            }

            Assert.LessOrEqual(worst, FillTolerance,
                $"A biome's expedition has drifted away from the ~{TargetFillSeconds:0} s the phase " +
                $"was specified at:\n{report}\n" +
                "The extraction decision is the product and the player should meet it many times a " +
                "session. If a biome here is marked spawn-limited, the fix is its pacing in " +
                "SpawnTables - an area that cannot send enemies as fast as the player kills them " +
                "fills slowly whatever a mote is worth.");
        }

        [Test]
        public void NoBiomeIsSpawnLimited()
        {
            // A spawn-limited biome is one where the player stands waiting. It is invisible in
            // play - the fight simply feels quiet - and it uncouples the fill rate from the
            // player's damage output, which is the identity the whole essence design rests on.
            //
            // The Verge was exactly this at Phase 3's 1.9 s pacing, and nothing noticed until
            // cargo existed to measure it against.
            var model = Model();

            foreach (var area in Areas.Biomes)
            {
                var run = model.For(area);
                Assert.IsFalse(run.SpawnLimited,
                    $"{area} sends enemies slower than the player kills them " +
                    $"({1f / Tables().EntryFor(area).secondsBetweenSpawns:0.00}/s against a " +
                    $"capability of {Tuning().DamagePerSecond / run.AverageEffectiveHealth:0.00}/s), " +
                    "so its expedition is longer than the essence numbers say it is.");
            }
        }

        [Test]
        public void KillsToFillAreReportedForEveryBiome()
        {
            // Kills-to-fill is *derived*, not designed - it is capacity divided by what an average
            // kill in that area pays, and it falls as depth rises because deep enemies are worth
            // more each. The spec's "about 40 kills" is true at depth and nowhere near true in the
            // Verge, and that is correct rather than a fault: what is held constant is the time.
            //
            // The band here is wide on purpose. It exists to catch an order-of-magnitude slip - a
            // biome where a bar is twelve kills or twelve hundred - not to pin a design number.
            var model = Model();

            foreach (var area in Areas.Biomes)
            {
                var run = model.For(area);
                Assert.That(run.KillsToFill, Is.InRange(15f, 200f),
                    $"{area} needs {run.KillsToFill:0} kills to fill a bar. Something has slipped " +
                    "by an order of magnitude in either the essence rate or the enemy health.");
            }
        }

        [Test]
        public void TheDeepestBiomeIsWorthGoingTo()
        {
            // Fill time is held constant across biomes, so the only thing that makes the walk out
            // worth taking is what a bar is worth when it gets home. If that ratio ever collapsed,
            // the optimal way to play would be to farm the Verge forever - which is the design
            // failing silently rather than loudly.
            var values = new EssenceValues(Layout().BuildMap());
            var model = Model();

            var deep = model.For(Area.EmberScar);
            var shallow = model.For(Area.Verge);

            var deepRate = values.PerUnit(Area.EmberScar) * deep.CargoPerSecond;
            var shallowRate = values.PerUnit(Area.Verge) * shallow.CargoPerSecond;

            Assert.Greater(deepRate, shallowRate * 2f,
                $"Ember Scar banks {deepRate / shallowRate:0.0}x the Verge per second of fighting. " +
                "For the most dangerous biome in the game that is not enough to be worth the walk.");
        }

        // -- The tuning asset itself -------------------------------------------------------------

        [Test]
        public void TheCapacityIsAWholeNumberOfMotes()
        {
            // Otherwise the bar stops one mote short of full forever, Cargo.IsFull never becomes
            // true, and DESIGN.md §4's full-cargo prompt to head home is a state the game cannot
            // reach. Nothing would report it; the bar would simply always look nearly full.
            var essence = Essence();

            Assert.IsTrue(essence.CapacityIsWholeMotes,
                $"Capacity {essence.cargoCapacity} is not a whole number of " +
                $"{essence.cargoPerMote}-unit motes.");
        }

        [Test]
        public void TheCapacityIsTheOneDerivedFromTheCombatNumbers()
        {
            // The capacity is not a chosen value: it is 150 s of the player's own damage output at
            // the shipped exchange rate. This is the assertion that notices when someone retunes
            // the fight and leaves the cargo bar behind.
            var essence = Essence();
            var expected = essence.Rules.CapacityForFillTime(TargetFillSeconds, Tuning().DamagePerSecond);

            Assert.AreEqual(expected, essence.cargoCapacity, essence.cargoPerMote,
                $"Cargo capacity is {essence.cargoCapacity} but the shipped combat numbers make " +
                $"{expected} the capacity that fills in {TargetFillSeconds:0} s. Either the fight " +
                "was retuned without the cargo bar, or the target moved.");
        }

        [Test]
        public void TheAttractRadiusKeepsAbsorptionOnScreen()
        {
            // Judged as a fraction of the frame, never in metres - the same rule the auto-attack's
            // reach is judged by. A magnet reaching past the visible width would make essence
            // arrive from off screen, which turns the reward for a kill into something the player
            // is told about rather than something they watch.
            var essence = Essence();
            var framing = AssetDatabase.LoadAssetAtPath<CameraFraming>(ProjectSetup.CameraFramingPath);
            Assert.IsNotNull(framing, "Missing CameraFraming - run ProjectSetup.ConfigureAll.");

            var fraction = essence.attractRadius / framing.VisibleWidthOnDevice;
            Assert.That(fraction, Is.InRange(0.15f, 0.55f),
                $"The magnet reaches {fraction:P0} of the visible width. Below about 15% the player " +
                "has to walk onto each mote; past about 55% essence flies in from outside the frame.");
        }

        [Test]
        public void MotesFlyInFasterThanThePlayerCanWalkAway()
        {
            // Otherwise essence you have already claimed can be outrun, which reads as the game
            // taking a reward back.
            var movement = AssetDatabase.LoadAssetAtPath<MovementTuning>(ProjectSetup.MovementTuningPath);
            Assert.IsNotNull(movement, "Missing MovementTuning - run ProjectSetup.ConfigureAll.");

            Assert.Greater(Essence().flightSpeed, movement.maxSpeed,
                "A mote in flight is slower than the player. Essence could be walked away from.");
        }

        [Test]
        public void TheHomeCueIsHiddenInsideBaseAndShownFromTheNearestBiome()
        {
            // The cue is not navigation, it is the extraction decision made visible - so it must be
            // absent where there is nothing to decide (you can see home) and present from the
            // moment there is (you cannot).
            var essence = Essence();
            var map = Layout().BuildMap();
            var verge = map.DistanceFromBase(map.AnchorFor(Area.Verge));

            Assert.Greater(essence.homeCueFadeDistance, map.BaseRadius,
                "The home cue is drawn while the player is standing inside Base, pointing at a " +
                "monument already filling the frame.");
            Assert.Less(essence.homeCueFadeDistance, verge,
                $"The cue does not appear until past {essence.homeCueFadeDistance:0} m, which is " +
                $"beyond the Verge's anchor at {verge:0} m - the nearest biome would have no cue.");
        }

        // -- The two rings at Base -----------------------------------------------------------------

        [Test]
        public void TheBankPlazaIsWellInsideTheSafeDisc()
        {
            // D-P4.2. If the two rings were the same size there would be no retreat valve: every
            // flight into Base would bank, and a route between two biomes that crosses Base would
            // end an expedition the player was in the middle of.
            var map = Layout().BuildMap();

            Assert.Less(map.BankRadius, map.BaseRadius * 0.6f,
                $"The bank plaza is {map.BankRadius:0.#} m inside a {map.BaseRadius:0.#} m safe " +
                "disc. There is not enough safe ground outside it to stop on.");
            Assert.Greater(map.BankRadius, 3f,
                "The bank plaza is smaller than the character is tall. Coming home would be a " +
                "matter of aim.");
        }

        [Test]
        public void TheBaseAnchorIsInsideTheBankPlaza()
        {
            // Death respawns at the anchor and an expedition starts there. It has to be somewhere
            // that banks, or a player who walks home and stops at the monument has not banked.
            var map = Layout().BuildMap();

            Assert.IsTrue(map.IsInBankPlaza(map.AnchorFor(Area.Base)),
                "Base's anchor is outside its own bank plaza.");
        }

        [Test]
        public void StandingInBaseIsNotTheSameAsStandingOnThePlaza()
        {
            // The assertion that proves the retreat valve exists at all: there is ground that is
            // safe and does not bank.
            var map = Layout().BuildMap();
            var between = map.BaseCentre + new Vec2((map.BankRadius + map.BaseRadius) * 0.5f, 0f);

            Assert.AreEqual((int)Area.Base, map.AreaAt(between), "The ring between the two is not Base.");
            Assert.IsFalse(map.IsInBankPlaza(between), "The whole of Base banks. There is no valve.");
        }

        [Test]
        public void BothLayoutsHaveTwoRings()
        {
            // The corridor is kept switchable (STATUS.md), and a fallback that has quietly rotted
            // is not a fallback.
            foreach (var path in new[] { ProjectSetup.ClusterLayoutPath, ProjectSetup.CorridorLayoutPath })
            {
                var layout = AssetDatabase.LoadAssetAtPath<WorldLayout>(path);
                Assert.IsNotNull(layout, $"Missing {path} - run ProjectSetup.ConfigureAll.");
                Assert.Less(layout.bankRadius, layout.baseRadius,
                    $"{path} has a bank plaza at least as big as its safe disc.");
            }
        }

        // -- Loading ------------------------------------------------------------------------------

        static ExpeditionModel Model()
        {
            var roster = Roster();
            return new ExpeditionModel(Tables().ToSpec(), family => roster.ProfileFor(family).Stats,
                Tuning().DamagePerSecond, Essence().Rules, Essence().cargoCapacity);
        }

        static EssenceTuning Essence() => Load<EssenceTuning>(ProjectSetup.EssenceTuningPath);
        static CombatTuning Tuning() => Load<CombatTuning>(ProjectSetup.CombatTuningPath);
        static EnemyRoster Roster() => Load<EnemyRoster>(ProjectSetup.EnemyRosterPath);
        static SpawnTables Tables() => Load<SpawnTables>(ProjectSetup.SpawnTablesPath);
        static WorldLayout Layout() => Load<WorldLayout>(ProjectSetup.ClusterLayoutPath);

        static T Load<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            Assert.IsNotNull(asset, $"Missing {path} - run ProjectSetup.ConfigureAll.");
            return asset;
        }
    }
}
