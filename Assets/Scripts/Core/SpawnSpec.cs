using System;

namespace Graft.Core
{
    /// <summary>
    /// The authored answer to "what lives where, and how much of it". Plain data with no engine in
    /// it, exactly as <see cref="WorldMapSpec"/> is - the ScriptableObject that holds these numbers
    /// lives in <c>Graft.Game</c> and hands one of these over, so the rules that consume them stay
    /// testable without an editor.
    /// </summary>
    public sealed class SpawnSpec
    {
        /// <summary>One entry per area. Base is present and empty; §7 says it has no enemies.</summary>
        public AreaSpawn[] Areas = Array.Empty<AreaSpawn>();

        /// <summary>
        /// Metres from the player that a new enemy appears at. The inner radius has to be outside
        /// the frame: the camera shows about 9.5 m across, and something that pops into existence
        /// inside that reads as a bug rather than as something that walked up.
        /// </summary>
        public float RingInnerRadius = 13f;

        public float RingOuterRadius = 22f;

        /// <summary>
        /// How far an enemy may get from the player before it is recycled. Larger than the ring, or
        /// enemies are deleted as fast as they are made; far enough that walking twenty metres and
        /// turning round does not find an empty world.
        /// </summary>
        public float DespawnRadius = 46f;

        public AreaSpawn For(Area area)
        {
            foreach (var entry in Areas)
            {
                if (entry.Area == area)
                {
                    return entry;
                }
            }

            return AreaSpawn.Empty(area);
        }
    }

    /// <summary>What one area spawns. Every field here is a difficulty dial (DESIGN.md §7).</summary>
    public readonly struct AreaSpawn
    {
        public readonly Area Area;

        /// <summary>How many may be alive at once near the player.</summary>
        public readonly int PopulationCap;

        public readonly float SecondsBetweenSpawns;

        /// <summary>Multiplies health and damage, never speed. See <see cref="EnemyStats.Scaled"/>.</summary>
        public readonly float StatMultiplier;

        /// <summary>Relative frequency per family, indexed by <see cref="EnemyFamilies.IndexOf"/>.</summary>
        readonly float[] _familyWeights;

        public AreaSpawn(Area area, int populationCap, float secondsBetweenSpawns, float statMultiplier,
            float[] familyWeights)
        {
            Area = area;
            PopulationCap = Math.Max(0, populationCap);
            SecondsBetweenSpawns = MathF.Max(0.05f, secondsBetweenSpawns);
            StatMultiplier = MathF.Max(0.01f, statMultiplier);
            _familyWeights = familyWeights ?? new float[EnemyFamilies.Count];
        }

        public static AreaSpawn Empty(Area area) =>
            new AreaSpawn(area, 0, 1f, 1f, new float[EnemyFamilies.Count]);

        public float WeightOf(EnemyFamily family)
        {
            var index = EnemyFamilies.IndexOf(family);
            return index >= 0 && index < _familyWeights.Length ? MathF.Max(0f, _familyWeights[index]) : 0f;
        }

        public float TotalWeight
        {
            get
            {
                var total = 0f;
                foreach (var family in EnemyFamilies.All)
                {
                    total += WeightOf(family);
                }

                return total;
            }
        }

        /// <summary>An area with a cap but no weights would tick forever producing nothing.</summary>
        public bool SpawnsAnything => PopulationCap > 0 && TotalWeight > 0f;
    }
}
