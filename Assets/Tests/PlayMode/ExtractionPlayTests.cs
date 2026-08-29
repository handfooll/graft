using System.Collections;
using Graft.Core;
using Graft.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Graft.Tests
{
    /// <summary>
    /// The extraction loop played rather than calculated: essence actually dropping off a corpse,
    /// actually flying to the player, actually refusing to when the hold is full, and banking
    /// exactly where D-P4.2 says it does and nowhere else.
    ///
    /// <c>ExtractionRulesTests</c> proves the arithmetic and <c>ExtractionAssetTests</c> proves the
    /// numbers; neither can prove the wiring. Everything here is a seam between two components
    /// where the failure mode is silence - a drop hung on a listener the pool clears, a bank zone
    /// asked about a position it never receives, a magnet whose radius was never set. All of those
    /// leave the game running, the tests green, and the loop simply not happening.
    ///
    /// <code>
    /// Unity -batchmode -projectPath . -runTests -testPlatform PlayMode \
    ///   -testFilter ExtractionPlayTests -testResults extraction.xml -logFile extraction.log
    /// </code>
    /// </summary>
    public class ExtractionPlayTests
    {
        [UnityTest]
        public IEnumerator AKillLeavesEssenceThatFliesToThePlayerAndFillsCargo()
        {
            yield return LoadWorld();
            var player = Stage(Area.Verge, out var ledger, out var field);

            var before = ledger.Carried.Carried;
            var victim = Kill(EnemyFamily.Venom, player.transform.position + Vector3.forward * 2f);

            Assert.Greater(field.LiveCount, 0,
                "Something died and left no essence. The drop is hung on Damageable.Died - check " +
                "it is subscribed *after* EnemyAgent.Configure, which clears listeners on its way in.");
            Assert.AreEqual(victim.Health.Pool.Maximum > 0f, true);

            // Long enough for a 9 m/s mote to cross two metres several times over.
            yield return Seconds(2.5f);

            Assert.Greater(ledger.Carried.Carried, before,
                "Essence was dropped but never absorbed. The magnet's radius, the field's magnet " +
                "reference, or the ledger behind it is not wired.");
            Assert.AreEqual(0, field.LiveCount,
                $"{field.LiveCount} motes are still on the ground two and a half seconds after a " +
                "kill two metres from the player.");
        }

        [UnityTest]
        public IEnumerator AFullHoldLeavesEssenceLyingOnTheGround()
        {
            // DESIGN.md §4's non-nagging prompt, and the thing the third gate image is a picture
            // of. At capacity the essence you are still killing for stops coming to you - no
            // banner, no interruption, it simply lies there.
            yield return LoadWorld();
            var player = Stage(Area.Verge, out var ledger, out var field);

            Assert.IsTrue(ledger.Absorb(new EssenceMote(EnemyFamily.Venom, ledger.Carried.Capacity, Area.Verge)),
                "Could not fill the hold in one go - the capacity is not a whole number of motes.");
            Assert.IsTrue(ledger.IsFull);

            Kill(EnemyFamily.Impact, player.transform.position + Vector3.forward * 2f);
            var dropped = field.LiveCount;
            Assert.Greater(dropped, 0, "The kill left no essence to refuse.");

            yield return Seconds(2.5f);

            Assert.AreEqual(dropped, field.RestingCount,
                "Motes were taken while the hold was full, or vanished instead of staying put.");
            Assert.IsTrue(ledger.Carried.IsFull, "The hold went past capacity.");
        }

        [UnityTest]
        public IEnumerator BankingHappensOnThePlazaAndNowhereElseInBase()
        {
            // **D-P4.2, and the assertion that proves the retreat valve exists.** If the whole 20 m
            // safe disc banked, every flight home would end the expedition - and so would any route
            // between two biomes that happened to cross Base.
            yield return LoadWorld();
            var player = Stage(Area.Verge, out var ledger, out _);
            var regions = Object.FindAnyObjectByType<WorldRegions>();
            var bank = Object.FindAnyObjectByType<BankZone>();
            Assert.IsNotNull(bank, "No BankZone in the World scene - nothing can ever be banked.");

            ledger.Absorb(new EssenceMote(EnemyFamily.Spark, 40f, Area.EmberScar));
            var carrying = ledger.CarriedValue;
            Assert.Greater(carrying, 0f);

            // Safe ground, outside the plaza: the place you flee to and keep your expedition.
            var home = regions.AnchorFor(Area.Base);
            var refuge = home + new Vector3((regions.BankRadius + regions.Map.BaseRadius) * 0.5f, 0f, 0f);
            Assert.IsTrue(regions.IsInBase(refuge), "The test's refuge point is not inside Base.");
            Assert.IsFalse(regions.IsInBankPlaza(refuge), "The test's refuge point is on the plaza.");

            player.Teleport(refuge, 0f);
            yield return Seconds(0.5f);

            Assert.AreEqual(0f, ledger.BankedTotal,
                "Standing in Base banked the cargo. The two rings have collapsed into one and " +
                "there is no way to retreat without ending the run.");
            Assert.AreEqual(carrying, ledger.CarriedValue, 1e-3f);

            // Now the last few metres to the monument.
            player.Teleport(home, 0f);
            yield return Seconds(0.5f);

            Assert.AreEqual(carrying, ledger.BankedTotal, 1e-3f,
                "Walking onto the plaza did not bank. There is no button (D8) - this is the only " +
                "way cargo is ever made permanent.");
            Assert.AreEqual(0f, ledger.Carried.Carried, "Banking left cargo in the hold.");
            Assert.AreEqual(1, bank.Bankings);
        }

        [UnityTest]
        public IEnumerator StandingOnThePlazaDoesNotBankOverAndOverAgain()
        {
            // Banking raises the event Phase 5 will reset corruption and clear grafts on. Firing it
            // every frame the player stands at the monument would drown that the moment it exists.
            yield return LoadWorld();
            var player = Stage(Area.Verge, out _, out _);
            var regions = Object.FindAnyObjectByType<WorldRegions>();
            var bank = Object.FindAnyObjectByType<BankZone>();

            player.Teleport(regions.AnchorFor(Area.Base), 0f);
            yield return Seconds(1.5f);

            Assert.LessOrEqual(bank.Bankings, 1,
                $"Standing still on the plaza banked {bank.Bankings} times.");
        }

        [UnityTest]
        public IEnumerator DyingOutInTheWorldCostsEveryUnbankedUnitAndNothingBanked()
        {
            // D4, played. The rules test proves the ledger obeys it; this proves PlayerVitals
            // actually calls it, and calls it before the teleport home rather than after.
            yield return LoadWorld();
            var player = Stage(Area.EmberScar, out var ledger, out _);
            var vitals = Object.FindAnyObjectByType<PlayerVitals>();
            var regions = Object.FindAnyObjectByType<WorldRegions>();
            Assert.IsNotNull(vitals, "No PlayerVitals in the World scene.");

            ledger.Absorb(new EssenceMote(EnemyFamily.Hide, 100f, Area.EmberScar));
            ledger.Bank();
            var banked = ledger.BankedTotal;
            Assert.Greater(banked, 0f);

            ledger.Absorb(new EssenceMote(EnemyFamily.Hide, 120f, Area.EmberScar));
            var atRisk = ledger.CarriedValue;
            Assert.Greater(atRisk, 0f);

            var falls = vitals.Falls;
            vitals.Health.ApplyDamage(99999f);
            yield return Seconds(0.3f);

            Assert.AreEqual(falls + 1, vitals.Falls, "The player did not die.");
            Assert.AreEqual(0f, ledger.Carried.Carried, "Dying left unbanked cargo in the hold.");
            Assert.AreEqual(banked, ledger.BankedTotal,
                "Dying changed the banked total. D4 puts only unbanked cargo at risk, and that " +
                "assertion is the one that must never be softened.");
            Assert.AreEqual(atRisk, vitals.LastLoss, 1e-3f, "The loss was misreported.");
            Assert.IsTrue(regions.IsInBase(player.transform.position),
                "Death did not return the player to Base.");
        }

        [UnityTest]
        public IEnumerator DeathIsNotBanking()
        {
            // Death teleports the player onto the plaza, which is also where banking happens. If
            // the bank zone treated that arrival as a crossing, dying would fire the event Phase 5
            // clears grafts and resets corruption on - a materially different rule from the one
            // DESIGN.md §3 draws.
            yield return LoadWorld();
            Stage(Area.EmberScar, out var ledger, out _);
            var vitals = Object.FindAnyObjectByType<PlayerVitals>();
            var bank = Object.FindAnyObjectByType<BankZone>();

            ledger.Absorb(new EssenceMote(EnemyFamily.Venom, 60f, Area.EmberScar));
            var before = bank.Bankings;

            vitals.Health.ApplyDamage(99999f);
            yield return Seconds(0.6f);

            Assert.AreEqual(before, bank.Bankings,
                "Dying counted as banking. Being carried home dead is not an extraction.");
            Assert.AreEqual(0f, ledger.BankedTotal, "Dying banked the cargo it was supposed to cost.");
        }

        [UnityTest]
        public IEnumerator TheHomeCueAppearsOnlyWhenThereIsSomethingToLose()
        {
            // The cue is not navigation - it is the extraction decision made visible. Nothing at
            // stake, nothing to point at.
            yield return LoadWorld();
            var player = Stage(Area.EmberScar, out var ledger, out _);
            var cue = Object.FindAnyObjectByType<HomeCue>();
            Assert.IsNotNull(cue, "No HomeCue in the World scene.");

            yield return Frames(3);
            Assert.IsFalse(cue.IsShowing,
                "The way home is being pointed out to a player carrying nothing.");

            ledger.Absorb(new EssenceMote(EnemyFamily.Spark, 40f, Area.EmberScar));
            yield return Frames(3);

            Assert.IsTrue(cue.IsShowing,
                "Deep in Ember Scar with a hold full of essence and no cue pointing home.");

            // And it points at Base rather than anywhere else.
            var regions = Object.FindAnyObjectByType<WorldRegions>();
            var toHome = regions.AnchorFor(Area.Base) - player.transform.position;
            toHome.y = 0f;
            var wanted = Quaternion.LookRotation(toHome.normalized, Vector3.up).eulerAngles.y;
            Assert.AreEqual(wanted, cue.Heading, 2f,
                $"The cue points {cue.Heading:0}° when Base is at {wanted:0}°.");

            ledger.Bank();
            yield return Frames(3);
            Assert.IsFalse(cue.IsShowing, "The cue is still up after banking emptied the hold.");
        }

        [UnityTest]
        public IEnumerator ADeepHaulBanksForMoreThanAShallowOneOfTheSameSize()
        {
            // The depth gradient, end to end through the real scene rather than through the map
            // alone: same number of units, different ground, different money.
            yield return LoadWorld();
            Stage(Area.Verge, out var ledger, out _);

            ledger.Absorb(new EssenceMote(EnemyFamily.Venom, 100f, Area.Verge));
            var shallow = ledger.Bank().Value;

            ledger.Absorb(new EssenceMote(EnemyFamily.Venom, 100f, Area.EmberScar));
            var deep = ledger.Bank().Value;

            Assert.Greater(deep, shallow * 2f,
                $"A hundred units from Ember Scar banked {deep:0} against the Verge's {shallow:0}. " +
                "There is no reason to walk out there.");
        }

        [UnityTest]
        public IEnumerator TheCargoBarOccupiesRealSpaceOnTheScreen()
        {
            // **The assertion that no picture and no rule test can replace.** DESIGN.md §4 requires
            // cargo to be visible at all times, and a HUD element with a collapsed RectTransform is
            // invisible while every other test stays green: the components tick, the ledger is
            // correct, the canvas exists, and there is simply nothing on the screen.
            //
            // Measured as a fraction of the canvas rather than in pixels, because the canvas is
            // ConstantPhysicalSize and its size in pixels differs on every device.
            yield return LoadWorld();
            Stage(Area.Verge, out _, out _);
            yield return Frames(3);

            var bar = Object.FindAnyObjectByType<Graft.UI.CargoBar>();
            Assert.IsNotNull(bar, "No CargoBar in the World scene - cargo is not visible at all.");

            var canvas = bar.GetComponentInParent<Canvas>();
            Assert.IsNotNull(canvas, "The CargoBar is not under a Canvas.");

            var canvasRect = ((RectTransform)canvas.transform).rect;
            var panel = (RectTransform)bar.transform;
            var safe = bar.GetComponentInParent<Graft.UI.SafeArea>();

            var detail =
                $"canvas {canvasRect.width:0}x{canvasRect.height:0}, " +
                $"panel {panel.rect.width:0}x{panel.rect.height:0}, " +
                $"safeArea rect {(safe != null ? ((RectTransform)safe.transform).rect.ToString() : "none")}, " +
                $"Screen {Screen.width}x{Screen.height}, Screen.safeArea {Screen.safeArea}";

            Assert.Greater(panel.rect.width, canvasRect.width * 0.5f,
                $"The cargo panel is {panel.rect.width:0} units wide inside a " +
                $"{canvasRect.width:0}-unit canvas. It has collapsed. {detail}");
            Assert.Greater(panel.rect.height, 20f,
                $"The cargo panel is {panel.rect.height:0} units tall - too short to hold a bar " +
                $"and two lines of text. {detail}");
        }

        // -- Helpers ---------------------------------------------------------------------------

        /// <summary>
        /// Puts the player somewhere with the spawn director and the field both cleared, and hands
        /// back the ledger everything is judged against.
        /// </summary>
        static PlayerController Stage(Area area, out ExtractionLedger ledger, out EssenceField field)
        {
            var player = Object.FindAnyObjectByType<PlayerController>();
            Assert.IsNotNull(player, "World scene has no PlayerController.");
            player.Input = new ScriptedMoveInput { Value = Vec2.Zero };

            var spawner = Object.FindAnyObjectByType<EnemySpawner>();
            Assert.IsNotNull(spawner, "No EnemySpawner in the World scene.");
            spawner.Running = false;
            spawner.ClearField();

            field = Object.FindAnyObjectByType<EssenceField>();
            Assert.IsNotNull(field, "No EssenceField in the World scene - nothing can ever drop.");
            field.Clear();

            var cargo = Object.FindAnyObjectByType<PlayerCargo>();
            Assert.IsNotNull(cargo, "No PlayerCargo on the player - there is no ledger.");
            ledger = cargo.Ledger;
            Assert.IsNotNull(ledger,
                "PlayerCargo has no ledger. It is missing its EssenceTuning or its WorldRegions.");

            var regions = Object.FindAnyObjectByType<WorldRegions>();
            player.Teleport(regions.AnchorFor(area), 0f);
            return player;
        }

        /// <summary>
        /// Spawns one enemy and kills it outright.
        ///
        /// Killed by draining its pool rather than by waiting for the player's auto-attack, so the
        /// test measures the drop rather than the fight - and so it takes the same path a shot
        /// does, through the one <see cref="Damageable.Died"/> that is already guarded against
        /// firing twice.
        /// </summary>
        static EnemyAgent Kill(EnemyFamily family, Vector3 at)
        {
            var spawner = Object.FindAnyObjectByType<EnemySpawner>();
            var agent = spawner.Spawn(family, at, 1f);
            Assert.IsNotNull(agent, $"Could not place a {family} at {at} to kill.");

            Assert.IsTrue(agent.Health.ApplyDamage(agent.Health.Pool.Maximum + 1f),
                "The killing blow did not report a kill.");
            return agent;
        }

        static IEnumerator LoadWorld()
        {
            // A fixed step, for the same reason the screenshot tour uses one: headless play mode
            // runs uncapped, and a mote given "2.5 seconds" to cross two metres otherwise gets a
            // different number of frames on every machine.
            Time.captureDeltaTime = 1f / 60f;
            Random.InitState(20260830);
            yield return SceneManager.LoadSceneAsync("World", LoadSceneMode.Single);

            var spawner = Object.FindAnyObjectByType<EnemySpawner>();
            if (spawner != null)
            {
                spawner.Running = false;
            }

            yield return Frames(5);
        }

        static IEnumerator Frames(int count)
        {
            for (var i = 0; i < count; i++)
            {
                yield return null;
            }
        }

        static IEnumerator Seconds(float seconds)
        {
            var deadline = Time.time + seconds;
            var guard = 0;
            while (Time.time < deadline && guard++ < 100000)
            {
                yield return null;
            }
        }
    }
}
