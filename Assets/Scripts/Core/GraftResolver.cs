namespace Graft.Core
{
    /// <summary>
    /// The GDD's central rule, in one place (S4 + S7):
    ///   the enemy <b>type</b> decides the ability,
    ///   the body <b>slot</b> decides how it is delivered,
    ///   the equipped <b>weapon</b> decides what shape that delivery takes.
    /// Everything downstream reads the resolved <see cref="AbilitySpec"/>, so adding a new
    /// enemy family automatically yields five new abilities per weapon without new code.
    /// </summary>
    public static class GraftResolver
    {
        public static Delivery DeliveryForSlot(GraftSlot slot)
        {
            switch (slot)
            {
                case GraftSlot.Head: return Delivery.Aimed;
                case GraftSlot.Core: return Delivery.Aura;
                case GraftSlot.LeftArm:
                case GraftSlot.RightArm: return Delivery.Swipe;
                case GraftSlot.Legs: return Delivery.Dash;
                default: return Delivery.Aura;
            }
        }

        public static AbilitySpec Resolve(EnemyArchetype archetype, GraftSlot slot, WeaponClass weapon, int tier)
        {
            var delivery = DeliveryForSlot(slot);
            var spec = new AbilitySpec
            {
                Element = archetype.Element,
                Delivery = delivery,
                Weapon = weapon,
                ArcDegrees = 60f,
                ProjectileCount = 1,
                ProjectileSpeed = 9f
            };

            ApplyDelivery(ref spec, delivery);
            ApplyElement(ref spec, archetype.Element);
            ApplyWeapon(ref spec, weapon);

            // Tier is how many times this graft has been reinforced this run.
            float tierScale = 1f + 0.35f * (tier - 1);
            spec.Damage *= tierScale;
            spec.DotDamagePerSecond *= tierScale;

            spec.DisplayName = NameFor(archetype, slot, weapon);
            spec.Description = Describe(spec);
            return spec;
        }

        static void ApplyDelivery(ref AbilitySpec s, Delivery delivery)
        {
            switch (delivery)
            {
                case Delivery.Aimed:
                    // Head: fires at the nearest target on a timer, longest reach, tightest arc.
                    s.Damage = 9f;
                    s.Cooldown = 0.85f;
                    s.Range = 7.5f;
                    s.ArcDegrees = 18f;
                    break;
                case Delivery.Swipe:
                    // Arms: close, fast, wide. Two arms can each hold one, so they stack.
                    s.Damage = 13f;
                    s.Cooldown = 0.6f;
                    s.Range = 2.3f;
                    s.ArcDegrees = 100f;
                    break;
                case Delivery.Aura:
                    // Core: no aim at all, pulses around the hero. Weakest per hit, hits everything.
                    s.Damage = 6f;
                    s.Cooldown = 1.4f;
                    s.Range = 3.0f;
                    s.ArcDegrees = 360f;
                    break;
                case Delivery.Dash:
                    // Legs: banked until the player dashes, then dumped along the path.
                    s.Damage = 18f;
                    s.Cooldown = 2.6f;
                    s.Range = 4.5f;
                    s.ArcDegrees = 140f;
                    break;
            }
        }

        static void ApplyElement(ref AbilitySpec s, Element element)
        {
            switch (element)
            {
                case Element.Venom:
                    // Trades up-front damage for a tick that keeps working after you move on.
                    s.Damage *= 0.65f;
                    s.DotDamagePerSecond = 4.5f;
                    s.DotDuration = 3.0f;
                    break;
                case Element.Impact:
                    s.Damage *= 1.3f;
                    s.Knockback = 5.5f;
                    s.Cooldown *= 1.15f;
                    break;
                case Element.Hide:
                    // Mostly a passive: a hide graft is the GDD's example of "+MaxHP, no active".
                    s.Damage *= 0.35f;
                    s.Cooldown *= 1.4f;
                    break;
                case Element.Spark:
                    s.Damage *= 0.8f;
                    s.ChainCount = 2;
                    s.Cooldown *= 0.7f;
                    break;
                case Element.Hollow:
                    s.Damage *= 1.6f;
                    s.DotDamagePerSecond = 3f;
                    s.DotDuration = 2f;
                    s.ChainCount = 1;
                    s.Knockback = 3f;
                    break;
            }
        }

        static void ApplyWeapon(ref AbilitySpec s, WeaponClass weapon)
        {
            if (weapon == WeaponClass.Blade)
            {
                // A blade turns everything into reach and sweep: the GDD's "claw-graft on a
                // bladed weapon becomes an extended melee slash".
                s.ArcDegrees *= 1.35f;
                s.Damage *= 1.2f;
                s.Range *= s.Delivery == Delivery.Aimed ? 0.55f : 1.25f;
                s.ProjectileCount = 1;
                s.ProjectileSpeed *= 1.4f;
            }
            else
            {
                // A firearm turns everything into projectiles: the same graft "becomes a
                // spread of thrown projectiles".
                s.ProjectileCount = s.Delivery == Delivery.Aura ? 4 : 3;
                s.Damage *= 0.62f;   // per projectile, so total output stays comparable
                s.Range *= 1.45f;
                s.ArcDegrees *= 0.85f;
            }

            if (s.ArcDegrees > 360f) s.ArcDegrees = 360f;
        }

        static string NameFor(EnemyArchetype a, GraftSlot slot, WeaponClass weapon)
        {
            string part;
            switch (slot)
            {
                case GraftSlot.Head: part = "Maw"; break;
                case GraftSlot.Core: part = "Heart"; break;
                case GraftSlot.LeftArm: part = "Left Claw"; break;
                case GraftSlot.RightArm: part = "Right Claw"; break;
                default: part = "Haunches"; break;
            }
            string chassis = weapon == WeaponClass.Blade ? "Edged" : "Flung";
            return chassis + " " + a.DisplayName.Replace("spitter", "").Replace("charger", "")
                   .Replace("brute", "").Replace("mite", "").Trim() + " " + part;
        }

        public static string Describe(AbilitySpec s)
        {
            string how;
            switch (s.Delivery)
            {
                case Delivery.Aimed: how = "Fires at the nearest enemy"; break;
                case Delivery.Swipe: how = "Sweeps a melee arc"; break;
                case Delivery.Aura: how = "Pulses around you"; break;
                default: how = "Discharges along your dash"; break;
            }
            string extra = "";
            if (s.DotDamagePerSecond > 0f) extra += " Leaves poison.";
            if (s.ChainCount > 0) extra += " Chains to " + s.ChainCount + " more.";
            if (s.Knockback > 0f) extra += " Knocks back.";
            if (s.ProjectileCount > 1) extra += " " + s.ProjectileCount + " projectiles.";
            return how + " for " + s.Damage.ToString("0") + "." + extra;
        }
    }
}
