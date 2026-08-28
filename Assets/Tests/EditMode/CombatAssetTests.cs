using Graft.Core;
using Graft.Editor;
using Graft.Game;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Graft.Tests
{
    /// <summary>
    /// Guards the shipped combat assets, not synthetic ones. <see cref="CombatRulesTests"/> proves
    /// the rules obey whatever numbers they are handed; this proves the numbers the game actually
    /// loads are ones worth obeying - the same split as <c>LocomotionTests</c> and
    /// <c>MovementTuningTests</c>.
    /// </summary>
    public class CombatAssetTests
    {
        static EnemyRoster Roster()
        {
            var roster = AssetDatabase.LoadAssetAtPath<EnemyRoster>(ProjectSetup.EnemyRosterPath);
            Assert.IsNotNull(roster, $"Missing {ProjectSetup.EnemyRosterPath} - run ProjectSetup.ConfigureAll.");
            return roster;
        }

        static SpawnTables Tables()
        {
            var tables = AssetDatabase.LoadAssetAtPath<SpawnTables>(ProjectSetup.SpawnTablesPath);
            Assert.IsNotNull(tables, $"Missing {ProjectSetup.SpawnTablesPath} - run ProjectSetup.ConfigureAll.");
            return tables;
        }

        static CombatTuning Tuning()
        {
            var tuning = AssetDatabase.LoadAssetAtPath<CombatTuning>(ProjectSetup.CombatTuningPath);
            Assert.IsNotNull(tuning, $"Missing {ProjectSetup.CombatTuningPath} - run ProjectSetup.ConfigureAll.");
            return tuning;
        }

        // -- The roster ------------------------------------------------------------------------

        [Test]
        public void TheRosterHasExactlyTheFourFamiliesDesignNames()
        {
            var roster = Roster();

            Assert.AreEqual(EnemyFamilies.Count, roster.Families.Count);
            foreach (var family in EnemyFamilies.All)
            {
                Assert.DoesNotThrow(() => roster.ProfileFor(family), $"No profile for {family}.");
            }
        }

        [Test]
        public void NoFamilyCanOutrunThePlayer()
        {
            // Checked against the movement asset rather than a copied number, so raising the
            // player's speed cannot silently invalidate this and lowering it fails here first.
            //
            // DESIGN.md §3 makes the walk home the game. The moment anything is faster than the
            // player, disengaging stops being a choice and the extraction loop has no exit.
            var roster = Roster();
            var movement = AssetDatabase.LoadAssetAtPath<MovementTuning>(ProjectSetup.MovementTuningPath);
            Assert.IsNotNull(movement);

            foreach (var profile in roster.Families)
            {
                Assert.Less(profile.moveSpeed, movement.maxSpeed,
                    $"{profile.displayName} runs at {profile.moveSpeed} against the player's " +
                    $"{movement.maxSpeed}. There would be no walking home from anything.");
            }
        }

        [Test]
        public void DepthNeverMakesAnythingFastEnoughToCatchThePlayer()
        {
            // The scaling is supposed to leave speed alone. This is the check that the *asset*
            // benefits from it - the deepest multiplier in the tables applied to the fastest family
            // still has to lose a footrace.
            var roster = Roster();
            var tables = Tables();
            var movement = AssetDatabase.LoadAssetAtPath<MovementTuning>(ProjectSetup.MovementTuningPath);

            var deepest = 1f;
            foreach (var entry in tables.Areas)
            {
                deepest = Mathf.Max(deepest, entry.statMultiplier);
            }

            foreach (var profile in roster.Families)
            {
                Assert.Less(profile.Stats.Scaled(deepest).MoveSpeed, movement.maxSpeed,
                    $"{profile.displayName} outruns the player once Ember Scar's multiplier is applied.");
            }
        }

        [Test]
        public void ExactlyOneFamilyShootsFromOutsideYourReach()
        {
            // Spark being the only ranged family is what makes it a different problem rather than
            // a recolour. Two ranged families and standing still stops being survivable anywhere.
            var roster = Roster();
            var ranged = 0;

            foreach (var profile in roster.Families)
            {
                if (profile.Stats.IsRanged)
                {
                    ranged++;
                    Assert.AreEqual(EnemyFamily.Spark, profile.family,
                        $"{profile.displayName} shoots, and DESIGN.md §5 gives that to Spark.");
                }
            }

            Assert.AreEqual(1, ranged);
        }

        [Test]
        public void ExactlyOneFamilyGlows()
        {
            // One emissive family in a lit world is a signal; two is a light show. It is the same
            // trap Phase 2 hit with Ember Scar's vents, and the answer is the same.
            var roster = Roster();
            var glowing = 0;

            foreach (var profile in roster.Families)
            {
                if (profile.accentEmissive)
                {
                    glowing++;
                    Assert.AreEqual(EnemyFamily.Spark, profile.family);
                }
            }

            Assert.AreEqual(1, glowing);
        }

        [Test]
        public void TheFourFamiliesDoNotFeelAlike()
        {
            // Four families that all die in the same time to the same shots are one family with
            // four colours. The spread is the design.
            var roster = Roster();
            var tuning = Tuning();

            var shortest = float.MaxValue;
            var longest = 0f;

            foreach (var profile in roster.Families)
            {
                var seconds = profile.maxHealth / tuning.DamagePerSecond;
                shortest = Mathf.Min(shortest, seconds);
                longest = Mathf.Max(longest, seconds);
            }

            Assert.Greater(longest / shortest, 3f,
                $"The toughest family takes {longest:0.0}s to kill and the weakest {shortest:0.0}s. " +
                "That is not four kinds of enemy, it is one with four palettes.");
        }

        [Test]
        public void EveryFamilyIsTellableApartWithoutColour()
        {
            // Colour-coding fails exactly where it matters - green Venom on the Verge's moss, tan
            // Hide on Bone Reach's bleached ground - so silhouette has to carry it. Two families
            // sharing a footprint and a height would be indistinguishable in a screenshot.
            var roster = Roster();
            var families = roster.Families;

            for (var i = 0; i < families.Count; i++)
            {
                for (var j = i + 1; j < families.Count; j++)
                {
                    var a = families[i];
                    var b = families[j];
                    var difference = Mathf.Abs(a.height - b.height) + Mathf.Abs(a.width - b.width);

                    Assert.Greater(difference, 0.3f,
                        $"{a.displayName} and {b.displayName} are the same size and shape. " +
                        "On a top-down camera that leaves only colour to tell them apart.");
                }
            }
        }

        // -- The spawn tables ------------------------------------------------------------------

        [Test]
        public void EveryAreaHasARowIncludingBase()
        {
            // Base's row is never consulted - SpawnDirector refuses regardless - but leaving it out
            // would make "Base spawns nothing" look like an oversight rather than a decision.
            var tables = Tables();

            foreach (var area in Areas.All)
            {
                Assert.DoesNotThrow(() => tables.EntryFor(area), $"No spawn row for {area}.");
            }

            var home = tables.EntryFor(Area.Base);
            Assert.AreEqual(0, home.populationCap);
            Assert.AreEqual(0f, home.venom + home.impact + home.hide + home.spark);
        }

        [Test]
        public void DistanceFromBaseIsTheDifficultyDial()
        {
            // DESIGN.md §7. Asserted as a shape rather than against absolute numbers, so retuning
            // the escalation is a value change and not a test rewrite.
            var tables = Tables();

            for (var i = 1; i < Areas.Biomes.Length; i++)
            {
                var nearer = tables.EntryFor(Areas.Biomes[i - 1]);
                var further = tables.EntryFor(Areas.Biomes[i]);

                Assert.GreaterOrEqual(further.populationCap, nearer.populationCap,
                    $"{further.displayName} sends no more than {nearer.displayName} despite being deeper.");
                Assert.LessOrEqual(further.secondsBetweenSpawns, nearer.secondsBetweenSpawns,
                    $"{further.displayName} sends them no faster than {nearer.displayName}.");
                Assert.Greater(further.statMultiplier, nearer.statMultiplier,
                    $"{further.displayName} is no tougher than {nearer.displayName}.");
            }
        }

        [Test]
        public void EveryBiomeActuallySendsSomething()
        {
            var tables = Tables();

            foreach (var area in Areas.Biomes)
            {
                var entry = tables.EntryFor(area).ToEntry();
                Assert.IsTrue(entry.SpawnsAnything,
                    $"{area} has a population cap but no family weights - it would tick forever " +
                    "producing nothing, and read as a biome with no enemies in it.");
            }
        }

        [Test]
        public void EachBiomeIsADifferentFightRatherThanAHarderOne()
        {
            // What separates the biomes is the mix, not just the multiplier. If two areas sent the
            // same families in the same proportions, walking deeper would only ever mean the same
            // fight with bigger numbers.
            var tables = Tables();

            for (var i = 0; i < Areas.Biomes.Length; i++)
            {
                for (var j = i + 1; j < Areas.Biomes.Length; j++)
                {
                    var a = tables.EntryFor(Areas.Biomes[i]);
                    var b = tables.EntryFor(Areas.Biomes[j]);

                    var difference = Mathf.Abs(a.venom - b.venom) + Mathf.Abs(a.impact - b.impact)
                                     + Mathf.Abs(a.hide - b.hide) + Mathf.Abs(a.spark - b.spark);

                    Assert.Greater(difference, 0.2f,
                        $"{a.displayName} and {b.displayName} send the same mix of families.");
                }
            }
        }

        [Test]
        public void TheRingKeepsSpawnsOffScreenAndTheDespawnRadiusIsOutsideIt()
        {
            var tables = Tables();
            var framing = AssetDatabase.LoadAssetAtPath<CameraFraming>(ProjectSetup.CameraFramingPath);
            Assert.IsNotNull(framing);

            var spec = tables.ToSpec();

            // Half the visible width is the furthest anything on screen can be from a centred
            // player. The inner ring has to clear it, with room for the diagonal.
            Assert.Greater(spec.RingInnerRadius, framing.VisibleWidthOnDevice,
                "Enemies would appear inside the frame - which reads as a bug, not as an arrival.");
            Assert.Greater(spec.RingOuterRadius, spec.RingInnerRadius);
            Assert.Greater(spec.DespawnRadius, spec.RingOuterRadius + 4f,
                "Enemies would be recycled almost as fast as they are spawned.");
        }

        // -- The tuning ------------------------------------------------------------------------

        [Test]
        public void TheAutoAttackDoesNotReachOffScreen()
        {
            // Past about three quarters of the visible width the player is shooting things they
            // cannot see, and the game gets played by watching health bars instead of the world.
            var tuning = Tuning();
            var framing = AssetDatabase.LoadAssetAtPath<CameraFraming>(ProjectSetup.CameraFramingPath);
            Assert.IsNotNull(framing);

            var fraction = tuning.RangeAsFractionOfScreen(framing);
            Assert.Greater(fraction, 0.25f,
                $"Reach is only {fraction:P0} of the screen - the player would have to stand on things.");
            Assert.Less(fraction, 0.75f,
                $"Reach is {fraction:P0} of the screen. Anything past ~75% is shooting off screen.");
        }

        [Test]
        public void ThePlayerOutrangesEveryFamilyExceptTheOneMeantToOutrangeThem()
        {
            var tuning = Tuning();
            var roster = Roster();

            foreach (var profile in roster.Families)
            {
                if (profile.family == EnemyFamily.Spark)
                {
                    continue;
                }

                Assert.Less(profile.attackRange, tuning.attackRange,
                    $"{profile.displayName} reaches as far as the player does, so there is no " +
                    "distance at which engaging it is a choice.");
            }
        }

        [Test]
        public void AFightIsDecidedInSecondsRatherThanMinutes()
        {
            var tuning = Tuning();
            var roster = Roster();
            var tables = Tables();

            var toughest = roster.ProfileFor(EnemyFamily.Hide).maxHealth
                           * tables.EntryFor(Area.EmberScar).statMultiplier;
            var seconds = toughest / tuning.DamagePerSecond;

            Assert.Less(seconds, 12f,
                $"The toughest thing in the game takes {seconds:0.0}s of unbroken fire to kill. " +
                "Nothing about that reads as a fight.");
        }

        [Test]
        public void ThePlayerSurvivesLongEnoughToWalkAway()
        {
            // The check that the walk home is possible at all - DESIGN.md §3 rests on it. Caught a
            // real one: at the placeholder 100 health this came out at half a second, which is not
            // a fight you disengage from, it is one you have already lost.
            //
            // The swarm is modelled with the area's own family mix rather than with its most
            // damaging family, because the mix is what the table can actually send - ten Breakers
            // is not a thing Ember Scar produces. And with half the population cap engaged rather
            // than all of it: the cap counts what is alive near the player, and a player is roughly
            // a metre wide, so the rest are queuing.
            var tuning = Tuning();
            var roster = Roster();
            var entry = Tables().EntryFor(Area.EmberScar);
            var mix = entry.ToEntry();

            var perEnemy = 0f;
            foreach (var profile in roster.Families)
            {
                var share = mix.WeightOf(profile.family) / mix.TotalWeight;
                perEnemy += share * profile.Stats.Scaled(entry.statMultiplier).DamagePerSecond;
            }

            var engaged = Mathf.Max(1f, entry.populationCap * 0.5f);
            var seconds = tuning.playerMaxHealth / (perEnemy * engaged);

            Assert.Greater(seconds, 2.5f,
                $"Half an Ember Scar swarm kills a healthy player in {seconds:0.0}s. There is not " +
                "time in that to notice it, turn, and break contact - so leaving is not a choice " +
                "the player gets to make.");
        }

        [Test]
        public void TheHeaviestHitInTheGameIsNotAQuarterOfYourHealth()
        {
            // The other half of survivability, and the one an average cannot see. A single blow
            // that removes a quarter of the bar makes the fight about whether you were touched at
            // all, which under an automatic attack is not something the player is steering.
            var tuning = Tuning();
            var deepest = Tables().EntryFor(Area.EmberScar).statMultiplier;

            var heaviest = 0f;
            foreach (var profile in Roster().Families)
            {
                heaviest = Mathf.Max(heaviest, profile.Stats.Scaled(deepest).Damage);
            }

            var share = heaviest / tuning.playerMaxHealth;
            Assert.Less(share, 0.25f,
                $"The heaviest single hit costs {share:P0} of the player's health.");
        }

        [Test]
        public void HitFeedbackIsShorterThanTheShotsThatCauseIt()
        {
            // A flash longer than the interval between hits means a swarm under fire is one solid
            // white blob rather than four enemies.
            var tuning = Tuning();

            Assert.Less(tuning.hitFlashSeconds, tuning.attackInterval,
                "The hit flash outlasts the gap between shots.");
            Assert.Less(tuning.deathSeconds, 1f,
                "Corpses stand around long enough to block the shots aimed at what is behind them.");
        }

        [Test]
        public void AHealthBarIsBigEnoughToReadAndSmallerThanWhatItSitsOn()
        {
            var tuning = Tuning();
            var roster = Roster();

            var narrowest = float.MaxValue;
            foreach (var profile in roster.Families)
            {
                narrowest = Mathf.Min(narrowest, profile.width);
            }

            Assert.Greater(tuning.healthBarWidth, narrowest * 0.75f,
                "The bar is narrower than the enemy it belongs to and will not read as its bar.");
            Assert.Less(tuning.healthBarWidth, 3f,
                "The bar is wide enough to overlap its neighbours in a crowd.");
            Assert.Greater(tuning.healthBarLift, 0f,
                "The bar sits inside the body rather than above it.");
        }

        [Test]
        public void EveryMaterialTheFightDrawsWithIsAReferencedAsset()
        {
            // An iOS build strips shaders nothing points at, so a material created at runtime finds
            // its shader missing on the device and renders magenta or not at all - while looking
            // perfect in the editor. D8 is iOS-first; that includes the failure modes.
            var tuning = Tuning();

            Assert.IsNotNull(tuning.playerProjectileMaterial, "No player projectile material.");
            Assert.IsNotNull(tuning.enemyProjectileMaterial, "No enemy projectile material.");
            Assert.IsNotNull(tuning.hitFlashMaterial, "No impact burst material.");
            Assert.IsNotNull(tuning.healthBarFillMaterial, "No health bar fill material.");
            Assert.IsNotNull(tuning.healthBarBackdropMaterial, "No health bar backdrop material.");

            foreach (var profile in Roster().Families)
            {
                Assert.IsNotNull(profile.bodyMaterial, $"{profile.displayName} has no body material.");
                Assert.IsNotNull(profile.accentMaterial, $"{profile.displayName} has no accent material.");
            }
        }
    }
}
