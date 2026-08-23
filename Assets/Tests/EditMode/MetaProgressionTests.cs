using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Graft.Core;

namespace Graft.Tests
{
    public class WeaponMergeTests
    {
        static List<OwnedWeapon> Stack(int count, Rarity tier = Rarity.Common)
        {
            var list = new List<OwnedWeapon>();
            for (int i = 0; i < count; i++)
            {
                list.Add(new OwnedWeapon
                {
                    DefinitionId = WeaponCatalog.RustEdge.Id, Tier = tier, Level = 1
                });
            }
            return list;
        }

        [Test]
        public void Three_copies_merge_into_one_of_the_next_tier()
        {
            var stack = Stack(3);
            Assert.IsTrue(WeaponMerge.CanMerge(stack, WeaponCatalog.RustEdge.Id, Rarity.Common));

            var merged = WeaponMerge.Merge(stack, WeaponCatalog.RustEdge.Id, Rarity.Common);
            Assert.IsNotNull(merged);
            Assert.AreEqual(Rarity.Uncommon, merged.Tier);
            Assert.AreEqual(1, stack.Count, "Three copies in, one copy out.");
        }

        [Test]
        public void Two_copies_are_not_enough()
        {
            var stack = Stack(2);
            Assert.IsFalse(WeaponMerge.CanMerge(stack, WeaponCatalog.RustEdge.Id, Rarity.Common));
            Assert.IsNull(WeaponMerge.Merge(stack, WeaponCatalog.RustEdge.Id, Rarity.Common));
            Assert.AreEqual(2, stack.Count, "A failed merge must not consume anything.");
        }

        [Test]
        public void Merging_keeps_the_best_level_so_invested_cores_are_never_lost()
        {
            var stack = Stack(3);
            stack[1].Level = 7;
            var merged = WeaponMerge.Merge(stack, WeaponCatalog.RustEdge.Id, Rarity.Common);
            Assert.AreEqual(7, merged.Level);
        }

        [Test]
        public void Merging_does_not_touch_a_different_weapon_in_the_same_stack()
        {
            var stack = Stack(3);
            stack.Add(new OwnedWeapon
            {
                DefinitionId = WeaponCatalog.SlugThrower.Id, Tier = Rarity.Common, Level = 4
            });
            WeaponMerge.Merge(stack, WeaponCatalog.RustEdge.Id, Rarity.Common);

            Assert.AreEqual(1, WeaponMerge.CountAt(stack, WeaponCatalog.SlugThrower.Id, Rarity.Common),
                "The unrelated weapon must survive untouched.");
        }

        [Test]
        public void Legendary_is_the_ceiling()
        {
            var stack = Stack(3, Rarity.Legendary);
            Assert.IsFalse(WeaponMerge.CanMerge(stack, WeaponCatalog.RustEdge.Id, Rarity.Legendary));
        }

        [Test]
        public void Tier_and_level_both_raise_power_independently()
        {
            var basic = new OwnedWeapon { DefinitionId = WeaponCatalog.RustEdge.Id, Tier = Rarity.Common, Level = 1 };
            var levelled = new OwnedWeapon { DefinitionId = WeaponCatalog.RustEdge.Id, Tier = Rarity.Common, Level = 5 };
            var tiered = new OwnedWeapon { DefinitionId = WeaponCatalog.RustEdge.Id, Tier = Rarity.Rare, Level = 1 };

            Assert.Greater(levelled.Power, basic.Power);
            Assert.Greater(tiered.Power, basic.Power);
        }
    }

    public class VesselMasteryTests
    {
        [Test]
        public void Upgrading_spends_gold_and_raises_the_level()
        {
            var m = new VesselMastery("scaleborn");
            int gold = 10000;
            Assert.IsTrue(m.TryUpgrade(MasteryNode.Power, ref gold));
            Assert.AreEqual(1, m.LevelOf(MasteryNode.Power));
            Assert.Less(gold, 10000);
        }

        [Test]
        public void Cannot_upgrade_without_enough_gold()
        {
            var m = new VesselMastery("scaleborn");
            int gold = 1;
            Assert.IsFalse(m.TryUpgrade(MasteryNode.Power, ref gold));
            Assert.AreEqual(0, m.LevelOf(MasteryNode.Power));
            Assert.AreEqual(1, gold, "A refused upgrade must not spend anything.");
        }

        [Test]
        public void Costs_escalate_so_late_nodes_are_a_real_sink()
        {
            var m = new VesselMastery("scaleborn");
            int first = m.CostToUpgrade(MasteryNode.Power);
            int gold = 999999;
            for (int i = 0; i < 5; i++) m.TryUpgrade(MasteryNode.Power, ref gold);
            Assert.Greater(m.CostToUpgrade(MasteryNode.Power), first);
        }

        [Test]
        public void Levels_stop_at_the_cap()
        {
            var m = new VesselMastery("scaleborn");
            int gold = int.MaxValue / 2;
            for (int i = 0; i < VesselMastery.MaxLevel + 10; i++) m.TryUpgrade(MasteryNode.MoveSpeed, ref gold);
            Assert.AreEqual(VesselMastery.MaxLevel, m.LevelOf(MasteryNode.MoveSpeed));
            Assert.IsTrue(m.IsMaxed(MasteryNode.MoveSpeed));
        }

        [Test]
        public void Corruption_resist_levels_let_you_take_more_grafts_before_turning()
        {
            var m = new VesselMastery("scaleborn");
            int gold = 999999;
            for (int i = 0; i < 10; i++) m.TryUpgrade(MasteryNode.CorruptionResist, ref gold);

            var plain = new RunState(VesselCatalog.Scaleborn, new VesselMastery("scaleborn"), null);
            var resistant = new RunState(VesselCatalog.Scaleborn, m, null);

            for (int i = 0; i < 4; i++)
            {
                plain.Purity.AddGraft(ArchetypeCatalog.Venomspitter);
                resistant.Purity.AddGraft(ArchetypeCatalog.Venomspitter);
            }
            Assert.Less(resistant.Purity.Corruption, plain.Purity.Corruption);
        }
    }

    public class SaveServiceTests
    {
        string _dir;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "graft_tests_" + Path.GetRandomFileName());
            Directory.CreateDirectory(_dir);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

        [Test]
        public void A_missing_file_yields_a_fresh_profile_rather_than_throwing()
        {
            var svc = new SaveService(_dir);
            var p = svc.Load();
            Assert.IsNotNull(p);
            Assert.IsTrue(p.HasVessel(VesselCatalog.Scaleborn.Id));
            Assert.Greater(p.Weapons.Count, 0);
        }

        [Test]
        public void A_saved_profile_round_trips()
        {
            var svc = new SaveService(_dir);
            var p = PlayerProfile.NewProfile();
            p.SoftGold = 4321;
            p.HighestWaveCleared = 9;
            p.EquippedVesselId = VesselCatalog.Ironhusk.Id;
            p.MasteryFor(VesselCatalog.Ironhusk.Id).Levels[(int)MasteryNode.Power] = 5;
            svc.Save(p);

            var loaded = new SaveService(_dir).Load();
            Assert.AreEqual(4321, loaded.SoftGold);
            Assert.AreEqual(9, loaded.HighestWaveCleared);
            Assert.AreEqual(VesselCatalog.Ironhusk.Id, loaded.EquippedVesselId);
            Assert.AreEqual(5, loaded.MasteryFor(VesselCatalog.Ironhusk.Id).LevelOf(MasteryNode.Power));
        }

        [Test]
        public void A_corrupt_file_falls_back_instead_of_crashing()
        {
            File.WriteAllText(Path.Combine(_dir, SaveService.FileName), "{ not json at all ");
            var loaded = new SaveService(_dir).Load();
            Assert.IsNotNull(loaded);
            Assert.IsTrue(loaded.HasVessel(VesselCatalog.Scaleborn.Id));
        }

        [Test]
        public void SaveService_refuses_to_run_without_an_explicit_directory()
        {
            // The structural guard that stops a test ever writing to the real profile.
            Assert.Throws<System.ArgumentException>(() => new SaveService(null));
            Assert.Throws<System.ArgumentException>(() => new SaveService(""));
        }
    }
}
