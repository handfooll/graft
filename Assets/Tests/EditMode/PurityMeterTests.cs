using NUnit.Framework;
using Graft.Core;

namespace Graft.Tests
{
    public class PurityMeterTests
    {
        [Test]
        public void Starts_focused_and_clean()
        {
            var p = new PurityMeter();
            Assert.AreEqual(0f, p.Corruption);
            Assert.AreEqual(PurityBand.Focused, p.Band);
            Assert.IsTrue(p.HasFocusBonus);
        }

        [Test]
        public void Grafting_walks_the_bands_upward_and_loses_the_focus_bonus()
        {
            var p = new PurityMeter();
            for (int i = 0; i < 3; i++) p.AddGraft(ArchetypeCatalog.Venomspitter);
            Assert.AreEqual(PurityBand.Tainted, p.Band);
            Assert.IsFalse(p.HasFocusBonus, "The focus bonus must be lost the moment you tip out of Focused.");

            for (int i = 0; i < 3; i++) p.AddGraft(ArchetypeCatalog.Venomspitter);
            Assert.AreEqual(PurityBand.Monstrous, p.Band);
        }

        [Test]
        public void Power_rises_with_corruption_which_is_the_whole_trade()
        {
            var clean = new PurityMeter();
            var dirty = new PurityMeter();
            dirty.Add(0.8f);

            Assert.Greater(dirty.DamageMultiplier, clean.DamageMultiplier);
            Assert.Greater(dirty.MaxHPMultiplier, clean.MaxHPMultiplier);
            Assert.Greater(clean.PrecisionMultiplier, dirty.PrecisionMultiplier);
            Assert.Less(clean.RerollCooldownMultiplier, dirty.RerollCooldownMultiplier);
        }

        [Test]
        public void Self_damage_only_starts_past_the_threshold()
        {
            var p = new PurityMeter();
            p.Add(PurityMeter.SelfDamageThreshold - 0.01f);
            Assert.AreEqual(0f, p.SelfDamageFractionPerSecond, "Below the threshold the hero must not burn.");

            p.Add(0.05f);
            Assert.Greater(p.SelfDamageFractionPerSecond, 0f, "Past the threshold the hero must start burning.");
        }

        [Test]
        public void Resistance_slows_corruption_which_is_what_Ironhusk_sells()
        {
            var soft = new PurityMeter(0f);
            var tough = new PurityMeter(0.5f);
            soft.AddGraft(ArchetypeCatalog.Hidebrute);
            tough.AddGraft(ArchetypeCatalog.Hidebrute);
            Assert.Less(tough.Corruption, soft.Corruption);
        }

        [Test]
        public void Boss_essence_corrupts_twice_as_fast()
        {
            var p = new PurityMeter();
            float normal = p.PreviewCost(ArchetypeCatalog.Venomspitter);
            float boss = p.PreviewCost(ArchetypeCatalog.HollowChoir);
            Assert.AreEqual(normal * 2f, boss, 0.0001f);
        }

        [Test]
        public void Corruption_never_exceeds_one()
        {
            var p = new PurityMeter();
            for (int i = 0; i < 50; i++) p.AddGraft(ArchetypeCatalog.HollowChoir);
            Assert.AreEqual(1f, p.Corruption, 0.0001f);
        }

        [Test]
        public void Band_change_fires_once_per_crossing()
        {
            var p = new PurityMeter();
            int changes = 0;
            p.BandChanged += (from, to) => changes++;
            p.Add(0.4f);   // Focused -> Tainted
            p.Add(0.1f);   // still Tainted
            p.Add(0.3f);   // Tainted -> Monstrous
            Assert.AreEqual(2, changes);
        }
    }
}
