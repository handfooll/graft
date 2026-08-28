using Graft.Core;
using UnityEngine;

namespace Graft.Game
{
    /// <summary>
    /// The player's health, and what closes the loop around it in Phase 3.
    ///
    /// **This is deliberately not the death rule.** DESIGN.md §3 and BUILD_PLAN.md Phase 4 own what
    /// dying costs - unbanked cargo - and there is no cargo yet, so there is nothing to lose. What
    /// Phase 3 needs is only that the fight has stakes and does not end in a permanent standstill:
    /// enemies can hurt you, walking back to Base makes you whole, and reaching zero puts you back
    /// at Base rather than leaving a corpse standing in Ember Scar.
    ///
    /// Phase 4 replaces <see cref="Fall"/> with the real thing. The seam is here on purpose, so
    /// that arrives as a change to one method rather than as combat needing to be reopened.
    /// </summary>
    [RequireComponent(typeof(Damageable))]
    public class PlayerVitals : MonoBehaviour
    {
        [SerializeField] CombatTuning tuning;
        [SerializeField] WorldRegions regions;

        Damageable _health;
        HealthBar _bar;
        PlayerController _controller;

        public CombatTuning Tuning { get => tuning; set => tuning = value; }
        public WorldRegions Regions { get => regions; set => regions = value; }

        public Damageable Health => _health;

        /// <summary>How many times the player has been put back at Base. Read by the tour.</summary>
        public int Falls { get; private set; }

        void Awake()
        {
            _health = GetComponent<Damageable>();
            _controller = GetComponent<PlayerController>();

            _bar = GetComponent<HealthBar>();
            if (_bar == null)
            {
                _bar = gameObject.AddComponent<HealthBar>();
            }
        }

        void Start()
        {
            if (tuning == null)
            {
                return;
            }

            _health.ResetTo(tuning.playerMaxHealth);
            _health.Died += OnDied;

            // The player carries the same world-space bar the enemies do. Under a fixed camera it
            // costs nothing, and it means the one number the player has to watch is in the same
            // place as everything they are watching - rather than in a corner of the screen the
            // HUD has not been designed for yet.
            _bar.Bind(_health, tuning, PlayerBarHeight());
        }

        void Update()
        {
            if (tuning == null || regions == null || !_health.IsAlive)
            {
                return;
            }

            // Base heals. §7 makes it the safe hub, and this is the cheapest possible statement of
            // that - Phase 4 replaces it with banking, which is the version that costs something.
            if (tuning.baseHealSeconds > 0f && regions.IsInBase(transform.position))
            {
                _health.Heal(_health.Pool.Maximum / tuning.baseHealSeconds * Time.deltaTime);
            }
        }

        void OnDied(Damageable damageable) => Fall();

        /// <summary>
        /// Phase 3's stand-in for death: back to Base, whole again, nothing lost - because there is
        /// nothing yet to lose. Phase 4 makes this cost the unbanked cargo (§3).
        /// </summary>
        void Fall()
        {
            Falls++;

            var home = regions != null ? regions.AnchorFor(Area.Base) : Vector3.zero;
            if (_controller != null)
            {
                _controller.Teleport(home, 0f);
            }
            else
            {
                transform.position = home;
            }

            _health.ResetTo(tuning != null ? tuning.playerMaxHealth : 100f);
            Debug.Log("[GRAFT] Player fell and was returned to Base. Phase 4 makes this cost cargo.");
        }

        /// <summary>
        /// Where to hang the bar. Read off the CharacterController rather than the proportions
        /// asset, because the controller is what was actually sized to the silhouette and this way
        /// there is one height in play instead of two that can disagree.
        /// </summary>
        float PlayerBarHeight()
        {
            var controller = GetComponent<CharacterController>();
            return controller != null ? controller.height : 1.5f;
        }
    }
}
