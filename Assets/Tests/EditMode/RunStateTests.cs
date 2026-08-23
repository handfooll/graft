using System.Linq;
using NUnit.Framework;
using Graft.Core;

namespace Graft.Tests
{
    public class RunStateTests
    {
        static RunState NewRun(VesselDefinition vessel = null, WeaponClass weapon = WeaponClass.Blade)
        {
            var w = new OwnedWeapon
            {
                DefinitionId = weapon == WeaponClass.Blade
                    ? WeaponCatalog.RustEdge.Id
                    : WeaponCatalog.SlugThrower.Id,
                Tier = Rarity.Common,
                Level = 1
            };
            return new RunState(vessel ?? VesselCatalog.Scaleborn, new VesselMastery("scaleborn"), w);
        }

        [Test]
        public void A_fresh_run_starts_at_full_health_with_no_grafts()
        {
            var run = NewRun();
            Assert.AreEqual(0, run.GraftCount);
            Assert.AreEqual(run.EffectiveMaxHP, run.CurrentHP, 0.001f);
            Assert.IsFalse(run.IsDead);
        }

        [Test]
        public void Offers_cover_all_five_slots()
        {
            var run = NewRun();
            var offers = run.BuildOffers(ArchetypeCatalog.Venomspitter);
            Assert.AreEqual(5, offers.Count);
            CollectionAssert.AreEquivalent(RunState.AllSlots, offers.Select(o => o.Slot).ToArray());
        }

        [Test]
        public void Installing_a_graft_fills_the_slot_and_raises_corruption()
        {
            var run = NewRun();
            var offer = run.BuildOffers(ArchetypeCatalog.Venomspitter).First(o => o.Slot == GraftSlot.Head);
            run.InstallGraft(offer);

            Assert.AreEqual(1, run.GraftCount);
            Assert.IsNotNull(run.GraftIn(GraftSlot.Head));
            Assert.AreEqual(ArchetypeCatalog.Venomspitter, run.GraftIn(GraftSlot.Head).Source);
            Assert.Greater(run.Purity.Corruption, 0f);
        }

        [Test]
        public void Grafting_a_different_family_into_a_filled_slot_overwrites_it()
        {
            var run = NewRun();
            run.InstallGraft(run.BuildOffers(ArchetypeCatalog.Venomspitter).First(o => o.Slot == GraftSlot.Head));

            var replacing = run.BuildOffers(ArchetypeCatalog.Sparkmite).First(o => o.Slot == GraftSlot.Head);
            Assert.IsNotNull(replacing.Replaces, "The offer must warn that it would replace something.");
            Assert.IsFalse(replacing.IsReinforcement);

            run.InstallGraft(replacing);
            Assert.AreEqual(1, run.GraftCount, "Overwriting must not add a second graft to one slot.");
            Assert.AreEqual(ArchetypeCatalog.Sparkmite, run.GraftIn(GraftSlot.Head).Source);
        }

        [Test]
        public void Grafting_the_same_family_into_the_same_slot_reinforces_instead_of_replacing()
        {
            var run = NewRun();
            run.InstallGraft(run.BuildOffers(ArchetypeCatalog.Venomspitter).First(o => o.Slot == GraftSlot.Head));

            var again = run.BuildOffers(ArchetypeCatalog.Venomspitter).First(o => o.Slot == GraftSlot.Head);
            Assert.IsTrue(again.IsReinforcement);
            Assert.IsNull(again.Replaces);

            run.InstallGraft(again);
            Assert.AreEqual(2, run.GraftIn(GraftSlot.Head).Tier);
        }

        [Test]
        public void Both_arms_hold_independent_grafts()
        {
            var run = NewRun();
            run.InstallGraft(run.BuildOffers(ArchetypeCatalog.Venomspitter).First(o => o.Slot == GraftSlot.LeftArm));
            run.InstallGraft(run.BuildOffers(ArchetypeCatalog.Sparkmite).First(o => o.Slot == GraftSlot.RightArm));

            Assert.AreEqual(2, run.GraftCount);
            Assert.AreEqual(Element.Venom, run.SpecInSlot(GraftSlot.LeftArm).Value.Element);
            Assert.AreEqual(Element.Spark, run.SpecInSlot(GraftSlot.RightArm).Value.Element);
        }

        [Test]
        public void An_empty_slot_has_no_spec()
        {
            var run = NewRun();
            Assert.IsNull(run.SpecInSlot(GraftSlot.Legs));
        }

        [Test]
        public void Vessel_affinity_makes_the_matching_element_hit_harder()
        {
            // Scaleborn is venom-affine; Ironhusk is not.
            var scaleborn = new RunState(VesselCatalog.Scaleborn, new VesselMastery("scaleborn"),
                new OwnedWeapon { DefinitionId = WeaponCatalog.RustEdge.Id, Tier = Rarity.Common });
            var ironhusk = new RunState(VesselCatalog.Ironhusk, new VesselMastery("ironhusk"),
                new OwnedWeapon { DefinitionId = WeaponCatalog.RustEdge.Id, Tier = Rarity.Common });

            var a = scaleborn.ResolveFor(ArchetypeCatalog.Venomspitter, GraftSlot.Head, 1);
            var b = ironhusk.ResolveFor(ArchetypeCatalog.Venomspitter, GraftSlot.Head, 1);
            Assert.Greater(a.Damage, b.Damage,
                "The same venom drop should be worth more on the venom-affine body.");
        }

        [Test]
        public void Taking_a_graft_does_not_read_as_being_hurt()
        {
            var run = NewRun();
            run.TakeDamage(run.EffectiveMaxHP * 0.5f);
            float fractionBefore = run.CurrentHP / run.EffectiveMaxHP;

            // A hide graft raises Max HP; corruption raises it again.
            run.InstallGraft(run.BuildOffers(ArchetypeCatalog.Hidebrute).First(o => o.Slot == GraftSlot.Core));

            float fractionAfter = run.CurrentHP / run.EffectiveMaxHP;
            Assert.AreEqual(fractionBefore, fractionAfter, 0.001f,
                "Health fraction must be preserved across a Max HP change.");
        }

        [Test]
        public void Passive_bonuses_from_grafts_accumulate_into_base_stats()
        {
            var run = NewRun();
            float powerBefore = run.BaseStats.Power;
            run.InstallGraft(run.BuildOffers(ArchetypeCatalog.Bloodcharger).First(o => o.Slot == GraftSlot.Legs));
            Assert.Greater(run.BaseStats.Power, powerBefore);
        }

        [Test]
        public void A_heavily_corrupted_hero_burns_health_over_time()
        {
            var run = NewRun(VesselCatalog.Emberkin);
            run.Purity.Add(0.95f);
            float before = run.CurrentHP;
            for (int i = 0; i < 60; i++) run.Tick(1f / 60f);
            Assert.Less(run.CurrentHP, before, "Past the threshold, a second of play must cost health.");
        }

        [Test]
        public void A_clean_hero_regenerates_instead()
        {
            var run = NewRun();
            run.TakeDamage(50f);
            float before = run.CurrentHP;
            for (int i = 0; i < 60; i++) run.Tick(1f / 60f);
            Assert.Greater(run.CurrentHP, before);
        }

        [Test]
        public void Health_never_exceeds_max_or_falls_below_zero()
        {
            var run = NewRun();
            run.Heal(99999f);
            Assert.AreEqual(run.EffectiveMaxHP, run.CurrentHP, 0.001f);
            run.TakeDamage(99999f);
            Assert.AreEqual(0f, run.CurrentHP);
            Assert.IsTrue(run.IsDead);
        }

        [Test]
        public void Kills_feed_the_matching_essence_meter()
        {
            var run = NewRun();
            run.RegisterKill(ArchetypeCatalog.Sparkmite);
            run.RegisterKill(ArchetypeCatalog.Sparkmite);
            Assert.AreEqual(2, run.Kills);
            Assert.AreEqual(2, run.Essence.AmountFor(ArchetypeCatalog.Sparkmite));
        }

        [Test]
        public void A_dirtier_run_pays_more_gold()
        {
            var clean = NewRun();
            var dirty = NewRun();
            clean.Wave = dirty.Wave = 5;
            for (int i = 0; i < 10; i++)
            {
                clean.RegisterKill(ArchetypeCatalog.Sparkmite);
                dirty.RegisterKill(ArchetypeCatalog.Sparkmite);
            }
            dirty.Purity.Add(0.9f);
            Assert.Greater(dirty.SoftGoldEarned(), clean.SoftGoldEarned());
        }
    }
}
