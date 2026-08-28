using Graft.Core;
using Graft.Game;
using UnityEngine;

namespace Graft.Editor
{
    /// <summary>
    /// Puts the fight into the World scene, and gives every combat asset the materials it needs.
    ///
    /// The materials are the part worth explaining. They are created here, as real assets, and
    /// *referenced* from the ScriptableObjects - rather than made at runtime out of
    /// <c>Shader.Find</c>. An iOS build strips every shader nothing in a scene or an asset points
    /// at, so a material built at runtime finds its shader missing on the device and renders
    /// magenta, or renders nothing. On the machine it was written on it looks perfect. D8 is
    /// iOS-first; that includes the failure modes.
    /// </summary>
    public static class CombatBuilder
    {
        /// <summary>
        /// Builds the combat objects and returns the root. Called by
        /// <see cref="BootstrapSceneBuilder"/> after the world and the player exist, because every
        /// one of these needs one or the other.
        /// </summary>
        public static GameObject Build(GameObject player, WorldRegions regions, CombatTuning tuning,
            EnemyRoster roster, SpawnTables tables)
        {
            // Note that the materials are NOT created here. They are made by ApplyMaterials before
            // the scene build starts - see the note on that method for why doing it from inside the
            // build does not stick.
            var root = new GameObject("Combat");

            var registry = root.AddComponent<EnemyRegistry>();

            var effects = root.AddComponent<CombatEffects>();
            effects.Tuning = tuning;

            var spawner = root.AddComponent<EnemySpawner>();
            spawner.Tables = tables;
            spawner.Roster = roster;
            spawner.Tuning = tuning;
            spawner.Regions = regions;
            spawner.Registry = registry;
            spawner.Effects = effects;
            spawner.Player = player.transform;

            // The player's half. A Damageable first: PlayerVitals requires it, and PlayerCombat's
            // shots need something on the other side to hurt.
            player.AddComponent<Damageable>();

            var vitals = player.AddComponent<PlayerVitals>();
            vitals.Tuning = tuning;
            vitals.Regions = regions;

            var combat = player.AddComponent<PlayerCombat>();
            combat.Tuning = tuning;
            combat.Registry = registry;
            combat.Effects = effects;

            return root;
        }

        /// <summary>
        /// Creates the flat materials the fight is drawn with and hangs them off the assets that
        /// use them.
        ///
        /// **Call this before the scene build, not from inside it, and save straight afterwards.**
        /// Materials created part-way through <see cref="BootstrapSceneBuilder.Build"/> - after the
        /// <c>NewScene</c> and before the <c>SaveScene</c> - do not survive to disk: the .mat files
        /// never appear and every reference to them serialises as a null. Nothing reports it. The
        /// scene saves, <c>ConfigureAll</c> exits zero, and the first sign of trouble is a fight
        /// drawn entirely in Unity's default white.
        ///
        /// The existing world materials hid this for a whole phase, because every one of them was
        /// already on disk from the run that first made them and so never took the create path.
        ///
        /// Three of the five glow. Shots, impacts and the health-bar fill all have to stay legible
        /// against ground that is sometimes near-black (Ember Scar) and sometimes bright
        /// (Bone Reach), and they have to stay legible in shadow - an unlit material under a
        /// 38-degree sun is the same colour as the thing shadowing it. Emission is the only
        /// property that survives all of that, which is the same reason Ember Scar's seams use it.
        /// </summary>
        public static void ApplyMaterials(CombatTuning tuning, EnemyRoster roster)
        {
            if (tuning != null)
            {
                tuning.playerProjectileMaterial =
                    MaterialLibrary.GetOrCreate("M_ShotPlayer", new Color(1.00f, 0.86f, 0.42f), true);
                tuning.enemyProjectileMaterial =
                    MaterialLibrary.GetOrCreate("M_ShotEnemy", new Color(0.42f, 0.92f, 1.00f), true);
                tuning.hitFlashMaterial =
                    MaterialLibrary.GetOrCreate("M_HitFlash", new Color(1.00f, 0.97f, 0.88f), true);
                tuning.healthBarFillMaterial =
                    MaterialLibrary.GetOrCreate("M_HealthBarFill", tuning.healthBarFill, true);
                // The one that does not glow. It is a hole punched behind the fill so the fill has
                // an edge; a glowing backdrop would make a full bar and an empty one look alike.
                tuning.healthBarBackdropMaterial =
                    MaterialLibrary.GetOrCreate("M_HealthBarBack", tuning.healthBarBackdrop);

                UnityEditor.EditorUtility.SetDirty(tuning);
            }

            if (roster == null)
            {
                return;
            }

            foreach (var profile in roster.Families)
            {
                var family = profile.family.ToString();
                profile.bodyMaterial = MaterialLibrary.GetOrCreate("M_Enemy" + family, profile.body);
                profile.accentMaterial = MaterialLibrary.GetOrCreate(
                    "M_Enemy" + family + "Accent", profile.accent, profile.accentEmissive);
            }

            UnityEditor.EditorUtility.SetDirty(roster);
        }

        /// <summary>
        /// Sanity check on the authored numbers, run at configure time so a bad edit is caught by
        /// the person who made it rather than by a screenshot three phases later.
        ///
        /// It only warns. The tests in <c>EnemyRosterTests</c> are what actually hold the line -
        /// this is here because <c>ConfigureAll</c> is what someone runs after changing a value,
        /// and a warning in that log is the shortest path between a mistake and noticing it.
        /// </summary>
        public static void WarnOnSuspectTuning(CombatTuning tuning, EnemyRoster roster,
            MovementTuning movement, CameraFraming framing)
        {
            if (tuning != null && framing != null)
            {
                var fraction = tuning.RangeAsFractionOfScreen(framing);
                if (fraction > 0.75f)
                {
                    Debug.LogWarning(
                        $"[GRAFT] Auto-attack reach is {fraction:P0} of the visible width. Past ~75% " +
                        "the player is shooting things that are off screen, and the game gets played " +
                        "by watching the health bars instead of the world.");
                }
            }

            if (roster == null || movement == null)
            {
                return;
            }

            foreach (var profile in roster.Families)
            {
                if (profile.moveSpeed >= movement.maxSpeed)
                {
                    Debug.LogWarning(
                        $"[GRAFT] {profile.displayName} moves at {profile.moveSpeed} m/s against the " +
                        $"player's {movement.maxSpeed}. DESIGN.md §3 makes the walk home the game; " +
                        "a walk home you cannot outrun is not a decision.");
                }
            }
        }
    }
}
