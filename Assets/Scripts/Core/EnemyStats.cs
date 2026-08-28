using System;

namespace Graft.Core
{
    /// <summary>
    /// One enemy family's numbers, with no engine anywhere near them. The authored values live in
    /// <c>Graft.Game.EnemyRoster</c>; this is the shape they are consumed in, and the place the
    /// rules about them are enforced.
    /// </summary>
    public readonly struct EnemyStats
    {
        /// <summary>Attack range at or beyond which a family shoots instead of closing.</summary>
        public const float RangedThreshold = 3.5f;

        public readonly float MaxHealth;

        /// <summary>
        /// Metres per second. **Never scaled by depth** - see <see cref="Scaled"/>. DESIGN.md §3
        /// makes the walk home the game, and a walk home you cannot outrun is not a walk, it is a
        /// death sentence with extra steps.
        /// </summary>
        public readonly float MoveSpeed;

        public readonly float Damage;

        /// <summary>Seconds between attacks.</summary>
        public readonly float AttackInterval;

        /// <summary>Metres at which this family attacks - contact for most, standoff for Spark.</summary>
        public readonly float AttackRange;

        public EnemyStats(float maxHealth, float moveSpeed, float damage, float attackInterval, float attackRange)
        {
            MaxHealth = MathF.Max(1f, maxHealth);
            MoveSpeed = MathF.Max(0.1f, moveSpeed);
            Damage = MathF.Max(0f, damage);
            AttackInterval = MathF.Max(0.05f, attackInterval);
            AttackRange = MathF.Max(0.2f, attackRange);
        }

        /// <summary>A family that shoots rather than closing to touching distance.</summary>
        public bool IsRanged => AttackRange >= RangedThreshold;

        public float DamagePerSecond => Damage / AttackInterval;

        /// <summary>
        /// Depth scaling. Health and damage only: those are what make a deep biome expensive.
        /// Speed is deliberately left alone, so the player can always disengage and walk out of
        /// Ember Scar exactly as fast as they walked out of the Verge.
        /// </summary>
        public EnemyStats Scaled(float multiplier)
        {
            multiplier = MathF.Max(0.01f, multiplier);
            return new EnemyStats(MaxHealth * multiplier, MoveSpeed, Damage * multiplier, AttackInterval, AttackRange);
        }
    }
}
