using System;
using System.Collections.Generic;
using Graft.Core;
using UnityEngine;

namespace Graft.Game
{
    /// <summary>
    /// The four enemy families, as an asset (BUILD_PLAN.md rule 3: data in ScriptableObjects).
    /// One asset holding four entries rather than four assets, for the same reason
    /// <see cref="WorldLayout"/> holds six palettes: the set is fixed by DESIGN.md §5, and a set
    /// that must be complete is easier to check when it is one file.
    ///
    /// Colours **and** silhouettes. Colour-coding alone survives a screenshot only until two
    /// families end up on the same ground - acid green on the Verge's moss is exactly that case -
    /// so <see cref="EnemyRig"/> gives each family a shape as well, on the same reasoning that gave
    /// each biome a prop family in Phase 2.
    /// </summary>
    [CreateAssetMenu(fileName = "EnemyRoster", menuName = "GRAFT/Enemy Roster")]
    public class EnemyRoster : ScriptableObject
    {
        [SerializeField] EnemyProfile[] families = DefaultFamilies();

        public IReadOnlyList<EnemyProfile> Families => families;

        public EnemyProfile ProfileFor(EnemyFamily family)
        {
            foreach (var profile in families)
            {
                if (profile.family == family)
                {
                    return profile;
                }
            }

            throw new KeyNotFoundException($"EnemyRoster has no profile for {family}.");
        }

        /// <summary>
        /// Four families that do not feel alike. The spread is the design: a swarm you can ignore
        /// individually, a single hit that actually hurts, something that will not die, and
        /// something that shoots from outside the range you can answer at without moving.
        ///
        /// **No family is faster than the player** (5.5 m/s). DESIGN.md §3 makes the walk home the
        /// game, and a walk home you cannot outrun is not a decision. <c>EnemyRosterTests</c>
        /// checks that against the movement asset rather than against a copied number.
        /// </summary>
        static EnemyProfile[] DefaultFamilies() => new[]
        {
            new EnemyProfile
            {
                family = EnemyFamily.Venom, displayName = "Chitter",
                maxHealth = 22f, moveSpeed = 4.2f, damage = 4f, attackInterval = 0.9f, attackRange = 1.5f,
                height = 0.85f, width = 0.95f,
                body = new Color(0.42f, 0.78f, 0.24f),
                accent = new Color(0.83f, 0.96f, 0.32f),
                accentEmissive = false,
            },
            new EnemyProfile
            {
                family = EnemyFamily.Impact, displayName = "Breaker",
                maxHealth = 55f, moveSpeed = 2.6f, damage = 16f, attackInterval = 1.8f, attackRange = 2.0f,
                height = 1.65f, width = 1.30f,
                body = new Color(0.78f, 0.26f, 0.14f),
                accent = new Color(1.00f, 0.63f, 0.20f),
                accentEmissive = false,
            },
            new EnemyProfile
            {
                family = EnemyFamily.Hide, displayName = "Shellback",
                maxHealth = 95f, moveSpeed = 2.2f, damage = 8f, attackInterval = 1.4f, attackRange = 1.8f,
                height = 1.20f, width = 1.55f,
                body = new Color(0.70f, 0.62f, 0.47f),
                accent = new Color(0.31f, 0.26f, 0.21f),
                accentEmissive = false,
            },
            new EnemyProfile
            {
                family = EnemyFamily.Spark, displayName = "Lantern",
                maxHealth = 30f, moveSpeed = 3.4f, damage = 7f, attackInterval = 1.35f, attackRange = 6.0f,
                height = 1.75f, width = 0.70f,
                // The one family that glows. It is also the only one that can hurt you from
                // outside your own reach, so being the thing your eye goes to first is correct.
                body = new Color(0.40f, 0.35f, 0.72f),
                accent = new Color(0.36f, 0.92f, 1.00f),
                accentEmissive = true,
            },
        };

        /// <summary>
        /// Stamps the defaults back over the asset. A ScriptableObject's field defaults apply
        /// exactly once, when the file is first created (GOTCHAS.md), so without this an edit to
        /// the numbers above would change nothing and report nothing.
        /// </summary>
        public void ApplyDefaults() => families = DefaultFamilies();
    }

    /// <summary>One family: what it is made of, what colour it is, and what it does to you.</summary>
    [Serializable]
    public class EnemyProfile
    {
        public EnemyFamily family;

        [Tooltip("Shown in logs and used to name the spawned objects, so a scene hierarchy during " +
                 "a fight is readable.")]
        public string displayName = "";

        [Header("Stats")]
        [Range(5f, 400f)] public float maxHealth = 30f;

        [Tooltip("Metres per second. Must stay under the player's max speed - see the note on " +
                 "EnemyRoster. Depth scaling never touches this.")]
        [Range(0.5f, 5.4f)] public float moveSpeed = 3f;

        [Range(0f, 60f)] public float damage = 8f;

        [Range(0.2f, 4f)] public float attackInterval = 1.2f;

        [Tooltip("Metres. At or above EnemyStats.RangedThreshold the family shoots instead of " +
                 "closing, which is what makes Spark a different problem rather than a recolour.")]
        [Range(0.5f, 12f)] public float attackRange = 1.8f;

        [Header("Silhouette")]
        [Range(0.4f, 3f)] public float height = 1.2f;
        [Range(0.3f, 2.5f)] public float width = 1f;

        [Header("Colour")]
        public Color body = Color.grey;
        public Color accent = Color.white;

        [Tooltip("Only Spark should glow. One emissive family in a lit world is a signal; two is " +
                 "a light show, and the same trap Phase 2 hit with Ember Scar's vents.")]
        public bool accentEmissive;

        [Header("Materials")]
        [Tooltip("Filled in by ProjectSetup from MaterialLibrary. Referenced from the asset rather " +
                 "than created at runtime, so the shaders survive an iOS build's shader stripping.")]
        public Material bodyMaterial;
        public Material accentMaterial;

        public EnemyStats Stats => new EnemyStats(maxHealth, moveSpeed, damage, attackInterval, attackRange);
    }
}
