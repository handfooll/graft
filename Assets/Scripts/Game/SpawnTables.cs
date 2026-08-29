using System;
using Graft.Core;
using UnityEngine;

namespace Graft.Game
{
    /// <summary>
    /// What each area sends at you. BUILD_PLAN.md Phase 3: "spawning driven by which biome the
    /// player is in (ScriptableObject spawn tables)".
    ///
    /// The escalation here is DESIGN.md §7's difficulty dial made concrete - cap, pace and stat
    /// multiplier all climb with distance from Base. What makes each biome a *different* fight
    /// rather than a harder one is the family mix: the Verge teaches two families, each middle
    /// biome specialises, and Ember Scar sends everything.
    ///
    /// Base is present and empty. It is never consulted - <see cref="SpawnDirector"/> refuses to
    /// spawn there whatever this asset says - but leaving the row out would make "Base spawns
    /// nothing" look like an oversight rather than a decision.
    /// </summary>
    [CreateAssetMenu(fileName = "SpawnTables", menuName = "GRAFT/Spawn Tables")]
    public class SpawnTables : ScriptableObject
    {
        [Header("Where things appear")]
        [Tooltip("Metres from the player. The inner radius must stay outside the frame - the " +
                 "camera shows about 9.5 m across, and anything appearing inside that reads as a " +
                 "bug rather than as something that walked up.")]
        [Range(8f, 40f)] public float ringInnerRadius = 13f;

        [Range(10f, 60f)] public float ringOuterRadius = 22f;

        [Tooltip("How far behind you an enemy may fall before it is recycled. Comfortably past " +
                 "the ring, or enemies are deleted as fast as they are made.")]
        [Range(20f, 120f)] public float despawnRadius = 46f;

        public int seed = 20260824;

        [Header("Per area")]
        [SerializeField] AreaSpawnEntry[] areas = DefaultAreas();

        public AreaSpawnEntry[] Areas => areas;

        public SpawnSpec ToSpec()
        {
            var entries = new AreaSpawn[areas.Length];
            for (var i = 0; i < areas.Length; i++)
            {
                entries[i] = areas[i].ToEntry();
            }

            return new SpawnSpec
            {
                Areas = entries,
                RingInnerRadius = ringInnerRadius,
                RingOuterRadius = Mathf.Max(ringInnerRadius + 1f, ringOuterRadius),
                DespawnRadius = Mathf.Max(ringOuterRadius + 4f, despawnRadius),
            };
        }

        public AreaSpawnEntry EntryFor(Area area)
        {
            foreach (var entry in areas)
            {
                if (entry.area == area)
                {
                    return entry;
                }
            }

            throw new System.Collections.Generic.KeyNotFoundException($"SpawnTables has no entry for {area}.");
        }

        /// <summary>
        /// The escalation, and the reason each biome feels like somewhere rather than something.
        ///
        /// | Area | Cap | Every | Stats | Sends |
        /// |---|---|---|---|---|
        /// | Verge | 4 | 1.15 s | x1.00 | mostly Venom, some Impact - two families, gently |
        /// | Rust Flats | 6 | 1.6 s | x1.35 | Impact country: slow, heavy, hits hard |
        /// | Glass Mire | 7 | 1.5 s | x1.70 | Spark: the first place that shoots back |
        /// | Bone Reach | 8 | 1.3 s | x2.05 | Hide: nothing dies quickly |
        /// | Ember Scar | 10 | 1.0 s | x2.40 | all four, and more of them |
        /// </summary>
        static AreaSpawnEntry[] DefaultAreas() => new[]
        {
            new AreaSpawnEntry
            {
                area = Area.Base, displayName = "The Anchor",
                populationCap = 0, secondsBetweenSpawns = 2f, statMultiplier = 1f,
                venom = 0f, impact = 0f, hide = 0f, spark = 0f,
            },
            new AreaSpawnEntry
            {
                // 1.15 s, down from Phase 3's 1.9. **Changed by Phase 4, and worth knowing why.**
                //
                // At 1.9 s the Verge was the one biome in the game that could not send enemies as
                // fast as the player killed them - 0.53 kills/s against a capability of 0.84 - so
                // the player stood waiting for about a third of every expedition. That was
                // invisible until cargo existed: it made the Verge take 238 s to fill a bar against
                // the ~145 s of every other biome, which is the tutorial biome being the slowest
                // grind in the game, and the exact opposite of what the difficulty gradient says.
                //
                // Nothing about the danger changes much - four Chitters at x1.00 still need twelve
                // seconds to empty a 225-health player - and the fix has to be here rather than in
                // the essence numbers, because a spawn-limited biome fills slowly whatever a mote
                // is worth. Revert this one field if the Verge turns out to feel crowded; the cost
                // of reverting is that its expedition goes back to being 60% longer than the rest.
                area = Area.Verge, displayName = "The Verge",
                populationCap = 4, secondsBetweenSpawns = 1.15f, statMultiplier = 1.00f,
                venom = 0.70f, impact = 0.30f, hide = 0f, spark = 0f,
            },
            new AreaSpawnEntry
            {
                area = Area.RustFlats, displayName = "Rust Flats",
                populationCap = 6, secondsBetweenSpawns = 1.6f, statMultiplier = 1.35f,
                venom = 0.35f, impact = 0.65f, hide = 0f, spark = 0f,
            },
            new AreaSpawnEntry
            {
                area = Area.GlassMire, displayName = "Glass Mire",
                populationCap = 7, secondsBetweenSpawns = 1.5f, statMultiplier = 1.70f,
                venom = 0.35f, impact = 0f, hide = 0.05f, spark = 0.60f,
            },
            new AreaSpawnEntry
            {
                area = Area.BoneReach, displayName = "Bone Reach",
                populationCap = 8, secondsBetweenSpawns = 1.3f, statMultiplier = 2.05f,
                venom = 0f, impact = 0.35f, hide = 0.60f, spark = 0.05f,
            },
            new AreaSpawnEntry
            {
                area = Area.EmberScar, displayName = "Ember Scar",
                populationCap = 10, secondsBetweenSpawns = 1.0f, statMultiplier = 2.40f,
                venom = 0.15f, impact = 0.30f, hide = 0.20f, spark = 0.35f,
            },
        };

        /// <summary>See the note on <see cref="EnemyRoster.ApplyDefaults"/> - defaults apply once.</summary>
        public void ApplyDefaults() => areas = DefaultAreas();
    }

    [Serializable]
    public class AreaSpawnEntry
    {
        public Area area;
        public string displayName = "";

        [Tooltip("Alive at once near the player. The camera sees roughly 250 m² of ground, so a " +
                 "cap of ten is a screen that is genuinely busy without being a wall.")]
        [Range(0, 24)] public int populationCap = 5;

        [Range(0.2f, 6f)] public float secondsBetweenSpawns = 1.5f;

        [Tooltip("Multiplies health and damage - never speed. Distance from Base is the difficulty " +
                 "dial (§7), and this is the dial.")]
        [Range(0.5f, 6f)] public float statMultiplier = 1f;

        [Header("Family mix (relative weights)")]
        [Range(0f, 1f)] public float venom;
        [Range(0f, 1f)] public float impact;
        [Range(0f, 1f)] public float hide;
        [Range(0f, 1f)] public float spark;

        public AreaSpawn ToEntry() => new AreaSpawn(area, populationCap, secondsBetweenSpawns, statMultiplier,
            new[] { venom, impact, hide, spark });
    }
}
