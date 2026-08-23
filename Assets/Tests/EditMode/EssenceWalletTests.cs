using NUnit.Framework;
using Graft.Core;

namespace Graft.Tests
{
    public class EssenceWalletTests
    {
        [Test]
        public void Meters_are_tracked_per_enemy_family_not_pooled()
        {
            var w = new EssenceWallet();
            w.Add(ArchetypeCatalog.Venomspitter, 5);
            Assert.AreEqual(5, w.AmountFor(ArchetypeCatalog.Venomspitter));
            Assert.AreEqual(0, w.AmountFor(ArchetypeCatalog.Sparkmite),
                "Killing one family must not progress another's meter.");
        }

        [Test]
        public void Meter_becomes_ready_at_its_own_threshold()
        {
            var w = new EssenceWallet();
            var a = ArchetypeCatalog.Venomspitter;
            w.Add(a, a.EssenceToGraft - 1);
            Assert.IsFalse(w.IsReady(a));
            w.Add(a, 1);
            Assert.IsTrue(w.IsReady(a));
        }

        [Test]
        public void Consuming_a_charge_carries_the_overflow_forward()
        {
            var w = new EssenceWallet();
            var a = ArchetypeCatalog.Hidebrute;
            w.Add(a, a.EssenceToGraft + 3);
            Assert.IsTrue(w.ConsumeGraftCharge(a));
            Assert.AreEqual(3, w.AmountFor(a), "Overflow essence must not be thrown away.");
        }

        [Test]
        public void Consuming_an_unfilled_meter_fails_and_changes_nothing()
        {
            var w = new EssenceWallet();
            var a = ArchetypeCatalog.Hidebrute;
            w.Add(a, 2);
            Assert.IsFalse(w.ConsumeGraftCharge(a));
            Assert.AreEqual(2, w.AmountFor(a));
        }

        [Test]
        public void Fill_fraction_is_clamped_for_the_meter_ui()
        {
            var w = new EssenceWallet();
            var a = ArchetypeCatalog.Sparkmite;
            w.Add(a, a.EssenceToGraft * 3);
            Assert.AreEqual(1f, w.FillFractionFor(a));
        }

        [Test]
        public void Ready_archetypes_lists_every_full_meter()
        {
            var w = new EssenceWallet();
            w.Add(ArchetypeCatalog.Venomspitter, 99);
            w.Add(ArchetypeCatalog.Sparkmite, 99);
            var ready = w.ReadyArchetypes();
            Assert.AreEqual(2, ready.Count);
            CollectionAssert.Contains(ready, ArchetypeCatalog.Venomspitter);
            CollectionAssert.Contains(ready, ArchetypeCatalog.Sparkmite);
        }
    }
}
