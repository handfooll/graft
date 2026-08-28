using System.Collections.Generic;
using Graft.Core;
using UnityEngine;
using UnityEngine.AI;

namespace Graft.Game
{
    /// <summary>
    /// Turns <see cref="SpawnDirector"/>'s decisions into objects. BUILD_PLAN.md Phase 3:
    /// "spawning driven by which biome the player is in (ScriptableObject spawn tables)".
    ///
    /// The decisions themselves are not made here - what to send, how often, and where on the ring
    /// are all answered in <c>Graft.Core</c>, where <c>SpawnDirectorTests</c> can hold them still.
    /// This class owns the two questions that need an engine: is the point the director picked
    /// somewhere an enemy can actually stand, and which pooled object gets used.
    /// </summary>
    public class EnemySpawner : MonoBehaviour
    {
        [SerializeField] SpawnTables tables;
        [SerializeField] EnemyRoster roster;
        [SerializeField] CombatTuning tuning;
        [SerializeField] WorldRegions regions;
        [SerializeField] EnemyRegistry registry;
        [SerializeField] CombatEffects effects;
        [SerializeField] Transform player;

        /// <summary>
        /// How far off the director's chosen point the NavMesh may be found. Generous, because the
        /// ring is drawn in flat world coordinates with no knowledge of where the ground is - but
        /// not unbounded, or a spawn aimed at the void quietly walks itself back to the nearest
        /// walkable tile and something appears in the player's face.
        /// </summary>
        const float NavMeshSearchRadius = 5f;

        readonly Stack<EnemyAgent> _idle = new Stack<EnemyAgent>();

        SpawnDirector _director;
        Damageable _playerHealth;
        Transform _pool;

        /// <summary>
        /// Off switch for the screenshot tour, which poses its fights by hand. A gate image posed
        /// by a live difficulty director is a different image every run.
        /// </summary>
        public bool Running { get; set; } = true;

        public SpawnTables Tables { get => tables; set { tables = value; _director = null; } }
        public EnemyRoster Roster { get => roster; set => roster = value; }
        public CombatTuning Tuning { get => tuning; set => tuning = value; }
        public WorldRegions Regions { get => regions; set => regions = value; }
        public EnemyRegistry Registry { get => registry; set => registry = value; }
        public CombatEffects Effects { get => effects; set => effects = value; }
        public Transform Player { get => player; set => player = value; }

        /// <summary>The area the tables are currently being read for - the player's, not anyone else's.</summary>
        public Area CurrentArea { get; private set; } = Area.Base;

        public SpawnDirector Director =>
            _director ??= tables != null ? new SpawnDirector(tables.ToSpec(), tables.seed) : null;

        void Awake()
        {
            _pool = new GameObject("Enemies").transform;
            _pool.SetParent(transform, false);

            if (player != null)
            {
                _playerHealth = player.GetComponent<Damageable>();
            }
        }

        void Update()
        {
            if (player == null || regions == null || registry == null || Director == null)
            {
                return;
            }

            CurrentArea = regions.AreaAt(player.position);

            RecycleTheLeftBehind();

            if (!Running)
            {
                return;
            }

            var here = new Vec2(player.position.x, player.position.z);
            if (Director.TryProduce(CurrentArea, registry.Count, Time.deltaTime, here, out var order))
            {
                Spawn(order.Family, new Vector3(order.Position.X, 0f, order.Position.Y), order.StatMultiplier);
            }
        }

        /// <summary>
        /// Spawns one enemy at or near <paramref name="wanted"/>, scaled by
        /// <paramref name="statMultiplier"/>. Returns null if there was nowhere to put it.
        ///
        /// Public because the screenshot tour builds its fights with it: the gate needs a specific
        /// arrangement of specific families, and going through the same path the game uses is what
        /// stops the picture being of something the game cannot actually produce.
        /// </summary>
        public EnemyAgent Spawn(EnemyFamily family, Vector3 wanted, float statMultiplier)
        {
            if (roster == null || tuning == null || registry == null)
            {
                return null;
            }

            if (!TryGround(wanted, out var position))
            {
                return null;
            }

            var profile = roster.ProfileFor(family);
            var agent = _idle.Count > 0 ? _idle.Pop() : NewAgent();

            if (!agent.PlaceAt(position))
            {
                _idle.Push(agent);
                return null;
            }

            agent.Configure(profile, profile.Stats.Scaled(statMultiplier), tuning, effects, registry,
                player, _playerHealth);
            return agent;
        }

        /// <summary>
        /// Finds a spot an enemy can stand on, near where the director asked.
        ///
        /// The Base rejection is the interesting half. <see cref="SpawnDirector"/> already refuses
        /// to spawn while the player is standing in Base, but the spawn ring is 13–22 m across and
        /// the player can be one step outside the plaza - so without this, walking the Verge's edge
        /// puts enemies on the safe hub's paving. DESIGN.md §7 makes Base safe as a place, not as a
        /// player state.
        /// </summary>
        bool TryGround(Vector3 wanted, out Vector3 position)
        {
            position = wanted;

            if (!NavMesh.SamplePosition(wanted, out var hit, NavMeshSearchRadius, NavMesh.AllAreas))
            {
                return false;
            }

            if (regions != null && regions.AreaAt(hit.position) == Area.Base)
            {
                return false;
            }

            position = hit.position;
            return true;
        }

        /// <summary>
        /// Retires anything the player has left far enough behind. Keeping the world in front of
        /// you populated costs nothing if the world behind you is not.
        /// </summary>
        void RecycleTheLeftBehind()
        {
            var here = new Vec2(player.position.x, player.position.z);
            var live = registry.Live;

            for (var i = live.Count - 1; i >= 0; i--)
            {
                var agent = live[i];
                if (agent == null || agent.IsDying)
                {
                    continue;
                }

                if (Director.ShouldRecycle(agent.GroundPosition, here))
                {
                    agent.Retire();
                    _idle.Push(agent);
                }
            }
        }

        EnemyAgent NewAgent()
        {
            var host = new GameObject("Enemy");
            host.transform.SetParent(_pool, false);

            var navAgent = host.AddComponent<NavMeshAgent>();
            navAgent.enabled = false;

            host.AddComponent<Damageable>();
            var agent = host.AddComponent<EnemyAgent>();

            host.SetActive(false);
            return agent;
        }

        /// <summary>Hands a retired enemy back to the pool. Called by the tour when it clears up.</summary>
        public void Reclaim(EnemyAgent agent)
        {
            if (agent != null)
            {
                _idle.Push(agent);
            }
        }
    }
}
