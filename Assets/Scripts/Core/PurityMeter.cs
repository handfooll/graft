using System;

namespace Graft.Core
{
    /// <summary>
    /// The greed mechanic (GDD S4). Every graft pushes Corruption up. Staying low keeps a
    /// small "focus" edge; going high trades that for raw power and, past a threshold, a
    /// self-damage tick that makes over-extending a real risk rather than a free upgrade.
    /// </summary>
    public sealed class PurityMeter
    {
        public const float TaintedThreshold = 0.34f;
        public const float MonstrousThreshold = 0.67f;
        /// <summary>Above this, the hero starts burning its own health.</summary>
        public const float SelfDamageThreshold = 0.85f;

        const float BaseCorruptionPerGraft = 0.14f;

        /// <summary>0 = fully human, 1 = fully monstrous.</summary>
        public float Corruption { get; private set; }

        /// <summary>Vessel + mastery resistance. 0.5 means grafts corrupt at half rate.</summary>
        public float Resistance { get; set; }

        public event Action<PurityBand, PurityBand> BandChanged;

        public PurityMeter(float resistance = 0f)
        {
            Resistance = resistance;
            Corruption = 0f;
        }

        public PurityBand Band => BandFor(Corruption);

        public static PurityBand BandFor(float corruption)
        {
            if (corruption >= MonstrousThreshold) return PurityBand.Monstrous;
            if (corruption >= TaintedThreshold) return PurityBand.Tainted;
            return PurityBand.Focused;
        }

        /// <summary>How much a single graft would add, after resistance. Shown on the offer card.</summary>
        public float PreviewCost(EnemyArchetype archetype)
        {
            // Boss essence is the GDD's "everything, but corrupts twice as fast".
            float raw = BaseCorruptionPerGraft * (archetype.Element == Element.Hollow ? 2f : 1f);
            float resisted = raw * (1f - Clamp01(Resistance));
            return resisted;
        }

        public void AddGraft(EnemyArchetype archetype)
        {
            Add(PreviewCost(archetype));
        }

        public void Add(float amount)
        {
            var before = Band;
            Corruption = Clamp01(Corruption + amount);
            var after = Band;
            if (before != after) BandChanged?.Invoke(before, after);
        }

        /// <summary>Focused only: sharper aim and a faster reroll. Lost the moment you tip over.</summary>
        public bool HasFocusBonus => Band == PurityBand.Focused;

        public float PrecisionMultiplier => HasFocusBonus ? 1.15f : 1.0f;
        public float RerollCooldownMultiplier => HasFocusBonus ? 0.6f : 1.0f;

        /// <summary>Raw power scales continuously with corruption, so the trade is gradual.</summary>
        public float DamageMultiplier => 1f + 0.60f * Corruption;
        public float MaxHPMultiplier => 1f + 0.40f * Corruption;

        /// <summary>Fraction of max HP burned per second once past the self-damage threshold.</summary>
        public float SelfDamageFractionPerSecond
        {
            get
            {
                if (Corruption < SelfDamageThreshold) return 0f;
                float over = (Corruption - SelfDamageThreshold) / (1f - SelfDamageThreshold);
                return 0.008f + 0.022f * over;
            }
        }

        /// <summary>Drives the visible mutation: 0 = human silhouette, 1 = full monster.</summary>
        public float MutationAmount => Corruption;

        public void ResetRun() { Corruption = 0f; }

        static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
    }
}
