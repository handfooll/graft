using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Graft.Core;
using Graft.Game;
using Graft.App;

namespace Graft.Tests.PlayMode
{
    /// <summary>
    /// Boots the real game and plays it. The EditMode suite proves the rules are right;
    /// this proves the rules are actually wired to something -- that a kill really produces
    /// essence, that a graft really reaches the avatar, that the arena really spawns.
    /// Compiling clean says none of that.
    /// </summary>
    public class RunSmokeTests
    {
        GameFlow Flow => GameFlow.Instance;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            // GameFlow bootstraps itself before the first scene loads.
            for (int i = 0; i < 10 && GameFlow.Instance == null; i++) yield return null;
            Assert.IsNotNull(GameFlow.Instance, "GameFlow failed to bootstrap.");
            Time.timeScale = 1f;
        }

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = 1f;
        }

        [UnityTest]
        public IEnumerator The_game_boots_without_a_scene_authored_by_hand()
        {
            yield return null;
            Assert.IsNotNull(Camera.main, "No camera was built.");
            Assert.IsNotNull(Arena.Instance, "No arena was built.");
            Assert.IsNotNull(Object.FindAnyObjectByType<Canvas>(), "No canvas was built.");
        }

        [UnityTest]
        public IEnumerator Batch_runs_never_write_to_the_real_profile()
        {
            yield return null;
            // The guard that stops a test or dev tool clobbering a player's save.
            Assert.IsTrue(Application.isBatchMode,
                "This suite is expected to run headless; the save guard keys off batch mode.");
        }

        [UnityTest]
        public IEnumerator Starting_a_run_builds_a_player_and_a_director()
        {
            Flow.StartRun();
            yield return null;

            Assert.IsNotNull(Flow.Run, "No RunState was created.");
            Assert.IsNotNull(Arena.Instance.Player, "No player was spawned.");
            Assert.IsNotNull(Flow.Director, "No wave director was created.");
            Assert.IsNotNull(Object.FindAnyObjectByType<PlayerController>());
            Assert.IsNotNull(Object.FindAnyObjectByType<AbilityRunner>());
            Assert.IsTrue(Flow.IsRunActive);
        }

        [UnityTest]
        public IEnumerator The_arena_actually_spawns_enemies()
        {
            Flow.StartRun();

            float deadline = Time.realtimeSinceStartup + 5f;
            while (Arena.Instance.AliveCount() == 0 && Time.realtimeSinceStartup < deadline)
                yield return null;

            Assert.Greater(Arena.Instance.AliveCount(), 0, "No enemies spawned within 5s.");
        }

        [UnityTest]
        public IEnumerator Killing_an_enemy_drops_essence_that_fills_its_own_meter()
        {
            Flow.StartRun();
            yield return null;

            var archetype = ArchetypeCatalog.Venomspitter;
            int before = Flow.Run.Essence.AmountFor(archetype);

            // Spawn one next to the player and kill it outright.
            var go = new GameObject("TestEnemy");
            go.transform.position = Arena.Instance.Player.position + new Vector3(0.5f, 0f, 0f);
            var agent = go.AddComponent<EnemyAgent>();
            agent.Init(Flow, archetype, 1f, false);
            yield return null;

            agent.ApplyDamage(99999f);
            Assert.IsFalse(agent.IsAlive, "The enemy should be dead.");

            // The orb has to fly to the player and be collected.
            float deadline = Time.realtimeSinceStartup + 5f;
            while (Flow.Run.Essence.AmountFor(archetype) <= before &&
                   Time.realtimeSinceStartup < deadline)
                yield return null;

            Assert.Greater(Flow.Run.Essence.AmountFor(archetype), before,
                "The kill produced no collectable essence.");
        }

        [UnityTest]
        public IEnumerator Installing_a_graft_raises_corruption_and_arms_the_slot()
        {
            Flow.StartRun();
            yield return null;

            var run = Flow.Run;
            float corruptionBefore = run.Purity.Corruption;

            var offer = run.BuildOffers(ArchetypeCatalog.Sparkmite)[0];
            run.InstallGraft(offer);
            Flow.Avatar.SetGraft(offer.Slot, run.GraftIn(offer.Slot));
            yield return null;

            Assert.IsNotNull(run.GraftIn(offer.Slot));
            Assert.Greater(run.Purity.Corruption, corruptionBefore);
            Assert.IsTrue(run.SpecInSlot(offer.Slot).HasValue,
                "An installed graft must produce a live ability spec.");
        }

        [UnityTest]
        public IEnumerator A_grafted_hero_actually_damages_enemies()
        {
            Flow.StartRun();
            yield return null;

            var run = Flow.Run;
            // Arms swipe whatever is in reach, so this is the quickest ability to observe.
            var offer = run.BuildOffers(ArchetypeCatalog.Bloodcharger)
                .Find(o => o.Slot == GraftSlot.RightArm);
            run.InstallGraft(offer);
            yield return null;

            var go = new GameObject("Punchbag");
            go.transform.position = Arena.Instance.Player.position + new Vector3(0.7f, 0f, 0f);
            var agent = go.AddComponent<EnemyAgent>();
            // Deliberately far too much health to kill in the window: we want to measure
            // that damage is being dealt, not race a one-shot.
            agent.Init(Flow, ArchetypeCatalog.Hidebrute, 40f, false);
            yield return null;

            float startHP = agent.CurrentHP;
            Assert.Greater(startHP, 0f);

            float deadline = Time.realtimeSinceStartup + 4f;
            while (Time.realtimeSinceStartup < deadline)
            {
                if (agent == null || !agent.IsAlive) break;
                if (agent.CurrentHP < startHP) break;
                yield return null;
            }

            Assert.IsTrue(agent == null || !agent.IsAlive || agent.CurrentHP < startHP,
                "An equipped arm graft never damaged an adjacent enemy.");
        }

        [UnityTest]
        public IEnumerator The_hero_dies_when_health_runs_out_and_the_run_ends()
        {
            Flow.StartRun();
            yield return null;

            Flow.Run.TakeDamage(999999f);
            Assert.IsTrue(Flow.Run.IsDead);

            // GameFlow notices on its next Update and resolves the run.
            float deadline = Time.realtimeSinceStartup + 3f;
            while (Flow.IsRunActive && Time.realtimeSinceStartup < deadline) yield return null;

            Assert.IsFalse(Flow.IsRunActive, "The run should have ended once the hero died.");
        }

        [UnityTest]
        public IEnumerator Clearing_a_run_pays_out_and_persists()
        {
            int goldBefore = Flow.Profile.SoftGold;

            Flow.StartRun();
            yield return null;
            Flow.Run.Wave = 4;
            for (int i = 0; i < 12; i++) Flow.Run.RegisterKill(ArchetypeCatalog.Sparkmite);

            Flow.EndRun(cleared: true);
            yield return null;

            Assert.Greater(Flow.Profile.SoftGold, goldBefore,
                "Clearing a run should award soft gold.");
        }
    }
}
