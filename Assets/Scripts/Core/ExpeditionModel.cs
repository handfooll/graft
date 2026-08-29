using System;

namespace Graft.Core
{
    /// <summary>
    /// How long an expedition into each area actually takes, worked out from the numbers that are
    /// already authored - the spawn tables, the roster's health, the depth multipliers and the
    /// player's damage output.
    ///
    /// **This exists because the phase's headline number is not observable.** "A bar fills in about
    /// 150 seconds" is not something a screenshot can show and not something anyone can eyeball
    /// from five ScriptableObjects; it is an emergent product of four assets that were tuned in
    /// three different phases for three different reasons. Left uncomputed it would be an
    /// assumption, and Phase 3 has already been bitten once by an assumption of exactly this shape
    /// - the player who died in 0.47 s looked completely fine in every picture.
    ///
    /// It models the idealised case: the player is always shooting, always at something, and never
    /// walking between targets. Real expeditions are slower. That is the right way round - a model
    /// that flatters the tuning would let a grind through undetected.
    /// </summary>
    public sealed class ExpeditionModel
    {
        readonly SpawnSpec _spawns;
        readonly Func<EnemyFamily, EnemyStats> _statsFor;
        readonly EssenceRules _rules;
        readonly float _damagePerSecond;
        readonly float _capacity;

        public ExpeditionModel(SpawnSpec spawns, Func<EnemyFamily, EnemyStats> statsFor,
            float damagePerSecond, EssenceRules rules, float cargoCapacity)
        {
            _spawns = spawns ?? throw new ArgumentNullException(nameof(spawns));
            _statsFor = statsFor ?? throw new ArgumentNullException(nameof(statsFor));
            _rules = rules;
            _damagePerSecond = MathF.Max(0.01f, damagePerSecond);
            _capacity = MathF.Max(1f, cargoCapacity);
        }

        public AreaExpedition For(Area area)
        {
            var entry = _spawns.For(area);
            if (!entry.SpawnsAnything)
            {
                return AreaExpedition.Barren(area);
            }

            var total = entry.TotalWeight;
            var averageHealth = 0f;
            var averageCargo = 0f;
            var averageOutput = 0f;

            foreach (var family in EnemyFamilies.All)
            {
                var share = entry.WeightOf(family) / total;
                if (share <= 0f)
                {
                    continue;
                }

                var stats = _statsFor(family).Scaled(entry.StatMultiplier);
                averageHealth += share * stats.MaxHealth;
                averageOutput += share * stats.DamagePerSecond;
                // Averaged across the mix *after* quantising, not computed from the average
                // health: rounding one representative enemy is not the same as rounding each of
                // them, and this is the number the fill time actually comes out of.
                averageCargo += share * _rules.CargoFor(stats.MaxHealth);
            }

            // Two ceilings, and which one binds is the interesting part. Deep biomes are limited by
            // how fast the player can chew through health; shallow ones by how fast the area sends
            // anything at all. A biome held back by its spawn pacing fills slower than the design
            // intends no matter what the essence numbers say, and no other test would notice.
            var byDamage = _damagePerSecond / MathF.Max(0.01f, averageHealth);
            var bySpawning = 1f / entry.SecondsBetweenSpawns;

            var killsPerSecond = MathF.Min(byDamage, bySpawning);
            var cargoPerSecond = killsPerSecond * averageCargo;

            // Every shipped biome sends enemies at least as fast as the player can clear them, so
            // the population sits at the cap and the pressure on the player is simply the cap's
            // worth of output. A biome that did *not* - a spawn-limited one - would hover well
            // below its cap, which is the other half of why that flag is worth reporting.
            var population = bySpawning >= byDamage ? entry.PopulationCap : bySpawning / byDamage;

            return new AreaExpedition(
                area,
                averageHealth,
                averageCargo,
                killsPerSecond,
                spawnLimited: bySpawning < byDamage,
                secondsToFill: _capacity / MathF.Max(1e-4f, cargoPerSecond),
                killsToFill: _capacity / MathF.Max(1e-4f, averageCargo),
                incomingDamagePerSecond: population * averageOutput);
        }

        /// <summary>
        /// The slowest biome to fill a bar in, of those that spawn anything. What "the loop stays
        /// tight" has to be judged against - an average over five biomes would hide one grind.
        /// </summary>
        public AreaExpedition Slowest()
        {
            var worst = AreaExpedition.Barren(Area.Verge);
            foreach (var area in Areas.Biomes)
            {
                var run = For(area);
                if (run.SpawnsAnything && run.SecondsToFill > worst.SecondsToFill)
                {
                    worst = run;
                }
            }

            return worst;
        }
    }

    /// <summary>What one area's expedition costs and pays, per <see cref="ExpeditionModel"/>.</summary>
    public readonly struct AreaExpedition
    {
        public readonly Area Area;

        /// <summary>Mean health of what this area sends, depth multiplier included.</summary>
        public readonly float AverageEffectiveHealth;

        public readonly float AverageCargoPerKill;

        public readonly float KillsPerSecond;

        /// <summary>
        /// True when the area cannot send enemies as fast as the player can kill them. The player
        /// is standing around waiting, and the bar fills slower than the damage output implies.
        /// </summary>
        public readonly bool SpawnLimited;

        public readonly float SecondsToFill;
        public readonly float KillsToFill;

        /// <summary>
        /// Damage a standing player takes per second at this area's steady population - **the
        /// difficulty of the biome**, as one number.
        ///
        /// It is the composite of all three of DESIGN.md §7's dials at once: the population cap,
        /// the family mix's output, and the depth multiplier. Worth having as a single figure
        /// because none of the three is a difficulty measure on its own - a biome can send more of
        /// them, more often, and still be gentler than one that sends fewer harder ones.
        /// </summary>
        public readonly float IncomingDamagePerSecond;

        public AreaExpedition(Area area, float averageEffectiveHealth, float averageCargoPerKill,
            float killsPerSecond, bool spawnLimited, float secondsToFill, float killsToFill,
            float incomingDamagePerSecond)
        {
            Area = area;
            AverageEffectiveHealth = averageEffectiveHealth;
            AverageCargoPerKill = averageCargoPerKill;
            KillsPerSecond = killsPerSecond;
            SpawnLimited = spawnLimited;
            SecondsToFill = secondsToFill;
            KillsToFill = killsToFill;
            IncomingDamagePerSecond = incomingDamagePerSecond;
        }

        public static AreaExpedition Barren(Area area) =>
            new AreaExpedition(area, 0f, 0f, 0f, false, float.PositiveInfinity, float.PositiveInfinity, 0f);

        /// <summary>Seconds a healthy player survives standing still here. The other end of the dial.</summary>
        public float SecondsToKillAPlayer(float playerHealth) =>
            IncomingDamagePerSecond <= 0f ? float.PositiveInfinity : playerHealth / IncomingDamagePerSecond;

        public bool SpawnsAnything => KillsPerSecond > 0f;

        public float CargoPerSecond => KillsPerSecond * AverageCargoPerKill;

        public override string ToString() =>
            $"{Area}: {SecondsToFill:0} s to fill, {KillsToFill:0} kills, " +
            $"{KillsPerSecond:0.00}/s{(SpawnLimited ? " (spawn-limited)" : "")}";
    }
}
