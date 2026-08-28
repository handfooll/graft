using System;
using Graft.Core;
using UnityEngine;

namespace Graft.Game
{
    /// <summary>
    /// Anything that can be hurt. One component on the player and one on every enemy, so a shot
    /// does not have to know what it hit - which is what lets the same projectile serve both
    /// directions, and what Phase 5's corruption self-damage tick (DESIGN.md §6) will hurt through.
    ///
    /// The arithmetic is <see cref="HealthPool"/>'s, over in <c>Graft.Core</c> where a test can
    /// reach it. This class owns only the things that need an engine: when it happened, and who to
    /// tell.
    /// </summary>
    public class Damageable : MonoBehaviour
    {
        HealthPool _pool = HealthPool.Full(1f);

        /// <summary>Raised on every hit that landed, with the damage actually taken.</summary>
        public event Action<Damageable, float> Damaged;

        /// <summary>Raised once, on the hit that emptied the pool. Never raised twice.</summary>
        public event Action<Damageable> Died;

        public HealthPool Pool => _pool;

        public bool IsAlive => _pool.IsAlive;

        public float Fraction => _pool.Fraction;

        /// <summary>
        /// Refills to a new maximum. Called when a pooled enemy is reused, so a Chitter recycled
        /// into a Breaker does not come back with 22 hit points and the wrong silhouette.
        /// </summary>
        public void ResetTo(float maximum)
        {
            _pool = HealthPool.Full(maximum);
        }

        /// <summary>
        /// Applies damage and reports whether this was the killing blow.
        ///
        /// Returns false for a hit on something already dead. A dying enemy is still on screen for
        /// <c>deathSeconds</c> while it collapses, which is easily long enough for a second shot
        /// that was already in flight to arrive - and without this guard that shot raises
        /// <see cref="Died"/> a second time, and the spawner recycles an enemy it has already
        /// recycled.
        /// </summary>
        public bool ApplyDamage(float amount)
        {
            if (!_pool.IsAlive || amount <= 0f)
            {
                return false;
            }

            _pool = _pool.Damaged(amount);
            Damaged?.Invoke(this, amount);

            if (_pool.IsAlive)
            {
                return false;
            }

            Died?.Invoke(this);
            return true;
        }

        public void Heal(float amount)
        {
            if (!_pool.IsAlive || amount <= 0f)
            {
                return;
            }

            var before = _pool.Current;
            _pool = _pool.Healed(amount);
            if (!Mathf.Approximately(before, _pool.Current))
            {
                // Healing raises Damaged with zero so a health bar has one thing to listen to
                // rather than two. A bar that redrew on damage but not on healing would sit empty
                // all the way back to Base and then snap full on the next scratch.
                Damaged?.Invoke(this, 0f);
            }
        }

        /// <summary>
        /// Drops every listener. A pooled enemy is subscribed to afresh each time it is spawned,
        /// and without this the subscriptions from its previous life are still attached - so the
        /// health bar of a Chitter that died two minutes ago redraws when this one is hit.
        /// </summary>
        public void ClearListeners()
        {
            Damaged = null;
            Died = null;
        }
    }
}
