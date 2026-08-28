using Graft.Core;
using UnityEngine;

namespace Graft.Game
{
    /// <summary>
    /// The player's auto-attack. BUILD_PLAN.md Phase 3: "player auto-attacks nearest target in
    /// range" - one sentence whose two hard parts both live in <c>Graft.Core</c>, where a test can
    /// reach them: <see cref="Targeting"/> picks who, <see cref="AttackClock"/> decides when.
    ///
    /// **There is no fire button, and there is not going to be one.** D8 makes this a one-thumb
    /// game and that thumb is already holding the stick. Attacking automatically is what leaves
    /// positioning as the only thing the player is actually doing - which is what makes the walk
    /// home (DESIGN.md §3) a decision rather than a chore.
    /// </summary>
    public class PlayerCombat : MonoBehaviour
    {
        [SerializeField] CombatTuning tuning;
        [SerializeField] EnemyRegistry registry;
        [SerializeField] CombatEffects effects;

        AttackClock _clock;
        EnemyAgent _target;

        public CombatTuning Tuning { get => tuning; set => tuning = value; }
        public EnemyRegistry Registry { get => registry; set => registry = value; }
        public CombatEffects Effects { get => effects; set => effects = value; }

        /// <summary>Who is being shot at. Null when nothing is in range.</summary>
        public EnemyAgent Target => _target;

        /// <summary>Metres from the muzzle. Read by the tour, and by the tests that judge reach.</summary>
        public float Range => tuning != null ? tuning.attackRange : 0f;

        void Update()
        {
            if (tuning == null || registry == null)
            {
                return;
            }

            _target = Acquire();

            if (_clock.Tick(Time.deltaTime, _target != null, tuning.attackInterval))
            {
                Fire();
            }
        }

        /// <summary>
        /// Keeps the current target while it stays roughly in range, and otherwise takes the
        /// nearest.
        ///
        /// The stickiness is what stops the auto-attack sliding onto whatever has drifted half a
        /// metre closer between two frames. In a crowd that produces a stream of shots spread
        /// across four enemies that all survive, instead of four kills - which reads as the weapon
        /// being weak rather than as the targeting being indecisive.
        /// </summary>
        EnemyAgent Acquire()
        {
            var here = new Vec2(transform.position.x, transform.position.z);

            if (_target != null && _target.IsTargetable &&
                Targeting.StaysAcquired(here, _target.GroundPosition, tuning.attackRange))
            {
                return _target;
            }

            var index = Targeting.NearestWithin(here, registry.TargetablePositions(), tuning.attackRange);
            return registry.At(index);
        }

        void Fire()
        {
            if (_target == null || effects == null)
            {
                return;
            }

            // Fired from chest height and slightly forward, so the bolt leaves the silhouette
            // rather than appearing out of the middle of it.
            var forward = _target.transform.position - transform.position;
            forward.y = 0f;
            forward = forward.sqrMagnitude > 1e-4f ? forward.normalized : transform.forward;

            var muzzle = transform.position + Vector3.up * 0.85f + forward * 0.5f;
            effects.FirePlayerShot(muzzle, _target.Health, _target.AimHeight, tuning.attackDamage);
        }
    }
}
