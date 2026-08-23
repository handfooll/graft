using System;
using System.Collections.Generic;

namespace Graft.Core
{
    /// <summary>
    /// Everything true about the current 3-6 minute run (GDD S3). Owns no scene objects and
    /// touches no MonoBehaviour, so the whole build -- grafts, corruption, stats, scoring --
    /// is exercisable in plain unit tests.
    /// </summary>
    public sealed class RunState
    {
        public readonly VesselDefinition Vessel;
        public readonly VesselMastery Mastery;
        public readonly OwnedWeapon Weapon;
        public readonly PurityMeter Purity;
        public readonly EssenceWallet Essence;

        readonly Dictionary<GraftSlot, GraftInstance> _grafts = new Dictionary<GraftSlot, GraftInstance>();

        public float CurrentHP { get; private set; }
        public float Elapsed { get; private set; }
        public int Wave { get; set; } = 1;
        public int Kills { get; private set; }
        public bool IsDead => CurrentHP <= 0f;

        public event Action<GraftInstance, GraftInstance> GraftInstalled;
        public event Action StatsChanged;

        public static readonly GraftSlot[] AllSlots =
        {
            GraftSlot.Head, GraftSlot.Core, GraftSlot.LeftArm, GraftSlot.RightArm, GraftSlot.Legs
        };

        public RunState(VesselDefinition vessel, VesselMastery mastery, OwnedWeapon weapon)
        {
            Vessel = vessel ?? VesselCatalog.Scaleborn;
            Mastery = mastery ?? new VesselMastery(Vessel.Id);
            Weapon = weapon ?? new OwnedWeapon { DefinitionId = WeaponCatalog.RustEdge.Id, Tier = Rarity.Common };

            float resist = Vessel.CorruptionResistance + BaseStats.CorruptionResist;
            if (resist > 0.85f) resist = 0.85f;
            Purity = new PurityMeter(resist);
            Essence = new EssenceWallet();

            CurrentHP = EffectiveMaxHP;
        }

        /// <summary>Vessel baseline + mastery tree + every installed graft's passive bump.</summary>
        public StatBlock BaseStats
        {
            get
            {
                var s = Vessel.Baseline + Mastery.ToStatBlock();
                foreach (var kv in _grafts)
                {
                    s = s + kv.Value.Source.PassiveBonus;
                }
                s.Power += Weapon.Power;
                s.AttackSpeed *= Weapon.Definition.BaseAttackSpeed;
                return s;
            }
        }

        public float EffectiveMaxHP => BaseStats.MaxHP * Purity.MaxHPMultiplier;
        public float EffectivePower => BaseStats.Power * Purity.DamageMultiplier;
        public float EffectiveMoveSpeed => BaseStats.MoveSpeed;

        public IReadOnlyDictionary<GraftSlot, GraftInstance> Grafts => _grafts;

        public GraftInstance GraftIn(GraftSlot slot) =>
            _grafts.TryGetValue(slot, out var g) ? g : null;

        public int GraftCount => _grafts.Count;

        /// <summary>
        /// The level-up card: the same absorbed family offered into each of the five slots,
        /// so the choice is "where does this go", not "which number is bigger".
        /// </summary>
        public List<GraftOffer> BuildOffers(EnemyArchetype source)
        {
            var offers = new List<GraftOffer>(AllSlots.Length);
            float cost = Purity.PreviewCost(source);

            for (int i = 0; i < AllSlots.Length; i++)
            {
                var slot = AllSlots[i];
                var existing = GraftIn(slot);
                bool reinforce = existing != null && existing.Source.Id == source.Id;
                int tier = reinforce ? existing.Tier + 1 : 1;

                offers.Add(new GraftOffer
                {
                    Source = source,
                    Slot = slot,
                    Resolved = ResolveFor(source, slot, tier),
                    Replaces = reinforce ? null : existing,
                    IsReinforcement = reinforce,
                    CorruptionCost = cost
                });
            }
            return offers;
        }

        /// <summary>Resolution plus the two run-level modifiers combat must not forget:
        /// the Vessel's affinity and the corruption damage curve.</summary>
        public AbilitySpec ResolveFor(EnemyArchetype source, GraftSlot slot, int tier)
        {
            var spec = GraftResolver.Resolve(source, slot, Weapon.Definition.Class, tier);
            float mult = Vessel.DamageMultiplierFor(source.Element) * Purity.DamageMultiplier;
            spec.Damage *= mult;
            spec.DotDamagePerSecond *= mult;
            if (Purity.HasFocusBonus) spec.ProjectileSpeed *= Purity.PrecisionMultiplier;
            // Regenerate the blurb after scaling, or the card's sentence quotes the
            // pre-multiplier damage while the number beside it shows the real one.
            spec.Description = GraftResolver.Describe(spec);
            return spec;
        }

        /// <summary>The live spec for whatever currently sits in a slot, or null if empty.</summary>
        public AbilitySpec? SpecInSlot(GraftSlot slot)
        {
            var g = GraftIn(slot);
            if (g == null) return null;
            return ResolveFor(g.Source, slot, g.Tier);
        }

        public void InstallGraft(GraftOffer offer)
        {
            var previous = GraftIn(offer.Slot);
            var installed = offer.IsReinforcement && previous != null
                ? new GraftInstance(offer.Source, offer.Slot, previous.Tier + 1)
                : new GraftInstance(offer.Source, offer.Slot, 1);

            float hpFractionBefore = EffectiveMaxHP > 0f ? CurrentHP / EffectiveMaxHP : 1f;

            _grafts[offer.Slot] = installed;
            Purity.AddGraft(offer.Source);

            // Grafts change Max HP (hide passives, the corruption multiplier). Preserve the
            // *fraction* so taking a +HP graft never reads as suddenly being hurt.
            CurrentHP = hpFractionBefore * EffectiveMaxHP;

            GraftInstalled?.Invoke(installed, previous);
            StatsChanged?.Invoke();
        }

        public void Tick(float dt)
        {
            Elapsed += dt;

            float max = EffectiveMaxHP;
            if (CurrentHP > 0f)
            {
                CurrentHP += BaseStats.HPRegen * dt;

                float burn = Purity.SelfDamageFractionPerSecond;
                if (burn > 0f) CurrentHP -= max * burn * dt;

                if (CurrentHP > max) CurrentHP = max;
                if (CurrentHP < 0f) CurrentHP = 0f;
            }
        }

        public void TakeDamage(float amount)
        {
            if (amount <= 0f) return;
            CurrentHP -= amount;
            if (CurrentHP < 0f) CurrentHP = 0f;
        }

        public void Heal(float amount)
        {
            CurrentHP += amount;
            float max = EffectiveMaxHP;
            if (CurrentHP > max) CurrentHP = max;
        }

        public void RegisterKill(EnemyArchetype archetype, int essence = 1)
        {
            Kills++;
            Essence.Add(archetype, essence);
        }

        /// <summary>Run reward. Deeper waves and a dirtier hero both pay more, which is the
        /// economic half of the greed mechanic.</summary>
        public int SoftGoldEarned()
        {
            float baseGold = 12f * Wave + 3f * Kills;
            float risk = 1f + 0.5f * Purity.Corruption;
            return (int)(baseGold * risk);
        }
    }
}
