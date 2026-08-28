using Graft.Core;
using UnityEngine;
using UnityEngine.AI;

namespace Graft.Game
{
    /// <summary>
    /// One enemy: a NavMeshAgent that walks at the player and hits them when it arrives.
    ///
    /// The pacing and the arithmetic are <c>Graft.Core</c>'s - <see cref="AttackClock"/> decides
    /// when a blow lands, <see cref="EnemyStats"/> says how hard, <see cref="HealthPool"/> takes it.
    /// What lives here is only what needs an engine: pathing, facing, and the fact that it is made
    /// of primitives for now.
    ///
    /// **Pathing is the whole point of using a NavMeshAgent rather than steering toward the
    /// player.** Phase 3's gate asks for enemies seen to path *around* obstacles rather than
    /// through them, and steering cannot do it - it walks into the first rock and pushes.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(Damageable))]
    public class EnemyAgent : MonoBehaviour
    {
        NavMeshAgent _agent;
        Damageable _damageable;
        HitFlash _flash;
        HealthBar _bar;
        CapsuleCollider _body;

        EnemyProfile _profile;
        EnemyStats _stats;
        CombatTuning _tuning;
        CombatEffects _effects;
        EnemyRegistry _registry;
        Transform _player;
        Damageable _playerHealth;

        AttackClock _clock;
        float _dyingFor = -1f;

        public EnemyFamily Family => _profile != null ? _profile.family : EnemyFamily.Venom;

        public EnemyStats Stats => _stats;

        public Damageable Health => _damageable;

        public NavMeshAgent Agent => _agent;

        /// <summary>Height to aim a shot at - partway up the body, not at its feet.</summary>
        public float AimHeight => _profile != null ? _profile.height * 0.55f : 0.6f;

        /// <summary>Alive, on the field, and worth spending a shot on.</summary>
        public bool IsTargetable => _dyingFor < 0f && _damageable != null && _damageable.IsAlive
                                    && isActiveAndEnabled;

        /// <summary>Dying enemies are still drawn for a moment, but nothing should shoot them.</summary>
        public bool IsDying => _dyingFor >= 0f;

        public Vec2 GroundPosition => new Vec2(transform.position.x, transform.position.z);

        void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _damageable = GetComponent<Damageable>();
            // Written out rather than with ??, because UnityEngine.Object overloads == and a
            // component that has been destroyed is not null to the null-coalescing operator.
            _flash = GetComponent<HitFlash>();
            if (_flash == null)
            {
                _flash = gameObject.AddComponent<HitFlash>();
            }

            _bar = GetComponent<HealthBar>();
            if (_bar == null)
            {
                _bar = gameObject.AddComponent<HealthBar>();
            }

            _body = GetComponent<CapsuleCollider>();
            if (_body == null)
            {
                _body = gameObject.AddComponent<CapsuleCollider>();
            }
        }

        /// <summary>
        /// Puts this object into service as one member of <paramref name="profile"/>'s family, with
        /// <paramref name="stats"/> already scaled for the depth it is being spawned at.
        ///
        /// Everything is re-read rather than assumed, because these objects are pooled: the thing
        /// being configured here was very probably a different family a moment ago.
        /// </summary>
        public void Configure(EnemyProfile profile, EnemyStats stats, CombatTuning tuning,
            CombatEffects effects, EnemyRegistry registry, Transform player, Damageable playerHealth)
        {
            _profile = profile;
            _stats = stats;
            _tuning = tuning;
            _effects = effects;
            _registry = registry;
            _player = player;
            _playerHealth = playerHealth;

            name = string.IsNullOrEmpty(profile.displayName) ? profile.family.ToString() : profile.displayName;

            var parts = EnemyRig.Build(gameObject, profile);
            _flash.Bind(parts, tuning.hitFlashSeconds);

            _body.height = profile.height;
            _body.radius = profile.width * 0.5f;
            _body.center = new Vector3(0f, profile.height * 0.5f, 0f);

            _agent.speed = stats.MoveSpeed;
            _agent.acceleration = stats.MoveSpeed * 4f;
            // Fast enough to face a player who has just cut past, slow enough that the turn is a
            // visible movement rather than a teleport of the silhouette. Well under the player's
            // 1440 deg/s, which is a deliberate advantage: turning is how you get away.
            _agent.angularSpeed = 520f;
            _agent.radius = Mathf.Max(0.15f, profile.width * 0.5f);
            _agent.height = Mathf.Max(0.3f, profile.height);
            // Stop short of the target by its own reach, so the agent brakes at attack range
            // instead of pushing into the player and attacking from inside them.
            _agent.stoppingDistance = Mathf.Max(0.4f, stats.AttackRange * 0.75f);
            _agent.autoBraking = true;
            // Priorities are spread so a crowd converging on one point does not deadlock with every
            // agent equally entitled to the same metre of ground.
            _agent.avoidancePriority = 40 + Random.Range(0, 20);

            _damageable.ClearListeners();
            _damageable.ResetTo(stats.MaxHealth);
            _damageable.Damaged += OnDamaged;
            _damageable.Died += OnDied;

            _bar.Bind(_damageable, tuning, profile.height);

            _clock = default;
            _dyingFor = -1f;
            _flash.SetDim(1f);

            _registry?.Register(this);
        }

        void Update()
        {
            if (_dyingFor >= 0f)
            {
                Collapse();
                return;
            }

            if (_player == null || _profile == null || !_agent.isOnNavMesh)
            {
                return;
            }

            var toPlayer = _player.position - transform.position;
            toPlayer.y = 0f;
            var distance = toPlayer.magnitude;
            var inRange = distance <= _stats.AttackRange;

            if (inRange)
            {
                // Stop where it stands rather than continuing to the stopping distance. A ranged
                // family that kept closing after it could already shoot would collapse the
                // difference between Spark and everything else.
                _agent.isStopped = true;
                Face(toPlayer);
            }
            else
            {
                _agent.isStopped = false;
                _agent.SetDestination(_player.position);
            }

            if (_clock.Tick(Time.deltaTime, inRange, _stats.AttackInterval))
            {
                Attack();
            }
        }

        void Face(Vector3 toPlayer)
        {
            if (toPlayer.sqrMagnitude < 1e-4f)
            {
                return;
            }

            // Turned by hand while stopped: NavMeshAgent only steers rotation while it is moving,
            // so an enemy standing at attack range would otherwise keep whatever heading it
            // happened to arrive on and hit the player side-on.
            var wanted = Quaternion.LookRotation(toPlayer.normalized, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation, wanted, _agent.angularSpeed * Time.deltaTime);
        }

        void Attack()
        {
            if (_playerHealth == null || !_playerHealth.IsAlive)
            {
                return;
            }

            if (_stats.IsRanged && _effects != null)
            {
                var muzzle = transform.position + Vector3.up * AimHeight + transform.forward * (_profile.width * 0.6f);
                _effects.FireEnemyShot(muzzle, _playerHealth, 0.75f, _stats.Damage);
                return;
            }

            // Contact families deal their damage directly. A projectile crossing a metre and a half
            // would arrive on a frame the swing had already finished on, and read as a delay.
            _playerHealth.ApplyDamage(_stats.Damage);
        }

        void OnDamaged(Damageable damageable, float amount)
        {
            if (amount > 0f)
            {
                _flash.Flash();
            }
        }

        void OnDied(Damageable damageable)
        {
            _dyingFor = 0f;
            _agent.isStopped = true;
            _body.enabled = false;
            // Off the NavMesh immediately, so a corpse spends its last third of a second out of the
            // way of the enemies still coming rather than as an obstacle they crowd around.
            if (_agent.isOnNavMesh)
            {
                _agent.ResetPath();
            }
        }

        /// <summary>Sinks and dims over <c>deathSeconds</c>, then hands itself back to the pool.</summary>
        void Collapse()
        {
            var span = _tuning != null ? _tuning.deathSeconds : 0.35f;
            _dyingFor += Time.deltaTime;
            var t = Mathf.Clamp01(_dyingFor / Mathf.Max(0.05f, span));

            _flash.SetDim(1f - t);
            transform.localScale = new Vector3(1f, Mathf.Lerp(1f, 0.15f, t), 1f);

            if (t >= 1f)
            {
                Retire();
            }
        }

        /// <summary>
        /// Takes this enemy off the field and makes it available for reuse. Called at the end of a
        /// death, when the spawner recycles something the player has walked away from, and when the
        /// tour clears the field between captures.
        /// </summary>
        public void Retire()
        {
            _registry?.Unregister(this);
            _damageable.ClearListeners();

            if (_agent != null && _agent.isOnNavMesh)
            {
                _agent.ResetPath();
            }

            if (_agent != null)
            {
                _agent.enabled = false;
            }

            transform.localScale = Vector3.one;
            _dyingFor = -1f;
            gameObject.SetActive(false);
        }

        /// <summary>
        /// Puts a recycled object back on the NavMesh at <paramref name="position"/>.
        ///
        /// <c>Warp</c> rather than assigning the transform: a NavMeshAgent moved by transform is
        /// still internally at its old position, and its first frame back is spent walking to catch
        /// up with itself - visibly, across whatever distance it was recycled over.
        /// </summary>
        public bool PlaceAt(Vector3 position)
        {
            gameObject.SetActive(true);
            transform.position = position;

            _agent.enabled = true;
            if (!_agent.Warp(position))
            {
                // Nothing to stand on. The spawner picked the point off a ring around the player
                // and the ring can cross the edge of the world, so this is an ordinary outcome
                // rather than an error - the caller simply tries again next interval.
                gameObject.SetActive(false);
                return false;
            }

            _agent.isStopped = false;
            _body.enabled = true;
            return true;
        }
    }
}
