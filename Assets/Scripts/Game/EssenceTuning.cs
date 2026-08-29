using Graft.Core;
using UnityEngine;

namespace Graft.Game
{
    /// <summary>
    /// The numbers behind the extraction loop, as an asset (BUILD_PLAN.md rule 3: data in
    /// ScriptableObjects, never hardcoded).
    ///
    /// <see cref="cargoCapacity"/> is the one value here that is *derived* rather than picked. The
    /// spec's target is ~150 s of sustained fighting to fill an empty bar in any biome, and
    /// <see cref="EssenceRules.SecondsToFill"/> shows why that pins the capacity exactly:
    ///
    /// <code>
    /// capacity = fill seconds x player damage per second x cargo per point of enemy health
    ///          = 150 x (12 / 0.45) x 0.1
    ///          = 400
    /// </code>
    ///
    /// So a change to <c>CombatTuning</c>'s damage or interval moves the fill time under this
    /// asset's feet without touching it. <c>ExtractionRulesTests</c> recomputes the whole chain
    /// from the shipped assets and fails if the answer has drifted out of band - which is the only
    /// way a number this emergent stays true across three phases of tuning.
    ///
    /// **Changing a default here does nothing to an asset that already exists** (GOTCHAS.md):
    /// <code>rm -f Assets/Settings/EssenceTuning.asset*</code> then re-run <c>ConfigureAll</c>.
    /// </summary>
    [CreateAssetMenu(fileName = "EssenceTuning", menuName = "GRAFT/Essence Tuning")]
    public class EssenceTuning : ScriptableObject
    {
        [Header("Cargo")]
        [Tooltip("Cargo units the player can carry. Derived, not chosen - 150 s of sustained " +
                 "shooting at the shipped combat numbers. See the note on this class.")]
        [Range(20f, 4000f)] public float cargoCapacity = 400f;

        [Tooltip("Cargo consumed by one mote. Every mote in the game is this size; a bigger kill " +
                 "drops more of them rather than fatter ones, so the reward is legible on the " +
                 "ground instead of only in a number. The capacity must stay a whole multiple of " +
                 "this or a full bar becomes a state the player can never quite reach.")]
        [Range(0.5f, 25f)] public float cargoPerMote = 2f;

        [Tooltip("Cargo earned per point of effective enemy health - the exchange rate that makes " +
                 "a mote worth what it cost to kill, and the reason the bar fills at the same rate " +
                 "at every depth. See EssenceRules.")]
        [Range(0.005f, 2f)] public float cargoPerEnemyHealth = 0.1f;

        [Tooltip("Ceiling on motes from one death. Nothing shipped comes near it - the biggest is " +
                 "a depth-scaled Shellback at eleven - but a tuning slip should cost a shower of " +
                 "motes rather than a frame.")]
        [Range(1, 64)] public int maxMotesPerKill = 24;

        [Header("Attraction")]
        [Tooltip("Metres at which motes start flying to the player. About a third of the 9.5 m " +
                 "visible width, so absorption happens on screen and visibly - a magnet reaching " +
                 "past the frame would make essence arrive from nowhere.")]
        [Range(0.5f, 12f)] public float attractRadius = 3.5f;

        [Tooltip("Metres per second a mote flies in at. Faster than the player can walk, so " +
                 "walking away from your own essence is not a thing that can happen by accident.")]
        [Range(2f, 30f)] public float flightSpeed = 9f;

        [Tooltip("Seconds a mote lies on the ground before it gives up. Long, because at full " +
                 "cargo the unclaimed motes ARE the message - they should still be there when the " +
                 "player turns round and decides to go home.")]
        [Range(3f, 240f)] public float moteLifetime = 45f;

        [Tooltip("Metres a mote is scattered from the kill. Enough that a shower reads as several " +
                 "objects rather than one bright dot, small enough to stay inside the magnet.")]
        [Range(0f, 4f)] public float scatterRadius = 1.1f;

        [Tooltip("Edge of the mote cube, in metres. Sized against the 0.95 m player, not the world.")]
        [Range(0.05f, 1f)] public float moteSize = 0.26f;

        [Header("The way home")]
        [Tooltip("Metres from the player that the home cue's chevrons are drawn. Inside the frame " +
                 "and outside the character, so it never sits on top of the thing it is pointing " +
                 "the player away from.")]
        [Range(0.8f, 6f)] public float homeCueRadius = 1.9f;

        [Tooltip("Chevrons in the cue. Three is a direction; one is a smudge and five is a road.")]
        [Range(1, 6)] public int homeCueChevrons = 3;

        [Tooltip("Metres from the Base anchor at which the cue stops being drawn. Inside this the " +
                 "player can see home, and an arrow pointing at something already on screen is " +
                 "clutter.")]
        [Range(5f, 80f)] public float homeCueFadeDistance = 26f;

        [Header("Materials")]
        [Tooltip("Filled in by ProjectSetup from MaterialLibrary. Referenced from the asset rather " +
                 "than created at runtime, so an iOS build cannot strip the shaders out from " +
                 "under them - the trap that cost Phase 3 a whole fight rendered in white.")]
        public Material moteMaterial;
        public Material moteFullMaterial;
        public Material homeCueMaterial;

        /// <summary>The rules these numbers describe, in the form <c>Graft.Core</c> consumes them.</summary>
        public EssenceRules Rules => new EssenceRules(cargoPerMote, cargoPerEnemyHealth, maxMotesPerKill);

        /// <summary>
        /// Seconds of sustained fighting to fill an empty bar, at a given damage output. The
        /// spec's headline number, computed rather than asserted - pass
        /// <c>CombatTuning.DamagePerSecond</c>.
        /// </summary>
        public float SecondsToFill(float damagePerSecond) =>
            Rules.SecondsToFill(cargoCapacity, damagePerSecond);

        /// <summary>
        /// Whether the capacity is a whole number of motes. If it is not, <see cref="Cargo.IsFull"/>
        /// is unreachable - the bar stops one mote short forever and the full-cargo state, which is
        /// §4's entire non-nagging prompt to go home, never fires.
        /// </summary>
        public bool CapacityIsWholeMotes =>
            Mathf.Abs(cargoCapacity / cargoPerMote - Mathf.Round(cargoCapacity / cargoPerMote)) < 1e-3f;
    }
}
