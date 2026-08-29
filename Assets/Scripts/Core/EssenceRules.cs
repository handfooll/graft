using System;

namespace Graft.Core
{
    /// <summary>
    /// How much essence a kill is worth, in cargo.
    ///
    /// **A mote is worth what it cost you.** The drop is proportional to the *effective* health of
    /// the thing that died - its family's health times the depth multiplier - rather than being a
    /// flat amount per kill. That one choice is what makes the spec's headline number true:
    ///
    /// <code>
    /// cargo per second  =  kills per second  x  cargo per kill
    ///                   =  (dps / effective health)  x  (effective health x cargoPerEnemyHealth)
    ///                   =  dps x cargoPerEnemyHealth
    /// </code>
    ///
    /// The effective health cancels. So the bar fills at a rate set by the player's damage output
    /// and by nothing else - the same 150 seconds in every biome, which is exactly what D-P4.4 asks
    /// for: *depth changes payout, not fill rate.* Depth is then felt entirely through
    /// <see cref="EssenceValues"/>, which is the difficulty dial DESIGN.md §7 already has.
    ///
    /// The flat alternative - one mote per kill wherever you are - was measured and is worse in
    /// both directions. It makes the Verge fill in 76 s and Bone Reach in 239 s, so the deepest,
    /// most dangerous biomes become the slowest way to fill a bar as well as the hardest, and a
    /// Shellback with four times a Chitter's health pays the same as one. That is a grind wearing a
    /// difficulty gradient's clothes.
    ///
    /// Motes are **quantised**: every mote costs exactly <see cref="CargoPerMote"/>, and a kill
    /// drops however many of them its health earns. Big kills therefore shower essence and small
    /// ones drop one, which is the reward legible on screen rather than in a number - and because
    /// the capacity is a whole multiple of the mote, the bar can actually reach exactly full, which
    /// <see cref="Cargo.IsFull"/> depends on.
    /// </summary>
    public readonly struct EssenceRules
    {
        /// <summary>Cargo consumed by one mote. Every mote in the game is the same size.</summary>
        public readonly float CargoPerMote;

        /// <summary>Cargo earned per point of effective enemy health. The whole exchange rate.</summary>
        public readonly float CargoPerEnemyHealth;

        /// <summary>
        /// Ceiling on how many objects one death may put on the ground. Nothing in the shipped
        /// tables comes close - the biggest is a depth-scaled Shellback at eleven - but a graft
        /// table or a tuning slip that made one enemy enormous should cost a shower of motes, not
        /// a frame drop.
        /// </summary>
        public readonly int MaxMotesPerKill;

        public EssenceRules(float cargoPerMote, float cargoPerEnemyHealth, int maxMotesPerKill = 24)
        {
            CargoPerMote = MathF.Max(0.01f, cargoPerMote);
            CargoPerEnemyHealth = MathF.Max(0.0001f, cargoPerEnemyHealth);
            MaxMotesPerKill = Math.Max(1, maxMotesPerKill);
        }

        /// <summary>
        /// How many motes something of this effective health drops. Always at least one: a kill
        /// that pays nothing teaches the player to walk past that family, and there is no family in
        /// the game they should be walking past.
        /// </summary>
        public int MotesFor(float effectiveMaxHealth)
        {
            var wanted = MathF.Max(0f, effectiveMaxHealth) * CargoPerEnemyHealth / CargoPerMote;
            return Math.Clamp((int)MathF.Round(wanted), 1, MaxMotesPerKill);
        }

        /// <summary>Cargo one kill of this size is worth, after quantising into whole motes.</summary>
        public float CargoFor(float effectiveMaxHealth) => MotesFor(effectiveMaxHealth) * CargoPerMote;

        /// <summary>
        /// Seconds of sustained shooting to fill a bar of <paramref name="capacity"/> - the
        /// identity at the top of this file, before quantising rounds individual kills up or down.
        /// This is the number D-P4.4 is about, and it does not mention depth anywhere.
        /// </summary>
        public float SecondsToFill(float capacity, float damagePerSecond) =>
            damagePerSecond <= 0f
                ? float.PositiveInfinity
                : capacity / (damagePerSecond * CargoPerEnemyHealth);

        /// <summary>
        /// The capacity that fills in <paramref name="seconds"/> at this damage output, rounded to
        /// a whole number of motes so <see cref="Cargo.IsFull"/> stays reachable. What
        /// <c>EssenceTuning</c>'s default capacity is derived from rather than guessed at.
        /// </summary>
        public float CapacityForFillTime(float seconds, float damagePerSecond)
        {
            var wanted = MathF.Max(1f, seconds) * MathF.Max(0.01f, damagePerSecond) * CargoPerEnemyHealth;
            return MathF.Max(1f, MathF.Round(wanted / CargoPerMote)) * CargoPerMote;
        }
    }
}
