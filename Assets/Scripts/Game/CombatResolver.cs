using System.Collections.Generic;
using UnityEngine;
using Graft.Core;

namespace Graft.Game
{
    /// <summary>
    /// Turns a resolved <see cref="AbilitySpec"/> into what actually happens to a target.
    /// All element behaviour lives here, so venom/spark/impact behave identically no matter
    /// which slot or weapon produced the hit -- which is the guarantee the GDD's three-axis
    /// design depends on.
    /// </summary>
    public static class CombatResolver
    {
        static readonly List<EnemyAgent> _scratch = new List<EnemyAgent>(64);

        public static void ApplyHit(RunContext ctx, AbilitySpec spec, float damage,
            EnemyAgent target, Vector2 direction)
        {
            if (target == null || !target.IsAlive) return;

            target.ApplyDamage(damage);

            if (spec.DotDamagePerSecond > 0f)
                target.ApplyDot(spec.DotDamagePerSecond, spec.DotDuration);

            if (spec.Knockback > 0f)
                target.ApplyKnockback(direction, spec.Knockback);

            if (spec.ChainCount > 0)
                Chain(ctx, spec, damage, target, spec.ChainCount);
        }

        /// <summary>Spark's jump. Each link is weaker and never re-hits the same target.</summary>
        static void Chain(RunContext ctx, AbilitySpec spec, float damage, EnemyAgent from, int remaining)
        {
            var hit = new HashSet<EnemyAgent> { from };
            var current = from;
            float linkDamage = damage;

            for (int i = 0; i < remaining; i++)
            {
                linkDamage *= 0.6f;
                EnemyAgent next = null;
                float bestSqr = 3.2f * 3.2f;

                Arena.Instance.CollectInRadius(current.transform.position, 3.2f, _scratch);
                for (int j = 0; j < _scratch.Count; j++)
                {
                    var candidate = _scratch[j];
                    if (hit.Contains(candidate)) continue;
                    float d = ((Vector2)candidate.transform.position -
                               (Vector2)current.transform.position).sqrMagnitude;
                    if (d < bestSqr) { bestSqr = d; next = candidate; }
                }

                if (next == null) break;
                AbilityFx.SpawnChainBolt(current.transform.position, next.transform.position,
                    Palette.For(spec.Element));
                next.ApplyDamage(linkDamage);
                if (spec.DotDamagePerSecond > 0f)
                    next.ApplyDot(spec.DotDamagePerSecond * 0.5f, spec.DotDuration);

                hit.Add(next);
                current = next;
            }
        }
    }
}
