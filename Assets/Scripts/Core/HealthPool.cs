using System;

namespace Graft.Core
{
    /// <summary>
    /// A pool of hit points, as a value. Kept in <c>Graft.Core</c> because "what happens when
    /// something is hit" is a rule and rules stay headlessly testable - and because Phase 6's
    /// corruption self-damage tick (DESIGN.md §6) and Phase 4's death-on-expedition both run
    /// through exactly this arithmetic.
    ///
    /// Immutable on purpose. Damage returns a new pool rather than mutating one, so a MonoBehaviour
    /// can raise its "I was damaged" event with the before and after in hand and cannot forget to.
    /// </summary>
    public readonly struct HealthPool
    {
        public readonly float Current;
        public readonly float Maximum;

        public HealthPool(float maximum, float current)
        {
            Maximum = MathF.Max(1f, maximum);
            Current = Math.Clamp(current, 0f, Maximum);
        }

        public static HealthPool Full(float maximum) => new HealthPool(maximum, maximum);

        public bool IsAlive => Current > 0f;

        /// <summary>0 to 1. What the health bar draws.</summary>
        public float Fraction => Current / Maximum;

        /// <summary>
        /// Applies damage, floored at zero. Negative amounts are ignored rather than healing:
        /// healing is a separate verb with separate rules, and quietly accepting a negative here
        /// would make one typo in a graft table into an unkillable enemy.
        /// </summary>
        public HealthPool Damaged(float amount) =>
            amount <= 0f ? this : new HealthPool(Maximum, Current - amount);

        public HealthPool Healed(float amount) =>
            amount <= 0f ? this : new HealthPool(Maximum, Current + amount);

        /// <summary>Seconds to empty this pool at a given rate. Used to sanity-check tuning.</summary>
        public float SecondsToEmptyAt(float damagePerSecond) =>
            damagePerSecond <= 0f ? float.PositiveInfinity : Current / damagePerSecond;

        public override string ToString() => $"{Current:0.#}/{Maximum:0.#}";
    }
}
