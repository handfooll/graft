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
            EnemyRoster roster, SpawnTables tables, EssenceTuning essence)
        {
            // Note that the materials are NOT created here. They are made by ApplyMaterials before
            // the scene build starts - see the note on that method for why doing it from inside the
            // build does not stick.
            var root = new GameObject("Combat");

            var registry = root.AddComponent<EnemyRegistry>();

            var effects = root.AddComponent<CombatEffects>();
            effects.Tuning = tuning;

            // The player's half. A Damageable first: PlayerVitals requires it, and PlayerCombat's
            // shots need something on the other side to hurt.
            player.AddComponent<Damageable>();

            // Cargo before anything that reads it. PlayerCargo owns the ExtractionLedger, and the
            // magnet, the bank, the death rule and the HUD all reach for that one object.
            var cargo = player.AddComponent<PlayerCargo>();
            cargo.Tuning = essence;
            cargo.Regions = regions;

            var magnet = player.AddComponent<EssenceMagnet>();
            magnet.Tuning = essence;

            var field = root.AddComponent<EssenceField>();
            field.Tuning = essence;
            field.Regions = regions;
            field.Magnet = magnet;

            var bank = root.AddComponent<BankZone>();
            bank.Regions = regions;
            bank.Cargo = cargo;

            var cue = player.AddComponent<HomeCue>();
            cue.Tuning = essence;
            cue.Regions = regions;
            cue.Cargo = cargo;

            var spawner = root.AddComponent<EnemySpawner>();
            spawner.Tables = tables;
            spawner.Roster = roster;
            spawner.Tuning = tuning;
            spawner.Regions = regions;
            spawner.Registry = registry;
            spawner.Effects = effects;
            spawner.Essence = field;
            spawner.Player = player.transform;

            var vitals = player.AddComponent<PlayerVitals>();
            vitals.Tuning = tuning;
            vitals.Regions = regions;
            vitals.Cargo = cargo;
            vitals.Bank = bank;

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
        public static void ApplyMaterials(CombatTuning tuning, EnemyRoster roster, EssenceTuning essence)
        {
            if (essence != null)
            {
                // Essence gold, and nothing else in the game is this colour. It is the currency, so
                // it wants its own hue - not the health bars' red, not Spark's cyan, not Ember
                // Scar's orange ground.
                essence.moteMaterial =
                    MaterialLibrary.GetOrCreate("M_Essence", new Color(1.00f, 0.78f, 0.32f), true);

                // What essence looks like when your hold is full: cold, and **not emissive**. The
                // hue change alone would be a weak signal at 26 cm across; losing the glow is what
                // makes a field of unclaimable motes read as switched off at a glance, in a still,
                // on near-black Ember Scar ground.
                essence.moteFullMaterial =
                    MaterialLibrary.GetOrCreate("M_EssenceFull", new Color(0.42f, 0.46f, 0.55f));

                // The way home is drawn in the same gold as the thing you are carrying, on purpose:
                // the cue is not navigation, it is the cargo asking to be banked.
                essence.homeCueMaterial =
                    MaterialLibrary.GetOrCreate("M_HomeCue", new Color(1.00f, 0.72f, 0.26f), true);

                UnityEditor.EditorUtility.SetDirty(essence);
            }

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
            MovementTuning movement, CameraFraming framing, EssenceTuning essence, SpawnTables tables)
        {
            WarnOnSuspectEssence(tuning, roster, essence, tables);

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

        /// <summary>
        /// Checks the one Phase 4 number that no picture can show: how long a bar takes to fill.
        ///
        /// It is emergent from four assets tuned in three phases for three unrelated reasons - the
        /// player's damage, the roster's health, the spawn pacing and the essence exchange rate -
        /// so it drifts silently whenever any of them is touched. `ExtractionRulesTests` is what
        /// actually holds the line; this is here because `ConfigureAll` is what someone runs after
        /// changing a value, and a warning in that log is the shortest path to noticing.
        /// </summary>
        static void WarnOnSuspectEssence(CombatTuning tuning, EnemyRoster roster, EssenceTuning essence,
            SpawnTables tables)
        {
            // Reported rather than returned on silently. This method is a diagnostic, and a
            // diagnostic that quietly does nothing when it is handed a null is worse than no
            // diagnostic at all - it looks exactly like a clean bill of health.
            if (tuning == null || roster == null || essence == null || tables == null)
            {
                Debug.LogWarning(
                    "[GRAFT] Expedition length could not be checked - missing " +
                    $"{(tuning == null ? "CombatTuning " : "")}{(roster == null ? "EnemyRoster " : "")}" +
                    $"{(essence == null ? "EssenceTuning " : "")}{(tables == null ? "SpawnTables " : "")}" +
                    "asset. Run ProjectSetup.ConfigureAll.");
                return;
            }

            if (!essence.CapacityIsWholeMotes)
            {
                Debug.LogWarning(
                    $"[GRAFT] Cargo capacity {essence.cargoCapacity} is not a whole number of " +
                    $"{essence.cargoPerMote}-unit motes. The bar will stop short of full forever, " +
                    "and DESIGN.md §4's full-cargo prompt to head home can never fire.");
            }

            var model = new ExpeditionModel(tables.ToSpec(), family => roster.ProfileFor(family).Stats,
                tuning.DamagePerSecond, essence.Rules, essence.cargoCapacity);

            var report = new System.Text.StringBuilder(
                $"[GRAFT] Expedition shape at {tuning.DamagePerSecond:0.#} player dps, " +
                $"{essence.cargoCapacity:0} cargo capacity:\n");

            foreach (var area in Areas.Biomes)
            {
                var run = model.For(area);
                if (!run.SpawnsAnything)
                {
                    continue;
                }

                report.AppendLine(
                    $"  {area,-10} {run.SecondsToFill,5:0} s to fill · {run.KillsToFill,4:0} kills · " +
                    $"{run.AverageCargoPerKill,4:0.#} cargo/kill · " +
                    $"{run.IncomingDamagePerSecond,4:0} incoming dps" +
                    (run.SpawnLimited ? "  (SPAWN-LIMITED)" : string.Empty));

                if (run.SecondsToFill < 110f || run.SecondsToFill > 195f)
                {
                    Debug.LogWarning(
                        $"[GRAFT] {area} fills a cargo bar in {run.SecondsToFill:0} s, against the " +
                        "~150 s the phase was specified at. The extraction decision is the product " +
                        "and the player should meet it many times a session." +
                        (run.SpawnLimited
                            ? $" It is spawn-limited: {area} cannot send enemies as fast as the " +
                              "player kills them, so the fix is its pacing, not the essence numbers."
                            : string.Empty));
                }
            }

            Debug.Log(report.ToString().TrimEnd());
        }
    }
}
