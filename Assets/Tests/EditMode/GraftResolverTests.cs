using NUnit.Framework;
using Graft.Core;

namespace Graft.Tests
{
    /// <summary>
    /// The GDD's central claim is that enemy type, slot, and weapon are three independent
    /// axes. These tests hold each one still and vary the others, which is the only way to
    /// catch a resolver that has quietly collapsed two axes into one.
    /// </summary>
    public class GraftResolverTests
    {
        [Test]
        public void Slot_determines_delivery_not_the_enemy_type()
        {
            Assert.AreEqual(Delivery.Aimed, GraftResolver.DeliveryForSlot(GraftSlot.Head));
            Assert.AreEqual(Delivery.Aura, GraftResolver.DeliveryForSlot(GraftSlot.Core));
            Assert.AreEqual(Delivery.Swipe, GraftResolver.DeliveryForSlot(GraftSlot.LeftArm));
            Assert.AreEqual(Delivery.Swipe, GraftResolver.DeliveryForSlot(GraftSlot.RightArm));
            Assert.AreEqual(Delivery.Dash, GraftResolver.DeliveryForSlot(GraftSlot.Legs));
        }

        [Test]
        public void Same_enemy_in_different_slots_yields_different_delivery()
        {
            var head = GraftResolver.Resolve(ArchetypeCatalog.Venomspitter, GraftSlot.Head, WeaponClass.Blade, 1);
            var arm = GraftResolver.Resolve(ArchetypeCatalog.Venomspitter, GraftSlot.LeftArm, WeaponClass.Blade, 1);

            Assert.AreEqual(Delivery.Aimed, head.Delivery);
            Assert.AreEqual(Delivery.Swipe, arm.Delivery);
            // Both are still venom -- the trait came from the enemy, not the slot.
            Assert.AreEqual(Element.Venom, head.Element);
            Assert.AreEqual(Element.Venom, arm.Element);
            Assert.Greater(head.Range, arm.Range, "A head graft should out-range an arm graft.");
        }

        [Test]
        public void Different_enemies_in_the_same_slot_yield_different_traits()
        {
            var venom = GraftResolver.Resolve(ArchetypeCatalog.Venomspitter, GraftSlot.Head, WeaponClass.Blade, 1);
            var spark = GraftResolver.Resolve(ArchetypeCatalog.Sparkmite, GraftSlot.Head, WeaponClass.Blade, 1);
            var impact = GraftResolver.Resolve(ArchetypeCatalog.Bloodcharger, GraftSlot.Head, WeaponClass.Blade, 1);

            Assert.Greater(venom.DotDamagePerSecond, 0f, "Venom should leave a damage-over-time.");
            Assert.AreEqual(0, venom.ChainCount);

            Assert.Greater(spark.ChainCount, 0, "Spark should chain.");
            Assert.AreEqual(0f, spark.DotDamagePerSecond);

            Assert.Greater(impact.Knockback, 0f, "Impact should knock back.");
        }

        [Test]
        public void Weapon_reshapes_delivery_without_changing_the_trait()
        {
            var blade = GraftResolver.Resolve(ArchetypeCatalog.Venomspitter, GraftSlot.LeftArm, WeaponClass.Blade, 1);
            var gun = GraftResolver.Resolve(ArchetypeCatalog.Venomspitter, GraftSlot.LeftArm, WeaponClass.Firearm, 1);

            Assert.AreEqual(1, blade.ProjectileCount, "A blade swings, it does not spray.");
            Assert.Greater(gun.ProjectileCount, 1, "A firearm turns the swipe into a spread.");
            Assert.Greater(gun.Range, blade.Range, "The firearm version should reach further.");
            Assert.Greater(blade.ArcDegrees, gun.ArcDegrees, "The blade version should sweep wider.");

            // Same trait on both chassis.
            Assert.AreEqual(Element.Venom, blade.Element);
            Assert.AreEqual(Element.Venom, gun.Element);
            Assert.Greater(blade.DotDamagePerSecond, 0f);
            Assert.Greater(gun.DotDamagePerSecond, 0f);
        }

        [Test]
        public void Reinforcing_a_graft_raises_its_damage()
        {
            var t1 = GraftResolver.Resolve(ArchetypeCatalog.Bloodcharger, GraftSlot.Head, WeaponClass.Blade, 1);
            var t3 = GraftResolver.Resolve(ArchetypeCatalog.Bloodcharger, GraftSlot.Head, WeaponClass.Blade, 3);
            Assert.Greater(t3.Damage, t1.Damage);
        }

        [Test]
        public void Every_archetype_slot_weapon_combination_resolves_to_something_usable()
        {
            foreach (var a in ArchetypeCatalog.All)
            {
                foreach (var slot in RunState.AllSlots)
                {
                    foreach (WeaponClass w in new[] { WeaponClass.Blade, WeaponClass.Firearm })
                    {
                        var spec = GraftResolver.Resolve(a, slot, w, 1);
                        Assert.Greater(spec.Damage, 0f, $"{a.Id}/{slot}/{w} had no damage");
                        Assert.Greater(spec.Cooldown, 0f, $"{a.Id}/{slot}/{w} had no cooldown");
                        Assert.Greater(spec.Range, 0f, $"{a.Id}/{slot}/{w} had no range");
                        Assert.LessOrEqual(spec.ArcDegrees, 360f, $"{a.Id}/{slot}/{w} arc exceeded a full circle");
                        Assert.IsNotEmpty(spec.DisplayName);
                    }
                }
            }
        }
    }
}
