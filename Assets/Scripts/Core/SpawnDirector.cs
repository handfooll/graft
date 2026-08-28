using System;

namespace Graft.Core
{
    /// <summary>
    /// Decides when something spawns, what it is, and where it appears - driven by which area the
    /// player is standing in (BUILD_PLAN.md Phase 3).
    ///
    /// Keying off the player's area rather than off each enemy's own area is deliberate. In a
    /// continuous world the two disagree constantly, right where the biomes meet, and "what am I
    /// fighting" should be answered by where *I* am: walking into Ember Scar should feel like
    /// walking into Ember Scar from the first step, not once you have reached its middle.
    ///
    /// Pure C#, and deterministic given a seed, so the same fight can be rebuilt for a screenshot.
    /// </summary>
    public sealed class SpawnDirector
    {
        readonly SpawnSpec _spec;
        readonly Random _rng;
        float _cooldown;

        public SpawnDirector(SpawnSpec spec, int seed)
        {
            _spec = spec ?? throw new ArgumentNullException(nameof(spec));
            _rng = new Random(seed);
        }

        public SpawnSpec Spec => _spec;

        /// <summary>Seconds until the next spawn is allowed. Exposed so tests can see the pacing.</summary>
        public float Cooldown => _cooldown;

        /// <summary>
        /// Advances the director and, at most once per call, produces one thing to spawn.
        ///
        /// Returns false in Base, always - DESIGN.md §7 makes it the safe hub, and that is a rule
        /// rather than a tuning value, so it is enforced here and not left to whoever fills in the
        /// spawn table.
        /// </summary>
        public bool TryProduce(Area area, int liveCount, float deltaTime, Vec2 playerPosition, out SpawnOrder order)
        {
            order = default;

            if (!Areas.IsBiome(area))
            {
                // Standing in Base also resets the clock, so stepping back out does not immediately
                // dump a backlog of enemies onto the plaza edge.
                _cooldown = 0f;
                return false;
            }

            var entry = _spec.For(area);
            if (!entry.SpawnsAnything)
            {
                return false;
            }

            if (deltaTime > 0f)
            {
                _cooldown = MathF.Max(0f, _cooldown - deltaTime);
            }

            if (liveCount >= entry.PopulationCap || _cooldown > 0f)
            {
                return false;
            }

            _cooldown = entry.SecondsBetweenSpawns;
            order = new SpawnOrder(PickFamily(entry), PickPoint(playerPosition), entry.StatMultiplier);
            return true;
        }

        /// <summary>
        /// Whether an enemy has been left far enough behind to recycle. Distance from the player,
        /// not from where it spawned: what matters is that the world in front of you is populated
        /// and the world you have walked away from costs nothing.
        /// </summary>
        public bool ShouldRecycle(Vec2 enemy, Vec2 player) =>
            (enemy - player).SqrMagnitude > _spec.DespawnRadius * _spec.DespawnRadius;

        EnemyFamily PickFamily(AreaSpawn entry)
        {
            var roll = (float)_rng.NextDouble() * entry.TotalWeight;
            foreach (var family in EnemyFamilies.All)
            {
                roll -= entry.WeightOf(family);
                if (roll <= 0f)
                {
                    return family;
                }
            }

            // Only reachable through floating-point drift on the last subtraction.
            return LastWeighted(entry);
        }

        static EnemyFamily LastWeighted(AreaSpawn entry)
        {
            var found = EnemyFamily.Venom;
            foreach (var family in EnemyFamilies.All)
            {
                if (entry.WeightOf(family) > 0f)
                {
                    found = family;
                }
            }

            return found;
        }

        /// <summary>
        /// A point on a ring around the player, at a uniformly random bearing. A ring rather than a
        /// disc because the inner radius is the whole point - it is what keeps things from
        /// appearing on screen - and area-uniform sampling inside a disc would put most of them at
        /// the outer edge anyway.
        /// </summary>
        Vec2 PickPoint(Vec2 playerPosition)
        {
            var bearing = (float)_rng.NextDouble() * 360f;
            var radius = _spec.RingInnerRadius +
                         (float)_rng.NextDouble() * MathF.Max(0f, _spec.RingOuterRadius - _spec.RingInnerRadius);
            return playerPosition + Vec2.FromHeading(bearing, radius);
        }
    }

    /// <summary>One thing to make. The engine side turns this into a GameObject and nothing more.</summary>
    public readonly struct SpawnOrder
    {
        public readonly EnemyFamily Family;

        /// <summary>Where to put it, on the ground plane. May not be walkable - the caller checks.</summary>
        public readonly Vec2 Position;

        public readonly float StatMultiplier;

        public SpawnOrder(EnemyFamily family, Vec2 position, float statMultiplier)
        {
            Family = family;
            Position = position;
            StatMultiplier = statMultiplier;
        }
    }
}
